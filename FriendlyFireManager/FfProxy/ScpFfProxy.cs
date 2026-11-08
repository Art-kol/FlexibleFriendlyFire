using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using AFK;
using CustomPlayerEffects;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Handlers;
using LabApi.Features.Wrappers;
using MEC;
using Mirror;
using NetworkManagerUtils.Dummies;
using PlayerRoles;
using PlayerRoles.FirstPersonControl;
using PlayerRoles.PlayableScps.Scp106;
using PlayerRoles.PlayableScps.Scp939;
using UnityEngine;
using Logger = LabApi.Features.Console.Logger;

namespace FriendlyFireManager.FfProxy {
    // ============================================================================================
    // Why this exists:
    //   Some SCP attacks are CLIENT-driven: the client decides whether there is a target and only then
    //   sends the cmd. The client uses the VANILLA HitboxIdentity.IsEnemy - SCP allies are never enemies
    //   there, and we can't patch the client. No cmd -> the server never runs ServerProcessCmd.
    //     * SCP-106 attack: OnKeyDown sends the cmd only if an "enemy" is within _maxRangeSqr.
    //     * SCP-939 lunge: Scp939Motor overlaps the lunge path with enemy HITBOXES and sends the cmd
    //       only when it touches one.
    //
    // How it works (ONE dummy hub for the whole server, but every client gets its own version of it):
    //   1. The proxy exists while any SCP-106 / SCP-939 is alive and was needed once (an FF rule lets
    //      it hurt an SCP). On the server it's a Tutorial in godmode parked far outside the map.
    //   2. Per receiver (GetVisibleRole / ValidatedVisibility / GetNewSyncData patches):
    //        SCP-106 with an FF SCP target -> Tutorial right under its feet (always in attack range)
    //        SCP-939 with an FF SCP target -> Tutorial INSIDE the SCP it's aiming at, so its hitboxes
    //                                         sit where the lunge will collide
    //        everyone else                 -> Spectator, position never sent
    //      Model: Fade 255 + 0.25 scale (+ inside the target SCP for 939), no footsteps, no nickname / info.
    //   3. The server ignores the proxy as a target and resolves the real one:
    //        Scp106AttackPatch - replaces ServerProcessCmd, picks the target itself.
    //        Scp939LungePatch  - only when the client hit the proxy: maps it to the real SCP behind it,
    //                            otherwise vanilla. Claw (Scp939ClawAbility) is untouched: it's fully
    //                            server-side and the proxy is never an enemy there.
    // ============================================================================================
    public static class ScpFfProxy {
        public enum ProxyMode : byte {
            None = 0,
            Scp106 = 1,
            Scp939 = 2
        }

        // ================= SETTINGS =================

        // Name shown in the RA / player list for the proxy dummy.
        public const string PROXY_NICKNAME = "SCP FF Proxy";

        // Role the proxy has on the server. Tutorial: IFpcRole (needed so its position gets synced),
        // FpcStandardRoleBase (needed for 939 lunge detection), not counted by RoundSummary,
        // never respawned, and the vanilla client treats it as an enemy of SCPs.
        public const RoleTypeId PROXY_ROLE = RoleTypeId.Tutorial;

        // Where the proxy really is on the server. Far away from every room so no server-side
        // ability / tesla / 096 / 173 logic ever touches it.
        public static readonly Vector3 ParkingPosition = new(5000f, 500f, 5000f);

        // Model scale (hitboxes shrink with it). Not lower than 0.2: the 939 client ignores hitboxes closer
        // than 0.63m horizontally, and with point-sized hitboxes one lunge frame can step over the detection
        // window (20% misses). 0.25 -> hitbox radius 5cm, no misses. The model is hidden by Fade, and for
        // SCP-939 it additionally sits inside the target SCP's body.
        public const float PROXY_SCALE = 0.25f;

        // Fade intensity: 255 = completely invisible model.
        public const byte FADE_INTENSITY = 255;

        // SilentWalk: each stack -10% footstep volume, 10 = silent.
        public const byte SILENT_WALK_INTENSITY = 10;

        // ---- SCP-106 ----

        // Proxy is placed below SCP-106: offset = attack range * this.
        public const float VERTICAL_OFFSET_FRACTION = 0.15f;

        // Proxy is moved ahead of a walking SCP-106 to compensate ping + client interpolation.
        public const float LEAD_SECONDS = 0.15f;

        // Max lead distance = attack range * this (so the proxy never leaves the attack range).
        public const float MAX_LEAD_FRACTION = 0.5f;

        // Same for vertical movement (jumps, falls over a pit). Longer than the horizontal lead: on the client a remote
        // player's height only follows the received one with Lerp(9 * deltaTime) (FpcMotor.UpdateThirdperson, ~0.11 s
        // behind) on top of the ping. Without it a jumping / falling SCP-106 left its own attack range of the proxy.
        public const float VERTICAL_LEAD_SECONDS = 0.25f;

        // Max vertical lead = attack range * this.
        public const float MAX_VERTICAL_LEAD_FRACTION = 1f;

        // No new position from the SCP-106 client for this long = it stands still (velocity 0).
        public const float STILL_AFTER_SECONDS = 0.25f;

        // ---- SCP-939 ----

        // FF SCPs further than this are not considered as lunge targets.
        public const float LUNGE_SEARCH_RANGE = 25f;

        // Target must be in front of SCP-939: min dot between horizontal camera forward and direction to it.
        public const float LUNGE_MIN_DOT = 0.3f;

        // The proxy is put at the target's X/Z, and its height is pulled towards SCP-939's height by at most
        // this much (vanilla lunge detection checks hitboxes around the 939's body height).
        public const float LUNGE_HEIGHT_MATCH = 0.4f;

        // Server: a lunge hit on the proxy is mapped to an FF SCP at most this far (horizontally) from
        // the position the client claimed for the proxy.
        public const float LUNGE_RESOLVE_MAX_DISTANCE = 3f;

        // How often the proxy lifecycle is checked (spawn/despawn/keep parked).
        public const float MAINTAIN_INTERVAL = 1f;

        // ================= STATE =================

        public static bool Enabled { get; private set; }

        public static ReferenceHub? Hub { get; private set; }

        private static bool _allowRoleChange;

        // True once SetupProxy has finished on the current dummy. Until then KeepParked leaves it alone:
        // a freshly spawned dummy isn't initialized yet (e.g. CharacterClassManager._hub is set in Start(),
        // the GodMode setter threw a NullReferenceException when called in the same frame as SpawnDummy).
        private static bool _setupDone;
        private static bool _maintainQueued;
        private static CoroutineHandle _maintainHandle;

        // IMPORTANT: ReferenceHub.GetHashCode() goes through .gameObject and throws once the hub is destroyed
        // (player left / dummy removed). Every ReferenceHub-keyed collection here uses reference hashing instead.
        private sealed class HubReferenceComparer : IEqualityComparer<ReferenceHub> {
            public static readonly HubReferenceComparer Instance = new();
            public bool Equals(ReferenceHub? x, ReferenceHub? y) => ReferenceEquals(x, y);
            public int GetHashCode(ReferenceHub obj) => RuntimeHelpers.GetHashCode(obj);
        }

        private static int _modeCacheFrame = -1;
        private static readonly Dictionary<ReferenceHub, ProxyMode> _modeCache = new(HubReferenceComparer.Instance);

        private sealed class MotionTracker {
            public Vector3 LastPosition;
            public float LastTime;
            public Vector3 Velocity;
        }

        // SCP-106 -> its motion (for the lead)
        private static readonly Dictionary<ReferenceHub, MotionTracker> _trackers = new(HubReferenceComparer.Instance);

        // SCP-939 -> the FF SCP the proxy is currently placed into for that client
        private static readonly Dictionary<ReferenceHub, ReferenceHub> _lungeTargets = new(HubReferenceComparer.Instance);

        // ================= ENABLE / DISABLE =================

        public static void Enable() {
            if (Enabled) {
                return;
            }

            Enabled = true;
            PlayerEvents.ValidatedVisibility += OnValidatedVisibility;
            PlayerEvents.ChangingRole += OnChangingRole;
            PlayerEvents.ChangedRole += OnChangedRole;
            PlayerEvents.Left += OnLeft;
            StartLoop();
        }

        public static void Disable() {
            if (!Enabled) {
                return;
            }

            Enabled = false;
            PlayerEvents.ValidatedVisibility -= OnValidatedVisibility;
            PlayerEvents.ChangingRole -= OnChangingRole;
            PlayerEvents.ChangedRole -= OnChangedRole;
            PlayerEvents.Left -= OnLeft;
            Timing.KillCoroutines(_maintainHandle);
            Despawn();
            Reset();
        }

        // Called on WaitingForPlayers by FfEventsManager.ClearState (dummy itself is destroyed by RoundRestart).
        public static void Reset() {
            _trackers.Clear();
            _lungeTargets.Clear();
            _modeCache.Clear();
            _modeCacheFrame = -1;
            _maintainQueued = false;

            if (Enabled) {
                StartLoop();
            }
        }

        private static void StartLoop() {
            if (!_maintainHandle.IsRunning) {
                _maintainHandle = Timing.RunCoroutine(MaintainLoop());
            }
        }

        // ================= QUERIES =================

        // Hot path (IsEnemy/IsDamageable guards) -> plain reference check, no Unity null overhead
        public static bool IsProxy(ReferenceHub? hub) {
            return hub is not null && Hub is not null && ReferenceEquals(hub, Hub);
        }

        // What (if anything) this receiver should see as the proxy.
        public static ProxyMode GetMode(ReferenceHub? receiver) {
            if (!Enabled || receiver == null || IsProxy(receiver)) {
                return ProxyMode.None;
            }

            ProxyMode roleMode;
            switch (receiver.roleManager.CurrentRole) {
                case Scp106Role:
                    roleMode = ProxyMode.Scp106;
                    break;
                case Scp939Role:
                    roleMode = ProxyMode.Scp939;
                    break;
                default:
                    return ProxyMode.None;
            }

            var frame = Time.frameCount;
            if (frame != _modeCacheFrame) {
                _modeCacheFrame = frame;
                _modeCache.Clear();
            }

            if (_modeCache.TryGetValue(receiver, out var cached)) {
                return cached;
            }

            var result = HasFriendlyFireScpTarget(receiver) ? roleMode : ProxyMode.None;
            _modeCache[receiver] = result;
            return result;
        }

        public static bool ShouldAssist(ReferenceHub? receiver) => GetMode(receiver) != ProxyMode.None;

        private static bool IsProxyUser(PlayerRoleBase role) => role is Scp106Role or Scp939Role;

        // Is there at least one SCP this attacker is explicitly allowed to hurt?
        public static bool HasFriendlyFireScpTarget(ReferenceHub attacker) {
            foreach (var hub in ReferenceHub.AllHubs) {
                if (hub == attacker || IsProxy(hub) || !hub.IsSCP() || hub.roleManager.CurrentRole is not IFpcRole) {
                    continue;
                }

                if (FireLogics.CanDamage(attacker, hub) == true) {
                    return true;
                }
            }

            return false;
        }

        // Shared targeting rule for the patched SCP attacks:
        // explicit FF rule wins; without a rule humans follow (patched) HitboxIdentity.IsEnemy, SCPs are never targets.
        public static bool CanHit(ReferenceHub owner, ReferenceHub? target) {
            if (target == null || target == owner || IsProxy(target)) {
                return false;
            }

            if (target.roleManager.CurrentRole is not IFpcRole) {
                return false;
            }

            var rule = FireLogics.CanDamage(owner, target);
            if (rule.HasValue) {
                return rule.Value;
            }

            return !target.IsSCP() && HitboxIdentity.IsEnemy(owner, target);
        }

        // Position of the proxy as this receiver's client should see it.
        public static Vector3 GetSpoofedPosition(ReferenceHub receiver) {
            return receiver.roleManager.CurrentRole switch {
                Scp106Role scp106 => GetScp106Position(receiver, scp106),
                Scp939Role scp939 => GetScp939Position(receiver, scp939),
                _ => ParkingPosition
            };
        }

        // ================= SCP-106 =================

        public static float GetAttackRange(Scp106Role role) {
            return role.SubroutineModule.TryGetSubroutine(out Scp106Attack attack) && attack._maxRangeSqr > 0f
                ? Mathf.Sqrt(attack._maxRangeSqr)
                : 2f;
        }

        // Right under SCP-106 (+ a lead when it moves, horizontally and vertically) - always inside the attack range.
        //
        // The SCP-106 client sees the proxy late: the server knows its position half a ping late, the proxy position
        // needs another half back, and the client smooths remote players (height ~0.11 s behind). So the proxy is put
        // where the SCP-106 will be by then. The server never moves the real dummy - it stays parked, only this sent
        // position follows the SCP-106 (that's why noclip / gravity of the dummy don't matter here).
        private static Vector3 GetScp106Position(ReferenceHub scp106, Scp106Role role) {
            var position = role.FpcModule.Position;
            var range = GetAttackRange(role);
            var now = Time.time;

            if (!_trackers.TryGetValue(scp106, out var tracker)) {
                tracker = new MotionTracker { LastPosition = position, LastTime = now };
                _trackers[scp106] = tracker;
            }

            // Velocity only from real position updates: this runs every network tick, the SCP-106 client may send
            // its position less often, and a "no change" sample would drag the velocity to 0 mid-jump.
            if (position != tracker.LastPosition) {
                var dt = now - tracker.LastTime;
                if (dt > 0.5f) {
                    tracker.Velocity = Vector3.zero;
                }
                else if (dt > 0.0001f) {
                    tracker.Velocity = Vector3.Lerp(tracker.Velocity, (position - tracker.LastPosition) / dt, 0.5f);
                }

                tracker.LastPosition = position;
                tracker.LastTime = now;
            }
            else if (now - tracker.LastTime > STILL_AFTER_SECONDS) {
                tracker.Velocity = Vector3.zero;
            }

            var velocity = tracker.Velocity;
            var lead = Vector3.ClampMagnitude(new Vector3(velocity.x, 0f, velocity.z) * LEAD_SECONDS, range * MAX_LEAD_FRACTION);
            var maxVertical = range * MAX_VERTICAL_LEAD_FRACTION;
            lead.y = Mathf.Clamp(velocity.y * VERTICAL_LEAD_SECONDS, -maxVertical, maxVertical);

            return position + lead + (Vector3.down * (range * VERTICAL_OFFSET_FRACTION));
        }

        // ================= SCP-939 =================

        private static bool IsValidLungeTarget(ReferenceHub scp939, ReferenceHub? target) {
            return target != null
                   && target.IsSCP()
                   && target.roleManager.CurrentRole is FpcStandardRoleBase
                   && CanHit(scp939, target);
        }

        // Which FF SCP is SCP-939 aiming at? Kept fixed while a lunge is in the air.
        public static ReferenceHub? GetLungeTarget(ReferenceHub scp939, Scp939Role role) {
            var origin = role.FpcModule.Position;
            var rangeSqr = LUNGE_SEARCH_RANGE * LUNGE_SEARCH_RANGE;

            if (role.SubroutineModule.TryGetSubroutine(out Scp939LungeAbility lunge)
                && lunge.State == Scp939LungeState.Triggered
                && _lungeTargets.TryGetValue(scp939, out var current)
                && IsValidLungeTarget(scp939, current)
                && (((IFpcRole)current.roleManager.CurrentRole).FpcModule.Position - origin).sqrMagnitude <= rangeSqr) {
                return current;
            }

            var forward = scp939.PlayerCameraReference.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : scp939.transform.forward;

            ReferenceHub? best = null;
            var bestDot = LUNGE_MIN_DOT;
            var bestDistance = float.MaxValue;

            foreach (var hub in ReferenceHub.AllHubs) {
                if (!IsValidLungeTarget(scp939, hub)) {
                    continue;
                }

                var offset = ((FpcStandardRoleBase)hub.roleManager.CurrentRole).FpcModule.Position - origin;
                var distanceSqr = offset.sqrMagnitude;
                if (distanceSqr > rangeSqr) {
                    continue;
                }

                offset.y = 0f;
                var dot = offset.sqrMagnitude < 0.0001f ? 1f : Vector3.Dot(offset.normalized, forward);
                if (dot < bestDot || (Mathf.Approximately(dot, bestDot) && distanceSqr >= bestDistance)) {
                    continue;
                }

                best = hub;
                bestDot = dot;
                bestDistance = distanceSqr;
            }

            if (best != null) {
                _lungeTargets[scp939] = best;
            }
            else {
                _lungeTargets.Remove(scp939);
            }

            return best;
        }

        // Inside the SCP the 939 is aiming at. No target -> at the 939 itself: the vanilla lunge
        // detection ignores hitboxes closer than ~0.63m horizontally, so it can't be "hit" there.
        private static Vector3 GetScp939Position(ReferenceHub scp939, Scp939Role role) {
            var own = role.FpcModule.Position;
            var target = GetLungeTarget(scp939, role);

            if (target == null || target.roleManager.CurrentRole is not IFpcRole targetRole) {
                return own;
            }

            var position = targetRole.FpcModule.Position;
            position.y = Mathf.Clamp(own.y, position.y - LUNGE_HEIGHT_MATCH, position.y + LUNGE_HEIGHT_MATCH);
            return position;
        }

        // Server: the client lunged into the proxy - which real SCP was that?
        public static ReferenceHub? ResolveLungeTarget(ReferenceHub scp939, Vector3 claimedProxyPosition) {
            var maxSqr = LUNGE_RESOLVE_MAX_DISTANCE * LUNGE_RESOLVE_MAX_DISTANCE;

            if (_lungeTargets.TryGetValue(scp939, out var assigned)
                && IsValidLungeTarget(scp939, assigned)
                && (((IFpcRole)assigned.roleManager.CurrentRole).FpcModule.Position - claimedProxyPosition).SqrMagnitudeIgnoreY() <= maxSqr) {
                return assigned;
            }

            ReferenceHub? best = null;
            var bestSqr = maxSqr;

            foreach (var hub in ReferenceHub.AllHubs) {
                if (!IsValidLungeTarget(scp939, hub)) {
                    continue;
                }

                var sqr = (((IFpcRole)hub.roleManager.CurrentRole).FpcModule.Position - claimedProxyPosition).SqrMagnitudeIgnoreY();
                if (sqr > bestSqr) {
                    continue;
                }

                best = hub;
                bestSqr = sqr;
            }

            return best;
        }

        // ================= EVENTS =================

        // FpcServerPositionDistributor.WriteAll -> decides if the target's position is sent to the receiver.
        private static void OnValidatedVisibility(PlayerValidatedVisibilityEventArgs ev) {
            if (ev.Target == null || !IsProxy(ev.Target.ReferenceHub)) {
                return;
            }

            ev.IsVisible = ev.Player != null && ShouldAssist(ev.Player.ReferenceHub);
        }

        // Proxy must stay a Tutorial: blocks late-join spawn, respawns, forceclass etc.
        private static void OnChangingRole(PlayerChangingRoleEventArgs ev) {
            if (_allowRoleChange || ev.Player == null || !IsProxy(ev.Player.ReferenceHub)) {
                return;
            }

            if (ev.NewRole is RoleTypeId.Destroyed or RoleTypeId.None or PROXY_ROLE) {
                return;
            }

            ev.IsAllowed = false;
        }

        // SCP-106 / SCP-939 can appear or disappear mid-round: react quickly instead of waiting for the loop.
        private static void OnChangedRole(PlayerChangedRoleEventArgs ev) {
            if (ev.Player != null && !IsProxy(ev.Player.ReferenceHub)) {
                RequestMaintain();
            }
        }

        private static void OnLeft(PlayerLeftEventArgs ev) {
            if (ev.Player != null && !IsProxy(ev.Player.ReferenceHub)) {
                RequestMaintain();
            }
        }

        // ================= LIFECYCLE =================

        // Call after FF rules change (fire command): spawn/remove the proxy right away instead of on the next poll.
        public static void RequestRefresh() {
            if (!Enabled) {
                return;
            }

            _modeCache.Clear();
            _modeCacheFrame = -1;
            RequestMaintain();
        }

        private static void RequestMaintain() {
            if (_maintainQueued) {
                return;
            }

            _maintainQueued = true;
            Timing.CallDelayed(0.1f, () => {
                _maintainQueued = false;
                SafeMaintain();
            });
        }

        private static IEnumerator<float> MaintainLoop() {
            while (true) {
                yield return Timing.WaitForSeconds(MAINTAIN_INTERVAL);
                SafeMaintain();
            }
        }

        private static void SafeMaintain() {
            try {
                Maintain();
            }
            catch (Exception e) {
                Logger.Error($"[ScpFfProxy] Maintain failed: {e}");
            }
        }

        private static void Maintain() {
            if (!Enabled) {
                return;
            }

            var anyUser = false;
            var needed = false;

            foreach (var hub in ReferenceHub.AllHubs) {
                if (IsProxy(hub) || !IsProxyUser(hub.roleManager.CurrentRole)) {
                    continue;
                }

                anyUser = true;
                if (HasFriendlyFireScpTarget(hub)) {
                    needed = true;
                    break;
                }
            }

            // Forget data of players that stopped being SCP-106 / SCP-939
            // (h == null is Unity's check -> true for destroyed hubs; Remove is safe thanks to HubReferenceComparer)
            foreach (var hub in _trackers.Keys.Where(h => h == null || h.roleManager.CurrentRole is not Scp106Role).ToList()) {
                _trackers.Remove(hub);
            }

            foreach (var hub in _lungeTargets.Keys.Where(h => h == null || h.roleManager.CurrentRole is not Scp939Role).ToList()) {
                _lungeTargets.Remove(hub);
            }

            if (needed) {
                EnsureSpawned();
            }
            else if (!anyUser) {
                Despawn();
            }

            KeepParked();
        }

        private static void EnsureSpawned() {
            if (Hub != null) {
                return;
            }

            var hub = DummyUtils.SpawnDummy(PROXY_NICKNAME);
            if (hub == null) {
                Logger.Warn("[ScpFfProxy] Failed to spawn the proxy dummy.");
                return;
            }

            Hub = hub;
            _setupDone = false;
            Logger.Info("[ScpFfProxy] Proxy dummy spawned.");

            // Give the dummy a moment to initialize before setting its role
            Timing.CallDelayed(0.25f, () => SetupProxy(hub));
        }

        private static void SetupProxy(ReferenceHub hub) {
            if (hub == null || !IsProxy(hub)) {
                return;
            }

            // Dummy components not started yet -> try again a bit later
            if (hub.characterClassManager == null || hub.characterClassManager._hub == null || hub.playerStats == null) {
                Timing.CallDelayed(0.25f, () => SetupProxy(hub));
                return;
            }

            if (hub.GetRoleId() != PROXY_ROLE) {
                _allowRoleChange = true;
                try {
                    hub.roleManager.ServerSetRole(PROXY_ROLE, RoleChangeReason.RemoteAdmin, RoleSpawnFlags.None);
                }
                finally {
                    _allowRoleChange = false;
                }
            }

            var player = Player.Get(hub);
            if (player == null) {
                return;
            }

            player.IsGodModeEnabled = true;
            player.Gravity = Vector3.zero;
            player.Scale = Vector3.one * PROXY_SCALE;
            player.Position = ParkingPosition;
            ApplyHidingEffects(hub);

            // Otherwise the vanilla AFK kicker kicks the idle dummy and it respawns every ~90s
            AFKManager.RemovePlayer(hub);

            _setupDone = true;
        }

        private static void KeepParked() {
            if (Hub == null || !_setupDone) {
                return;
            }

            if (Hub.GetRoleId() != PROXY_ROLE) {
                SetupProxy(Hub);
                return;
            }

            AFKManager.RemovePlayer(Hub);
            ApplyHidingEffects(Hub);

            if (Hub.roleManager.CurrentRole is IFpcRole fpcRole
                && (fpcRole.FpcModule.Position - ParkingPosition).sqrMagnitude > 25f) {
                SetupProxy(Hub);
            }
        }

        // Fade 255  -> CharacterModel.Fade = 0 on every client (model fully invisible, synced to all observers).
        //              Side effect on SCP-939 clients: Scp939VisibilityController never sees the model fading
        //              out from a non-zero value, so it never disables the proxy's hitbox colliders.
        // SilentWalk -> extra footstep muting on top of the "not grounded" trick in ScpFfProxySyncDataPatch.
        // Player info -> nothing shown when a SCP-106 / SCP-939 looks at it: no nickname, badge, role...
        //              (_playerInfoToShow = none, and ViewRange 0 so the client never shows the info at all).
        //              Fade hides only the model; the hitboxes (needed for the SCP-939 lunge) still let the client
        //              show the nickname under the crosshair.
        // Not Invisible (SCP-268): it's purely server-side - the server stops sending the position, but the proxy
        // MUST be sent to SCP-106 / SCP-939 (OnValidatedVisibility overrides it), so on their clients it would change
        // nothing and the model would be visible.
        // Re-applied every Maintain tick in case something (role change, admin command) cleared them.
        private static void ApplyHidingEffects(ReferenceHub hub) {
            var nicknameSync = hub.nicknameSync;
            if (nicknameSync != null) {
                if (nicknameSync.ShownPlayerInfo != 0) {
                    nicknameSync.ShownPlayerInfo = 0;
                }

                if (nicknameSync.ViewRange != 0f) {
                    nicknameSync.NetworkViewRange = 0f;
                }
            }

            var effects = hub.playerEffectsController;

            var fade = effects.GetEffect<Fade>();
            if (fade != null && fade.Intensity != FADE_INTENSITY) {
                fade.ServerSetState(FADE_INTENSITY);
            }

            var silentWalk = effects.GetEffect<SilentWalk>();
            if (silentWalk != null && silentWalk.Intensity != SILENT_WALK_INTENSITY) {
                silentWalk.ServerSetState(SILENT_WALK_INTENSITY);
            }
        }

        public static void Despawn() {
            if (Hub == null) {
                Hub = null;
                _setupDone = false;
                return;
            }

            var hub = Hub;
            Hub = null;
            _setupDone = false;
            NetworkServer.Destroy(hub.gameObject);
            Logger.Info("[ScpFfProxy] Proxy dummy destroyed.");
        }
    }
}

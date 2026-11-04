using System.Collections.Generic;
using CustomPlayerEffects;
using FriendlyFireManager.Utilities;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Handlers;
using PlayerRoles;
using PlayerRoles.FirstPersonControl;
using PlayerRoles.PlayableScps.Scp079;
using PlayerRoles.PlayableScps.Scp106;
using PlayerRoles.PlayableScps.Scp939;
using PlayerStatsSystem;
using UnityEngine;

namespace FriendlyFireManager.Handlers {
    // What one SCP may SEE of another SCP it can hit by FF rules. Vanilla does both of these on the CLIENT
    // (fading models), and the client only does it for vanilla enemies, never for SCPs - so for SCP targets
    // the server simply stops sending the position to that one client (the model disappears for that client only).
    //
    //   SCP-106 underground (stalk): sees an FF SCP target only like vanilla sees humans
    //     (Scp106StalkVisibilityController.GetVisibilityForPlayer):
    //       * normally within 4 m
    //       * damaged targets from farther: 4 m + 0.3 m per missing HP (HP only, no hume shield; max 255 HP -> 80.5 m)
    //       * Traumatized (escaped the pocket dimension) -> visible at any distance
    //       * Invigorated -> never visible
    //     Applies once the 106 is >= 80% submerged with stalk active, exactly like vanilla.
    //
    //   ...and the other way round: an SCP that SCP-106 may hit sees the HIDDEN 106 only like vanilla humans do
    //     (Scp106VisibilityController.CheckPlayer, while the sinkhole is hidden):
    //       * full HP -> never (vanilla: no SyncDamage entry -> the 106 isn't sent at all)
    //       * damaged -> within 4 m + 0.3 m per missing HP (that SCP is "bleeding" and the 106 can see it too)
    //       * Traumatized -> at any distance;  Invigorated -> never while the 106 stalks
    //       * 106 hidden but NOT stalking (Hunter's Atlas) -> damaged/Traumatized SCPs nearby see it (vanilla tolerance +5 m)
    //
    //   SCP-939 amnestic cloud: an SCP with AmnesiaVision stops seeing every SCP-939 that may hit it
    //     (Scp939Model.Visible): hidden unless closer than the model's amnesia range; stays visible for 30 s after
    //     the amnesia ends; otherwise also hidden while standing in a cloud of such a 939.
    //
    // Re-appearing: a client keeps an unsent player at a far "invisible" spot and would glide the model back from
    // there for ~1 s. So when we stop hiding a pair, its PreviouslySentRole entry is dropped: in the same
    // FpcServerPositionDistributor.WriteAll pass the server re-sends the (same) role to that client, whose spawn
    // data places the model right at its real position - it pops in instead of flying in.
    public static class ScpFfVisibility {
        // Extra distance before a visible target gets hidden again (no flicker / role re-sends at the border).
        public const float HYSTERESIS = 1f;

        // SCP-939 amnesia range fallback, if the 939 has no model instance (the model's _amnesiaVisibleRange is used).
        public const float DEFAULT_AMNESIA_VISIBLE_RANGE = 4f;

        // (receiver netId, target netId) pairs currently hidden by this class
        private static readonly HashSet<(uint Receiver, uint Target)> _hiddenPairs = new();

        private static bool _subscribed;

        public static void Subscribe() {
            if (_subscribed) {
                return;
            }

            _subscribed = true;
            PlayerEvents.ValidatedVisibility += OnValidatedVisibility;
            PlayerEvents.Left += OnLeft;
        }

        public static void Unsubscribe() {
            if (!_subscribed) {
                return;
            }

            _subscribed = false;
            PlayerEvents.ValidatedVisibility -= OnValidatedVisibility;
            PlayerEvents.Left -= OnLeft;

            // Show everything we hid, without the glide-in
            foreach (var (receiverId, targetId) in _hiddenPairs) {
                if (ReferenceHub.TryGetHubNetID(targetId, out var target)) {
                    target.roleManager.PreviouslySentRole.Remove(receiverId);
                }
            }

            _hiddenPairs.Clear();
        }

        public static void Clear() {
            _hiddenPairs.Clear();
        }

        // ================= EVENTS =================

        // Fired by FpcServerPositionDistributor.WriteAll for every (receiver, target) pair, every position tick.
        private static void OnValidatedVisibility(PlayerValidatedVisibilityEventArgs ev) {
            var receiver = ev.Player?.ReferenceHub;
            var target = ev.Target?.ReferenceHub;
            if (receiver == null || target == null) {
                return;
            }

            var key = (receiver.netId, target.netId);
            var wasHidden = _hiddenPairs.Contains(key);

            if (ShouldHide(receiver, target, wasHidden)) {
                if (!wasHidden) {
                    _hiddenPairs.Add(key);
                }

                ev.IsVisible = false;
                return;
            }

            // Hidden by vanilla instead - e.g. SCP-939's own vision radius (Scp939VisibilityController, server side),
            // which with the IsEnemy patch also culls the SCPs that 939 may hit. The 939 client doesn't fade SCPs itself,
            // so they'd glide in from the "invisible" spot when they come back - remember the pair to snap it as well.
            if (!ev.IsVisible) {
                if (!wasHidden && IsFfScpPair(receiver, target)) {
                    _hiddenPairs.Add(key);
                }

                return;
            }

            // Visible now. If it was hidden before -> make the client snap the model in place.
            if (!wasHidden) {
                return;
            }

            _hiddenPairs.Remove(key);
            target.roleManager.PreviouslySentRole.Remove(receiver.netId);
        }

        // An SCP receiver and an SCP target where one of them may hit the other (only such pairs ever get culled
        // because of FF; vanilla never culls SCP allies, so nothing else needs the snap).
        private static bool IsFfScpPair(ReferenceHub receiver, ReferenceHub target) {
            return receiver.roleManager.CurrentRole.Team == Team.SCPs
                   && target.roleManager.CurrentRole.Team == Team.SCPs
                   && (ScpFfTargets.IsFfScpTarget(receiver, target) || ScpFfTargets.IsFfScpTarget(target, receiver));
        }

        private static void OnLeft(PlayerLeftEventArgs ev) {
            if (ev.Player == null) {
                return;
            }

            var netId = ev.Player.ReferenceHub.netId;
            _hiddenPairs.RemoveWhere(pair => pair.Receiver == netId || pair.Target == netId);
        }

        // ================= RULES =================

        private static bool ShouldHide(ReferenceHub receiver, ReferenceHub target, bool wasHidden) {
            // Cheap role checks first: this runs for every pair of players every tick.
            var receiverRole = receiver.roleManager.CurrentRole;
            if (receiverRole.Team != Team.SCPs) {
                return false;
            }

            if (receiverRole is Scp106Role scp106 && target.IsSCP() && Scp106HidesTarget(receiver, scp106, target, wasHidden)) {
                return true;
            }

            var targetRole = target.roleManager.CurrentRole;

            if (targetRole is Scp106Role hiddenScp106 && HiddenScp106HiddenFrom(receiver, target, hiddenScp106, wasHidden)) {
                return true;
            }

            // SCP-079 that may hit SCP-106: doesn't see the 106 (its moving black spot) while it is underground.
            // Vanilla never culls anything for 079 (it has no visibility controller), so this is done here.
            if (receiverRole is Scp079Role && targetRole is Scp106Role buried106
                && buried106.Sinkhole.IsHidden && ScpFfTargets.IsFfScpTarget(receiver, target)) {
                return true;
            }

            return targetRole is Scp939Role scp939 && AmnesiaHides939(receiver, target, scp939, wasHidden);
        }

        // Server version of Scp106StalkVisibilityController.GetVisibilityForPlayer, for SCP targets the 106 may hit.
        private static bool Scp106HidesTarget(ReferenceHub scp106Hub, Scp106Role scp106, ReferenceHub target, bool wasHidden) {
            if (!scp106.SubroutineModule.TryGetSubroutine(out Scp106StalkAbility stalk)
                || !stalk.StalkActive
                || scp106.Sinkhole.SubmergeProgress < 0.8f) {
                return false;
            }

            if (!ScpFfTargets.IsFfScpTarget(scp106Hub, target) || target.roleManager.CurrentRole is not IFpcRole targetFpc) {
                return false;
            }

            var effects = target.playerEffectsController;
            if (effects.GetEffect<Invigorated>().IsEnabled) {
                return true;
            }

            if (effects.GetEffect<Traumatized>().IsEnabled) {
                return false;
            }

            var health = target.playerStats.GetModule<HealthStat>();
            var missingHealth = Mathf.Clamp(Mathf.FloorToInt(health.MaxValue - health.CurValue), 0, 255);

            var radius = Scp106StalkVisibilityController.AbsoluteDistance
                         + (missingHealth * Scp106StalkVisibilityController.HealthToDistance)
                         + (wasHidden ? 0f : HYSTERESIS);

            return Vector3.Distance(scp106.FpcModule.Position, targetFpc.FpcModule.Position) >= radius;
        }

        // Server version of Scp106VisibilityController.CheckPlayer (+ the SyncDamage entry it requires),
        // for an SCP observer that this SCP-106 may hit: may the observer see the 106 while it's underground?
        private static bool HiddenScp106HiddenFrom(ReferenceHub observer, ReferenceHub scp106Hub, Scp106Role scp106, bool wasHidden) {
            if (!scp106.Sinkhole.IsHidden) {
                return false;
            }

            if (!ScpFfTargets.IsFfScpTarget(scp106Hub, observer) || observer.roleManager.CurrentRole is not IFpcRole observerFpc) {
                return false;
            }

            var effects = observer.playerEffectsController;
            var traumatized = effects.GetEffect<Traumatized>().IsEnabled;
            var distance = Vector3.Distance(scp106.FpcModule.Position, observerFpc.FpcModule.Position);
            var margin = wasHidden ? 0f : HYSTERESIS;

            // Scp106StalkVisibilityController.RefreshDamageDictionary: Traumatized -> 0, otherwise only damaged
            // players within (missing HP * 0.3 + 4) m (+5 m server tolerance) get an entry. No entry = never visible.
            var missingHealth = 0;
            if (!traumatized) {
                var health = observer.playerStats.GetModule<HealthStat>();
                missingHealth = Mathf.Clamp(Mathf.FloorToInt(health.MaxValue - health.CurValue), 0, 255);

                var entryRange = (missingHealth * Scp106StalkVisibilityController.HealthToDistance)
                                 + Scp106StalkVisibilityController.AbsoluteDistance
                                 + Scp106StalkVisibilityController.ServerTolerance
                                 + margin;

                if (missingHealth == 0 || distance > entryRange) {
                    return true;
                }
            }

            // Scp106StalkVisibilityController.GetVisibilityForPlayer
            if (!scp106.SubroutineModule.TryGetSubroutine(out Scp106StalkAbility stalk)
                || !stalk.StalkActive
                || scp106.Sinkhole.SubmergeProgress < 0.8f) {
                return false;
            }

            if (effects.GetEffect<Invigorated>().IsEnabled) {
                return true;
            }

            if (traumatized) {
                return false;
            }

            var radius = Scp106StalkVisibilityController.AbsoluteDistance
                         + (missingHealth * Scp106StalkVisibilityController.HealthToDistance)
                         + margin;

            return distance >= radius;
        }

        // Server version of Scp939Model.Visible, for an SCP viewer that this SCP-939 may hit.
        private static bool AmnesiaHides939(ReferenceHub viewer, ReferenceHub scp939Hub, Scp939Role scp939, bool wasHidden) {
            if (!ScpFfTargets.IsFfScpTarget(scp939Hub, viewer) || viewer.roleManager.CurrentRole is not IFpcRole viewerFpc) {
                return false;
            }

            var viewerPosition = viewerFpc.FpcModule.Position;

            var visibleRange = scp939.FpcModule.CharacterModelInstance is Scp939Model model
                ? model._amnesiaVisibleRange
                : DEFAULT_AMNESIA_VISIBLE_RANGE;

            if (Vector3.Distance(viewerPosition, scp939.FpcModule.Position) < visibleRange + (wasHidden ? 0f : HYSTERESIS)) {
                return false;
            }

            if (!viewer.playerEffectsController.TryGetEffect(out AmnesiaVision amnesia)) {
                return false;
            }

            if (amnesia.IsEnabled) {
                return true;
            }

            if (amnesia.LastActive < Scp939Model.FullVisCooldown) {
                return false;
            }

            // Standing in a cloud - only clouds of 939s that may hit this SCP count
            foreach (var cloud in Scp939AmnesticCloudInstance.ActiveInstances) {
                if (cloud != null
                    && cloud.IsInArea(cloud.SourcePosition, viewerPosition)
                    && ScpFfTargets.IsFfScpTarget(cloud.Owner, viewer)) {
                    return true;
                }
            }

            return false;
        }
    }
}

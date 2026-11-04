using System;
using HarmonyLib;
using LabApi.Events.Arguments.Scp939Events;
using LabApi.Events.Handlers;
using Mirror;
using PlayerRoles;
using PlayerRoles.FirstPersonControl;
using PlayerRoles.PlayableScps.Scp939;
using RelativePositioning;
using UnityEngine;
using Utils.Networking;
using Logger = LabApi.Features.Console.Logger;

namespace FriendlyFireManager.FfProxy {
    // SCP-939 lunge hit.
    //
    // Client side (vanilla, can't be changed): while lunging, Scp939Motor overlaps the flight path with
    // HITBOXES of players the client considers enemies, and on contact sends ClientSendHit(target).
    // SCP allies are never enemies there, so the proxy (a Tutorial, placed by ScpFfProxy inside the FF SCP
    // the 939 is aiming at) is what the lunge collides with.
    //
    // Server side (here):
    //   * cmd target is NOT the proxy -> reader is rewound, vanilla ServerProcessCmd runs untouched
    //     (humans, vanilla splash, etc.).
    //   * cmd target IS the proxy     -> map it to the real FF SCP and run the vanilla hit logic on it:
    //     same tolerance / backtracking checks, 120 lunge damage, Scp939Attacking/Attacked LabAPI events,
    //     splash to everyone ScpFfProxy.CanHit allows, hitmarker, LandHit state.
    // base.ServerProcessCmd (SubroutineBase) is empty, nothing is skipped.
    [HarmonyPatch(typeof(Scp939LungeAbility), nameof(Scp939LungeAbility.ServerProcessCmd))]
    public static class Scp939LungePatch {
        public static bool Prefix(Scp939LungeAbility __instance, NetworkReader reader) {
            // Same early-out as vanilla (it returns before reading anything)
            if (!ScpFfProxy.Enabled || ScpFfProxy.Hub == null || __instance.State != Scp939LungeState.Triggered) {
                return true;
            }

            var start = reader.Position;

            // Same read order as Scp939LungeAbility.ClientWriteCmd
            var claimedOwnerPosition = reader.ReadRelativePosition().Position;
            var claimedTarget = reader.ReadReferenceHub();
            var claimedTargetPosition = reader.ReadRelativePosition().Position;

            if (!ScpFfProxy.IsProxy(claimedTarget)) {
                reader.Position = start;
                return true;
            }

            try {
                ProcessProxyHit(__instance, claimedOwnerPosition, claimedTargetPosition);
            }
            catch (Exception e) {
                Logger.Error($"[ScpFfProxy] SCP-939 lunge failed: {e}");
            }

            return false;
        }

        private static void ProcessProxyHit(Scp939LungeAbility lunge, Vector3 claimedOwnerPosition, Vector3 claimedProxyPosition) {
            var owner = lunge.Owner;

            var target = ScpFfProxy.ResolveLungeTarget(owner, claimedProxyPosition);
            if (target == null || target.roleManager.CurrentRole is not FpcStandardRoleBase targetRole) {
                return;
            }

            var targetModule = targetRole.FpcModule;

            // ---- vanilla: was the 939 really at the target? ----
            // The claimed proxy position is a recent position of the target as the client saw it,
            // the backtracker clamps it into the target's real movement history.
            using (new FpcBacktracker(target, claimedProxyPosition)) {
                using (new FpcBacktracker(owner, targetModule.Position, Quaternion.identity)) {
                    var diff = targetModule.Position - lunge.CastRole.FpcModule.Position;
                    if (diff.SqrMagnitudeIgnoreY() > lunge._overallTolerance * lunge._overallTolerance
                        || diff.y > lunge._overallTolerance
                        || diff.y < -lunge._bottomTolerance) {
                        return;
                    }
                }
            }

            Vector3 ownerPosition;
            using (new FpcBacktracker(owner, claimedOwnerPosition, Quaternion.identity)) {
                ownerPosition = lunge.CastRole.FpcModule.Position;
            }

            // ---- vanilla: pin the victim under the 939 (for the death animation / ragdoll) ----
            var targetTransform = target.transform;
            var targetPosition = targetModule.Position;
            var targetRotation = targetTransform.rotation;
            var landingPosition = new Vector3(ownerPosition.x, targetPosition.y, ownerPosition.z);
            targetTransform.forward = -owner.transform.forward;
            targetModule.Position = landingPosition;

            var hit = false;
            var allowed = true;

            if (!Physics.Linecast(ownerPosition, targetPosition, (int)PlayerRolesUtils.AttackMask)) {
                var damage = Scp939LungeAbility.LungeDamage;
                var attacking = new Scp939AttackingEventArgs(owner, target, damage);
                Scp939Events.OnAttacking(attacking);

                if (!attacking.IsAllowed) {
                    allowed = false;
                }
                else {
                    var victim = attacking.Target.ReferenceHub;
                    damage = attacking.Damage;

                    // Goes through PlayerEvents.Hurting -> FireHurtingEvents.FireLogicsOnPlayerHurting as usual
                    hit = victim.playerStats.DealDamage(new Scp939DamageHandler(lunge.CastRole, damage, Scp939DamageType.LungeTarget));
                    if (hit) {
                        Scp939Events.OnAttacked(new Scp939AttackedEventArgs(owner, victim, damage));
                    }
                }
            }

            // Vanilla restores the victim unless it died (dead body stays under the 939).
            // (Vanilla forgets to restore when Scp939Attacking is cancelled - we do restore.)
            if (!hit || target.IsAlive()) {
                targetModule.Position = targetPosition;
                targetTransform.rotation = targetRotation;
            }

            if (!allowed) {
                return;
            }

            // ---- vanilla: splash damage around the landing spot ----
            var hitmarkerSize = hit ? Scp939LungeAbility.MainHitmarkerSize : 0f;

            foreach (var hub in ReferenceHub.AllHubs) {
                if (hub == target || !ScpFfProxy.CanHit(owner, hub) || hub.roleManager.CurrentRole is not FpcStandardRoleBase splashRole) {
                    continue;
                }

                var splashPosition = splashRole.FpcModule.Position;
                if ((splashPosition - landingPosition).sqrMagnitude > lunge._secondaryRangeSqr) {
                    continue;
                }

                // (Vanilla aborts the whole hit here with "return"; skipping just this player is the sane version.)
                if (Physics.Linecast(splashPosition, ownerPosition, (int)PlayerRolesUtils.AttackMask)) {
                    continue;
                }

                var splashDamage = Scp939LungeAbility.SecondaryDamage;
                var splashAttacking = new Scp939AttackingEventArgs(owner, hub, splashDamage);
                Scp939Events.OnAttacking(splashAttacking);
                if (!splashAttacking.IsAllowed) {
                    continue;
                }

                var splashVictim = splashAttacking.Target.ReferenceHub;
                splashDamage = splashAttacking.Damage;

                if (splashVictim.playerStats.DealDamage(new Scp939DamageHandler(lunge.CastRole, splashDamage, Scp939DamageType.LungeSecondary))) {
                    Scp939Events.OnAttacked(new Scp939AttackedEventArgs(owner, splashVictim, splashDamage));
                    hit = true;
                    hitmarkerSize = Mathf.Max(hitmarkerSize, Scp939LungeAbility.SecondaryHitmarkerSize);
                }
            }

            if (hit) {
                Hitmarker.SendHitmarkerDirectly(owner, hitmarkerSize);
            }

            lunge.State = Scp939LungeState.LandHit;
        }
    }
}

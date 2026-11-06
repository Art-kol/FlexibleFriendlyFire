using System;
using HarmonyLib;
using Mirror;
using PlayerRoles;
using PlayerRoles.FirstPersonControl;
using PlayerRoles.PlayableScps.Scp106;
using RelativePositioning;
using UnityEngine;
using FriendlyFireManager.Utilities;
using PlayerStatsSystem;
using Utils.Networking;
using Logger = LabApi.Features.Console.Logger;

namespace FriendlyFireManager.FfProxy {
    // Full replacement of Scp106Attack.ServerProcessCmd while the module is enabled.
    // base.ServerProcessCmd (SubroutineBase) is empty, so nothing is lost by skipping the original.
    //
    // Vanilla trusts the target the client picked. The client can't see SCP allies as enemies (and with the
    // proxy it often "picks" the proxy), so the server picks the target itself, the same way the client does:
    // the target with the best dot to the camera forward, inside _maxRangeSqr, from the claimed owner position.
    [HarmonyPatch(typeof(Scp106Attack), nameof(Scp106Attack.ServerProcessCmd))]
    public static class Scp106AttackPatch {
        public static bool Prefix(Scp106Attack __instance, NetworkReader reader) {
            if (!ScpFfProxy.Enabled) {
                return true;
            }

            try {
                ServerProcessCmd(__instance, reader);
            }
            catch (Exception e) {
                Logger.Error($"[ScpFfProxy] ServerProcessCmd failed: {e}");
            }

            return false;
        }

        private static void ServerProcessCmd(Scp106Attack attack, NetworkReader reader) {
            // Same early-out as vanilla
            if (attack._nextAttack > NetworkTime.time || attack.CastRole.Sinkhole.SubmergeProgress > 0f) {
                return;
            }

            // Same read order as Scp106Attack.ClientWriteCmd
            var claimedTarget = reader.ReadReferenceHub();
            var claimedTargetPosition = reader.ReadRelativePosition().Position;
            var claimedCamRotation = reader.ReadQuaternion();
            var claimedOwnerPosition = reader.ReadRelativePosition().Position;

            attack._claimedOwnerCamRotation = claimedCamRotation;
            attack._claimedOwnerPosition = claimedOwnerPosition;

            if (!TrySelectTarget(attack, claimedTarget, claimedTargetPosition, out var target, out var targetPosition)) {
                return;
            }

            attack._targetHub = target;
            attack._claimedTargetPosition = targetPosition;

            // Vanilla hit logic for EVERY target, SCPs included:
            //   1st hit  -> _damage + Corroding (ticking damage credited to this SCP-106)
            //   2nd hit  -> PocketCorroding = pocket dimension (+ vigor, sinkhole cooldown, LabAPI teleport events)
            //   Traumatized (escaped the pocket before) -> instant kill, same as humans
            // None of these effects has a human-only check, they work on any hub.
            // Config scp_vs_scp_damage.scp106_attack: on an SCP, a configured value replaces all of that with plain damage.
            var hasOverride = ScpDamageOverrides.TryGet(attack.Owner, target, ScpAttack.Scp106Attack, out var damage);

            using (new FpcBacktracker(attack.Owner, claimedOwnerPosition, claimedCamRotation)) {
                if (hasOverride) {
                    ServerShootFixed(attack, target, damage);
                }
                else {
                    attack.ServerShoot();
                }
            }
        }

        // Scp106Attack.ServerShoot with only the damage: same shot check, cooldown, sinkhole cooldown, hitmarker;
        // no Corroding, no pocket dimension, no vigor.
        private static void ServerShootFixed(Scp106Attack attack, ReferenceHub target, float damage) {
            if (!attack.VerifyShot()) {
                return;
            }

            if (!target.playerStats.DealDamage(new ScpDamageHandler(attack.Owner, damage, DeathTranslations.PocketDecay))) {
                return;
            }

            attack.SendCooldown(attack._hitCooldown);
            attack.ReduceSinkholeCooldown();
            Hitmarker.SendHitmarkerDirectly(attack.Owner, 1f);
        }

        private static bool TrySelectTarget(Scp106Attack attack, ReferenceHub? claimedTarget, Vector3 claimedTargetPosition,
            out ReferenceHub target, out Vector3 targetPosition) {
            target = null!;
            targetPosition = default;

            var owner = attack.Owner;
            var ownerPosition = attack._claimedOwnerPosition;
            var forward = attack._claimedOwnerCamRotation * Vector3.forward;

            // If the client already picked a real, valid target, it has already compared all HUMANS (with its
            // own, lag-compensated positions) - then only SCP allies are added to the comparison.
            // If it picked the proxy (or anything invalid), we compare everyone.
            var claimedIsValid = ScpFfProxy.CanHit(owner, claimedTarget);

            var bestDot = float.NegativeInfinity;
            ReferenceHub? best = null;

            foreach (var hub in ReferenceHub.AllHubs) {
                if (!ScpFfProxy.CanHit(owner, hub)) {
                    continue;
                }

                var isClaimed = claimedIsValid && hub == claimedTarget;
                if (claimedIsValid && !isClaimed && !hub.IsSCP()) {
                    continue;
                }

                if (hub.roleManager.CurrentRole is not IFpcRole fpcRole) {
                    continue;
                }

                var position = isClaimed ? claimedTargetPosition : fpcRole.FpcModule.Position;
                var offset = position - ownerPosition;
                if (offset.sqrMagnitude > attack._maxRangeSqr) {
                    continue;
                }

                var dot = Vector3.Dot(offset.normalized, forward);
                if (dot < bestDot) {
                    continue;
                }

                bestDot = dot;
                best = hub;
                targetPosition = position;
            }

            if (best == null) {
                return false;
            }

            target = best;
            return true;
        }
    }
}

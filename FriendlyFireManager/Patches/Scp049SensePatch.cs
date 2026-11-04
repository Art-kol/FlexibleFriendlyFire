using System;
using System.Collections.Generic;
using FriendlyFireManager.Utilities;
using HarmonyLib;
using LabApi.Events.Arguments.Scp049Events;
using LabApi.Events.Handlers;
using Mirror;
using PlayerRoles.FirstPersonControl;
using PlayerRoles.PlayableScps;
using PlayerRoles.PlayableScps.Scp049;
using UnityEngine;
using Utils.Networking;
using Logger = LabApi.Features.Console.Logger;

namespace FriendlyFireManager.Patches {
    // SCP-049 "Good Sense of the Doctor" on SCPs.
    //
    // The client (vanilla, can't be changed) looks for a target only among vanilla enemies and sends the cmd
    // even when it finds nobody (target = null -> vanilla 2.5 s "missed" cooldown). So the cmd always reaches
    // the server, and the server can look for SCP targets itself - no dummy needed:
    //   * client sent null      -> look for an FF SCP with the vanilla rules (CanFindTarget): in front of the
    //                              camera (dot >= 0.88), within 100 m, in line of sight / not in fog;
    //   * client sent a human   -> an FF SCP replaces it only if it wins by the same rules
    //                              (looked at at least as directly AND closer);
    //   * nothing found / wins  -> vanilla ServerProcessCmd runs untouched.
    // With an SCP target the rest is vanilla: Scp049UsingSense/UsedSense events (FireLogicsOnScp049Sensing
    // still applies), 20 s duration, speed boost, pulse effect, chase theme for the target, kill -> DeadTargets.
    [HarmonyPatch(typeof(Scp049SenseAbility), nameof(Scp049SenseAbility.ServerProcessCmd))]
    public static class Scp049SenseCmdPatch {
        public static bool Prefix(Scp049SenseAbility __instance, NetworkReader reader) {
            // Vanilla returns here before reading anything
            if (!__instance.Cooldown.IsReady || !__instance.Duration.IsReady) {
                return true;
            }

            var start = reader.Position;
            var claimedTarget = reader.ReadReferenceHub();

            ReferenceHub? scpTarget;
            try {
                scpTarget = FindFfScpTarget(__instance, claimedTarget);
            }
            catch (Exception e) {
                Logger.Error($"[FF] SCP-049 sense target search failed: {e}");
                scpTarget = null;
            }

            if (scpTarget == null) {
                reader.Position = start;
                return true;
            }

            UseSenseOn(__instance, scpTarget);
            return false;
        }

        // Vanilla CanFindTarget scoring, but over the SCPs this 049 may hit, with the client's pick as the bar to beat.
        private static ReferenceHub? FindFfScpTarget(Scp049SenseAbility sense, ReferenceHub? claimedTarget) {
            var owner = sense.Owner;
            var camera = owner.PlayerCameraReference;
            var ownerPosition = sense.CastRole.FpcModule.Position;

            var maxDistanceSqr = sense._distanceThreshold * sense._distanceThreshold;
            var minDot = sense._dotThreshold;

            if (claimedTarget != null
                && HitboxIdentity.IsEnemy(owner, claimedTarget)
                && claimedTarget.roleManager.CurrentRole is FpcStandardRoleBase claimedRole) {
                var claimedPosition = claimedRole.FpcModule.Position;
                maxDistanceSqr = (claimedPosition - ownerPosition).sqrMagnitude;
                minDot = Mathf.Max(minDot, GetDot(camera, ownerPosition, claimedPosition));
            }

            ReferenceHub? best = null;

            foreach (var hub in ReferenceHub.AllHubs) {
                if (!ScpFfTargets.IsFfScpTarget(owner, hub) || hub.roleManager.CurrentRole is not FpcStandardRoleBase role) {
                    continue;
                }

                var position = role.FpcModule.Position;
                var dot = GetDot(camera, ownerPosition, position);
                if (dot < minDot) {
                    continue;
                }

                var distanceSqr = (position - ownerPosition).sqrMagnitude;
                if (distanceSqr > maxDistanceSqr) {
                    continue;
                }

                var radius = role.FpcModule.CharacterControllerSettings.Radius;
                if (!VisionInformation.GetVisionInformation(owner, camera, role.CameraPosition, radius, sense._distanceThreshold,
                        checkFog: true, checkLineOfSight: true, 0, checkInDarkness: false).IsLooking) {
                    continue;
                }

                best = hub;
                maxDistanceSqr = distanceSqr;
                minDot = dot;
            }

            return best;
        }

        // Same as in Scp049SenseAbility.CanFindTarget (flat comparison for targets right next to 049 on the same floor)
        private static float GetDot(Transform camera, Vector3 ownerPosition, Vector3 targetPosition) {
            var toTarget = targetPosition - camera.position;
            var forward = camera.forward;

            if (Mathf.Abs((targetPosition - ownerPosition).y) < Scp049SenseAbility.HeightDiffIgnoreY
                && toTarget.sqrMagnitude < Scp049SenseAbility.NearbyDistanceSqr) {
                forward.y = 0f;
                forward.Normalize();
                toTarget.y = 0f;
            }

            return Vector3.Dot(forward, toTarget.normalized);
        }

        // Vanilla ServerProcessCmd, "valid target" branch (vanilla would reject an SCP on its IsEnemy check).
        private static void UseSenseOn(Scp049SenseAbility sense, ReferenceHub target) {
            var owner = sense.Owner;

            sense.HasTarget = false;
            sense.Target = target;

            if (target.roleManager.CurrentRole is not FpcStandardRoleBase targetRole) {
                return;
            }

            var radius = targetRole.FpcModule.CharController.radius;
            if (!VisionInformation.GetVisionInformation(owner, owner.PlayerCameraReference, targetRole.CameraPosition, radius, sense._distanceThreshold,
                    checkFog: true, checkLineOfSight: true, 0, checkInDarkness: false).IsLooking) {
                return;
            }

            var usingSense = new Scp049UsingSenseEventArgs(owner, target);
            Scp049Events.OnUsingSense(usingSense);
            if (!usingSense.IsAllowed) {
                return;
            }

            sense.Target = usingSense.Target.ReferenceHub;
            sense.Duration.Trigger(Scp049SenseAbility.EffectDuration);
            sense.HasTarget = true;
            sense.ServerSendRpc(toAll: true);
            Scp049Events.OnUsedSense(new Scp049UsedSenseEventArgs(owner, sense.Target));
        }
    }

    // Scp049SenseAbility.Update drops the target as soon as !HitboxIdentity.IsEnemy(owner, target) (every frame,
    // server side). That single call is swapped for ScpFfTargets.IsEnemyOrFfScp, so an SCP target is kept for the
    // whole duration - and still lost like a human (out of sight, duration over, died, rule removed).
    [HarmonyPatch(typeof(Scp049SenseAbility), nameof(Scp049SenseAbility.Update))]
    public static class Scp049SenseUpdatePatch {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
            var isEnemy = AccessTools.Method(typeof(HitboxIdentity), nameof(HitboxIdentity.IsEnemy), [typeof(ReferenceHub), typeof(ReferenceHub)]);
            var replacement = AccessTools.Method(typeof(ScpFfTargets), nameof(ScpFfTargets.IsEnemyOrFfScp));
            var replaced = 0;

            foreach (var instruction in instructions) {
                if (instruction.Calls(isEnemy)) {
                    instruction.operand = replacement;
                    replaced++;
                }

                yield return instruction;
            }

            if (replaced == 0) {
                Logger.Warn("[FF] Scp049SenseUpdatePatch: IsEnemy call not found - SCP sense targets will be dropped instantly.");
            }
        }
    }
}

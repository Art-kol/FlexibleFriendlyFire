using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using PlayerRoles;
using PlayerRoles.FirstPersonControl;
using PlayerRoles.PlayableScps.HumanTracker;
using PlayerRoles.PlayableScps.Scp096;
using PlayerRoles.PlayableScps.Scp106;
using PlayerRoles.PlayableScps.Scp173;
using PlayerRoles.PlayableScps.Scp939;
using UnityEngine;
using Logger = LabApi.Features.Console.Logger;

namespace FriendlyFireManager.Patches {
    // SCP-939 is blind: it only "sees" what is inside its detection radius. Vanilla never lets a 939 look at
    // SCP-096 / SCP-173 (they're allies), but with FF rules it can - and then it would trigger 096 / freeze 173
    // from any distance just by pointing its camera at them. Here the 939 counts as looking at them only while
    // they are inside the same radius the server uses to decide what the 939 sees
    // (Scp939VisibilityController.ValidateVisibility).
    public static class Scp939Perception {
        // Would this SCP-939 currently see target? (vanilla 939 vision radius, without the "allies are always
        // visible" shortcut and without touching the LastSeen cache)
        public static bool CanPerceive(ReferenceHub scp939Hub, ReferenceHub target) {
            if (scp939Hub.roleManager.CurrentRole is not Scp939Role scp939
                || scp939.VisibilityController is not Scp939VisibilityController vision
                || target.roleManager.CurrentRole is not FpcStandardRoleBase targetRole) {
                return true;
            }

            // Vanilla: after the warhead the 939 sees everything. (Vanilla also does it with one human left -
            // not for SCPs here, see Scp939SeesScpsRegardlessOfHumansPatch.)
            if (AlphaWarheadController.Detonated) {
                return true;
            }

            var range = Mathf.Max(vision.BaseRangeForPlayer(target, targetRole), vision.DetectionRangeForPlayer(target));
            range += scp939.FpcModule.MaxMovementSpeed * vision._pingTolerance;

            if ((targetRole.FpcModule.Position - scp939.FpcModule.Position).sqrMagnitude <= range * range) {
                return true;
            }

            // Vanilla "sustain": recently seen targets stay visible for a moment
            return Scp939VisibilityController.LastSeen.TryGetValue(target.netId, out var lastSeen)
                   && lastSeen.Elapsed < vision._sustain;
        }
    }

    // Vanilla Scp939VisibilityController.ValidateVisibility (server: which players' positions the 939 gets):
    //   if (AlphaWarheadController.Detonated || LastHumanTracker.IsOneTargetLeft) return true;  -> sees everyone
    // That "one human left" mode is meant for hunting the last human. With the IsEnemy patch the same check also
    // decides for SCPs the 939 may hit, so with 1 human alive the 939 saw every such SCP on the whole map.
    // Here the IsOneTargetLeft call is swapped for IsOneTargetLeftFor(target): unchanged for humans, always false
    // for SCP targets -> the 939 sees SCPs only within its radius, whatever the number of humans.
    // (Warhead detonation still makes everything visible, as in vanilla.)
    [HarmonyPatch(typeof(Scp939VisibilityController), nameof(Scp939VisibilityController.ValidateVisibility))]
    public static class Scp939SeesScpsRegardlessOfHumansPatch {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
            var isOneTargetLeft = AccessTools.PropertyGetter(typeof(LastHumanTracker), nameof(LastHumanTracker.IsOneTargetLeft));
            var replacement = AccessTools.Method(typeof(Scp939SeesScpsRegardlessOfHumansPatch), nameof(IsOneTargetLeftFor));
            var replaced = 0;

            foreach (var instruction in instructions) {
                if (!instruction.Calls(isOneTargetLeft)) {
                    yield return instruction;
                    continue;
                }

                // static bool get_IsOneTargetLeft()  ->  IsOneTargetLeftFor(this, hub)
                // (labels moved to the first new instruction: this call is a jump target of the "Detonated ||" check)
                var loadThis = new CodeInstruction(OpCodes.Ldarg_0);
                loadThis.labels.AddRange(instruction.labels);
                instruction.labels.Clear();

                yield return loadThis;
                yield return new CodeInstruction(OpCodes.Ldarg_1);
                yield return new CodeInstruction(OpCodes.Call, replacement);
                replaced++;
            }

            if (replaced != 1) {
                Logger.Warn($"[FF] Scp939SeesScpsRegardlessOfHumansPatch: expected 1 IsOneTargetLeft call, replaced {replaced}.");
            }
        }

        public static bool IsOneTargetLeftFor(Scp939VisibilityController vision, ReferenceHub target) {
            return LastHumanTracker.IsOneTargetLeft && !target.IsSCP();
        }
    }

    // Which SCPs can't "look" at SCP-096 / SCP-173 even though FF rules let them (IsObservedBy below):
    //   * SCP-939 - only within its vision radius (Scp939Perception.CanPerceive)
    //   * SCP-106 - not at all while it is underground, even partly (SubmergeProgress > 0) - the same moment
    //     vanilla stops letting it attack (Scp106Attack.ServerProcessCmd). Its camera stays above the floor,
    //     so without this a buried 106 triggered 096 / froze 173 from anywhere.
    public static class ScpObservers {
        public static bool CanObserve(ReferenceHub observer, ReferenceHub observed) {
            return observer.roleManager.CurrentRole switch {
                Scp939Role => Scp939Perception.CanPerceive(observer, observed),
                Scp106Role scp106 => scp106.Sinkhole.SubmergeProgress <= 0f,
                _ => true
            };
        }
    }

    // SCP-096 rage trigger ("looked at the face"). Hitting 096 still adds the attacker as a target as in vanilla
    // (a 939 has to be next to it for that, a buried 106 can't attack at all).
    [HarmonyPatch(typeof(Scp096TargetsTracker), nameof(Scp096TargetsTracker.IsObservedBy))]
    public static class Scp096ObservedByScpPatch {
        public static bool Prefix(Scp096TargetsTracker __instance, ReferenceHub target, ref bool __result) {
            if (ScpObservers.CanObserve(target, __instance.Owner)) {
                return true; // vanilla
            }

            __result = false;
            return false;
        }
    }

    // SCP-173 freeze. Checked every update, so walking out of a 939's radius / a 106 going underground
    // releases 173 right away.
    [HarmonyPatch(typeof(Scp173ObserversTracker), nameof(Scp173ObserversTracker.IsObservedBy))]
    public static class Scp173ObservedByScpPatch {
        public static bool Prefix(Scp173ObserversTracker __instance, ReferenceHub target, ref bool __result) {
            if (ScpObservers.CanObserve(target, __instance.Owner)) {
                return true; // vanilla
            }

            __result = false;
            return false;
        }
    }
}

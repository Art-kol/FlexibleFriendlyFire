using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Mirror;
using PlayerRoles.PlayableScps.Scp173;
using Utils.Networking;
using Logger = LabApi.Features.Console.Logger;

namespace FriendlyFireManager.Patches {
    // Purpose: Let SCP-173 kill an SCP it is allowed to attack by blinking onto it.
    //
    // Problem: the blink-kill target is calculated on the CLIENT (Scp173TeleportAbility.BestTarget)
    //          and sent to the server. The client runs vanilla IsEnemy, so it never picks an SCP.
    //          (Neck snap is the same - the client never even sends the command for an SCP target,
    //           so it cannot be fixed server-side.)
    //
    // What it does: in ServerProcessCmd we replace
    //                   ReferenceHub target = reader.ReadReferenceHub();
    //               with
    //                   ReferenceHub target = ReadBlinkTarget(reader, this);
    //               ReadBlinkTarget keeps the client's target if it sent one (vanilla behaviour vs humans),
    //               otherwise the SERVER calculates BestTarget itself, with our patched IsEnemy.
    //               All vanilla checks after that (IsEnemy, distance, line of sight) still run.
    //               173 never targets itself: ScpFriendlyFirePatch returns false for attacker == victim.
    [HarmonyPatch(typeof(Scp173TeleportAbility), nameof(Scp173TeleportAbility.ServerProcessCmd))]
    public static class Scp173TeleportPatch {
        private static readonly MethodInfo _readReferenceHubMethod =
            AccessTools.Method(typeof(ReferenceHubReaderWriter), nameof(ReferenceHubReaderWriter.ReadReferenceHub), new[] { typeof(NetworkReader) });

        private static readonly MethodInfo _readBlinkTargetMethod =
            AccessTools.Method(typeof(Scp173TeleportPatch), nameof(ReadBlinkTarget));

        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
            var original = new List<CodeInstruction>(instructions);
            var matcher = new CodeMatcher(original);

            // ... ldarg.1 (reader) | call ReferenceHubReaderWriter::ReadReferenceHub(NetworkReader) ...
            matcher.MatchStartForward(new CodeMatch(instruction => instruction.Calls(_readReferenceHubMethod)));

            if (matcher.IsInvalid) {
                Logger.Error("[FriendlyFire] Scp173TeleportPatch: ReadReferenceHub call not found, patch skipped (game update?)");
                return original;
            }

            // ... ldarg.1 (reader) | ldarg.0 (this) | call ReadBlinkTarget(NetworkReader, Scp173TeleportAbility) ...
            matcher
                .InsertAndAdvance(new CodeInstruction(OpCodes.Ldarg_0))
                .SetOperandAndAdvance(_readBlinkTargetMethod);

            return matcher.InstructionEnumeration();
        }

        public static ReferenceHub? ReadBlinkTarget(NetworkReader reader, Scp173TeleportAbility ability) {
            var clientTarget = reader.ReadReferenceHub();

            if (clientTarget != null && clientTarget != ability.Owner) {
                return clientTarget;
            }

            // Uses ability._tpPosition, which TryBlink() has already set on the server at this point
            return ability.BestTarget;
        }
    }
}

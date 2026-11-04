using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using PlayerRoles;
using PlayerRoles.PlayableScps.Scp096;
using Logger = LabApi.Features.Console.Logger;

namespace FriendlyFireManager.Patches {

    // Purpose: Let SCP-096 hit (charge / swing) an SCP it is allowed to attack.
    //
    // Problem: Scp096HitHandler.ProcessHits checks the victim with the TEAM overload:
    //              HitboxIdentity.IsEnemy(Team.SCPs, hitboxIdentity.TargetHub.GetTeam())
    //          It doesn't know which 096 is attacking, so ScpFriendlyFirePatch can't work there.
    //
    // What it does: replaces that call with
    //                   Scp096IsEnemy(Team.SCPs, hitboxIdentity.TargetHub, this)
    //               which takes the real 096 player and calls IsEnemy(ReferenceHub, ReferenceHub).
    //               For non-SCP victims the result is exactly the same as vanilla.
    [HarmonyPatch(typeof(Scp096HitHandler), "ProcessHits")]
    public static class Scp096HitPatch {
        private static readonly MethodInfo _getTeamMethod =
            AccessTools.Method(typeof(PlayerRolesUtils), nameof(PlayerRolesUtils.GetTeam), new[] { typeof(ReferenceHub) });

        private static readonly MethodInfo _isEnemyTeamMethod =
            AccessTools.Method(typeof(HitboxIdentity), nameof(HitboxIdentity.IsEnemy), new[] { typeof(Team), typeof(Team) });

        private static readonly MethodInfo _scp096IsEnemyMethod =
            AccessTools.Method(typeof(Scp096HitPatch), nameof(Scp096IsEnemy));

        private static readonly AccessTools.FieldRef<Scp096HitHandler, Scp096Role> _scpRoleField =
            AccessTools.FieldRefAccess<Scp096HitHandler, Scp096Role>("_scpRole");

        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
            var original = new List<CodeInstruction>(instructions);
            var matcher = new CodeMatcher(original);

            // ... ldc.i4.0 (Team.SCPs) | ldloc hitbox | callvirt get_TargetHub | call GetTeam(ReferenceHub) | call IsEnemy(Team, Team) ...
            matcher.MatchStartForward(
                new CodeMatch(instruction => instruction.Calls(_getTeamMethod)),
                new CodeMatch(instruction => instruction.Calls(_isEnemyTeamMethod)));

            if (matcher.IsInvalid) {
                Logger.Error("[FriendlyFire] Scp096HitPatch: IsEnemy(Team, Team) call not found, patch skipped (game update?)");
                return original;
            }

            // ... ldc.i4.0 (Team.SCPs) | ldloc hitbox | callvirt get_TargetHub | ldarg.0 (this) | call Scp096IsEnemy(Team, ReferenceHub, Scp096HitHandler) ...
            matcher
                .SetAndAdvance(OpCodes.Ldarg_0, null)
                .SetOperandAndAdvance(_scp096IsEnemyMethod);

            return matcher.InstructionEnumeration();
        }

        public static bool Scp096IsEnemy(Team attackerTeam, ReferenceHub victim, Scp096HitHandler handler) {
            if (victim == null) {
                return false;
            }

            var scpRole = _scpRoleField(handler);

            if (scpRole != null && scpRole.TryGetOwner(out var owner)) {
                return HitboxIdentity.IsEnemy(owner, victim);
            }

            return HitboxIdentity.IsEnemy(attackerTeam, victim.GetTeam());
        }
    }
}

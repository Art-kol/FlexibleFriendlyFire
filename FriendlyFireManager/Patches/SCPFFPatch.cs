using HarmonyLib;
using PlayerRoles;

namespace FriendlyFireManager.Patches {
    [HarmonyPatch(typeof(HitboxIdentity), nameof(HitboxIdentity.IsEnemy), new[] { typeof(Team), typeof(Team) })]
    public static class HitboxIdentityTeamIsEnemy {
        [HarmonyPrefix]
        public static bool Prefix(Team attackerTeam, Team victimTeam, ref bool __result) {
            __result = attackerTeam != Team.Dead && victimTeam != Team.Dead && attackerTeam.GetFaction() != victimTeam.GetFaction();
            __result = __result || attackerTeam == Team.SCPs || victimTeam == Team.SCPs;
            return false;
        }
    }

    [HarmonyPatch(typeof(HitboxIdentity), nameof(HitboxIdentity.IsEnemy), new[] { typeof(ReferenceHub), typeof(ReferenceHub) })]
    public static class HitboxIdentityReferenceHubIsEnemy {
        [HarmonyPrefix]
        public static bool Prefix(ReferenceHub attacker, ReferenceHub victim, ref bool __result) {
            if (!attacker.IsSCP() || !victim.IsSCP()) {
                return true;
            }

            if (attacker == victim) {
                __result = false;
                return false;
            }

            __result = FireLogics.CanDamage(attacker, victim) == true;
            return false;
        }
    }
}


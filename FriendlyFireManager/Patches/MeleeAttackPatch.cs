using HarmonyLib;

namespace FriendlyFireManager.Patches {
    [HarmonyPatch(typeof(HitboxIdentity), nameof(HitboxIdentity.IsDamageable), typeof(ReferenceHub), typeof(ReferenceHub))]
    // Purpose: If a attacker can/cant attack another target then damage delt by melees should/shoudnt apply on the target
    //          see Handlers/HurtingEvents FireLogicsOnPlayerHurting for more
    public class HitboxIdentityPatch {
        public static bool Prefix(ReferenceHub attacker, ReferenceHub victim, ref bool __result) {
            if (attacker == null || victim == null) {
                return true;
            }

            var result = FireLogics.CanDamage(attacker, victim);

            if (result != null) {
                __result = result.Value;
                return false;
            }

            return true;
        }
    }
}
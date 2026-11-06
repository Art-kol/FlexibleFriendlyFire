using HarmonyLib;
using InventorySystem.Items.Pickups;
using UnityEngine;
using FriendlyFireManager.Handlers;
using FriendlyFireManager.Utilities;

namespace FriendlyFireManager.Patches {
    public class SCP079BreakWindowPatchThrowables {
        [HarmonyPatch(typeof(CollisionDetectionPickup), nameof(CollisionDetectionPickup.ProcessCollision))]
        // Purpose: If a player cant attack 079 then any throwables thrown at 079 recontam button wont break the window
        //          (see Handlers/SCP079Events FireLogics079ShootingWindow for more)
        //          Several SCP-079s: config scp079.*_recontainment_button (Any / All)
        public static class CollisionDetectionPickupPatch {
            // __state = the still whole recontainment glass this throwable hits (null = any other collision)
            private static bool Prefix(CollisionDetectionPickup __instance, Collision collision, out BreakableWindow? __state) {
                __state = null;

                if (!collision.collider.TryGetComponent<BreakableWindow>(out var window)
                    || window.IsBroken
                    || !ScpFfTargets.IsRecontainerGlass(window)) {
                    return true;
                }

                // Unknown thrower (left the server...) -> vanilla
                var thrower = __instance.PreviousOwner.IsSet ? __instance.PreviousOwner.Hub : null;
                if (thrower == null) {
                    return true;
                }

                if (!ScpFfTargets.MayActAgainst079(thrower, FireScp079Events.RecontainmentButtonRequirement(thrower))) {
                    return false;
                }

                __state = window;
                return true;
            }

            // Vanilla damages the glass without an attacker here (BreakableWindow.Damage(damage, null, ...)), so it never
            // learns who broke it. Remember the thrower for config scp079.*_recontainment_targets.
            private static void Postfix(CollisionDetectionPickup __instance, BreakableWindow? __state) {
                var thrower = __instance.PreviousOwner.Hub;
                if (__state != null && __state.IsBroken && thrower != null) {
                    FireScp079Events.RememberGlassBreaker(thrower);
                }
            }
        }
    }
}

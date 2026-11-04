using System.Linq;
using HarmonyLib;
using InventorySystem.Items.Pickups;
using LabApi.Features.Wrappers;
using PlayerRoles;
using PlayerRoles.PlayableScps.Scp079;
using UnityEngine;
using static FriendlyFireManager.FireLogics;
using static FriendlyFireManager.Handlers.FireScp079Events;

namespace FriendlyFireManager.Patches {
    public class SCP079BreakWindowPatchThrowables {
        [HarmonyPatch(typeof(CollisionDetectionPickup), nameof(CollisionDetectionPickup.ProcessCollision))]
        // Purpose: If a player cant attack 079 then any throwables thrown at 079 recontam button wont break the window
        //          (see Handlers/SCP079Events FireLogics079ShootingWindow for more)
        public static class CollisionDetectionPickupPatch {
            private static bool Prefix(CollisionDetectionPickup __instance, Collision collision) {
                //Logger.Debug($"CollisionDetectionPickupPatch.Prefix called on {__instance.name}");
                if (!Recontainer) {
                    Recontainer = Object.FindAnyObjectByType<Scp079Recontainer>()._activatorGlass;
                }

                //Logger.Debug($"Recontainer found: {_knownWindow != null}");
                if (!collision.collider.TryGetComponent<BreakableWindow>(out var window)
                    || Recontainer == null
                    || window != Recontainer) {
                    // Logger.Debug("Not a breakable window or activator glass, returning true");
                    return true;
                }

                //Logger.Debug($"Window detected: {window != null}, Is activator glass: {_knownWindow != null && window == _knownWindow}");

                if (__instance.PreviousOwner.IsSet) {
                    //Logger.Debug($"Previous owner is set: {__instance.PreviousOwner.Hub?.nicknameSync.Network_myNickSync}");
                    var hub = __instance.PreviousOwner.Hub;

                    foreach (var player in Player.List.Where(p => p.Role == RoleTypeId.Scp079)) {
                        //Logger.Debug($"Checking SCP079 player: {player.ReferenceHub.nicknameSync.Network_myNickSync}, CanDamage: {CanDamage(hub, player.ReferenceHub)}");
                        if (CanDamage(hub, player.ReferenceHub) != false) {
                            // Logger.Debug($"CanDamage returned not false, returning true");
                            return true;
                        }
                    }
                    // Logger.Debug($"All SCP079 players cannot damage, returning false");
                    return false;
                }

                //Logger.Debug("Previous owner not set, returning true");
                return true;
            }
        }
    }
}
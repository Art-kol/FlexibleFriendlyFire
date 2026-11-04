using CustomPlayerEffects;
using FriendlyFireManager.Utilities;
using HarmonyLib;
using PlayerRoles;
using PlayerRoles.FirstPersonControl;
using PlayerRoles.PlayableScps;
using PlayerRoles.PlayableScps.Scp049.Zombies;
using UnityEngine;

namespace FriendlyFireManager.Patches {

    [HarmonyPatch(typeof(ZombieBloodlustAbility), nameof(ZombieBloodlustAbility.AnyTargets))]
    // Purpose: If zombie cant attack the player, zombie wont get bloodlust.
    //          If zombie CAN attack an SCP (FF rule), that SCP triggers bloodlust exactly like a human.
    // What it does: vanilla AnyTargets (humans only) + SCPs allowed by ScpFfTargets.IsFfScpTarget
    public static class ZombieBloodlustPatch {
        public static bool Prefix(ReferenceHub owner, Transform camera, ref bool __result, ZombieBloodlustAbility __instance) {
            foreach (var target in ReferenceHub.AllHubs) {
                if (!(target.IsHuman() || ScpFfTargets.IsFfScpTarget(owner, target))
                    || FireLogics.CanDamage(owner, target) == false
                    || target.playerEffectsController.GetEffect<Invisible>().IsEnabled
                    || target.roleManager.CurrentRole is not IFpcRole fpcRole) {

                    continue;
                }

                if (VisionInformation.GetVisionInformation(
                    owner,
                    camera,
                    fpcRole.FpcModule.Position,
                    fpcRole.FpcModule.CharacterControllerSettings.Radius,
                    __instance._maxViewDistance,
                    checkFog: true,
                    checkLineOfSight: true,
                    0,
                    checkInDarkness: false
                    ).IsLooking) {

                    __result = true;
                    return false;
                }
            }

            __result = false;
            return false;
        }
    }
}
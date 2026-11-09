using HarmonyLib;
using PlayerRoles;
using PlayerRoles.FirstPersonControl;
using PlayerRoles.FirstPersonControl.NetworkMessages;
using RelativePositioning;
using RemoteAdmin.Communication;

namespace FriendlyFireManager.FfProxy {
    // Which role every client sees for the proxy:
    //   SCP-106 / SCP-939 that needs it -> its real role (Tutorial) = "enemy" for the vanilla client
    //   everyone else          -> Spectator (no model, nothing in the world)
    // Used by both FpcServerPositionDistributor.WriteAll and PlayerRoleManager.SendNewRoleInfo.
    // Runs BEFORE RoleSyncEvent, so DisguiseModule's FakeRoleManager handler just passes our value through.
    [HarmonyPatch(typeof(FpcServerPositionDistributor), nameof(FpcServerPositionDistributor.GetVisibleRole))]
    public static class ScpFfProxyVisibleRolePatch {
        public static bool Prefix(ReferenceHub receiver, ReferenceHub target, ref RoleTypeId __result) {
            if (!ScpFfProxy.IsProxy(target)
                || receiver == target
                || receiver.isLocalPlayer
                || target.roleManager.CurrentRole is not IFpcRole) {
                return true;
            }

            __result = ScpFfProxy.ShouldAssist(receiver) ? target.GetRoleId() : RoleTypeId.Spectator;
            return false;
        }
    }

    // Position of the proxy sent per receiver (instead of the real parking spot far outside the map):
    //   SCP-106 -> right under its feet (inside the attack range)
    //   SCP-939 -> inside the FF SCP it is aiming at (where the lunge will collide)
    [HarmonyPatch(typeof(FpcServerPositionDistributor), nameof(FpcServerPositionDistributor.GetNewSyncData))]
    public static class ScpFfProxySyncDataPatch {
        public static bool Prefix(ReferenceHub receiver, ReferenceHub target, FirstPersonMovementModule fpmm, bool isInvisible, ref FpcSyncData __result) {
            if (isInvisible || !ScpFfProxy.IsProxy(target) || !ScpFfProxy.ShouldAssist(receiver)) {
                return true;
            }

            var spoofedPosition = ScpFfProxy.GetSpoofedPosition(receiver);
            var previous = FpcServerPositionDistributor.GetPrevSyncData(receiver, target); // also creates the receiver's dictionary

            // grounded bit = false: on the client FpcModule.IsGrounded is set straight from this bit, and
            // AnimatedCharacterModel only plays footsteps (and landing sounds) for grounded players -> proxy is silent.
            // Sneaking just in case: the quietest movement state.
            __result = new FpcSyncData(previous, PlayerMovementState.Sneaking, false, new RelativePosition(spoofedPosition), fpmm.MouseLook);
            FpcServerPositionDistributor.PreviouslySent[receiver.netId][target.netId] = __result;
            return false;
        }
    }

    // Safety net: nothing on the server should ever treat the proxy as an enemy / damageable.
    // Priority.First: runs before HitboxIdentityPatch / HitboxIdentityTeamIsEnemyPatch; once a prefix returns false,
    // Harmony skips the remaining bool prefixes, so our "false" wins.
    [HarmonyPatch]
    public static class ScpFfProxyGuardPatches {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        [HarmonyPatch(typeof(HitboxIdentity), nameof(HitboxIdentity.IsEnemy), [typeof(ReferenceHub), typeof(ReferenceHub)])]
        public static bool IsEnemyPrefix(ReferenceHub attacker, ReferenceHub victim, ref bool __result) {
            if (!ScpFfProxy.IsProxy(attacker) && !ScpFfProxy.IsProxy(victim)) {
                return true;
            }

            __result = false;
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        [HarmonyPatch(typeof(HitboxIdentity), nameof(HitboxIdentity.IsDamageable), [typeof(ReferenceHub), typeof(ReferenceHub)])]
        public static bool IsDamageablePrefix(ReferenceHub attacker, ReferenceHub victim, ref bool __result) {
            if (!ScpFfProxy.IsProxy(attacker) && !ScpFfProxy.IsProxy(victim)) {
                return true;
            }

            __result = false;
            return false;
        }
    }

    // Remote Admin "Dummies" panel (RaDummyActions): the proxy's actions are never sent, like it isn't in the RA player
    // list (ScpFfProxy.OnRaPlayerListAddingPlayer).
    [HarmonyPatch(typeof(RaDummyActions), nameof(RaDummyActions.AppendDummy))]
    public static class ScpFfProxyRaDummyActionsPatch {
        public static bool Prefix(ReferenceHub dummy) {
            return !ScpFfProxy.IsProxy(dummy);
        }
    }
}

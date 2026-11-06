using HarmonyLib;
using Mirror;
using PlayerRoles;
using PlayerRoles.FirstPersonControl.NetworkMessages;
using PlayerRoles.PlayableScps.Scp3114;

namespace FriendlyFireManager.Patches {
    // SCP-3114's disguise against the SCPs it may hurt (config scp3114_disguise_fools_scps).
    //
    // Vanilla: every client gets SCP-3114 as SCP-3114. The client draws the stolen human on top of the skeleton, and if
    // the LOCAL player is an SCP it uses a see-through "teammate" look, so SCPs always recognise it. That choice is
    // made by the client from its own role - the server can't change it.
    //
    // So, the same way the admin disguise of TakChanges (DisguiseModule) works: while the disguise is ACTIVE, every SCP
    // that this SCP-3114 may hurt (FireLogics.CanDamage(3114, scp) == true) gets SCP-3114 sent as the stolen human role,
    // per client (FpcServerPositionDistributor.GetVisibleRole). On that client it is a normal human of that class: model,
    // held item, unit name; it isn't in the SCP list. When the disguise drops (reveal, timeout, skin lost) or the rule is
    // removed, the real role is sent again and the skeleton is back. Humans, other SCPs, spectators and the SCP-3114 itself
    // see vanilla.
    //
    // Not spoofed: the nickname. The fooled SCP sees the real nickname of the SCP-3114 player instead of the victim's
    // (the name is a synced variable of the player, the same for everyone).
    public static class Scp3114FfDisguise {
        // Does this SCP-3114 currently fool this observer? stolenRole = what the observer should see.
        public static bool Fools(ReferenceHub? scp3114, ReferenceHub? observer, out RoleTypeId stolenRole) {
            stolenRole = RoleTypeId.None;

            if (!FfConfig.Current.Scp3114DisguiseFoolsScps
                || scp3114 == null || observer == null || scp3114 == observer || !observer.IsSCP()) {
                return false;
            }

            if (scp3114.roleManager.CurrentRole is not Scp3114Role role
                || role.CurIdentity.Status != Scp3114Identity.DisguiseStatus.Active) {
                return false;
            }

            var stolen = role.CurIdentity.StolenRole;
            if (!stolen.TryGetRoleTemplate<HumanRole>(out _)) {
                return false;
            }

            if (FireLogics.CanDamage(scp3114, observer) != true) {
                return false;
            }

            stolenRole = stolen;
            return true;
        }
    }

    // Which role this receiver gets for SCP-3114 (FpcServerPositionDistributor.WriteAll / PlayerRoleManager.SendNewRoleInfo).
    // WriteAll compares it with the role sent before, so a change (disguise on / off, rule added / removed) is sent
    // automatically on the next tick.
    [HarmonyPatch(typeof(FpcServerPositionDistributor), nameof(FpcServerPositionDistributor.GetVisibleRole))]
    public static class Scp3114DisguiseVisibleRolePatch {
        public static void Postfix(ReferenceHub receiver, ReferenceHub target, ref RoleTypeId __result) {
            if (__result != RoleTypeId.Scp3114 || receiver.isLocalPlayer || target.isLocalPlayer) {
                return;
            }

            if (Scp3114FfDisguise.Fools(target, receiver, out var stolenRole)) {
                __result = stolenRole;
            }
        }
    }

    // RoleSyncInfo spawn data: the receiving client reads the spawn data of the HUMAN role, while the server writes the one
    // of SCP-3114 (position + rotation, FpcStandardRoleBase). Humans with unit names (MTF, guards) read one more byte
    // first - the unit name - so it is prepended here (the same byte the vanilla disguise uses: CurIdentity.UnitNameId).
    [HarmonyPatch(typeof(FpcServerPositionDistributor), nameof(FpcServerPositionDistributor.SendRole))]
    public static class Scp3114DisguiseSendRolePatch {
        public static void Prefix(ReferenceHub receiver, ReferenceHub hub, RoleTypeId targetRole, NetworkWriter spoofedData) {
            // Something else (an admin disguise plugin) already wrote spawn data -> leave it alone
            if (spoofedData == null || spoofedData.Position != 0 || targetRole == RoleTypeId.Scp3114 || receiver == hub) {
                return;
            }

            if (!Scp3114FfDisguise.Fools(hub, receiver, out var stolenRole) || stolenRole != targetRole) {
                return;
            }

            if (stolenRole.TryGetRoleTemplate<HumanRole>(out var template) && template.UsesUnitNames) {
                spoofedData.WriteByte(((Scp3114Role)hub.roleManager.CurrentRole).CurIdentity.UnitNameId);
            }
        }
    }
}

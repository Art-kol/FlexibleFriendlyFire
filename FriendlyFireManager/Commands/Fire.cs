using System;
using System.Collections.Generic;
using System.Text;
using CommandSystem;
using FriendlyFireManager.Commands.HelpersForCommand;
using FriendlyFireManager.Utilities;
using LabApi.Features.Wrappers;
using PlayerRoles;
using Utils;
using static FriendlyFireManager.StaticData;

namespace FriendlyFireManager.Commands {
    internal static class FireCommandHelper {
        public delegate void FFAction(
            char attackerType,
            char targetType,
            List<ReferenceHub> attackerPlayers,
            List<RoleTypeId> attackerRoles,
            List<ReferenceHub> targetPlayers,
            List<RoleTypeId> targetRoles
        );

        public static bool ExecuteRule(ArraySegment<string> arguments, ICommandSender sender, out string response, FFAction action, string successMessage, string usage, bool reset = false) {
            response = "";

            if (!FirePreCommandChecks.SenderCheck(sender, out var failMessage)) {
                response = failMessage;
                return false;
            }

            if (reset && (arguments.Count == 0 || string.IsNullOrWhiteSpace(arguments.At(0)))) {
                FireLogics.ClearAll();

                // ADDED:
                FfProxy.ScpFfProxy.RequestRefresh();

                response = "All fire rules have been reset.";
                return true;
            }

            if (!FirePreCommandChecks.ArgumentCheck(arguments, out failMessage)) {
                response = usage + Usages.GetClassList() + failMessage;
                return false;
            }

            string type = arguments.At(0).ToUpper();

            char attackerType = type[0];
            char targetType = type[1];

            bool mutual = true;

            if (arguments.Count >= 4 && int.TryParse(arguments.At(3), out int res)) {
                mutual = res != 0;
            }

            List<ReferenceHub> attackerPlayers = new();
            List<RoleTypeId> attackerRoles = new();

            List<ReferenceHub> targetPlayers = new();
            List<RoleTypeId> targetRoles = new();

            if (attackerType == 'P' && !(arguments.At(1) is "all" or "*")) {
                attackerPlayers = RAUtils.ProcessPlayerIdOrNamesList(arguments, 1, out _);

                if (!FirePreCommandChecks.PlayerIDCheck(attackerPlayers, out failMessage)) {
                    response = usage + failMessage;
                    return false;
                }
            }
            else {
                if (arguments.At(1) is "*" or "all") {
                    attackerType = 'R';
                    attackerRoles.AddRange(AvailableRoles);
                }
                else if (!WondertainmentUtils.TryParseRoleTypeIDsList(arguments.At(1), out attackerRoles)) {
                    response = Usages.GetClassList() + "Incorrect ATTACKER Role IDs!";
                    return false;
                }
            }

            if (targetType == 'P' && !(arguments.At(2) is "all" or "*")) {
                targetPlayers = RAUtils.ProcessPlayerIdOrNamesList(arguments, 2, out _);

                if (!FirePreCommandChecks.PlayerIDCheck(targetPlayers, out failMessage)) {
                    response = usage + failMessage;
                    return false;
                }
            }
            else {
                if (arguments.At(2) is "*" or "all") {
                    targetType = 'R';
                    targetRoles.AddRange(AvailableRoles);
                }
                else if (!WondertainmentUtils.TryParseRoleTypeIDsList(arguments.At(2), out targetRoles)) {
                    response = Usages.GetClassList() + "Incorrect TARGET Role IDs!";
                    return false;
                }
            }

            action(attackerType, targetType,
                attackerPlayers, attackerRoles,
                targetPlayers, targetRoles);

            if (mutual) {
                action(targetType, attackerType,
                    targetPlayers, targetRoles,
                    attackerPlayers, attackerRoles);
            }

            // ADDED:
            // Rules changed -> spawn/remove the SCP-106/939 FF proxy right away instead of waiting for the poll
            FfProxy.ScpFfProxy.RequestRefresh();

            response = successMessage + (mutual ? " [MUTUAL]" : "");
            return true;
        }
    }

    [CommandHandler(typeof(RemoteAdminCommandHandler))]
    [CommandHandler(typeof(GameConsoleCommandHandler))]
    public class Firerule : ParentCommand, ICommand, IUsageProvider {
        public Firerule() {
            LoadGeneratedCommands();
        }

        public override string Command => "fire";
        public override string[] Aliases { get; } = { "fire" };
        public override string Description => "Allows/blocks/resets to default/lists damage between chosen players/roles.";
        public string[] Usage { get; } = ["allow/block/reset/list", "PP/PR/RP/RR", "Attacker PlayerID('P')/RoleID('R')", "Attacked PlayerID/RoleID", "Optional:0/1 (1 - action's mutual)"];

        public sealed override void LoadGeneratedCommands() {
            RegisterCommand(new FireruleAllowCommand());
            RegisterCommand(new FireruleBlockCommand());
            RegisterCommand(new FireruleResetCommand());
            RegisterCommand(new FireruleListCommand());
        }

        protected override bool ExecuteParent(ArraySegment<string> arguments, ICommandSender sender, out string response) {
            response = Usages.GetCommonUsage();
            return false;
        }
    }

    public class FireruleAllowCommand : ICommand {
        public string Command => "allow";
        public string[] Aliases { get; } = { "allow" };
        public string Description => "Allows damage between chosen players/roles.";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response) {
            return FireCommandHelper.ExecuteRule(arguments, sender, out response, ApplyAllow, "Allow rule applied", Usages.GetAllowUsage());
        }

        // ================= HELPER =================
        private void ApplyAllow(
            char attackerType,
            char targetType,
            List<ReferenceHub> attackingPlayers,
            List<RoleTypeId> attackingRoles,
            List<ReferenceHub> targetPlayers,
            List<RoleTypeId> targetRoles) {

            if (attackerType == 'P' && targetType == 'P') {
                FireLogics.AllowAttacks(attackingPlayers, targetPlayers);
            }
            else if (attackerType == 'P' && targetType == 'R') {
                FireLogics.AllowAttacks(attackingPlayers, targetRoles);
            }
            else if (attackerType == 'R' && targetType == 'P') {
                FireLogics.AllowAttacks(attackingRoles, targetPlayers);
            }
            else if (attackerType == 'R' && targetType == 'R') {
                FireLogics.AllowAttacks(attackingRoles, targetRoles);
            }
        }
    }

    public class FireruleBlockCommand : ICommand {
        public string Command => "block";
        public string[] Aliases { get; } = { "block" };
        public string Description => "Blocks damage between chosen players/roles.";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response) {
            return FireCommandHelper.ExecuteRule(arguments, sender, out response, ApplyBlock, "Block rule applied", Usages.GetBlockUsage());
        }

        // ================= HELPER =================
        private void ApplyBlock(
            char attackerType,
            char targetType,
            List<ReferenceHub> attackingPlayers,
            List<RoleTypeId> attackingRoles,
            List<ReferenceHub> targetPlayers,
            List<RoleTypeId> targetRoles) {

            if (attackerType == 'P' && targetType == 'P') {
                FireLogics.BlockAttacks(attackingPlayers, targetPlayers);
            }
            else if (attackerType == 'P' && targetType == 'R') {
                FireLogics.BlockAttacks(attackingPlayers, targetRoles);
            }
            else if (attackerType == 'R' && targetType == 'P') {
                FireLogics.BlockAttacks(attackingRoles, targetPlayers);
            }
            else if (attackerType == 'R' && targetType == 'R') {
                FireLogics.BlockAttacks(attackingRoles, targetRoles);
            }
        }
    }

    public class FireruleResetCommand : ICommand {
        public string Command => "reset";
        public string[] Aliases { get; } = { "reset" };
        public string Description => "Resets damage rules between players/roles or clears all.";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response) {
            return FireCommandHelper.ExecuteRule(arguments, sender, out response, ApplyReset, "Rule reset", Usages.GetResetUsage(), true);
        }

        // ================= HELPER =================
        private void ApplyReset(
            char attackerType,
            char targetType,
            List<ReferenceHub> attackingPlayers,
            List<RoleTypeId> attackingRoles,
            List<ReferenceHub> targetPlayers,
            List<RoleTypeId> targetRoles) {

            if (attackerType == 'P' && targetType == 'P') {
                FireLogics.ResetAttacks(attackingPlayers, targetPlayers);
            }
            else if (attackerType == 'P' && targetType == 'R') {
                FireLogics.ResetAttacks(attackingPlayers, targetRoles);
            }
            else if (attackerType == 'R' && targetType == 'P') {
                FireLogics.ResetAttacks(attackingRoles, targetPlayers);
            }
            else if (attackerType == 'R' && targetType == 'R') {
                FireLogics.ResetAttacks(attackingRoles, targetRoles);

            }
        }
    }

    public class FireruleListCommand : ICommand {
        public string Command => "list";
        public string[] Aliases { get; } = { "ls" };
        public string Description => "Lists current damage rules between players/roles.";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response) {
            // ===== CHECKS =====
            if (!FirePreCommandChecks.SenderCheck(sender, out var failResponse1)) {
                response = failResponse1;
                return false;
            }


            // Not a rule, but works like "fire allow RR <every SCP> <every SCP>" under all rules
            var serverFriendlyFire = FireLogics.ScpFriendlyFireFromServer
                ? "\n\nSCP vs SCP: allowed by the server friendly fire (config scp_vs_scp_with_server_friendly_fire)."
                : "";

            var activeFFRules = new StringBuilder();

            activeFFRules.AppendLine("=== ALLOW RULES ===");

            AppendPP(activeFFRules, FireLogics.AllowPlayerPlayerFriendlyFire, "PP");
            AppendPR(activeFFRules, FireLogics.AllowPlayerRoleFriendlyFire, "PR");
            AppendRP(activeFFRules, FireLogics.AllowRolePlayerFriendlyFire, "RP");
            AppendRR(activeFFRules, FireLogics.AllowRoleRoleFriendlyFire, "RR");

            activeFFRules.AppendLine("\n=== BLOCK RULES ===");

            AppendPP(activeFFRules, FireLogics.BlockPlayerPlayerFriendlyFire, "PP");
            AppendPR(activeFFRules, FireLogics.BlockPlayerRoleFriendlyFire, "PR");
            AppendRP(activeFFRules, FireLogics.BlockRolePlayerFriendlyFire, "RP");
            AppendRR(activeFFRules, FireLogics.BlockRoleRoleFriendlyFire, "RR");

            if (FireLogics.AllowPlayerPlayerFriendlyFire.Count == 0 &&
                FireLogics.AllowPlayerRoleFriendlyFire.Count == 0 &&
                FireLogics.AllowRolePlayerFriendlyFire.Count == 0 &&
                FireLogics.AllowRoleRoleFriendlyFire.Count == 0 &&
                FireLogics.BlockPlayerPlayerFriendlyFire.Count == 0 &&
                FireLogics.BlockPlayerRoleFriendlyFire.Count == 0 &&
                FireLogics.BlockRolePlayerFriendlyFire.Count == 0 &&
                FireLogics.BlockRoleRoleFriendlyFire.Count == 0) {

                response = "No fire rules found." + serverFriendlyFire;
                return true;
            }

            response = activeFFRules + serverFriendlyFire;
            return true;
        }

        // ================= FORMATTERS =================

        private void AppendPP(StringBuilder result, HashSet<(ReferenceHub, ReferenceHub)> set, string label) {
            if (set.Count == 0) {
                return;
            }

            result.AppendLine($"[{label}]");

            foreach (var (attacker, target) in set) {
                result.AppendLine($"  {GetName(attacker)} -> {GetName(target)}");
            }
        }

        private void AppendPR(StringBuilder result, HashSet<(ReferenceHub, RoleTypeId)> set, string label) {
            if (set.Count == 0) {
                return;
            }

            result.AppendLine($"[{label}]");

            foreach (var (attacker, targetrole) in set) {
                result.AppendLine($"  {GetName(attacker)} -> [{(sbyte)targetrole}]{RolesTranslation[targetrole]}");
            }
        }

        private void AppendRP(StringBuilder result, HashSet<(RoleTypeId, ReferenceHub)> set, string label) {
            if (set.Count == 0) {
                return;
            }

            result.AppendLine($"[{label}]");

            foreach (var (attackerrole, target) in set) {
                result.AppendLine($"  [{(sbyte)attackerrole}]{RolesTranslation[attackerrole]} -> {GetName(target)}");
            }
        }

        private void AppendRR(StringBuilder result, HashSet<(RoleTypeId, RoleTypeId)> set, string label) {
            if (set.Count == 0) {
                return;
            }

            result.AppendLine($"[{label}]");

            foreach (var (attackerrole, targetrole) in set) {
                result.AppendLine($"  [{(sbyte)attackerrole}]{RolesTranslation[attackerrole]} -> [{(sbyte)targetrole}]{RolesTranslation[targetrole]}");
            }
        }

        // ================= HELPERS =================

        private string GetName(ReferenceHub hub) {
            var player = Player.Get(hub);
            return player != null ? $"({player.PlayerId}){player.Nickname}" : "null";
        }
    }
}


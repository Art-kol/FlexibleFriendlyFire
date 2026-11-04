using System;
using System.Collections.Generic;
using CommandSystem;
using LabApi.Features.Permissions;


namespace FriendlyFireManager.Commands.HelpersForCommand {
    public class FirePreCommandChecks {
        // =============== SENDER CHECKS ===============

        // The command works from the Remote Admin panel and from the server console. Players need the LabAPI
        // permission from the config (command_permission, LabAPI/configs/permissions.yml); the server console
        // always passes. An empty permission in the config lets everyone with RA access use the command.
        public static bool SenderCheck(ICommandSender sender, out string response) {
            var permission = FriendlyFireManagerPlugin.Instance?.Config.CommandPermission;

            if (!string.IsNullOrWhiteSpace(permission) && !sender.HasPermissions(permission!)) {
                response = $"You don't have permission to use this command ({permission}).";
                return false;
            }

            response = "";
            return true;
        }



        // =============== ARGUMENT CHECKS ===============

        public static bool ArgumentCheck(ArraySegment<string> arguments, out string response) {
            // Not enough arguments
            if (arguments.Count < 3) {
                response = $"Incorrect format!\n\n";
                return false;
            }

            // Check if type is correct
            if (arguments.At(0).Length != 2) {
                response = "Type must be like PP/PR/RP/RR";
                return false;
            }

            // Parse types (P - Player, R - Role)
            var attackerType = arguments.At(0).ToUpper()[0];
            var attackedType = arguments.At(0).ToUpper()[1];
            if ((attackerType != 'P' && attackerType != 'R') || (attackedType != 'P' && attackedType != 'R')) {
                response = $"Invalid type: {attackerType} or {attackedType}. Must be 'P' (Player) or 'R' (Role).\n\n";
                return false;
            }

            // Parse optional mode (0/1)
            if (arguments.Count >= 4) {
                if (!int.TryParse(arguments.At(3), out var attackerCanBeKilled) || (attackerCanBeKilled != 0 && attackerCanBeKilled != 1)) {
                    response = $"Invalid mode: {arguments.At(3)}. Must be 0 or 1.";
                    return false;
                }
            }

            response = "";
            return true;
        }



        // =============== PLAYERIDS CHECKS ===============

        public static bool PlayerIDCheck(List<ReferenceHub> target, out string response) {
            if (target.Count == 0) {
                response = "Incorrect player ID(s): \n\n";
                return false;
            }

            response = "";
            return true;
        }

    }
}

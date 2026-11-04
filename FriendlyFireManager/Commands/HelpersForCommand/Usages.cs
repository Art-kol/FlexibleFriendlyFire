namespace FriendlyFireManager.Commands.HelpersForCommand {
    public static class Usages {
        public static string GetCommonUsage() {
            return "How to use the command: fire <allow/block/reset/list> <Keywords: PP/PR/RP/RR> <list of attackers> <list of attacked> [Optional: 0/1. Default: 1 - The action's mutual]\n\n" +
                   "allow - allows fire between 2 objects\n" +
                   "block - blocks fire between 2 objects\n" +
                   "reset - resets fire settings between 2 objects to default\n" +
                   "list - lists current fire settings between 2 objects\n\n" +
                   "Keywords:\n" +
                   "1st letter in Keywords represents the attacker type\n" +
                   "2nd letter in Keywords represents the attacked type\n" +
                   "  P - Players (use PlayerID)\n" +
                   "  R - Roles (use RoleID from class list)\n\n" +
                   "Examples:\n" +
                   "  fire allow PP 3.4 5 0 - Players 3 & 4 CAN hurt Player 5. 5 can hurt 3 & 4 if DEFAULT GAME ALLOWS SO\n" +
                   "  fire block RR 0.3 12 - SCP-173 & 106 ARE BLOCKED FROM hurting NTF-Captains. Captains ARE BLOCKED FROM hurting them too\n" +
                   "  fire reset RP 0 1.2 0 - resets SCP-173's rules for hurting Players 1 & 2 to DEFAULT GAME behavior\n" +
                   "  fire reset - resets all fire damage setting to default game's\n" +
                   "  fire list - lists all current fire settings\n\n" +
                   "IMPORTANT!!!\n" +
                   "1) All player-targeted fire changes are MORE PRIORITIZED than role-targeted.\n" +
                   "2) All blocks are MORE PRIORITIZED than allows. If a block is set, it overrides allows.\n" +
                   "If you wanna remove the block, you need to RESET the fire settings.\n\n";
        }

        public static string GetAllowUsage() {
            return "How to use the command: fire allow <Keywords: PP/PR/RP/RR> <list of attackers> <list of attacked> [Optional: Default: 1 - The action's mutual]\n\n" +
                   "Keywords:\n" +
                   "1st letter in Keywords represents the attacker type\n" +
                   "2nd letter in Keywords represents the attacked type\n" +
                   "  P - Players (use PlayerID)\n" +
                   "  R - Roles (use RoleID from class list)\n\n" +
                   "Examples:\n" +
                   "  fire allow PP 3.4 5 0 - Players 3 & 4 CAN hurt Player 5. 5 can hurt 3 & 4 if DEFAULT GAME ALLOWS SO\n" +
                   "  fire allow RR 0.3 12 - SCP-173 & 106 CAN hurt NTF-Captains. Captains CAN hurt them too\n" +
                   "  fire allow RP 0 1.2 0 - SCP-173 CAN hurt Players 1 & 2. Players CAN hurt SCP-173 if DEFAULT GAME ALLOWS SO\n\n" +
                   "IMPORTANT!!!\n" +
                   "1) All player-targeted fire changes are MORE PRIORITIZED than role-targeted.\n" +
                   "2) All blocks are MORE PRIORITIZED than allows. If a block is set, it overrides allows.\n" +
                   "If you wanna remove the block, you need to RESET the fire settings.\n\n";
        }

        public static string GetBlockUsage() {
            return "How to use the command: fire block <Keywords: PP/PR/RP/RR> <list of attackers> <list of attacked> [Optional: Default: 1 - The action's mutual]\n\n" +
                   "Keywords:\n" +
                   "1st letter in Keywords represents the attacker type\n" +
                   "2nd letter in Keywords represents the attacked type\n" +
                   "  P - Players (use PlayerID)\n" +
                   "  R - Roles (use RoleID from class list)\n\n" +
                   "Examples:\n" +
                   "  fire block PP 3.4 5 0 - Players 3 & 4 ARE BLOCKED FROM hurting Player 5. 5 can hurt 3 & 4 if DEFAULT GAME ALLOWS SO\n" +
                   "  fire block RR 0.3 12 - SCP-173 & 106 ARE BLOCKED FROM hurting NTF-Captains. Captains ARE BLOCKED FROM hurting them too\n" +
                   "  fire block RP 0 1.2 0 - SCP-173 ARE BLOCKED FROM hurting Players 1 & 2. Players can hurt SCP-173 if DEFAULT GAME ALLOWS SO\n\n" +
                   "IMPORTANT!!!\n" +
                   "1) All player-targeted fire changes are MORE PRIORITIZED than role-targeted.\n" +
                   "2) All blocks are MORE PRIORITIZED than allows. If a block is set, it overrides allows.\n" +
                   "If you wanna remove the block, you need to RESET the fire settings.\n\n";
        }

        public static string GetResetUsage() {
            return "How to use the command: \n" +
                   "fire reset <Keywords: PP/PR/RP/RR> <list of attackers> <list of attacked> [Optional: Default: 1 - The action's mutual]\n" +
                   "OR \n" +
                   "fire reset\n\n" +
                   "Keywords:\n" +
                   "1st letter in Keywords represents the attacker type\n" +
                   "2nd letter in Keywords represents the attacked type\n" +
                   "  P - Players (use PlayerID)\n" +
                   "  R - Roles (use RoleID from class list)\n\n" +
                   "Examples:\n" +
                   "  fire reset PP 3.4 5 0 - resets Players 3 & 4 rules for hurting Player 5 to DEFAULT GAME behavior\n" +
                   "  fire reset RR 0.3 12 - resets SCP-173 & 106 rules for hurting NTF-Captains to DEFAULT GAME behavior.\n" +
                   "  AS WELL AS Captains' rules for SCP-173 & 106\n" +
                   "  fire reset RP 0 1.2 0 - resets SCP-173 rules for hurting Players 1 & 2 to DEFAULT GAME behavior\n\n" +
                   "  fire reset - resets all fire damage setting to default game's\n\n";
        }

        public static string GetListUsage() {
            return "How to use the command: \n" +
                   "fire list - lists all current damage settings and modifications\n\n";
        }

        public static string GetClassList() {
            return "\nClass IDs:\n" +
                   "0 - SCP-173\n" +
                   "1 - Class-D\n" +
                   "2 - Spectator (fire cannot be changed)\n" +
                   "3 - SCP-106\n" +
                   "4 - NTF Specialist\n" +
                   "5 - SCP-049\n" +
                   "6 - Scientist\n" +
                   "7 - SCP-079\n" +
                   "8 - Chaos Conscript\n" +
                   "9 - SCP-096\n" +
                   "10 - SCP-049-2\n" +
                   "11 - NTF Sergeant\n" +
                   "12 - NTF Captain\n" +
                   "13 - NTF Private\n" +
                   "14 - Tutorial\n" +
                   "15 - Facility Guard\n" +
                   "16 - SCP-939\n" +
                   "17 - Custom Role\n" +
                   "18 - Chaos Rifleman\n" +
                   "19 - Chaos Marauder\n" +
                   "20 - Chaos Repressor\n" +
                   "21 - Overwatch (fire cannot be changed)\n" +
                   "22 - Filmmaker (fire cannot be changed)\n" +
                   "23 - SCP-3114\n\n";
        }
    }
}

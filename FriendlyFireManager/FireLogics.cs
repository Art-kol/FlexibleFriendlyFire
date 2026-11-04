using System.Collections.Generic;
using LabApi.Features.Wrappers;
using PlayerRoles;
using static FriendlyFireManager.StaticConstants;

namespace FriendlyFireManager {
    public static class FireLogics {
        // Explanation:
        // This is adavanced FF plugin that allows to set all FF setting manually. It works like this:
        // U open RA and type: fire <allow/block/reset> <PP/PR/RP/RR> <attacker PlayerID/RoleID> <target PlayerID/RoleID> [optional: OneWay or TwoWay]

        // allow - forcefully permits FF
        // block - forcefully prevents FF
        // reset - ctrl+z of the FF setting

        // PP = Attacker is Player, Target is Player = FF will be switched ONLY between 2 certain players (ex. Onesie vs MyDad), no matter what roles they have
        // PR = Attacker is Player, Target is Role = FF will be switched ONLY between 1 certain player and 1 certain role (ex. Onesie vs all D-bois)
        // RP = Attacker is Role, Target is Player = FF will be switched ONLY between 1 certain player and 1 certain role  (ex. all scientists vs Onesie)
        // RR = Attacker is Role, Target is Role = FF will be switched ONLY between 2 certain roles (ex. Scientists and MTF), no matter what players are playing these roles

        // ?: What's the difference between RP and PR
        // > If optional parameter is set to "OneWay" then Attacker can hurt the target BUT THE TARGET CANT ATTACK BACK
        // ?: Why do we need this PP/RP etc keywords
        // > Otherwise we cant tell if we want to allow/block fire between specified players or specified roles (cuz they both are in the same 0-40 sbyte interval)


        // The code below in this .cs file is purely dedicated to methods that store the info abt all allows/blocks/resets of FF
        // & DamageCheck method which returns if one role/player can attack another role/player.


        // ================= RULE STORAGE =================

        // ALLOW
        public static readonly HashSet<(ReferenceHub, ReferenceHub)> AllowPlayerPlayerFriendlyFire = new();
        public static readonly HashSet<(ReferenceHub, RoleTypeId)> AllowPlayerRoleFriendlyFire = new();
        public static readonly HashSet<(RoleTypeId, ReferenceHub)> AllowRolePlayerFriendlyFire = new();
        public static readonly HashSet<(RoleTypeId, RoleTypeId)> AllowRoleRoleFriendlyFire = new();

        // BLOCK
        public static readonly HashSet<(ReferenceHub, ReferenceHub)> BlockPlayerPlayerFriendlyFire = new();
        public static readonly HashSet<(ReferenceHub, RoleTypeId)> BlockPlayerRoleFriendlyFire = new();
        public static readonly HashSet<(RoleTypeId, ReferenceHub)> BlockRolePlayerFriendlyFire = new();
        public static readonly HashSet<(RoleTypeId, RoleTypeId)> BlockRoleRoleFriendlyFire = new();

        // ================= ALLOW =================

        // Player -> Player
        public static void AllowAttacks(List<ReferenceHub> attackers, List<ReferenceHub> targets) {
            foreach (var a in attackers) {
                foreach (var t in targets) {
                    if (IsForbidden(a, t)) {
                        continue;
                    }

                    if (BlockPlayerPlayerFriendlyFire.Contains((a, t))) {
                        continue;
                    }

                    AllowPlayerPlayerFriendlyFire.Add((a, t));
                }
            }
        }

        // Player -> Role
        public static void AllowAttacks(List<ReferenceHub> attackers, List<RoleTypeId> targetRoles) {
            foreach (var a in attackers) {
                foreach (var r in targetRoles) {
                    if (IsForbidden(a, r)) {
                        continue;
                    }

                    if (BlockPlayerRoleFriendlyFire.Contains((a, r))) {
                        continue;
                    }

                    AllowPlayerRoleFriendlyFire.Add((a, r));
                }
            }
        }

        // Role -> Player
        public static void AllowAttacks(List<RoleTypeId> attackerRoles, List<ReferenceHub> targets) {
            foreach (var ar in attackerRoles) {
                foreach (var t in targets) {
                    if (IsForbidden(ar, t)) {
                        continue;
                    }

                    if (BlockRolePlayerFriendlyFire.Contains((ar, t))) {
                        continue;
                    }

                    AllowRolePlayerFriendlyFire.Add((ar, t));
                }
            }
        }

        // Role -> Role
        public static void AllowAttacks(List<RoleTypeId> attackerRoles, List<RoleTypeId> targetRoles) {
            foreach (var ar in attackerRoles) {
                foreach (var tr in targetRoles) {
                    if (IsForbidden(ar, tr)) {
                        continue;
                    }

                    if (BlockRoleRoleFriendlyFire.Contains((ar, tr))) {
                        continue;
                    }

                    AllowRoleRoleFriendlyFire.Add((ar, tr));
                }
            }
        }



        // ================= BLOCK =================

        public static void BlockAttacks(List<ReferenceHub> attackers, List<ReferenceHub> targets) {
            foreach (var a in attackers) {
                foreach (var t in targets) {
                    if (IsForbidden(a, t)) {
                        continue;
                    }

                    BlockPlayerPlayerFriendlyFire.Add((a, t));
                    AllowPlayerPlayerFriendlyFire.Remove((a, t));
                }
            }
        }

        public static void BlockAttacks(List<ReferenceHub> attackers, List<RoleTypeId> targetRoles) {
            foreach (var a in attackers) {
                foreach (var r in targetRoles) {
                    if (IsForbidden(a, r)) {
                        continue;
                    }

                    BlockPlayerRoleFriendlyFire.Add((a, r));
                    AllowPlayerRoleFriendlyFire.Remove((a, r));
                }
            }
        }

        public static void BlockAttacks(List<RoleTypeId> attackerRoles, List<ReferenceHub> targets) {
            foreach (var ar in attackerRoles) {
                foreach (var t in targets) {
                    if (IsForbidden(ar, t)) {
                        continue;
                    }

                    BlockRolePlayerFriendlyFire.Add((ar, t));
                    AllowRolePlayerFriendlyFire.Remove((ar, t));
                }
            }
        }

        public static void BlockAttacks(List<RoleTypeId> attackerRoles, List<RoleTypeId> targetRoles) {
            foreach (var ar in attackerRoles) {
                foreach (var tr in targetRoles) {
                    if (IsForbidden(ar, tr)) {
                        continue;
                    }

                    BlockRoleRoleFriendlyFire.Add((ar, tr));
                    AllowRoleRoleFriendlyFire.Remove((ar, tr));
                }
            }
        }



        // ================= RESET =================

        public static void ResetAttacks(List<ReferenceHub> attackers, List<ReferenceHub> targets) {
            foreach (var a in attackers) {
                foreach (var t in targets) {
                    AllowPlayerPlayerFriendlyFire.Remove((a, t));
                    BlockPlayerPlayerFriendlyFire.Remove((a, t));
                }
            }
        }

        public static void ResetAttacks(List<ReferenceHub> attackers, List<RoleTypeId> targetRoles) {
            foreach (var a in attackers) {
                foreach (var r in targetRoles) {
                    AllowPlayerRoleFriendlyFire.Remove((a, r));
                    BlockPlayerRoleFriendlyFire.Remove((a, r));
                }
            }
        }

        public static void ResetAttacks(List<RoleTypeId> attackerRoles, List<ReferenceHub> targets) {
            foreach (var ar in attackerRoles) {
                foreach (var t in targets) {
                    AllowRolePlayerFriendlyFire.Remove((ar, t));
                    BlockRolePlayerFriendlyFire.Remove((ar, t));
                }
            }
        }

        public static void ResetAttacks(List<RoleTypeId> attackerRoles, List<RoleTypeId> targetRoles) {
            foreach (var ar in attackerRoles) {
                foreach (var tr in targetRoles) {
                    AllowRoleRoleFriendlyFire.Remove((ar, tr));
                    BlockRoleRoleFriendlyFire.Remove((ar, tr));
                }
            }
        }



        // ================= CLEAR =================

        public static void ClearAll() {
            AllowPlayerPlayerFriendlyFire.Clear();
            AllowPlayerRoleFriendlyFire.Clear();
            AllowRolePlayerFriendlyFire.Clear();
            AllowRoleRoleFriendlyFire.Clear();

            BlockPlayerPlayerFriendlyFire.Clear();
            BlockPlayerRoleFriendlyFire.Clear();
            BlockRolePlayerFriendlyFire.Clear();
            BlockRoleRoleFriendlyFire.Clear();
        }



        // ================= DAMAGE CHECK =================

        public static bool? CanDamage(ReferenceHub attacker, ReferenceHub target) {
            var AttackerPlayer = Player.Get(attacker);
            var TargetPlayer = Player.Get(target);

            if (AttackerPlayer == null || TargetPlayer == null) {
                return false;
            }

            var AttackerRole = AttackerPlayer.Role;
            var TargetRole = TargetPlayer.Role;

            if (InactiveRoles.Contains(AttackerRole) || InactiveRoles.Contains(TargetRole)) {
                return false;
            }

            // ================= PRIORITY 1: PP =================
            if (BlockPlayerPlayerFriendlyFire.Contains((attacker, target))) {
                return false;
            }
            if (AllowPlayerPlayerFriendlyFire.Contains((attacker, target))) {
                return true;
            }


            // ================= PRIORITY 2: PR / RP =================
            if (BlockPlayerRoleFriendlyFire.Contains((attacker, TargetRole))) {
                return false;
            }
            if (BlockRolePlayerFriendlyFire.Contains((AttackerRole, target))) {
                return false;
            }

            if (AllowPlayerRoleFriendlyFire.Contains((attacker, TargetRole))) {
                return true;
            }
            if (AllowRolePlayerFriendlyFire.Contains((AttackerRole, target))) {
                return true;
            }


            // ================= PRIORITY 3: RR =================
            if (BlockRoleRoleFriendlyFire.Contains((AttackerRole, TargetRole))) {
                return false;
            }
            if (AllowRoleRoleFriendlyFire.Contains((AttackerRole, TargetRole))) {
                return true;
            }


            // ================= DEFAULT =================
            return null;
        }



        // ================= HELPERS =================

        private static bool IsForbidden(ReferenceHub a, ReferenceHub t) {
            return InactiveRoles.Contains(Player.Get(a).Role) || InactiveRoles.Contains(Player.Get(t).Role);
        }

        private static bool IsForbidden(ReferenceHub a, RoleTypeId r) {
            return InactiveRoles.Contains(Player.Get(a).Role) || InactiveRoles.Contains(r);
        }

        private static bool IsForbidden(RoleTypeId r, ReferenceHub t) {
            return InactiveRoles.Contains(r) || InactiveRoles.Contains(Player.Get(t).Role);
        }

        private static bool IsForbidden(RoleTypeId r1, RoleTypeId r2) {
            return InactiveRoles.Contains(r1) || InactiveRoles.Contains(r2);
        }
    }
}
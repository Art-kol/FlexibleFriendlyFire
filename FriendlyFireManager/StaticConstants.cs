using System.Collections.Generic;
using PlayerRoles;

// File for UI that shows more readable data
namespace FriendlyFireManager {
    public class StaticConstants {
        public static readonly Dictionary<RoleTypeId, string> RolesTranslation = new Dictionary<RoleTypeId, string> {
            { RoleTypeId.Tutorial, "Tutorial" },
            { RoleTypeId.ClassD, "Class-D" },
            { RoleTypeId.Scientist, "Scientist" },
            { RoleTypeId.FacilityGuard, "Facility Guard" },
            { RoleTypeId.Filmmaker, "Film Maker" },
            { RoleTypeId.Spectator, "Spectator" },
            { RoleTypeId.None, "None" },
            { RoleTypeId.Destroyed, "Destroyed" },
            { RoleTypeId.Overwatch, "Overwatch" },
            { RoleTypeId.NtfPrivate, "MTF Private" },
            { RoleTypeId.NtfSergeant, "MTF Sergeant" },
            { RoleTypeId.NtfSpecialist, "MTF Specialist" },
            { RoleTypeId.NtfCaptain, "MTF Captain" },
            { RoleTypeId.ChaosConscript, "CI Conscript" },
            { RoleTypeId.ChaosRifleman, "CI Rifleman" },
            { RoleTypeId.ChaosRepressor, "CI Repressor" },
            { RoleTypeId.ChaosMarauder, "CI Marauder" },
            { RoleTypeId.Scp049, "SCP-049" },
            { RoleTypeId.Scp0492, "SCP-049-2" },
            { RoleTypeId.Scp079, "SCP-079" },
            { RoleTypeId.Scp096, "SCP-096" },
            { RoleTypeId.Scp106, "SCP-106" },
            { RoleTypeId.Scp173, "SCP-173" },
            { RoleTypeId.Scp939, "SCP-939" },
            { RoleTypeId.Scp3114, "SCP-3114" },
            { RoleTypeId.Flamingo, "Flamingo" },
            { RoleTypeId.AlphaFlamingo, "Alpha Flamingo" },
            { RoleTypeId.ZombieFlamingo, "Zombie Flamingo" },
            { RoleTypeId.NtfFlamingo, "MTF Flamingo" },
            { RoleTypeId.ChaosFlamingo, "Chaos Flamingo" }
        };

        public static readonly IReadOnlyList<RoleTypeId> AvailableRoles = new[] {
                RoleTypeId.Scp173,
                RoleTypeId.ClassD,
                RoleTypeId.Scp106,
                RoleTypeId.NtfSpecialist,
                RoleTypeId.Scp049,
                RoleTypeId.Scientist,
                RoleTypeId.Scp079,
                RoleTypeId.ChaosConscript,
                RoleTypeId.Scp096,
                RoleTypeId.Scp0492,
                RoleTypeId.NtfSergeant,
                RoleTypeId.NtfCaptain,
                RoleTypeId.NtfPrivate,
                RoleTypeId.Tutorial,
                RoleTypeId.FacilityGuard,
                RoleTypeId.Scp939,
                RoleTypeId.ChaosRifleman,
                RoleTypeId.ChaosMarauder,
                RoleTypeId.ChaosRepressor,
                RoleTypeId.Scp3114,
                RoleTypeId.Flamingo,
                RoleTypeId.AlphaFlamingo,
                RoleTypeId.ZombieFlamingo,
                RoleTypeId.NtfFlamingo,
                RoleTypeId.ChaosFlamingo
        };

        public static readonly HashSet<RoleTypeId> InactiveRoles = new() {
            RoleTypeId.None,
            RoleTypeId.Destroyed,
            RoleTypeId.Spectator,
            RoleTypeId.Overwatch,
            RoleTypeId.Filmmaker
        };
    }
}

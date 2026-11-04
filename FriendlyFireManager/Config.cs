using System.ComponentModel;

namespace FriendlyFireManager {
    // LabAPI/configs/<port>/FriendlyFireManager/config.yml (keys are written in snake_case)
    public sealed class Config {
        [Description("LabAPI permission needed to use the 'fire' command from the Remote Admin panel (LabAPI/configs/permissions.yml). Empty = everyone with RA access. The server console can always use it.")]
        public string CommandPermission { get; set; } = "ffm.manage";
    }
}

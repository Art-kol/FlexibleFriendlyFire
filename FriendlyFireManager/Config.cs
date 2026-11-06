using System.ComponentModel;

namespace FriendlyFireManager {
    // LabAPI/configs/<port>/FriendlyFireManager/config.yml (keys are written in snake_case)
    public sealed class Config {
        [Description("true = when the vanilla friendly fire is on (friendly_fire in config_gameplay.txt or Remote Admin -> Server configs), SCPs may hurt each other too, as if every SCP had an allow rule against every other SCP. Rules of the 'fire' command still win.")]
        public bool ScpVsScpWithServerFriendlyFire { get; set; } = false;

        [Description("true = SCP-3114's disguise also fools the SCPs it may hurt: they see it as the human whose skin it wears.")]
        public bool Scp3114DisguiseFoolsScps { get; set; } = true;

        [Description("How much damage SCP attacks deal to OTHER SCPs (only when they may hurt each other). Values: 0 = default behaviour of the attack, with its effects (pocket dimension, cardiac arrest, corrosion, concussion...); -1 = kills in one hit; a number above 0 = that much damage (hume shield first, then HP); a number with % (for example 25%) = that percent of the target's maximum HP. Any value other than 0 turns the attack's effects off: the hit only deals that damage. Other abilities (sense, bloodlust, stalk, amnestic cloud, tantrum...) are not affected.")]
        public ScpVsScpDamageConfig ScpVsScpDamage { get; set; } = new();

        [Description("Several SCP-079s at once, when a player or an SCP may hurt only some of them.")]
        public Scp079Config Scp079 { get; set; } = new();
    }

    public sealed class ScpVsScpDamageConfig {
        [Description("SCP-049 attack (default: the first hit stops the heart, the next one kills)")]
        public string Scp049Attack { get; set; } = "0";

        [Description("SCP-049-2 claws (default: 40)")]
        public string Scp0492Claws { get; set; } = "0";

        [Description("Tesla gate activated by an SCP-079 that may hurt the SCP (default: 200-300)")]
        public string Scp079Tesla { get; set; } = "0";

        [Description("SCP-096 slap (default: 60 + concussion; SCP-096 only slaps its targets)")]
        public string Scp096Slap { get; set; } = "0";

        [Description("SCP-096 charge (default: 90 to its targets, 35 to others + concussion)")]
        public string Scp096Charge { get; set; } = "0";

        [Description("SCP-096 prying a gate open, to whoever stands in it (default: 200)")]
        public string Scp096GateKill { get; set; } = "0";

        [Description("SCP-106 attack (default: damage + corrosion, the next hit sends to the pocket dimension)")]
        public string Scp106Attack { get; set; } = "0";

        [Description("SCP-173 kill by blink teleport (default: kills)")]
        public string Scp173Snap { get; set; } = "0";

        [Description("SCP-939 claws (default: 40, split between several targets)")]
        public string Scp939Claws { get; set; } = "0";

        [Description("SCP-939 lunge, the target it lands on (default: 120)")]
        public string Scp939Lunge { get; set; } = "0";

        [Description("SCP-939 lunge, everyone else around the landing point")]
        public string Scp939LungeSplash { get; set; } = "0";

        [Description("SCP-3114 slap (default: 20)")]
        public string Scp3114Slap { get; set; } = "0";
    }

    public sealed class Scp079Config {
        [Description("SCPs: an SCP may pull a generator lever if it may hurt Any = at least one SCP-079, All = every SCP-079.")]
        public Scp079Requirement ScpGenerators { get; set; } = Scp079Requirement.Any;

        [Description("SCPs: an SCP may break the glass of the SCP-079 recontainment button if it may hurt Any = at least one SCP-079, All = every SCP-079.")]
        public Scp079Requirement ScpRecontainmentButton { get; set; } = Scp079Requirement.Any;

        [Description("SCPs: when an SCP breaks the glass, All = every SCP-079 is recontained, Attackable = only the SCP-079s this SCP may hurt (the others survive).")]
        public Scp079RecontainmentTargets ScpRecontainmentTargets { get; set; } = Scp079RecontainmentTargets.All;

        [Description("Humans: a human may unlock and activate generators if he is not blocked from hurting Any = at least one SCP-079, All = every SCP-079.")]
        public Scp079Requirement HumanGenerators { get; set; } = Scp079Requirement.Any;

        [Description("Humans: a human may break the glass of the SCP-079 recontainment button if he is not blocked from hurting Any = at least one SCP-079, All = every SCP-079.")]
        public Scp079Requirement HumanRecontainmentButton { get; set; } = Scp079Requirement.Any;

        [Description("Humans: when a human breaks the glass, All = every SCP-079 is recontained, Attackable = only the SCP-079s he is not blocked from hurting (the others survive).")]
        public Scp079RecontainmentTargets HumanRecontainmentTargets { get; set; } = Scp079RecontainmentTargets.Attackable;
    }

    public enum Scp079Requirement {
        Any,
        All
    }

    public enum Scp079RecontainmentTargets {
        All,
        Attackable
    }

    // The loaded config, or the defaults while the plugin isn't enabled
    public static class FfConfig {
        private static readonly Config Defaults = new();

        public static Config Current => FriendlyFireManagerPlugin.Instance?.Config ?? Defaults;
    }
}

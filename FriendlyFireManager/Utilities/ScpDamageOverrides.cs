using System.Collections.Generic;
using System.Globalization;
using PlayerRoles;
using PlayerRoles.PlayableScps.Scp3114;
using PlayerRoles.PlayableScps.Scp939;
using PlayerStatsSystem;
using UnityEngine;
using Logger = LabApi.Features.Console.Logger;

namespace FriendlyFireManager.Utilities {
    // One configurable attack of config.yml -> scp_vs_scp_damage
    public enum ScpAttack {
        Scp049Attack,
        Scp0492Claws,
        Scp079Tesla,
        Scp096Slap,
        Scp096Charge,
        Scp096GateKill,
        Scp106Attack,
        Scp173Snap,
        Scp939Claws,
        Scp939Lunge,
        Scp939LungeSplash,
        Scp3114Slap
    }

    // A parsed value: "0" / "-1" / "150" / "25%"
    public readonly struct ScpDamageValue {
        public enum ValueMode : byte {
            Default,
            Kill,
            Fixed,
            Percent
        }

        public readonly ValueMode Mode;
        public readonly float Amount;

        private ScpDamageValue(ValueMode mode, float amount) {
            Mode = mode;
            Amount = amount;
        }

        public static readonly ScpDamageValue Default = new(ValueMode.Default, 0f);

        // false = not a valid value (then it works like 0)
        public static bool TryParse(string? text, out ScpDamageValue value) {
            value = Default;
            text = text?.Trim() ?? string.Empty;
            if (text.Length == 0) {
                return true;
            }

            var percent = text.EndsWith("%");
            var number = (percent ? text.Substring(0, text.Length - 1) : text).Trim().Replace(',', '.');
            if (!float.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out var amount)
                || float.IsNaN(amount) || float.IsInfinity(amount)) {
                return false;
            }

            if (amount == 0f) {
                return true;
            }

            if (percent) {
                if (amount < 0f) {
                    return false;
                }

                value = new ScpDamageValue(ValueMode.Percent, amount);
                return true;
            }

            if (Mathf.Approximately(amount, -1f)) {
                value = new ScpDamageValue(ValueMode.Kill, 0f);
                return true;
            }

            if (amount < 0f) {
                return false;
            }

            value = new ScpDamageValue(ValueMode.Fixed, amount);
            return true;
        }
    }

    // config.yml -> scp_vs_scp_damage: damage of SCP attacks against other SCPs.
    //
    // Where it is applied:
    //   * SCP-049 attack, SCP-106 attack - their effects (cardiac arrest / corrosion + pocket dimension) are applied by
    //     the ability itself, so with a configured value the ability patch deals the damage instead of the vanilla hit
    //     (Patches/ScpVsScpDamagePatches.cs Scp049AttackDamagePatch, FfProxy/Scp106AttackPatch.cs).
    //   * everything else - the attack only deals damage, so the damage of its handler is replaced in PlayerHurting
    //     (Handlers/HurtingEvents.cs). The only on-hit effect there is SCP-096's concussion, which is skipped for that
    //     target (ScpHitEffectSuppressionPatch).
    // Only between two different SCPs that may hurt each other (FireLogics.CanDamage == true). Humans: always vanilla.
    public static class ScpDamageOverrides {
        private static readonly HashSet<string> WarnedValues = new();

        public static string GetRaw(ScpVsScpDamageConfig config, ScpAttack attack) {
            return attack switch {
                ScpAttack.Scp049Attack => config.Scp049Attack,
                ScpAttack.Scp0492Claws => config.Scp0492Claws,
                ScpAttack.Scp079Tesla => config.Scp079Tesla,
                ScpAttack.Scp096Slap => config.Scp096Slap,
                ScpAttack.Scp096Charge => config.Scp096Charge,
                ScpAttack.Scp096GateKill => config.Scp096GateKill,
                ScpAttack.Scp106Attack => config.Scp106Attack,
                ScpAttack.Scp173Snap => config.Scp173Snap,
                ScpAttack.Scp939Claws => config.Scp939Claws,
                ScpAttack.Scp939Lunge => config.Scp939Lunge,
                ScpAttack.Scp939LungeSplash => config.Scp939LungeSplash,
                ScpAttack.Scp3114Slap => config.Scp3114Slap,
                _ => "0"
            };
        }

        public static ScpDamageValue GetValue(ScpAttack attack) {
            var raw = GetRaw(FfConfig.Current.ScpVsScpDamage, attack);
            if (ScpDamageValue.TryParse(raw, out var value)) {
                return value;
            }

            // Once per wrong value, also after a config reload
            if (WarnedValues.Add(attack + "=" + raw)) {
                Logger.Warn($"[FF] config scp_vs_scp_damage: \"{raw}\" is not a valid value for {attack}, using 0. "
                            + "Use 0, -1, a number above 0 or a percent like 25%.");
            }

            return ScpDamageValue.Default;
        }

        // Logs every invalid value of the config (on plugin enable)
        public static void ValidateConfig() {
            WarnedValues.Clear();
            foreach (ScpAttack attack in System.Enum.GetValues(typeof(ScpAttack))) {
                GetValue(attack);
            }
        }

        // attacker (an SCP) hits target (another SCP it may hurt) with this attack and the config has a value for it ->
        // the damage to deal instead (StandardDamageHandler.KillValue = kills in one hit).
        public static bool TryGet(ReferenceHub? attacker, ReferenceHub? target, ScpAttack attack, out float damage) {
            damage = 0f;

            if (attacker == null || target == null || attacker == target || !attacker.IsSCP() || !target.IsSCP()) {
                return false;
            }

            var value = GetValue(attack);
            if (value.Mode == ScpDamageValue.ValueMode.Default || FireLogics.CanDamage(attacker, target) != true) {
                return false;
            }

            switch (value.Mode) {
                case ScpDamageValue.ValueMode.Kill:
                    damage = StandardDamageHandler.KillValue;
                    return true;

                case ScpDamageValue.ValueMode.Fixed:
                    damage = value.Amount;
                    return true;

                case ScpDamageValue.ValueMode.Percent:
                    var maxHealth = target.playerStats.GetModule<HealthStat>().MaxValue;
                    if (maxHealth <= 0f) {
                        return false;
                    }

                    damage = maxHealth * value.Amount / 100f;
                    return damage > 0f;

                default:
                    return false;
            }
        }

        // Which configurable attack a damage handler of an SCP attacker belongs to. SCP-049's attack / cardiac arrest
        // and SCP-106's attack / corrosion are NOT listed: they are handled by the ability patches.
        public static bool TryClassify(AttackerDamageHandler handler, out ScpAttack attack) {
            attack = default;

            switch (handler) {
                case Scp049DamageHandler scp049 when scp049.DamageSubType == Scp049DamageHandler.AttackType.Scp0492:
                    attack = ScpAttack.Scp0492Claws;
                    return true;

                case Scp096DamageHandler scp096:
                    attack = scp096._attackType switch {
                        Scp096DamageHandler.AttackType.Charge => ScpAttack.Scp096Charge,
                        Scp096DamageHandler.AttackType.GateKill => ScpAttack.Scp096GateKill,
                        _ => ScpAttack.Scp096Slap
                    };
                    return true;

                case Scp939DamageHandler scp939:
                    switch (scp939.Scp939DamageType) {
                        case Scp939DamageType.Claw:
                            attack = ScpAttack.Scp939Claws;
                            return true;
                        case Scp939DamageType.LungeTarget:
                            attack = ScpAttack.Scp939Lunge;
                            return true;
                        case Scp939DamageType.LungeSecondary:
                            attack = ScpAttack.Scp939LungeSplash;
                            return true;
                        default:
                            return false;
                    }

                case Scp3114DamageHandler scp3114 when scp3114.Subtype == Scp3114DamageHandler.HandlerType.Slap:
                    attack = ScpAttack.Scp3114Slap;
                    return true;

                // SCP-173's own handler (Scp173Role.DamageHandler) - the blink teleport kill
                case ScpDamageHandler scp when scp.Attacker.Role == RoleTypeId.Scp173 && scp._translationId == DeathTranslations.Scp173.Id:
                    attack = ScpAttack.Scp173Snap;
                    return true;

                default:
                    return false;
            }
        }

        // ================= ON-HIT EFFECTS =================

        // A configured hit must not apply the attack's on-hit effects. The effect is enabled right after the damage, in
        // the same call (Scp096HitHandler.OnPlayerHit -> Concussed), so "this target, this frame" is enough.
        private static readonly HashSet<ReferenceHub> SuppressedTargets = new(ReferenceHubComparer.Instance);
        private static int _suppressedFrame = -1;

        public static void SuppressHitEffects(ReferenceHub target) {
            if (_suppressedFrame != Time.frameCount) {
                SuppressedTargets.Clear();
                _suppressedFrame = Time.frameCount;
            }

            SuppressedTargets.Add(target);
        }

        public static bool AreHitEffectsSuppressed(ReferenceHub? hub) {
            return hub is not null && _suppressedFrame == Time.frameCount && SuppressedTargets.Contains(hub);
        }
    }
}

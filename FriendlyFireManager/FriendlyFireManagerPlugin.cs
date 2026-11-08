using System;
using FriendlyFireManager.Utilities;
using HarmonyLib;
using LabApi.Loader.Features.Plugins;
using Logger = LabApi.Features.Console.Logger;

namespace FriendlyFireManager {
    // Plugin entry point.
    //
    // Everything the plugin changes is driven by the rules of the "fire" command (FireLogics) and, if enabled in the
    // config, by the vanilla friendly fire (scp_vs_scp_with_server_friendly_fire). Without them the game plays
    // vanilla, so there is no separate "on/off" mode: while the plugin is enabled (LabAPI
    // properties.yml -> is_enabled) the patches are applied and the events are subscribed. Rules live for one
    // round and are cleared on every WaitingForPlayers (see FfEventsManager).
    public sealed class FriendlyFireManagerPlugin : Plugin<Config> {
        // Own Harmony id: Disable() removes only this plugin's patches, never other plugins' ones
        public const string HarmonyId = "friendlyfiremanager.patches";

        public static FriendlyFireManagerPlugin? Instance { get; private set; }

        private Harmony? _harmony;

        public override string Name => "FriendlyFireManager";

        public override string Description => "Rule-based friendly fire between any players and roles, including SCP vs SCP with SCP abilities.";

        public override string Author => "Dr. Wondertainment";

        // Version of Northwood.LabAPI the plugin is compiled against (FriendlyFireManager.csproj)
        public override Version RequiredApiVersion { get; } = new(1, 1, 7);

        public override void Enable() {
            Instance = this;
            _harmony = new Harmony(HarmonyId);

            try {
                _harmony.PatchAll(typeof(FriendlyFireManagerPlugin).Assembly);
            }
            catch (Exception e) {
                // A half-patched game is worse than a vanilla one: roll back and stay inactive
                Logger.Error($"Failed to apply Harmony patches, {Name} stays disabled. Probably the game was updated.\n{e}");
                _harmony.UnpatchAll(HarmonyId);
                _harmony = null;
                Instance = null;
                return;
            }

            FfEventsManager.Subscribe();
            FfEventsManager.ClearState();

            // Warns about wrong scp_vs_scp_damage values right away instead of on the first hit
            ScpDamageOverrides.ValidateConfig();

            Logger.Info($"{Name} v{Version} enabled.");
        }

        public override void Disable() {
            FfEventsManager.Unsubscribe();
            FfEventsManager.ClearState();

            _harmony?.UnpatchAll(HarmonyId);
            _harmony = null;
            Instance = null;
        }
    }
}

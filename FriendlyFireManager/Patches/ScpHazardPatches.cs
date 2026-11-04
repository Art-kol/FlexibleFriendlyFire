using CustomPlayerEffects;
using FriendlyFireManager.Utilities;
using HarmonyLib;
using Hazards;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Handlers;
using PlayerRoles;
using PlayerRoles.PlayableScps.Scp939;
using static FriendlyFireManager.Handlers.FireSCPEvents;

namespace FriendlyFireManager.Patches {
    // SCP-173 tantrum (puddle) and SCP-939 amnestic cloud against SCPs.
    //
    // Vanilla: both hazards ignore every SCP (tantrum: player.IsSCP(), cloud: IsEnemy(Team.SCPs, ...)).
    // Here: an SCP is affected exactly like a human, but ONLY if the hazard's owner may hit it by FF rules
    // (ScpFfTargets.IsFfScpTarget). The owner itself and all other SCPs stay unaffected.
    // Humans are not touched by these patches - they keep the vanilla path (+ FireLogicsOnEnteringHazard).
    //
    // EnvironmentalHazard.OnEnter/OnExit (the "base" calls) can't be called non-virtually from a Harmony prefix,
    // so their few lines are reproduced in BaseOnEnter/BaseOnExit (LabAPI events + AffectedPlayers).
    public static class ScpHazardFf {
        // Vanilla EnvironmentalHazard.OnEnter
        public static bool BaseOnEnter(EnvironmentalHazard hazard, ReferenceHub player) {
            var entering = new PlayerEnteringHazardEventArgs(player, hazard);
            PlayerEvents.OnEnteringHazard(entering); // -> FireLogicsOnEnteringHazard can still block it
            if (!entering.IsAllowed) {
                return false;
            }

            hazard.AffectedPlayers.Add(player);
            return true;
        }

        // Vanilla EnvironmentalHazard.OnExit
        public static bool BaseOnExit(EnvironmentalHazard hazard, ReferenceHub player) {
            var leaving = new PlayerLeavingHazardEventArgs(player, hazard);
            PlayerEvents.OnLeavingHazard(leaving);
            if (!leaving.IsAllowed) {
                return false;
            }

            hazard.AffectedPlayers.Remove(player);
            return true;
        }

        // Owner of a tantrum - tracked by FireSCPEvents.FireLogicsOnCreatedTantrum (event rounds only).
        public static ReferenceHub? GetTantrumOwner(TantrumEnvironmentalHazard tantrum) {
            return HazardOwners.TryGetValue(tantrum, out var owner) ? owner : null;
        }
    }

    // ================= SCP-173 TANTRUM =================

    // Vanilla: if (!IsActive || player.IsSCP()) return false; base.OnEnter; Stained(1s); EnteredHazard event.
    [HarmonyPatch(typeof(TantrumEnvironmentalHazard), nameof(TantrumEnvironmentalHazard.OnEnter))]
    public static class Scp173TantrumEnterPatch {
        public static bool Prefix(TantrumEnvironmentalHazard __instance, ReferenceHub player, ref bool __result) {
            if (!player.IsSCP()) {
                return true; // humans: vanilla
            }

            __result = false;

            if (!__instance.IsActive || !ScpFfTargets.IsFfScpTarget(ScpHazardFf.GetTantrumOwner(__instance), player)) {
                return false; // vanilla result for SCPs: not affected
            }

            if (!ScpHazardFf.BaseOnEnter(__instance, player)) {
                return false;
            }

            player.playerEffectsController.EnableEffect<Stained>(1f);
            PlayerEvents.OnEnteredHazard(new PlayerEnteredHazardEventArgs(player, __instance));
            __result = true;
            return false;
        }
    }

    // Vanilla OnStay keeps re-applying Stained to everyone in AffectedPlayers.
    // For SCPs: only while the FF rule still allows it (rule removed / 173 changed role -> no more slowdown).
    [HarmonyPatch(typeof(TantrumEnvironmentalHazard), nameof(TantrumEnvironmentalHazard.OnStay))]
    public static class Scp173TantrumStayPatch {
        public static bool Prefix(TantrumEnvironmentalHazard __instance, ReferenceHub player) {
            return !player.IsSCP() || ScpFfTargets.IsFfScpTarget(ScpHazardFf.GetTantrumOwner(__instance), player);
        }
    }

    // Vanilla: base.OnExit; if (!IsActive || player.IsSCP()) return false; Stained(2s); LeftHazard event.
    [HarmonyPatch(typeof(TantrumEnvironmentalHazard), nameof(TantrumEnvironmentalHazard.OnExit))]
    public static class Scp173TantrumExitPatch {
        public static bool Prefix(TantrumEnvironmentalHazard __instance, ReferenceHub player, ref bool __result) {
            // Only SCPs that were let in by Scp173TantrumEnterPatch; everyone else -> vanilla
            if (!player.IsSCP() || !__instance.AffectedPlayers.Contains(player)) {
                return true;
            }

            __result = false;

            if (!ScpHazardFf.BaseOnExit(__instance, player) || !__instance.IsActive) {
                return false;
            }

            // Same 2s "sticky feet" as humans get when stepping out (if the rule still allows it)
            if (ScpFfTargets.IsFfScpTarget(ScpHazardFf.GetTantrumOwner(__instance), player)) {
                player.playerEffectsController.EnableEffect<Stained>(2f);
            }

            PlayerEvents.OnLeftHazard(new PlayerLeftHazardEventArgs(player, __instance));
            __result = true;
            return false;
        }
    }

    // ================= SCP-939 AMNESTIC CLOUD =================

    // Vanilla: if (!IsEnemy(Team.SCPs, player.GetTeam()) || player.IsFlamingo()) return false; base.OnEnter; EnteredHazard.
    // That Team check is also what SCPFFPatch's Team overload may turn into "every SCP is affected" - so for SCPs
    // the decision is made here: only SCPs the cloud owner may hit; the owner and other SCPs never.
    [HarmonyPatch(typeof(Scp939AmnesticCloudInstance), nameof(Scp939AmnesticCloudInstance.OnEnter))]
    public static class Scp939CloudEnterPatch {
        public static bool Prefix(Scp939AmnesticCloudInstance __instance, ReferenceHub player, ref bool __result) {
            if (!player.IsSCP()) {
                return true; // humans: vanilla
            }

            __result = false;

            if (!ScpFfTargets.IsFfScpTarget(__instance.Owner, player)) {
                return false;
            }

            if (!ScpHazardFf.BaseOnEnter(__instance, player)) {
                return false;
            }

            PlayerEvents.OnEnteredHazard(new PlayerEnteredHazardEventArgs(player, __instance));
            __result = true;
            return false;
        }
    }

    // Vanilla OnStay applies AmnesiaVision (with all its cooldown rules) to everyone in AffectedPlayers.
    // For SCPs: only while the owner may still hit them. What AmnesiaVision then does for an SCP is in
    // Handlers/ScpFfVisibility (the SCP client wouldn't hide SCP-939 by itself).
    [HarmonyPatch(typeof(Scp939AmnesticCloudInstance), nameof(Scp939AmnesticCloudInstance.OnStay))]
    public static class Scp939CloudStayPatch {
        public static bool Prefix(Scp939AmnesticCloudInstance __instance, ReferenceHub player) {
            return !player.IsSCP() || ScpFfTargets.IsFfScpTarget(__instance.Owner, player);
        }
    }
}

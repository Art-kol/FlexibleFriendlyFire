using System.Linq;
using FriendlyFireManager.Utilities;
using MapGeneration;
using PlayerRoles;
using PlayerRoles.PlayableScps.Scp079;
using PlayerRoles.PlayableScps.Scp079.Rewards;
using PlayerStatsSystem;

namespace FriendlyFireManager.Handlers {
    // SCP-079 vs SCPs: event handlers (the Harmony side is in Patches/Scp079ScpFfPatches.cs).
    public static class FireScp079ScpEvents {
        // ================= TESLA KILL EXPERIENCE =================

        // Vanilla gives SCP-079 nothing for SCPs (TerminationRewards.TryGetBaseReward has no SCP entry).
        // A tesla kill of a human = base reward * 2 (base: 30 Class-D/guard, 40 scientist, 50 MTF/Chaos).
        // An SCP killed by a tesla gives: ScpTeslaKillBaseReward * 2 * ScpTeslaKillMultiplier = 50 * 2 * 5 = 500 XP.
        public const int SCP_TESLA_KILL_BASE_REWARD = 50;
        public const int SCP_TESLA_KILL_MULTIPLIER = 5;

        // Subscribed to PlayerStats.OnAnyPlayerDied - the same hook vanilla TerminationRewards uses
        // (the victim still has its SCP role here). Same conditions as the vanilla direct tesla reward, per SCP-079:
        //   * it was a tesla death (TerminationRewards.CheckDirectTermination)
        //   * that SCP-079 used something in the victim's room within the last 12 s (tesla activation marks the room)
        //   * and, on top, that SCP-079 may hit this SCP by FF rules
        // With several SCP-079s each one is checked on its own (like vanilla does for humans).
        public static void FireLogics079ScpTeslaKill(ReferenceHub victim, DamageHandlerBase handler) {
            if (victim == null || !victim.IsSCP() || !TerminationRewards.CheckDirectTermination(handler)) {
                return;
            }

            if (!victim.TryGetCurrentRoom(out var room)) {
                return;
            }

            var reward = SCP_TESLA_KILL_BASE_REWARD * 2 * SCP_TESLA_KILL_MULTIPLIER;
            var victimRole = victim.GetRoleId();

            foreach (var scp079 in Scp079Role.ActiveInstances.ToList()) {
                if (!scp079.TryGetOwner(out var scp079Hub)
                    || !ScpFfTargets.IsFfScpTarget(scp079Hub, victim)
                    || !Scp079RewardManager.CheckForRoomInteractions(scp079, room)) {
                    continue;
                }

                Scp079RewardManager.GrantExp(scp079, reward, Scp079HudTranslation.ExpGainTerminationDirect, victimRole);
            }
        }
    }
}

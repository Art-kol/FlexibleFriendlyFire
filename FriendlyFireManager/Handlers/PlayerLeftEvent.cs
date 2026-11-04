using System.Linq;
using CustomPlayerEffects;
using LabApi.Events.Arguments.PlayerEvents;
using static FriendlyFireManager.FireLogics;
using static FriendlyFireManager.Handlers.FireScp079Events;
using static FriendlyFireManager.Handlers.FireSCPEvents;

namespace FriendlyFireManager.Handlers {
    // Full copy of ChangingRole event, nothing to see here
    public static class FirePlayerLeftEvent {
        public static void FireLogicsOnPlayerLeft(PlayerLeftEventArgs ev) {
            var playerhub = ev.Player.ReferenceHub;

            // Remove NightVision effects from all dead players
            PlayersWithNightVision.Remove(ev.Player);

            // If SCP-079 dies, clear the array of the rooms which were blackouted by him, remove NightVision from players inside
            foreach (var RoomAndPlayerPair in RoomBlackoutOwners.Where(RoomAndPlayerPair => RoomAndPlayerPair.Value == playerhub).ToList()) {
                foreach (var player in RoomAndPlayerPair.Key.Players) {
                    if (CanDamage(RoomBlackoutOwners[RoomAndPlayerPair.Key], player.ReferenceHub) == false) {
                        player.DisableEffect<NightVision>();
                        PlayersWithNightVision.Remove(player);
                    }
                }

                RoomBlackoutOwners.Remove(RoomAndPlayerPair.Key);
            }

            foreach (var HazardAndPlayerPair in HazardOwners.Where(HazardAndPlayerPair => HazardAndPlayerPair.Value == playerhub).ToList()) {
                HazardOwners.Remove(HazardAndPlayerPair.Key);
            }

            // If player "dies", remove all fire allows & fire blocks
            AllowPlayerPlayerFriendlyFire.RemoveWhere(AttackerAndTargetPair => AttackerAndTargetPair.Item1 == playerhub || AttackerAndTargetPair.Item2 == playerhub);
            BlockPlayerPlayerFriendlyFire.RemoveWhere(AttackerAndTargetPair => AttackerAndTargetPair.Item1 == playerhub || AttackerAndTargetPair.Item2 == playerhub);

            AllowPlayerRoleFriendlyFire.RemoveWhere(AttackerAndTargetPair => AttackerAndTargetPair.Item1 == playerhub);
            BlockPlayerRoleFriendlyFire.RemoveWhere(AttackerAndTargetPair => AttackerAndTargetPair.Item1 == playerhub);

            AllowRolePlayerFriendlyFire.RemoveWhere(AttackerAndTargetPair => AttackerAndTargetPair.Item2 == playerhub);
            BlockRolePlayerFriendlyFire.RemoveWhere(AttackerAndTargetPair => AttackerAndTargetPair.Item2 == playerhub);
        }
    }
}

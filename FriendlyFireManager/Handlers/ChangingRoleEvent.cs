using System.Linq;
using CustomPlayerEffects;
using LabApi.Events.Arguments.PlayerEvents;
using static FriendlyFireManager.FireLogics;
using static FriendlyFireManager.Handlers.FireScp079Events;
using static FriendlyFireManager.Handlers.FireSCPEvents;
using static FriendlyFireManager.StaticData;

namespace FriendlyFireManager.Handlers {
    public static class FireChangingRoleEvent {
        public static void FireLogicsChangingRole(PlayerChangingRoleEventArgs ev) {
            // If role changes to NOT dead, then skip everything
            if (!ev.IsAllowed || !InactiveRoles.Contains(ev.NewRole)) {
                return;
            }

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

            // If SCP-939 or SCP-173 dies, clear the array of poops and amnestic clouds they've spawned
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

using System;
using System.Collections.Generic;
using System.Linq;
using CustomPlayerEffects;
using Interactables.Interobjects.DoorUtils;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Arguments.Scp079Events;
using LabApi.Events.Arguments.ServerEvents;
using LabApi.Features.Wrappers;
using MapGeneration;
using PlayerRoles;
using PlayerRoles.PlayableScps.Scp079;
using static FriendlyFireManager.FireLogics;

namespace FriendlyFireManager.Handlers {
    public class FireScp079Events {
        // Storages
        public static readonly Dictionary<Room, ReferenceHub> RoomBlackoutOwners = new();
        public static readonly Dictionary<Door, ReferenceHub> DoorLockOwners = new();
        public static readonly Dictionary<TeslaGate, TeslaActivationInfo> TeslaActivations = new();
        public static readonly HashSet<Player> PlayersWithNightVision = new();
        public static BreakableWindow? Recontainer;
        public class TeslaActivationInfo {
            public DateTime Time;
            public required Player Scp079;
        }


        // ================= MAIN EVENTS =================
        // Purpose: If 079 cant attack player, player wont suffer from darkness effect
        // What it does: If 079 blacks the room then we store the room which was darken by 079
        //               & give players inside that room Night Vision
        public static void FireLogics079BlackedOutRoom(Scp079BlackedOutRoomEventArgs ev) {
            RoomBlackoutOwners[ev.Room] = ev.Player.ReferenceHub;

            foreach (var player in ev.Room.Players) {
                if (CanDamage(RoomBlackoutOwners[ev.Room], player.ReferenceHub) == false) {
                    player.EnableEffect<NightVision>(40);
                    PlayersWithNightVision.Add(player);
                }
                else {
                    if (PlayersWithNightVision.Contains(player)) {
                        player.DisableEffect<NightVision>();
                        PlayersWithNightVision.Remove(player);
                    }
                }
            }
        }

        // Purpose: If 079 cant attack player, player wont suffer from darkness effect
        // What it does: If 079 blacks the zone then we store the zone which was darken by 079
        //               & give players inside that zone Night Vision
        public static void FireLogics079BlackedOutZone(Scp079BlackedOutZoneEventArgs ev) {

            foreach (var room in Map.Rooms.Where(room => room.Zone == ev.Zone)) {
                RoomBlackoutOwners[room] = ev.Player.ReferenceHub;
            }

            foreach (var player in Player.List.Where(player => player.Room != null && player.Room.Zone == ev.Zone)) {
                if (player.Room == null) {
                    continue;
                }

                if (CanDamage(RoomBlackoutOwners[player.Room], player.ReferenceHub) == false) {
                    player.EnableEffect<NightVision>(40);
                    PlayersWithNightVision.Add(player);
                }
                else {
                    if (PlayersWithNightVision.Contains(player)) {
                        player.DisableEffect<NightVision>();
                        PlayersWithNightVision.Remove(player);
                    }
                }
            }
        }

        // Purpose: If 079 is recontained, all players in HCZ insta lose Night Vision (cuz PC died)
        // What it does: If 079 is recontained, all players in HCZ insta lose Night Vision (cuz PC died)
        //               thus they wont see shit during Zone blackout in HCZ caused by recontainment
        //               & we clear the dictionary of HCZ rooms (just to be sure)
        public static void FireLogics079Recontained(Scp079RecontainedEventArgs ev) {
            if (ev.Activator == null) {
                return;
            }

            var playersToRemove = PlayersWithNightVision
            .Where(p => p.Room != null && p.Room.Zone == FacilityZone.HeavyContainment)
            .ToList();

            foreach (var p in playersToRemove) {
                p.DisableEffect<NightVision>();
                PlayersWithNightVision.Remove(p);
            }

            foreach (var room in RoomBlackoutOwners.Keys.ToList().Where(room => room.Zone == FacilityZone.HeavyContainment)) {
                RoomBlackoutOwners.Remove(room);
            }

        }

        // Purpose: If 079 cant attack player, player will be able to open doors locked by PC
        // What it does: If 079 locks the door we store that door
        public static void FireLogics079LockingDoor(Scp079LockingDoorEventArgs ev) {
            DoorLockOwners[ev.Door] = ev.Player.ReferenceHub;
        }


        // Purpose: If 079 cant attack player, player will be able to open doors locked by PC
        // What it does: If 079 locks the room we store that room
        public static void FireLogics079LockingDownRoom(Scp079LockingDownRoomEventArgs ev) {
            foreach (var door in ev.Room.Doors) {
                DoorLockOwners[door] = ev.Player.ReferenceHub;
            }
        }

        // Purpose: If 079 cant attack player, player wont be hurt by Tesla activated by PC
        // What it does: If 079 activates Tesla then we store the action in dictionary
        public static void FireLogics079ActivatingTesla(Scp079UsingTeslaEventArgs ev) {
            var gate = Tesla.Dictionary.FirstOrDefault(x => x.Value == ev.Tesla).Key;
            TeslaActivations[gate] = new TeslaActivationInfo {
                Time = DateTime.UtcNow,
                Scp079 = ev.Player
            };
        }

        // Purpose: If 079 cant attack player, player will be able to open doors locked by PC
        // What it does: If 079 unlocks the door, we delete that from dict
        public static void FireLogics079UnlockingDoor(Scp079UnlockingDoorEventArgs ev) {
            DoorLockOwners.Remove(ev.Door);
        }


        // Purpose: If player cant attack 079, player will not be able to activate the gen
        // What it does: If player cant attack 079 then we decline the event
        public static void FireLogics079ActivatingGen(PlayerActivatingGeneratorEventArgs ev) {
            var scp079s = Player.List.Where(p => p.Role == RoleTypeId.Scp079);

            if (!scp079s.Any()) {
                return;
            }

            if (!scp079s.Any(scp079 => CanDamage(ev.Player.ReferenceHub, scp079.ReferenceHub) != false)) {
                ev.IsAllowed = false;
            }
        }

        // Purpose: If player cant attack 079, player will not be able to unlock the gen with keycard
        // What it does: If player cant attack 079 then we decline the event
        public static void FireLogics079UnlockingGen(PlayerUnlockingGeneratorEventArgs ev) {
            var scp079s = Player.List.Where(p => p.Role == RoleTypeId.Scp079);

            if (!scp079s.Any()) {
                return;
            }

            if (!scp079s.Any(scp079 => CanDamage(ev.Player.ReferenceHub, scp079.ReferenceHub) != false)) {
                ev.CanOpen = false;
                ev.IsAllowed = false;
            }
        }

        // Purpose: If player cant attack 079, player will not be able to damage the recontainment window
        // What it does: We find the instance of the particular window which is the 079 recontam window (on the button)
        //               if player cant attack 079 then we decline the event
        // Problem: This event works only for shooting. If u throw a projectile at the window it will break anyways
        // Solution: Check Patches/SCP079BreakWindowPatchThrowables.cs
        public static void FireLogics079ShootingWindow(PlayerDamagingWindowEventArgs ev) {
            if (!Recontainer) {
                Recontainer = UnityEngine.Object.FindAnyObjectByType<Scp079Recontainer>()._activatorGlass;
            }

            if (Recontainer != null && ev.Window == Recontainer) {
                var scp079s = Player.List.Where(p => p.Role == RoleTypeId.Scp079);

                if (!scp079s.Any()) {
                    return;
                }

                if (!scp079s.Any(scp079 => CanDamage(ev.Player.ReferenceHub, scp079.ReferenceHub) != false)) {
                    ev.IsAllowed = false;
                }
            }
        }


        // ================= HELPING EVENTS =================
        // Purpose: If 079 cant attack player, player wont suffer from darkness effect
        // What it does: If the light turned on => remove Night Vision and delete that room from dict
        public static void OnLightInRoomChanged(RoomLightChangedEventArgs ev) {
            if (ev.NewState) {
                foreach (var player in ev.Room.Players) {
                    if (PlayersWithNightVision.Contains(player)) {
                        player.DisableEffect<NightVision>();
                        PlayersWithNightVision.Remove(player);
                    }
                }

                if (RoomBlackoutOwners.TryGetValue(ev.Room, out _)) {
                    RoomBlackoutOwners.Remove(ev.Room);
                }

            }
        }

        // Purpose: If 079 cant attack player, player wont suffer from darkness effect
        // What it does: If player entered the room with lights on => remove Night Vision effect
        //               If 079 cant attack player and the latter entered the darkened (by 079) room
        //                  we give the player Night Vision, otherwise we remove his Night Vision
        public static void OnRoomChanged(PlayerRoomChangedEventArgs ev) {
            if (ev.Player == null || ev.NewRoom == null) {
                return;
            }

            var player = ev.Player;
            var room = ev.NewRoom;

            var NewRoomHasLight = room.AllLightControllers.All(lightcontroller => lightcontroller.LightsEnabled);

            if (NewRoomHasLight) {
                if (PlayersWithNightVision.Contains(player)) {
                    player.DisableEffect<NightVision>();
                    PlayersWithNightVision.Remove(player);
                }
            }
            else {
                if (RoomBlackoutOwners.TryGetValue(room, out var scp079)) {
                    if (CanDamage(scp079, player.ReferenceHub) == false) {
                        if (!PlayersWithNightVision.Contains(player)) {
                            player.EnableEffect<NightVision>(40);
                            PlayersWithNightVision.Add(player);
                        }
                    }
                    else {
                        if (PlayersWithNightVision.Contains(player)) {
                            player.DisableEffect<NightVision>();
                            PlayersWithNightVision.Remove(player);
                        }
                    }
                }
                else {
                    if (PlayersWithNightVision.Contains(player)) {
                        player.DisableEffect<NightVision>();
                        PlayersWithNightVision.Remove(player);
                    }
                }
            }
        }

        // Purpose: If 079 cant attack player, player can open doors locked by 079
        // What it does: If 079 is not locking the door then delete the action from dict
        public static void OnChangedLockReasonDoor(DoorLockChangedEventArgs ev) {
            if (Has079Lock(ev.PrevLockReason) &&
                !Has079Lock(ev.LockReason)) {
                DoorLockOwners.Remove(ev.Door);
            }
        }

        // Purpose: If 079 cant attack player, player can open doors locked by 079
        // What it does: If the latter opens the door locked by 079 => force the event to true
        public static void OnPlayerInteractringDoor(PlayerInteractingDoorEventArgs ev) {
            if (DoorLockOwners.TryGetValue(ev.Door, out var scp079)) {
                if (CanDamage(scp079, ev.Player.ReferenceHub) == false) {
                    ev.IsAllowed = true;
                    ev.CanOpen = true;
                }
            }
        }

        // Purpose: If 079 cant attack player, player can open doors locked by 079
        // What it does: Checks if the door is locked by 079
        private static bool Has079Lock(DoorLockReason reason) {
            return (reason & (DoorLockReason.Regular079 | DoorLockReason.Lockdown079)) != 0;
        }
    }
}

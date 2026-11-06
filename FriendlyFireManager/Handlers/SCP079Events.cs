using CustomPlayerEffects;
using Interactables.Interobjects.DoorUtils;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Arguments.ServerEvents;
using LabApi.Events.Arguments.Scp079Events;
using MapGeneration;
using PlayerRoles;
using PlayerRoles.PlayableScps.Scp079;
using System;
using System.Collections.Generic;
using System.Linq;
using LabApi.Features.Wrappers;
using FriendlyFireManager.Utilities;
using static FriendlyFireManager.FireLogics;
using Logger = LabApi.Features.Console.Logger;

namespace FriendlyFireManager.Handlers {
    public class FireScp079Events {
        // Storages
        public static readonly Dictionary<Room, ReferenceHub> RoomBlackoutOwners = new();
        public static readonly Dictionary<Door, ReferenceHub> DoorLockOwners = new();
        public static readonly Dictionary<TeslaGate, TeslaActivationInfo> TeslaActivations = new();
        public static readonly HashSet<Player> PlayersWithNightVision = new();

        // SCP-079s that survive the recontainment started by the button glass (config scp079.*_recontainment_targets),
        // remembered when the glass breaks. null = the glass isn't broken yet (or nothing was remembered).
        public static HashSet<ReferenceHub>? SparedFromRecontainment;
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


        // Several SCP-079s: how many of them the player / SCP must be allowed to hurt (config scp079.*: Any / All).
        // SCPs need an explicit allow, humans just must not be blocked (ScpFfTargets.MayHit079).
        public static Scp079Requirement GeneratorRequirement(ReferenceHub actor) {
            var config = FfConfig.Current.Scp079;
            return actor.IsSCP() ? config.ScpGenerators : config.HumanGenerators;
        }

        public static Scp079Requirement RecontainmentButtonRequirement(ReferenceHub actor) {
            var config = FfConfig.Current.Scp079;
            return actor.IsSCP() ? config.ScpRecontainmentButton : config.HumanRecontainmentButton;
        }

        // Purpose: If player cant attack 079, player will not be able to activate the gen
        // What it does: If player cant attack 079 then we decline the event
        //               (SCPs pulling the lever too, see Patches/Scp079ScpFfPatches Scp079GeneratorLeverScpPatch)
        public static void FireLogics079ActivatingGen(PlayerActivatingGeneratorEventArgs ev) {
            if (ev.Player == null) {
                return;
            }

            var actor = ev.Player.ReferenceHub;
            if (!ScpFfTargets.MayActAgainst079(actor, GeneratorRequirement(actor))) {
                ev.IsAllowed = false;
            }
        }

        // Purpose: If player cant attack 079, player will not be able to unlock the gen with keycard
        // What it does: If player cant attack 079 then we decline the event
        public static void FireLogics079UnlockingGen(PlayerUnlockingGeneratorEventArgs ev) {
            if (ev.Player == null) {
                return;
            }

            var actor = ev.Player.ReferenceHub;
            if (!ScpFfTargets.MayActAgainst079(actor, GeneratorRequirement(actor))) {
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
            if (ev.Player == null || !ScpFfTargets.IsRecontainerGlass(ev.Window)) {
                return;
            }

            var actor = ev.Player.ReferenceHub;
            if (!ScpFfTargets.MayActAgainst079(actor, RecontainmentButtonRequirement(actor))) {
                ev.IsAllowed = false;
            }
        }

        // ================= RECONTAINMENT: WHICH SCP-079s DIE =================
        // Config scp079.scp_recontainment_targets / human_recontainment_targets:
        //   All        - every SCP-079 is recontained (vanilla)
        //   Attackable - only the SCP-079s the one who broke the glass may hurt; the others survive
        // Decided once, when the glass breaks: until the recontainment (after the CASSIE countdown) the rules or the
        // breaker's role may change. The recontainment that starts because the last other SCP died is not touched.

        // The glass took damage from a player (shots, SCP attacks): did it just break?
        public static void FireLogics079GlassDamaged(PlayerDamagedWindowEventArgs ev) {
            if (ev.Player == null || ev.Window == null || !ev.Window.IsBroken || !ScpFfTargets.IsRecontainerGlass(ev.Window)) {
                return;
            }

            RememberGlassBreaker(ev.Player.ReferenceHub);
        }

        // Also called by Patches/SCP079BreakWindowPatchThrowables (vanilla doesn't record who threw the item)
        public static void RememberGlassBreaker(ReferenceHub breaker) {
            if (SparedFromRecontainment != null) {
                return;
            }

            SparedFromRecontainment = GetSpared079s(breaker);
        }

        public static void FireLogics079Recontaining(Scp079RecontainingEventArgs ev) {
            if (ev.Player == null || !ScpFfTargets.IsRecontainerGlassBroken()) {
                return; // not the button: vanilla, every SCP-079 dies
            }

            if (SparedFromRecontainment == null) {
                if (ev.Activator == null || ev.Activator.IsDestroyed) {
                    return; // nobody to ask: vanilla
                }

                SparedFromRecontainment = GetSpared079s(ev.Activator.ReferenceHub);
            }

            if (SparedFromRecontainment.Contains(ev.Player.ReferenceHub)) {
                ev.IsAllowed = false;
            }
        }

        private static HashSet<ReferenceHub> GetSpared079s(ReferenceHub breaker) {
            var spared = new HashSet<ReferenceHub>(ReferenceHubComparer.Instance);

            var config = FfConfig.Current.Scp079;
            var targets = breaker.IsSCP() ? config.ScpRecontainmentTargets : config.HumanRecontainmentTargets;
            if (targets == Scp079RecontainmentTargets.All) {
                return spared;
            }

            foreach (var scp079 in Scp079Role.ActiveInstances) {
                if (scp079.TryGetOwner(out var owner) && owner != breaker && !ScpFfTargets.MayHit079(breaker, owner)) {
                    spared.Add(owner);
                }
            }

            return spared;
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
                if (RoomBlackoutOwners.TryGetValue(ev.Room, out var scp079)) {
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

            bool NewRoomHasLight = room.AllLightControllers.All(lightcontroller => lightcontroller.LightsEnabled);

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

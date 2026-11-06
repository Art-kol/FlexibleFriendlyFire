using FriendlyFireManager.FfProxy;
using FriendlyFireManager.Handlers;
using Hazards;
using LabApi.Events.Handlers;
using PlayerStatsSystem;

namespace FriendlyFireManager {
    // Every event subscription of the plugin in one place (the Harmony patches are applied by
    // FriendlyFireManagerPlugin). Subscribe/Unsubscribe are idempotent.
    internal static class FfEventsManager {
        private static bool _subscribed;

        public static void Subscribe() {
            if (_subscribed) {
                return;
            }

            _subscribed = true;

            ServerEvents.WaitingForPlayers += OnWaitingForPlayers;

            // Damage rules
            PlayerEvents.ChangingRole += FireChangingRoleEvent.FireLogicsChangingRole;
            PlayerEvents.Left += FirePlayerLeftEvent.FireLogicsOnPlayerLeft;
            PlayerEvents.Hurting += FireHurtingEvents.FireLogicsOnPlayerHurting;
            Scp049Events.Attacking += FireHurtingEvents.FireLogicsOnScp049Attacking;

            // SCP abilities
            Scp049Events.UsingSense += FireSCPEvents.FireLogicsOnScp049Sensing;
            Scp173Events.AddingObserver += FireSCPEvents.FireLogicsOnAddingObserver;
            Scp096Events.AddingTarget += FireSCPEvents.FireLogicsOnAddingTarget;

            // SCP-079
            Scp079Events.BlackedOutRoom += FireScp079Events.FireLogics079BlackedOutRoom;
            Scp079Events.BlackedOutZone += FireScp079Events.FireLogics079BlackedOutZone;
            Scp079Events.Recontaining += FireScp079Events.FireLogics079Recontaining;
            Scp079Events.Recontained += FireScp079Events.FireLogics079Recontained;
            Scp079Events.LockingDoor += FireScp079Events.FireLogics079LockingDoor;
            Scp079Events.LockingDownRoom += FireScp079Events.FireLogics079LockingDownRoom;
            Scp079Events.UnlockingDoor += FireScp079Events.FireLogics079UnlockingDoor;
            Scp079Events.UsingTesla += FireScp079Events.FireLogics079ActivatingTesla;
            PlayerEvents.ActivatingGenerator += FireScp079Events.FireLogics079ActivatingGen;
            PlayerEvents.UnlockingGenerator += FireScp079Events.FireLogics079UnlockingGen;
            PlayerEvents.DamagingWindow += FireScp079Events.FireLogics079ShootingWindow;
            PlayerEvents.DamagedWindow += FireScp079Events.FireLogics079GlassDamaged;
            ServerEvents.RoomLightChanged += FireScp079Events.OnLightInRoomChanged;
            PlayerEvents.RoomChanged += FireScp079Events.OnRoomChanged;
            ServerEvents.DoorLockChanged += FireScp079Events.OnChangedLockReasonDoor;
            PlayerEvents.InteractingDoor += FireScp079Events.OnPlayerInteractringDoor;
            // SCP-079 vs SCPs (doors/darkness/generators/glass are Harmony patches in Patches/Scp079ScpFfPatches.cs)
            PlayerStats.OnAnyPlayerDied += FireScp079ScpEvents.FireLogics079ScpTeslaKill;

            // Hazards (SCP-173 tantrum, SCP-939 amnestic cloud)
            PlayerEvents.EnteringHazard += FireSCPEvents.FireLogicsOnEnteringHazard;
            Scp173Events.CreatedTantrum += FireSCPEvents.FireLogicsOnCreatedTantrum;
            Scp939Events.CreatedAmnesticCloud += FireSCPEvents.FireLogicsOnCreatedCloud;
            EnvironmentalHazard.OnRemoved += FireSCPEvents.FireLogicsHazardRemoved;

            // SCP-106 attack / SCP-939 lunge FF (proxy dummy + cmd patches). Idempotent.
            ScpFfProxy.Enable();

            // SCP-106 stalk (both directions) / SCP-939 amnesia: what an SCP may see of SCPs. Idempotent.
            ScpFfVisibility.Subscribe();
        }

        public static void Unsubscribe() {
            if (!_subscribed) {
                return;
            }

            _subscribed = false;

            ServerEvents.WaitingForPlayers -= OnWaitingForPlayers;

            PlayerEvents.ChangingRole -= FireChangingRoleEvent.FireLogicsChangingRole;
            PlayerEvents.Left -= FirePlayerLeftEvent.FireLogicsOnPlayerLeft;
            PlayerEvents.Hurting -= FireHurtingEvents.FireLogicsOnPlayerHurting;
            Scp049Events.Attacking -= FireHurtingEvents.FireLogicsOnScp049Attacking;

            Scp049Events.UsingSense -= FireSCPEvents.FireLogicsOnScp049Sensing;
            Scp173Events.AddingObserver -= FireSCPEvents.FireLogicsOnAddingObserver;
            Scp096Events.AddingTarget -= FireSCPEvents.FireLogicsOnAddingTarget;

            Scp079Events.BlackedOutRoom -= FireScp079Events.FireLogics079BlackedOutRoom;
            Scp079Events.BlackedOutZone -= FireScp079Events.FireLogics079BlackedOutZone;
            Scp079Events.Recontaining -= FireScp079Events.FireLogics079Recontaining;
            Scp079Events.Recontained -= FireScp079Events.FireLogics079Recontained;
            Scp079Events.LockingDoor -= FireScp079Events.FireLogics079LockingDoor;
            Scp079Events.LockingDownRoom -= FireScp079Events.FireLogics079LockingDownRoom;
            Scp079Events.UnlockingDoor -= FireScp079Events.FireLogics079UnlockingDoor;
            Scp079Events.UsingTesla -= FireScp079Events.FireLogics079ActivatingTesla;
            PlayerEvents.ActivatingGenerator -= FireScp079Events.FireLogics079ActivatingGen;
            PlayerEvents.UnlockingGenerator -= FireScp079Events.FireLogics079UnlockingGen;
            PlayerEvents.DamagingWindow -= FireScp079Events.FireLogics079ShootingWindow;
            PlayerEvents.DamagedWindow -= FireScp079Events.FireLogics079GlassDamaged;
            ServerEvents.RoomLightChanged -= FireScp079Events.OnLightInRoomChanged;
            PlayerEvents.RoomChanged -= FireScp079Events.OnRoomChanged;
            ServerEvents.DoorLockChanged -= FireScp079Events.OnChangedLockReasonDoor;
            PlayerEvents.InteractingDoor -= FireScp079Events.OnPlayerInteractringDoor;
            PlayerStats.OnAnyPlayerDied -= FireScp079ScpEvents.FireLogics079ScpTeslaKill;

            PlayerEvents.EnteringHazard -= FireSCPEvents.FireLogicsOnEnteringHazard;
            Scp173Events.CreatedTantrum -= FireSCPEvents.FireLogicsOnCreatedTantrum;
            Scp939Events.CreatedAmnesticCloud -= FireSCPEvents.FireLogicsOnCreatedCloud;
            EnvironmentalHazard.OnRemoved -= FireSCPEvents.FireLogicsHazardRemoved;

            // Unsubscribes and destroys the proxy dummy. Idempotent.
            ScpFfProxy.Disable();

            // Also shows again everything it hid. Idempotent.
            ScpFfVisibility.Unsubscribe();
        }

        // Rules and per-round state. Called on every WaitingForPlayers: player rules would point at players of
        // the previous round, and role rules are reset too so every round starts vanilla.
        public static void ClearState() {
            FireLogics.ClearAll();
            FireSCPEvents.HazardOwners.Clear();
            FireScp079Events.RoomBlackoutOwners.Clear();
            FireScp079Events.DoorLockOwners.Clear();
            FireScp079Events.TeslaActivations.Clear();
            FireScp079Events.PlayersWithNightVision.Clear();
            FireScp079Events.SparedFromRecontainment = null;
            ScpFfProxy.Reset();
            ScpFfVisibility.Clear();
        }

        private static void OnWaitingForPlayers() {
            ClearState();
        }
    }
}

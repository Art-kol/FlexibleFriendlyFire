using System.Collections.Generic;
using Hazards;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Arguments.Scp049Events;
using LabApi.Events.Arguments.Scp096Events;
using LabApi.Events.Arguments.Scp173Events;
using LabApi.Events.Arguments.Scp939Events;
using static FriendlyFireManager.FireLogics;

namespace FriendlyFireManager.Handlers {
    public static class FireSCPEvents {

        public static readonly Dictionary<EnvironmentalHazard, ReferenceHub> HazardOwners = new();

        // Purpose: If 049 cant attack the player, 049 cant get speed from chase ability used on that player
        // What it does: If 049 cant attack the player, we decline the event
        public static void FireLogicsOnScp049Sensing(Scp049UsingSenseEventArgs ev) {
            if (ev.Player == null || ev.Target == null) {
                return;
            }

            var PlagueCanAttackTarget = CanDamage(ev.Player.ReferenceHub, ev.Target.ReferenceHub);

            if (PlagueCanAttackTarget == false) {
                ev.IsAllowed = false;
                return;
            }
        }

        // Purpose: If 173 cant attack the player, 173 will ignore if that player stares at it
        // What it does: If 173 cant attack the player, we decline the event
        public static void FireLogicsOnAddingObserver(Scp173AddingObserverEventArgs ev) {
            if (ev.Player == null || ev.Target == null) {
                return;
            }

            var PeanusCanAttackTarget = CanDamage(ev.Player.ReferenceHub, ev.Target.ReferenceHub);

            if (PeanusCanAttackTarget == false) {
                ev.IsAllowed = false;
                return;
            }
        }

        // Purpose: If 096 cant attack the player, 096 will ignore if that player stares at it
        // What it does: If 096 cant attack the player, we decline the event
        public static void FireLogicsOnAddingTarget(Scp096AddingTargetEventArgs ev) {
            if (ev.Player == null || ev.Target == null) {
                return;
            }

            var ShyguyCanAttackTarget = CanDamage(ev.Player.ReferenceHub, ev.Target.ReferenceHub);

            if (ShyguyCanAttackTarget == false) {
                ev.IsAllowed = false;
                return;
            }
        }

        // Purpose: If 173 or 939 cant attack the player, 173's puddles and 939's mnestic cloud wont affect that player
        // What it does: If 173 or 939 cant attack the player, we decline the event
        public static void FireLogicsOnEnteringHazard(PlayerEnteringHazardEventArgs ev) {
            if (ev.Player == null) {
                return;
            }

            if (HazardOwners.TryGetValue(ev.Hazard.Base, out var owner) && CanDamage(owner, ev.Player.ReferenceHub) == false) {
                ev.IsAllowed = false;
                return;
            }
        }



        // ================= HELPING EVENTS =================

        // Purpose: If 173 cant attack the player, 173's puddles wont affect that player
        // What it does: If 173 creates a puddle, we store the action in dict    
        public static void FireLogicsOnCreatedTantrum(Scp173CreatedTantrumEventArgs ev) {
            if (ev.Player == null) {
                return;
            }

            HazardOwners[ev.Tantrum.Base] = ev.Player.ReferenceHub;
        }

        // Purpose: If 939 cant attack the player, 939's amnestic cloud wont affect that player
        // What it does: If 939 creates an amnestic cloud, we store the action in dict
        public static void FireLogicsOnCreatedCloud(Scp939CreatedAmnesticCloudEventArgs ev) {
            if (ev.Player == null) {
                return;
            }

            HazardOwners[ev.AmnesticCloud.Base] = ev.Player.ReferenceHub;
        }

        // Purpose: If 173 or 939 cant attack the player, 173's puddles and 939's mnestic cloud wont affect that player
        // What it does: If the amnestic cloud or puddle is removed somehow, we clear the dict
        public static void FireLogicsHazardRemoved(EnvironmentalHazard hazard) {
            HazardOwners.Remove(hazard);
        }
    }
}

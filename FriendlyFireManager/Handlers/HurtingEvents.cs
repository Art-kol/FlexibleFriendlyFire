using System;
using System.Linq;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Arguments.Scp049Events;
using PlayerRoles;
using PlayerStatsSystem;
using static FriendlyFireManager.FireLogics;
using static FriendlyFireManager.Handlers.FireScp079Events;

namespace FriendlyFireManager.Handlers {
    // MAIN PART OF FF PLUGIN
    public static class FireHurtingEvents {
        public static void FireLogicsOnPlayerHurting(PlayerHurtingEventArgs ev) {

            // If 079 cant attack player and player got hit by Tesla when 079 activated it, then we decline this damage  
            if (ev.DamageHandler is UniversalDamageHandler universal && universal.TranslationId == DeathTranslations.Tesla.Id) {
                var tesla = TeslaGate.AllGates
                    .Where(x => UnityEngine.Vector3.Distance(x.Position, ev.Player.Position) <= x.sizeOfTrigger * 2.2f)
                    .OrderBy(x => UnityEngine.Vector3.Distance(x.Position, ev.Player.Position))
                    .FirstOrDefault();

                if (TeslaActivations.TryGetValue(tesla, out var info)) {
                    if (DateTime.UtcNow - info.Time > TimeSpan.FromSeconds(3)) {
                        TeslaActivations.Remove(tesla);
                    }
                    else {
                        if (CanDamage(info.Scp079.ReferenceHub, ev.Player.ReferenceHub) == false) {
                            ev.IsAllowed = false;
                            return;
                        }
                    }

                }
            }

            // If weird damage reason then proceed as usual
            if (ev.Attacker == null || ev.Player == null || ev.DamageHandler is not AttackerDamageHandler attackerDamageHandler) {
                return;
            }

            // SCP-079 recontainment glass broken by an SCP (Patches/Scp079RecontainerGlassScpPatch):
            // EVERY SCP-079 dies, no matter the FF rules between that SCP and each 079.
            // (Attacker.Role = the role the attacker had when it broke the glass.)
            if (attackerDamageHandler is RecontainmentDamageHandler
                && attackerDamageHandler.Attacker.Role.GetTeam() == Team.SCPs
                && ev.Player.Role == RoleTypeId.Scp079) {
                attackerDamageHandler.ForceFullFriendlyFire = true;
                attackerDamageHandler.IsFriendlyFire = false;
                return;
            }

            var AttackerCanAttackPlayer = CanDamage(ev.Attacker.ReferenceHub, ev.Player.ReferenceHub);

            // If attacker cant attack player then decline damage
            // Problem: It doesnt work for melees
            // Solution: Check Patches/MeleeAttackPatch
            if (AttackerCanAttackPlayer == false) {
                ev.IsAllowed = false;
                return;
            }

            // If attacker can attack player then we force friendly fire
            // Problem: It doesnt work for melees
            // Solution: Check Patches/MeleeAttackPatch
            if (AttackerCanAttackPlayer == true) {
                attackerDamageHandler.ForceFullFriendlyFire = true;
                attackerDamageHandler.IsFriendlyFire = false;
                return;
            }
        }

        // Special case for SCP-049 damage handler, cuz only it works differently for some reason
        public static void FireLogicsOnScp049Attacking(Scp049AttackingEventArgs ev) {
            if (ev.Player == null || ev.Target == null) {
                return;
            }

            var PlagueCanAttackTarget = CanDamage(ev.Player.ReferenceHub, ev.Target.ReferenceHub);

            if (PlagueCanAttackTarget == false) {
                ev.IsAllowed = false;
                return;
            }
        }

    }
}
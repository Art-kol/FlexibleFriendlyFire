using Hazards;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Arguments.Scp049Events;
using LabApi.Events.Arguments.Scp173Events;
using PlayerRoles;
using PlayerStatsSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using FriendlyFireManager.Utilities;
using static FriendlyFireManager.FireLogics;
using static FriendlyFireManager.Handlers.FireScp079Events;

namespace FriendlyFireManager.Handlers;

// MAIN PART OF FF PLUGIN
public static class FireHurtingEvents {
    public static void FireLogicsOnPlayerHurting(PlayerHurtingEventArgs ev) {

        // If 079 cant attack player and player got hit by Tesla when 079 activated it, then we decline this damage  
        if (ev.DamageHandler is UniversalDamageHandler universal && universal.TranslationId == DeathTranslations.Tesla.Id) {
            var tesla = TeslaGate.AllGates
                .Where(x => UnityEngine.Vector3.Distance(x.Position, ev.Player.Position) <= x.sizeOfTrigger * 2.2f)
                .OrderBy(x => UnityEngine.Vector3.Distance(x.Position, ev.Player.Position))
                .FirstOrDefault();

            // No gate near the player -> nothing to look up (TryGetValue(null) would throw)
            if (tesla != null && TeslaActivations.TryGetValue(tesla, out var info)) {
                if (DateTime.UtcNow - info.Time > TimeSpan.FromSeconds(3)) {
                    TeslaActivations.Remove(tesla);
                }
                else if (!info.Scp079.IsDestroyed) {
                    var scp079 = info.Scp079.ReferenceHub;

                    if (CanDamage(scp079, ev.Player.ReferenceHub) == false) {
                        ev.IsAllowed = false;
                        return;
                    }

                    // Config scp_vs_scp_damage.scp079_tesla: tesla of an SCP-079 against an SCP it may hurt
                    if (ScpDamageOverrides.TryGet(scp079, ev.Player.ReferenceHub, ScpAttack.Scp079Tesla, out var teslaDamage)) {
                        universal.Damage = teslaDamage;
                    }
                }

            }
        }

        // If weird damage reason then proceed as usual
        if (ev.Attacker == null || ev.Player == null || ev.DamageHandler is not AttackerDamageHandler attackerDamageHandler) {
            return;
        }

        // SCP-079 recontainment (recontainment button glass broken). WHICH SCP-079s die was already decided in
        // FireScp079Events.FireLogics079Recontaining (config scp079.*_recontainment_targets): the spared ones never
        // get this damage. So every SCP-079 that gets it must die, even against the FF rules:
        //   * glass broken by an SCP (Patches/Scp079RecontainerGlassScpPatch) - SCPs never hurt 079 by default
        //   * by a human that is blocked from hurting this SCP-079 (human_recontainment_targets: All)
        // (Attacker.Role = the role the attacker had when it broke the glass.)
        if (attackerDamageHandler is RecontainmentDamageHandler && ev.Player.Role == RoleTypeId.Scp079
            && (attackerDamageHandler.Attacker.Role.GetTeam() == Team.SCPs
                || CanDamage(ev.Attacker.ReferenceHub, ev.Player.ReferenceHub) == false)) {
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

            // Config scp_vs_scp_damage: SCP attack on another SCP -> configured damage, without the on-hit effects
            // (SCP-049 / SCP-106 attacks are handled by their ability patches, see ScpDamageOverrides)
            if (ScpDamageOverrides.TryClassify(attackerDamageHandler, out var attack)
                && ScpDamageOverrides.TryGet(ev.Attacker.ReferenceHub, ev.Player.ReferenceHub, attack, out var damage)) {
                attackerDamageHandler.Damage = damage;
                ScpDamageOverrides.SuppressHitEffects(ev.Player.ReferenceHub);
            }

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
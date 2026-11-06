using System;
using System.Reflection;
using CustomPlayerEffects;
using FriendlyFireManager.Utilities;
using HarmonyLib;
using LabApi.Events.Arguments.Scp049Events;
using LabApi.Events.Handlers;
using Mirror;
using PlayerRoles.PlayableScps.Scp049;
using PlayerStatsSystem;
using Utils.Networking;
using Logger = LabApi.Features.Console.Logger;

namespace FriendlyFireManager.Patches {
    // config.yml -> scp_vs_scp_damage.scp049_attack
    //
    // Vanilla SCP-049 attack (Scp049AttackAbility.ServerProcessCmd): the first hit gives the target Cardiac Arrest,
    // a hit on a target that already has it kills. With a configured value, a hit on an SCP it may hurt only deals
    // that damage: no cardiac arrest, no instant kill. Everything around it stays vanilla: the same checks
    // (cooldown, resurrect in progress, IsTargetValid = distance + line of sight + FF), the Scp049Attacking /
    // Scp049Attacked LabAPI events, cooldown, OnServerHit (SCP-049's sense uses it), RPC, hitmarker.
    // Humans and value 0: vanilla, untouched.
    [HarmonyPatch(typeof(Scp049AttackAbility), nameof(Scp049AttackAbility.ServerProcessCmd))]
    public static class Scp049AttackDamagePatch {
        // The event can only be raised from inside the class -> through its backing field
        private static readonly FieldInfo? OnServerHitField = AccessTools.Field(typeof(Scp049AttackAbility), nameof(Scp049AttackAbility.OnServerHit));

        public static bool Prefix(Scp049AttackAbility __instance, NetworkReader reader) {
            // Same early-out as vanilla (before it reads anything)
            if (!__instance.Cooldown.IsReady || __instance._resurrect.IsInProgress) {
                return true;
            }

            // Peek at the target, vanilla reads it again if we don't take over
            var start = reader.Position;
            var target = reader.ReadReferenceHub();
            reader.Position = start;

            var owner = __instance.Owner;
            if (target == null || !ScpDamageOverrides.TryGet(owner, target, ScpAttack.Scp049Attack, out var damage)) {
                return true;
            }

            reader.ReadReferenceHub(); // consume it, like vanilla

            try {
                Attack(__instance, owner, target, damage);
            }
            catch (Exception e) {
                Logger.Error($"[FF] Scp049AttackDamagePatch failed: {e}");
            }

            return false;
        }

        private static void Attack(Scp049AttackAbility ability, ReferenceHub owner, ReferenceHub target, float damage) {
            ability._target = target;
            if (!ability.IsTargetValid(target)) {
                return;
            }

            var isSenseTarget = ability._sense.IsTarget(target);
            var attacking = new Scp049AttackingEventArgs(owner, target, false, isSenseTarget, Scp049AttackAbility.CooldownTime);
            Scp049Events.OnAttacking(attacking);
            if (!attacking.IsAllowed) {
                return;
            }

            // Another plugin may still ask for the vanilla instant kill
            ability._isInstaKillAttack = attacking.InstantKill;
            ability._isTarget = attacking.IsSenseTarget;
            ability.Cooldown.Trigger(attacking.CooldownTime);

            var dealt = attacking.InstantKill ? StandardDamageHandler.KillValue : damage;
            target.playerStats.DealDamage(new Scp049DamageHandler(owner, dealt, Scp049DamageHandler.AttackType.Instakill));

            (OnServerHitField?.GetValue(ability) as Action<ReferenceHub>)?.Invoke(target);
            ability.ServerSendRpc(toAll: true);
            Hitmarker.SendHitmarkerDirectly(owner, 1f, playAudio: false);
            Scp049Events.OnAttacked(new Scp049AttackedEventArgs(owner, target, ability._isInstaKillAttack, ability._isTarget));
        }
    }

    // On-hit effects of attacks with a configured scp_vs_scp_damage value (SCP-096's slap / charge concussion).
    // ScpDamageOverrides.SuppressHitEffects marks the target in PlayerHurting; the effect that the attack enables
    // right after the damage (same frame) is skipped for it. Everything else enables effects as usual.
    [HarmonyPatch(typeof(StatusEffectBase), nameof(StatusEffectBase.ServerSetState))]
    public static class ScpHitEffectSuppressionPatch {
        public static bool Prefix(StatusEffectBase __instance, byte intensity) {
            return intensity == 0 || __instance is not Concussed || !ScpDamageOverrides.AreHitEffectsSuppressed(__instance.Hub);
        }
    }
}

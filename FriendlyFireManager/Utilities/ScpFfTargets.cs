using PlayerRoles;
using PlayerRoles.FirstPersonControl;

namespace FriendlyFireManager.Utilities {
    // Shared "can this SCP use its ability on that SCP?" rule for the SCP-vs-SCP secondary abilities
    // (SCP-173 tantrum, SCP-106 stalk vision, SCP-049 sense, SCP-049-2 bloodlust, SCP-939 amnestic cloud,
    // SCP-079 locks / blackouts / tesla) and for SCPs acting against SCP-079 (generators, recontainment glass).
    //
    // SCP targets are decided ONLY by an explicit FF rule (FireLogics.CanDamage == true), never by
    // HitboxIdentity.IsEnemy - so it doesn't matter whether the IsEnemy patches in SCPFFPatch are active
    // or what they return for SCP-vs-SCP without a rule. Humans are not touched here: they keep vanilla logic.
    public static class ScpFfTargets {
        // attacker may use its SCP abilities on target: target is an SCP (not attacker itself),
        // is alive in a first person role, and an FF rule explicitly allows attacker -> target.
        public static bool IsFfScpTarget(ReferenceHub? attacker, ReferenceHub? target) {
            if (attacker == null || target == null || attacker == target) {
                return false;
            }

            if (!target.IsSCP() || target.roleManager.CurrentRole is not IFpcRole) {
                return false;
            }

            return FireLogics.CanDamage(attacker, target) == true;
        }

        // SCP-079 is not a first person role, so it has its own check: scp may act against this SCP-079.
        public static bool IsFfScp079Target(ReferenceHub? scp, ReferenceHub? scp079) {
            if (scp == null || scp079 == null || scp == scp079) {
                return false;
            }

            return scp.IsSCP() && scp079.roleManager.CurrentRole is PlayerRoles.PlayableScps.Scp079.Scp079Role
                               && FireLogics.CanDamage(scp, scp079) == true;
        }

        // There can be several SCP-079s and an SCP may be allowed against only one of them:
        // anything that works against "SCP-079 in general" (generators, recontainment glass) needs at least one.
        public static bool CanAttackAny079(ReferenceHub? scp) {
            if (scp == null || !scp.IsSCP()) {
                return false;
            }

            foreach (var scp079 in PlayerRoles.PlayableScps.Scp079.Scp079Role.ActiveInstances) {
                if (scp079.TryGetOwner(out var owner) && IsFfScp079Target(scp, owner)) {
                    return true;
                }
            }

            return false;
        }

        // Drop-in replacement for HitboxIdentity.IsEnemy(ReferenceHub, ReferenceHub) in vanilla ability code:
        // vanilla enemies + SCPs allowed by FF rules. Used by transpilers.
        public static bool IsEnemyOrFfScp(ReferenceHub attacker, ReferenceHub victim) {
            return HitboxIdentity.IsEnemy(attacker, victim) || IsFfScpTarget(attacker, victim);
        }
    }
}

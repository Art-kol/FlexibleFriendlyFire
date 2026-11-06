using System;
using PlayerRoles;
using PlayerRoles.FirstPersonControl;
using PlayerRoles.PlayableScps.Scp079;

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
            return scp != null && scp.IsSCP() && MayActAgainst079(scp, Scp079Requirement.Any);
        }

        // May this player / SCP hurt this SCP-079 (for generators and the recontainment button)?
        //   SCP:   only with an explicit allow (SCPs never hurt SCP-079 in vanilla)
        //   human: unless explicitly blocked (humans hurt SCP-079 in vanilla)
        public static bool MayHit079(ReferenceHub actor, ReferenceHub scp079) {
            var rule = FireLogics.CanDamage(actor, scp079);
            return actor.IsSCP() ? rule == true : rule != false;
        }

        // Something done against SCP-079 in general (generators, recontainment button) while there may be several
        // SCP-079s and the actor may hurt only some of them (config scp079.*): Any = one is enough, All = all of them.
        // No SCP-079 at all: humans -> vanilla (allowed), SCPs -> nothing they could act against.
        public static bool MayActAgainst079(ReferenceHub? actor, Scp079Requirement requirement) {
            if (actor == null) {
                return false;
            }

            var total = 0;
            var allowed = 0;

            foreach (var scp079 in Scp079Role.ActiveInstances) {
                if (!scp079.TryGetOwner(out var owner) || owner == actor) {
                    continue;
                }

                total++;
                if (MayHit079(actor, owner)) {
                    allowed++;
                }
            }

            if (total == 0) {
                return !actor.IsSCP();
            }

            return requirement == Scp079Requirement.All ? allowed == total : allowed > 0;
        }

        // ================= RECONTAINMENT BUTTON GLASS =================

        private static Scp079Recontainer[] _recontainers = Array.Empty<Scp079Recontainer>();

        // Exactly the glass the game itself watches (Scp079Recontainer._activatorGlass), compared by reference.
        public static bool IsRecontainerGlass(BreakableWindow? window) {
            if (window == null) {
                return false;
            }

            foreach (var recontainer in GetRecontainers()) {
                if (ReferenceEquals(recontainer._activatorGlass, window)) {
                    return true;
                }
            }

            return false;
        }

        // Is any recontainment button glass broken (= recontainment started by the button, not by the last SCP dying)?
        public static bool IsRecontainerGlassBroken() {
            foreach (var recontainer in GetRecontainers()) {
                if (recontainer._activatorGlass != null && recontainer._activatorGlass.IsBroken) {
                    return true;
                }
            }

            return false;
        }

        // Cached per map; looked up again after a round restart (the old objects are destroyed)
        private static Scp079Recontainer[] GetRecontainers() {
            var valid = _recontainers.Length > 0;
            foreach (var recontainer in _recontainers) {
                if (recontainer == null) {
                    valid = false;
                    break;
                }
            }

            if (!valid) {
                _recontainers = UnityEngine.Object.FindObjectsByType<Scp079Recontainer>(UnityEngine.FindObjectsSortMode.None);
            }

            return _recontainers;
        }

        // Drop-in replacement for HitboxIdentity.IsEnemy(ReferenceHub, ReferenceHub) in vanilla ability code:
        // vanilla enemies + SCPs allowed by FF rules. Used by transpilers.
        public static bool IsEnemyOrFfScp(ReferenceHub attacker, ReferenceHub victim) {
            return HitboxIdentity.IsEnemy(attacker, victim) || IsFfScpTarget(attacker, victim);
        }
    }
}

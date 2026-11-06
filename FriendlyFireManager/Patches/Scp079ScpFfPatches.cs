using System.Collections.Generic;
using CustomPlayerEffects;
using HarmonyLib;
using Interactables.Interobjects.DoorUtils;
using LabApi.Events.Arguments.PlayerEvents;
using LabApi.Events.Handlers;
using LabApi.Features.Wrappers;
using MapGeneration.Distributors;
using PlayerRoles;
using PlayerRoles.PlayableScps;
using PlayerRoles.PlayableScps.Scp079;
using PlayerStatsSystem;
using FriendlyFireManager.Utilities;
using Logger = LabApi.Features.Console.Logger;
using static FriendlyFireManager.Handlers.FireScp079Events;

namespace FriendlyFireManager.Patches {
    // SCP-079 against SCPs it may hit by FF rules (and SCPs that may hit SCP-079), mirroring Handlers/SCP079Events
    // for humans. "079 may hit SCP" = FireLogics.CanDamage(079, scp) == true, decided per SCP-079 (DoorLockOwners /
    // RoomBlackoutOwners remember WHICH 079 locked/darkened what). "SCP may hit 079" = CanDamage(scp, 079) == true for
    // at least one SCP-079, or for every one of them (config scp079.scp_generators / scp_recontainment_button: Any / All,
    // ScpFfTargets.MayActAgainst079), like the human generator/window checks.

    // ================= DOORS / ROOM LOCKDOWN =================

    // Vanilla: 079 locks (Regular079 / Lockdown079) have DoorLockMode.ScpOverride -> EVERY SCP opens them.
    // Here: an SCP that the locking 079 may hit is treated like a human - the lock holds (with the vanilla
    // "lock bypass denied" feedback), and the PlayerInteractingDoor plugin request is still sent, exactly as for
    // humans (so OnPlayerInteractringDoor and other plugins can still let it through).
    [HarmonyPatch(typeof(DoorVariant), nameof(DoorVariant.TryResolveLock))]
    public static class Scp079DoorLockScpPatch {
        public static bool Prefix(DoorVariant __instance, ReferenceHub ply, ref bool pluginRequestSent, ref bool __result) {
            if (!ply.IsSCP() || !IsLockedFor(__instance, ply)) {
                return true; // vanilla
            }

            // Vanilla TryResolveLock without the "ScpOverride && IsSCP" shortcut
            pluginRequestSent = false;

            if (ply.serverRoles.BypassMode) {
                __result = true;
                return false;
            }

            var mode = DoorLockUtils.GetMode((DoorLockReason)__instance.ActiveLocks);
            if ((mode.HasFlagFast(DoorLockMode.CanClose) && __instance.TargetState)
                || (mode.HasFlagFast(DoorLockMode.CanOpen) && !__instance.TargetState)) {
                __result = true;
                return false;
            }

            var interacting = new PlayerInteractingDoorEventArgs(ply, __instance, false);
            PlayerEvents.OnInteractingDoor(interacting);
            pluginRequestSent = true;

            __result = interacting.IsAllowed && interacting.CanOpen;
            return false;
        }

        // The door is locked by SCP-079 and the 079 that locked it may hit this SCP
        private static bool IsLockedFor(DoorVariant door, ReferenceHub scp) {
            if ((((DoorLockReason)door.ActiveLocks) & (DoorLockReason.Regular079 | DoorLockReason.Lockdown079)) == 0) {
                return false;
            }

            var wrapper = Door.Get(door);
            return wrapper != null
                   && DoorLockOwners.TryGetValue(wrapper, out var scp079)
                   && FireLogics.CanDamage(scp079, scp) == true;
        }
    }

    // ================= DARKNESS =================

    // Vanilla: in a dark room every SCP has InsufficientLighting on, and FpcStandardScp turns that into a fixed
    // 0.2 ambient light ("SCP vision"). Humans get no such boost - only NightVision etc.
    // Here: in a room darkened by an SCP-079 that may hit this SCP, InsufficientLighting stays OFF for that SCP,
    // so its client falls back to the human formula (black + NightVision if any) - "in the dark like humans".
    // Everything else (humans, other SCPs, other darkness) is vanilla.
    [HarmonyPatch(typeof(InsufficientLighting), nameof(InsufficientLighting.UpdateServer))]
    public static class Scp079ScpDarknessPatch {
        public static bool Prefix(InsufficientLighting __instance) {
            var hub = __instance.Hub;
            if (hub == null || hub.roleManager.CurrentRole is not FpcStandardScp) {
                return true; // humans etc.: vanilla
            }

            // Vanilla UpdateServer
            var insufficient = __instance.CurRole is IAmbientLightRole ambientLightRole && ambientLightRole.InsufficientLight;

            if (insufficient && IsInFfBlackout(hub)) {
                insufficient = false;
            }

            if (insufficient != __instance._prevTarget) {
                __instance.Intensity = (byte)(insufficient ? 1 : 0);
                __instance._prevTarget = insufficient;
            }

            return false;
        }

        // Room darkened by an SCP-079 (RoomBlackoutOwners, room or zone blackout) that may hit this SCP
        private static bool IsInFfBlackout(ReferenceHub scp) {
            var room = Player.Get(scp)?.CachedRoom;
            return room != null
                   && RoomBlackoutOwners.TryGetValue(room, out var scp079)
                   && FireLogics.CanDamage(scp079, scp) == true;
        }
    }

    // ================= GENERATORS =================

    // Vanilla Scp079Generator.ServerInteract, lever case: "if ((ply.IsSCP() && !Activating) || Engaged) break;"
    // -> SCPs can only switch a generator OFF. That single IsSCP call is swapped: an SCP that may hit at least one
    // SCP-079 counts as non-SCP there, so it can pull the lever ON. Everything else stays vanilla:
    //   * the lever needs the generator door OPEN (vanilla check before the switch) and the door can only be
    //     unlocked with a keycard - SCPs have no keycard permissions, so they still can't unlock it themselves;
    //   * PlayerActivatingGenerator still fires -> FireLogics079ActivatingGen applies as for humans.
    [HarmonyPatch(typeof(Scp079Generator), nameof(Scp079Generator.ServerInteract))]
    public static class Scp079GeneratorLeverScpPatch {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
            var isScp = AccessTools.Method(typeof(PlayerRolesUtils), nameof(PlayerRolesUtils.IsSCP), [typeof(ReferenceHub), typeof(bool)]);
            var replacement = AccessTools.Method(typeof(Scp079GeneratorLeverScpPatch), nameof(IsScpForLever));
            var replaced = 0;

            foreach (var instruction in instructions) {
                if (instruction.Calls(isScp)) {
                    instruction.operand = replacement;
                    replaced++;
                }

                yield return instruction;
            }

            if (replaced != 1) {
                Logger.Warn($"[FF] Scp079GeneratorLeverScpPatch: expected 1 IsSCP call, replaced {replaced}.");
            }
        }

        public static bool IsScpForLever(ReferenceHub hub, bool includeZombies) {
            return hub.IsSCP(includeZombies) && !ScpFfTargets.MayActAgainst079(hub, FfConfig.Current.Scp079.ScpGenerators);
        }
    }

    // ================= RECONTAINMENT GLASS =================

    // Vanilla: the 079 recontainment glass has _preventScpDamage -> BreakableWindow.Damage rejects every SCP
    // attacker before any event. Here: an SCP that may hit at least one SCP-079 damages it like a human
    // (PlayerDamagingWindow still fires -> FireLogics079ShootingWindow applies). Once it breaks, every SCP-079 dies, or
    // only the ones this SCP may hurt (config scp079.scp_recontainment_targets) - see FireScp079Events
    // .FireLogics079Recontaining and the RecontainmentDamageHandler branch in Handlers/HurtingEvents.
    // Only attacks that can hit windows at all can break it: SCP-939 claw/lunge, SCP-096, SCP-049-2, SCP-3114...
    // (SCP-173 / SCP-106 / SCP-049 attacks only hit players).
    [HarmonyPatch(typeof(BreakableWindow), nameof(BreakableWindow.Damage))]
    public static class Scp079RecontainerGlassScpPatch {
        public static bool Prefix(BreakableWindow __instance, float damage, DamageHandlerBase handler, ref bool __result) {
            if (handler is not AttackerDamageHandler attackerHandler
                || attackerHandler.Attacker.Role.GetTeam() != Team.SCPs
                || !__instance._preventScpDamage
                || !ScpFfTargets.IsRecontainerGlass(__instance)) {
                return true; // vanilla
            }

            var attacker = attackerHandler.Attacker.Hub;
            if (!ScpFfTargets.MayActAgainst079(attacker, FfConfig.Current.Scp079.ScpRecontainmentButton) || !IsAimingAt(attacker, __instance)) {
                return true; // vanilla: SCPs can't damage it
            }

            // Vanilla BreakableWindow.Damage without CheckDamagePerms
            var damaging = new PlayerDamagingWindowEventArgs(attacker, __instance, handler);
            PlayerEvents.OnDamagingWindow(damaging);
            if (!damaging.IsAllowed) {
                __result = false;
                return false;
            }

            __instance.LastAttacker = attackerHandler.Attacker;
            __instance.ServerDamageWindow(damage);
            PlayerEvents.OnDamagedWindow(new PlayerDamagedWindowEventArgs(attacker, __instance, handler));

            Logger.Info($"[FF] {attacker?.nicknameSync.MyNick} ({attackerHandler.Attacker.Role}) damaged the SCP-079 recontainment glass "
                        + $"'{__instance.name}' at {__instance.transform.position}, broken: {__instance.IsBroken}");

            __result = true;
            return false;
        }

        // SCP attacks hit EVERY destructible around them (claw sphere, 939 lunge path, 096 charge), so clawing a
        // window next to the button would also break the button glass. An SCP must look straight at the glass.
        public const float AimDistance = 4f;

        private static int _aimMask;

        private static bool IsAimingAt(ReferenceHub? attacker, BreakableWindow window) {
            if (attacker == null) {
                return false;
            }

            if (_aimMask == 0) {
                _aimMask = UnityEngine.LayerMask.GetMask("Default", "Glass", "Door");
            }

            var camera = attacker.PlayerCameraReference;
            return UnityEngine.Physics.Raycast(camera.position, camera.forward, out var hit, AimDistance, _aimMask)
                   && hit.collider.GetComponentInParent<BreakableWindow>() == window;
        }
    }
}

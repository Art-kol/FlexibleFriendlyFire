using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Mirror;
using PlayerRoles.FirstPersonControl.NetworkMessages;
using PlayerRoles.PlayableScps.Scp049.Zombies;
using PlayerRoles.PlayableScps.Scp096;
using PlayerRoles.PlayableScps.Scp173;
using PlayerRoles.PlayableScps.Scp3114;
using PlayerRoles.PlayableScps.Scp939;
using PlayerRoles.PlayableScps.Scp939.Mimicry;
using PlayerRoles.PlayableScps.Scp939.Ripples;
using PlayerRoles.PlayableScps.Subroutines;
using PlayerRoles.Subroutines;
using Logger = LabApi.Features.Console.Logger;

namespace FriendlyFireManager.Patches {
    // Bug: every time an SCP-939 popped into view it played its "damaged" sound + animation (also other one-shot
    // sounds of SCPs could repeat).
    //
    // Why: when the server re-sends a role to a client (FpcServerPositionDistributor.SendRole), the client builds a
    // new role instance, so the server also sends a "confirmation" of EVERY subroutine to bring its state over. A
    // confirmation is the same message as a normal RPC: ServerWriteRpc on the server, ClientProcessRpc on the client.
    // For state subroutines that's what's needed, but some subroutines' RPC is a one-shot event - the client just
    // plays it again: Scp939DamagedEffect.ClientProcessRpc -> Scp939Model.PlayDamagedEffect, nothing else.
    // Vanilla almost never re-sends the role of an SCP (only when a player joins), so it doesn't notice. This plugin
    // does it on purpose when an SCP that was hidden from another SCP shows up again (Handlers/ScpFfVisibility -
    // the model pops in instead of flying in from the sky), and the SCP-3114 disguise changes the role too.
    //
    // Fix: in SendRole only the confirmations of the one-shot subroutines below are left out. All state subroutines
    // (focus, lunge, sinkhole, rage, disguise, cooldowns...) are still sent as in vanilla.
    [HarmonyPatch(typeof(FpcServerPositionDistributor), nameof(FpcServerPositionDistributor.SendRole))]
    public static class RoleResyncOneShotRpcPatch {
        // Subroutines whose RPC only plays something once (sound, animation, ripple, hit effect)
        private static readonly Type[] _oneShotTypes = [
            typeof(Scp939DamagedEffect),    // damaged sound + animation
            typeof(EnvironmentalMimicry),   // last environmental mimicry sound
            typeof(MimicryRecorder),        // "record this voice" for the 939 owner only
            typeof(RippleTriggerBase),      // ripples + footstep sounds for the 939 owner
            typeof(Scp096AttackAbility),    // last slap hit
            typeof(Scp096AudioPlayer),      // last hit sound
            typeof(Scp173AudioPlayer),      // last 173 sound (snap, teleport...)
            typeof(ZombieAudioPlayer),      // last 049-2 sound
            typeof(Scp3114VoiceLines)       // last voice line
        ];

        private static readonly Dictionary<Type, bool> _oneShotCache = new();

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
            var replacement = AccessTools.Method(typeof(RoleResyncOneShotRpcPatch), nameof(SendConfirmation));
            var replaced = 0;

            foreach (var instruction in instructions) {
                if (IsSendSubroutineMessage(instruction)) {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = replacement;
                    replaced++;
                }

                yield return instruction;
            }

            if (replaced != 1) {
                Logger.Warn($"[FF] RoleResyncOneShotRpcPatch: expected 1 subroutine confirmation in SendRole, replaced {replaced}.");
            }
        }

        // connection.Send<SubroutineMessage>(message, channelId)
        private static bool IsSendSubroutineMessage(CodeInstruction instruction) {
            return (instruction.opcode == OpCodes.Callvirt || instruction.opcode == OpCodes.Call)
                   && instruction.operand is MethodInfo method
                   && method.Name == nameof(NetworkConnection.Send)
                   && method.IsGenericMethod
                   && method.GetGenericArguments()[0] == typeof(SubroutineMessage)
                   && method.GetParameters().Length == 2;
        }

        // Same stack as the replaced instance call: (connection, message, channelId)
        public static void SendConfirmation(NetworkConnection connection, SubroutineMessage message, int channelId) {
            if (IsOneShot(message._subroutine)) {
                return;
            }

            connection.Send(message, channelId);
        }

        private static bool IsOneShot(SubroutineBase? subroutine) {
            if (subroutine == null) {
                return false;
            }

            // Mixed one: "kill" is a one-shot sound, "choke sync" is state
            if (subroutine is Scp3114StrangleAudio strangleAudio) {
                return strangleAudio._rpcType == Scp3114StrangleAudio.RpcType.Kill;
            }

            var type = subroutine.GetType();
            if (_oneShotCache.TryGetValue(type, out var oneShot)) {
                return oneShot;
            }

            oneShot = false;
            foreach (var oneShotType in _oneShotTypes) {
                if (oneShotType.IsAssignableFrom(type)) {
                    oneShot = true;
                    break;
                }
            }

            // Claws / slaps (SCP-939, SCP-049-2, SCP-3114...): the last attack result or a swing in progress
            for (var baseType = type; !oneShot && baseType != null; baseType = baseType.BaseType) {
                if (baseType.IsGenericType && baseType.GetGenericTypeDefinition() == typeof(ScpAttackAbilityBase<>)) {
                    oneShot = true;
                }
            }

            _oneShotCache[type] = oneShot;
            return oneShot;
        }
    }
}

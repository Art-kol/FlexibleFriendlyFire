using System;
using System.Linq;
using System.Text;
using Cassie;
using Footprinting;
using HarmonyLib;
using LabApi.Events.Arguments.ServerEvents;
using LabApi.Events.Handlers;
using PlayerRoles;
using PlayerStatsSystem;
using Subtitles;
using Logger = LabApi.Features.Console.Logger;

namespace FriendlyFireManager.Patches {
    // CASSIE when an SCP is killed by another SCP.
    //
    // Vanilla: CassieScpTerminationAnnouncement.AnnounceScpTermination (on every death) announces an SCP death with
    // the text of the damage handler (DamageHandlerBase.CassieDeathAnnouncement):
    //   * AttackerDamageHandler (base)   -> "TERMINATED BY SCP X" for SCP attackers - that's why SCP-939 already works
    //   * ScpDamageHandler (173, 106 + its corrosion), Scp049DamageHandler (049 + cardiac arrest, SCP-049-2 claws),
    //     Scp096DamageHandler, Scp3114DamageHandler -> EMPTY -> no announcement at all
    //     (vanilla never needs them: those SCPs only kill humans)
    //   * pocket dimension (UniversalDamageHandler, PocketDecay) -> "SUCCESSFULLY TERMINATED. TERMINATION CAUSE UNSPECIFIED"
    //
    // Here, for an SCP victim (SCP-049-2 victims are skipped by vanilla itself - IsSCP(includeZombies: false)):
    //   * killed by an SCP (any attacker handler, also SCP-079 recontainment glass broken by an SCP)
    //       -> "SCP-<victim> TERMINATED BY SCP-<attacker>", same text and subtitles as vanilla SCP-939 kills.
    //       SCP-049-2 attackers are read out as "SCP-049-2" (vanilla's ConvertSCP says "049" - also for handlers
    //       that already had a text, like 939 claws or recontainment, so those are rebuilt here too).
    //   * died in the pocket dimension (SCP-106 teleport)  -> "SCP-<victim> LOST IN POCKET DIMENSION"
    //       (own announcement with plain-text subtitles: the subtitle translation has no line for it)
    //   * SCP-106 one-hit kill (target was Traumatized) / corrosion -> those are ScpDamageHandler -> first rule (106)
    //   * anything else -> vanilla
    // The rest is the vanilla queueing: deaths with the same text are merged ("SCP 0 4 9 . SCP 1 7 3 TERMINATED BY ..."),
    // LabAPI CassieQueuingScpTermination / CassieQueuedScpTermination events fire as usual.
    [HarmonyPatch(typeof(CassieScpTerminationAnnouncement), nameof(CassieScpTerminationAnnouncement.AnnounceScpTermination))]
    public static class ScpKillCassiePatch {
        // CASSIE words (TTS) + subtitle for an SCP that died in the pocket dimension
        public const string POCKET_TTS = "LOST IN POCKET DIMENSION";
        public const string POCKET_SUBTITLE = "lost in Pocket dimension.";

        private static bool _pocketWordsChecked;

        public static bool Prefix(ReferenceHub scp, DamageHandlerBase hit) {
            try {
                if (!TryGetAnnouncement(scp, hit, out var tts, out var subtitles, out var pocket)) {
                    return true; // vanilla
                }

                Queue(scp, hit, tts, subtitles, pocket);
                return false;
            }
            catch (Exception e) {
                Logger.Error($"[FF] ScpKillCassiePatch failed, falling back to vanilla: {e}");
                return true;
            }
        }

        private static bool TryGetAnnouncement(ReferenceHub victim, DamageHandlerBase hit, out string tts, out SubtitlePart[] subtitles, out bool pocket) {
            tts = string.Empty;
            subtitles = Array.Empty<SubtitlePart>();
            pocket = false;

            // Same victim filter as vanilla for SCPs (SCP-049-2 never gets an announcement)
            if (victim == null || !victim.IsSCP(includeZombies: false)) {
                return false;
            }

            // Pocket dimension: only SCP-106 can bring an SCP there
            if (hit is UniversalDamageHandler universal && universal.TranslationId == DeathTranslations.PocketDecay.Id) {
                CheckPocketWords();
                tts = POCKET_TTS;
                // Only used to merge several pocket deaths into one line; the shown subtitle is built in
                // PocketTerminationAnnouncement (a SubtitleType.Custom part isn't rendered in SCP terminations)
                subtitles = [new SubtitlePart(SubtitleType.Custom, POCKET_SUBTITLE)];
                pocket = true;
                return true;
            }

            // Killed by another SCP - whatever text the handler had (empty for 173/106/049/049-2/096/3114,
            // "TERMINATED BY SCP ..." for 939-like handlers and recontainment): rebuilt for the 049-2 name
            if (hit is not AttackerDamageHandler attackerHandler) {
                return false;
            }

            var attacker = attackerHandler.Attacker;
            if (!attacker.IsSet || attacker.NetId == victim.netId || attacker.Role.GetTeam() != Team.SCPs) {
                return false;
            }

            ConvertScp(attacker, out var withoutSpace, out var withSpace);
            if (string.IsNullOrEmpty(withSpace)) {
                return false;
            }

            // Vanilla AttackerDamageHandler text for SCP attackers
            tts = "TERMINATED BY SCP " + withSpace;
            subtitles = [new SubtitlePart(SubtitleType.TerminatedBySCP, withoutSpace)];
            return true;
        }

        // Vanilla ConvertSCP takes the part after the first '-' of the role name -> "SCP-049-2" would become "049".
        private static void ConvertScp(Footprint attacker, out string withoutSpace, out string withSpace) {
            if (attacker.Role == RoleTypeId.Scp0492) {
                withoutSpace = "049-2";
                withSpace = "0 4 9 2 ";
                return;
            }

            CassieScpTerminationAnnouncement.ConvertSCP(attacker.Role, out withoutSpace, out withSpace);
        }

        // Vanilla AnnounceScpTermination after the text is known
        private static void Queue(ReferenceHub victim, DamageHandlerBase hit, string tts, SubtitlePart[] subtitles, bool pocket) {
            foreach (var queued in CassieAnnouncementDispatcher.AllAnnouncementsPreview) {
                if (queued is CassieScpTerminationAnnouncement termination
                    && termination._announcementTts == tts
                    && SubtitlePart.CheckEqualValues(termination._subtitles, subtitles)) {
                    termination._victims.Add(new Footprint(victim));
                    termination._remainingWait = CassieScpTerminationAnnouncement.WaitForOtherScpsDuration;
                    return;
                }
            }

            var queuing = new CassieQueuingScpTerminationEventArgs(victim, tts, subtitles, hit);
            ServerEvents.OnCassieQueuingScpTermination(queuing);
            if (!queuing.IsAllowed) {
                return;
            }

            tts = queuing.Announcement;
            subtitles = queuing.SubtitleParts;

            var announcement = pocket && tts == POCKET_TTS
                ? new PocketTerminationAnnouncement(new Footprint(victim), tts, subtitles)
                : new CassieScpTerminationAnnouncement(new Footprint(victim), tts, subtitles);
            announcement.AddToQueue();
            ServerEvents.OnCassieQueuedScpTermination(new CassieQueuedScpTerminationEventArgs(victim, tts, subtitles, hit));
        }

        // CASSIE silently skips words it doesn't know - warn once in the console if the pocket line has any
        private static void CheckPocketWords() {
            if (_pocketWordsChecked) {
                return;
            }

            _pocketWordsChecked = true;

            try {
                var unknown = POCKET_TTS.Split(' ').Where(word => !LabApi.Features.Wrappers.Announcer.IsValid(word)).ToArray();
                if (unknown.Length > 0) {
                    Logger.Warn($"[FF] CASSIE doesn't know these words of \"{POCKET_TTS}\": {string.Join(", ", unknown)}. Change ScpKillCassiePatch.PocketTts.");
                }
            }
            catch (Exception e) {
                Logger.Warn($"[FF] Couldn't check CASSIE words: {e.Message}");
            }
        }
    }
    // Pocket dimension death announcement. Same queueing / merging / 1 s wait as vanilla SCP terminations, but the
    // subtitle is sent as plain text ("SCP-049, SCP-173 lost in Pocket dimension.") because the translated subtitle
    // list has no entry for it.
    public class PocketTerminationAnnouncement : CassieScpTerminationAnnouncement {
        public PocketTerminationAnnouncement(Footprint victim, string tts, SubtitlePart[] subtitles) : base(victim, tts, subtitles) { }

        public override void OnStartedPlaying() {
            var tts = new StringBuilder();
            var subtitle = new StringBuilder();

            for (var i = 0; i < Victims.Count; i++) {
                ConvertSCP(Victims[i].Role, out var withoutSpace, out var withSpace);

                tts.Append(i == 0 ? "SCP " : ". SCP ").Append(withSpace);
                subtitle.Append(i == 0 ? "SCP-" : ", SCP-").Append(withoutSpace);
            }

            tts.Append(_announcementTts);
            subtitle.Append(' ').Append(ScpKillCassiePatch.POCKET_SUBTITLE);

            // What CassieAnnouncement.OnStartedPlaying does (glitch + upper case). The direct base would replace the
            // payload with translated subtitles again, so it isn't called.
            var content = CassieGlitchifier.Glitchify(tts.ToString(), GlitchScale).ToUpperInvariant();
            Payload = new CassieTtsPayload(content, subtitle.ToString(), playBackground: true);
        }
    }
}

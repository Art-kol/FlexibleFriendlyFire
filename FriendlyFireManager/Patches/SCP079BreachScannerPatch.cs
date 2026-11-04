using System;
using System.Collections.Generic;
using HarmonyLib;
using PlayerRoles.PlayableScps.Scp079;
using static FriendlyFireManager.FireLogics;

namespace FriendlyFireManager.Patches {

    [HarmonyPatch(typeof(Scp079ScannerSequence), "UpdateSequence")]
    // Purpose: If 079 cant attack the player, breach scanner wont detect that player (same as SCPs, which are never tracked)
    // What it does: Breach scanner picks its target from Scp079ScannerTracker.TrackedPlayers inside UpdateSequence.
    //               Right before UpdateSequence runs we temporarily null out every tracked player that 079 cant attack,
    //               so the sequence simply doesnt see them. Right after it (Finalizer = always runs, even on exception)
    //               we put them back, so camping timers/positions keep working as usual.
    //               Every SCP-079 has its own tracker, so the check is done against the 079 that owns this scanner.
    public static class SCP079BreachScannerPatch {
        private static readonly List<KeyValuePair<int, Scp079ScannerTrackedPlayer>> _hiddenPlayers = new();
        private static Scp079ScannerTrackedPlayer[]? _hiddenFrom;

        public static void Prefix(Scp079ScannerTracker ____tracker) {
            if (____tracker == null || ____tracker.Owner == null) {
                return;
            }

            var scp079 = ____tracker.Owner;
            var tracked = ____tracker.TrackedPlayers;

            for (var i = 0; i < tracked.Length; i++) {
                var entry = tracked[i];

                if (entry == null || entry.Hub == null) {
                    continue;
                }

                if (CanDamage(scp079, entry.Hub) == false) {
                    _hiddenPlayers.Add(new KeyValuePair<int, Scp079ScannerTrackedPlayer>(i, entry));
                    tracked[i] = null!;
                }
            }

            if (_hiddenPlayers.Count > 0) {
                _hiddenFrom = tracked;
            }
        }

        public static Exception? Finalizer(Exception? __exception) {
            if (_hiddenFrom != null) {
                foreach (var pair in _hiddenPlayers) {
                    // Array cant be resized/changed during UpdateSequence
                    if (pair.Key < _hiddenFrom.Length && _hiddenFrom[pair.Key] == null) {
                        _hiddenFrom[pair.Key] = pair.Value;
                    }
                }
            }

            _hiddenPlayers.Clear();
            _hiddenFrom = null;
            return __exception;
        }
    }
}

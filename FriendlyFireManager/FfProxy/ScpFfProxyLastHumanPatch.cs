using HarmonyLib;
using PlayerRoles;
using PlayerRoles.PlayableScps.HumanTracker;

namespace FriendlyFireManager.FfProxy {
    // The proxy dummy is a Tutorial, and vanilla counts it as a HUMAN in LastHumanTracker.TryGetLastTarget.
    // With no real humans left (only SCPs + proxy) the game thought "one human left":
    //   * LastHumanTracker.IsOneTargetLeft = true -> Scp939VisibilityController.ValidateVisibility (server) and the
    //     939 client both switch to "see everyone at any distance" -> SCP-939 saw every SCP on the whole map;
    //   * SCPs got the "last human" tracking orb pointing at the proxy; IsLastScpTarget picked the proxy.
    // And with exactly one real human left it was counted as two, so the vanilla last-human mode never started.
    // Fix: vanilla TryGetLastTarget, with the proxy skipped.
    [HarmonyPatch(typeof(LastHumanTracker), nameof(LastHumanTracker.TryGetLastTarget))]
    public static class ScpFfProxyLastHumanPatch {
        public static bool Prefix(ref ReferenceHub lastTarget, ref bool __result) {
            lastTarget = null!;

            var humans = 0;
            var scps = 0;

            foreach (var hub in ReferenceHub.AllHubs) {
                if (ScpFfProxy.IsProxy(hub)) {
                    continue;
                }

                if (hub.IsHuman()) {
                    humans++;
                    lastTarget = hub;
                }
                else if (hub.IsSCP()) {
                    scps++;
                }
            }

            __result = humans == 1 && scps > 0;
            return false;
        }
    }
}

using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace FriendlyFireManager.Utilities {
    // ReferenceHub.GetHashCode() goes through .gameObject and throws once the hub is destroyed (player left).
    // Collections keyed by ReferenceHub that may outlive the player use reference equality instead.
    public sealed class ReferenceHubComparer : IEqualityComparer<ReferenceHub> {
        public static readonly ReferenceHubComparer Instance = new();

        public bool Equals(ReferenceHub? x, ReferenceHub? y) => ReferenceEquals(x, y);

        public int GetHashCode(ReferenceHub obj) => RuntimeHelpers.GetHashCode(obj);
    }
}

using UnityEngine;

namespace SimpleUIScreensSystem
{
    /// <summary>Process-wide motion preferences. Set from your settings screen or platform accessibility APIs.</summary>
    public static class UIMotion
    {
        /// <summary>When true, screens open and close instantly instead of animating.</summary>
        public static bool ReduceMotion { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => ReduceMotion = false;
    }
}

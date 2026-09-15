#region Libraries

using UnityEngine;

#endregion

namespace SimpleUIScreensSystem
{
    /// <summary>Registers every scene screen with <see cref="UINavigator"/> on startup and hides it.</summary>
    public class UIInitializer
    {
        private static UIInitializer _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            if (_instance != null) return;

            _instance = new UIInitializer();
            var navigator = UINavigator.Instance;
#if UNITY_6000_5_OR_NEWER
            var screens = Object.FindObjectsByType<UIScreen>(FindObjectsInactive.Include);
#else
            var screens = Object.FindObjectsByType<UIScreen>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#endif
            foreach (var screen in screens)
            {
                screen.Init();
                navigator.Add(screen);
                screen.Hide();
            }
        }
    }
}

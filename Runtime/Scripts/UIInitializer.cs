#region Libraries

using UnityEngine;

#endregion

namespace SimpleUIScreensSystem
{
    public class UIInitializer
    {
        private static UIInitializer _instance;

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
                screen.Close();
            }
        }
    }
}
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
            var screens = Object.FindObjectsByType<UIScreen>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var screen in screens)
            {
                screen.Init();
                navigator.Add(screen);
                screen.Close();
            }
        }
    }
}
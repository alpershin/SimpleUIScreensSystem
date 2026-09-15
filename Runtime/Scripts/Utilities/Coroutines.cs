#region Libraries

using System;
using System.Collections;
using UnityEngine;

#endregion

namespace SimpleUIScreensSystem
{
    [Obsolete("Screens run their transitions on their own component. This shared runner is no longer used by the package and will be removed in 3.0.")]
    public class Coroutines : MonoBehaviour
    {
        private static Coroutines _runner;

        public static Coroutines Runner => _runner != null ? _runner : CreateRunner();

        public static void Run(IEnumerator coroutine) => Runner.StartCoroutine(coroutine);

        public static void StopAll()
        {
            if (_runner != null) _runner.StopAllCoroutines();
        }

        private static Coroutines CreateRunner()
        {
            _runner = new GameObject("CoroutinesRunner").AddComponent<Coroutines>();
            DontDestroyOnLoad(_runner.gameObject);
            return _runner;
        }
    }
}

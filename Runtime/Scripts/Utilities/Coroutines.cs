#region Libraries

using System.Collections;
using UnityEngine;

#endregion

namespace SimpleUIScreensSystem
{
    public class Coroutines : MonoBehaviour
    {
        private static Coroutines _runner;

        public static Coroutines Runner => _runner ??= CreateRunner();

        public static void Run(IEnumerator coroutine)
        {
            if (_runner == null) CreateRunner();
            
            _runner.StartCoroutine(coroutine);
        }

        public static void StopAll() =>
            _runner.StopAllCoroutines();

        private static Coroutines CreateRunner()
        {
            if (_runner != null) return _runner;

            _runner = new GameObject("CoroutinesRunner").AddComponent<Coroutines>();
            DontDestroyOnLoad(_runner.gameObject);
            
            return _runner;
        }
    }
}
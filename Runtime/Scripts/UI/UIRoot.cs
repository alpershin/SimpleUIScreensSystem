#region Libraries

using System;
using System.Collections.Generic;
using UnityEngine;

#endregion

namespace SimpleUIScreensSystem
{
    /// <summary>
    /// One entry point for opening screens by key. Each request goes to the first source that owns
    /// the key, so calling code does not care whether a screen sits in the scene or is loaded on
    /// demand. Assign sources in the inspector, or register them from code with <see cref="AddSource"/>.
    /// </summary>
    public sealed class UIRoot : MonoBehaviour
    {
        [SerializeField, Tooltip("Components implementing IScreenSource, for example AddressableUIRoot. Queried in the order listed.")]
        private Component[] _sources = Array.Empty<Component>();
        [SerializeField, Tooltip("Serve keys registered with UINavigator.Instance when none of the sources above owns them.")]
        private bool _includeSceneScreens = true;

        private readonly List<IScreenSource> _resolved = new List<IScreenSource>();
        private bool _sourcesResolved;
        private bool _subscribed;

        /// <summary>Raised once a screen becomes active. Its opening transition may still be running.</summary>
        public event Action<ScreenId, UIScreen> ScreenOpened;

        /// <summary>The sources in query order. Scene screens, when enabled, come last.</summary>
        public IReadOnlyList<IScreenSource> Sources
        {
            get
            {
                ResolveSources();
                return _resolved;
            }
        }

        private void OnEnable()
        {
            ResolveSources();
            if (_subscribed) return;
            _subscribed = true;
            foreach (var source in _resolved) source.ScreenOpened += OnSourceScreenOpened;
        }

        private void OnDisable()
        {
            if (!_subscribed) return;
            _subscribed = false;
            foreach (var source in _resolved) source.ScreenOpened -= OnSourceScreenOpened;
        }

        /// <summary>Appends a source. It is queried after the ones already registered.</summary>
        public void AddSource(IScreenSource source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            ResolveSources();
            if (_resolved.Contains(source)) return;
            _resolved.Add(source);
            if (_subscribed) source.ScreenOpened += OnSourceScreenOpened;
        }

        public bool RemoveSource(IScreenSource source)
        {
            if (source == null || !_resolved.Remove(source)) return false;
            if (_subscribed) source.ScreenOpened -= OnSourceScreenOpened;
            return true;
        }

        public bool Contains(ScreenId screenId) => TryResolve(screenId, out _);

        public void Open(ScreenId screenId) => Resolve(screenId).Open(screenId);

        public void Close(ScreenId screenId) => Resolve(screenId).Close(screenId);

        public void CloseAll()
        {
            ResolveSources();
            // A close may register or drop sources, so iterate over a snapshot.
            foreach (var source in _resolved.ToArray()) source.CloseAll();
        }

        public bool TryGetScreen(ScreenId screenId, out UIScreen screen)
        {
            screen = null;
            return TryResolve(screenId, out var source) && source.TryGetScreen(screenId, out screen);
        }

        /// <summary>Finds the source that would serve this key.</summary>
        public bool TryResolve(ScreenId screenId, out IScreenSource source)
        {
            source = null;
            if (!screenId.IsValid) return false;
            ResolveSources();
            foreach (var candidate in _resolved)
            {
                if (!candidate.Contains(screenId)) continue;
                source = candidate;
                return true;
            }

            return false;
        }

        private IScreenSource Resolve(ScreenId screenId)
        {
            if (TryResolve(screenId, out var source)) return source;
            throw new ArgumentException($"No screen source owns '{screenId}'.", nameof(screenId));
        }

        private void ResolveSources()
        {
            if (_sourcesResolved) return;
            _sourcesResolved = true;
            if (_sources != null)
            {
                foreach (var component in _sources)
                {
                    if (component == null) continue;
                    if (component is not IScreenSource source)
                    {
                        Debug.LogError(
                            $"'{component.GetType().Name}' does not implement IScreenSource and was skipped.", component);
                        continue;
                    }

                    if (!_resolved.Contains(source)) _resolved.Add(source);
                }
            }

            // Scene screens come last: a source that loads a key on demand should win over a
            // leftover registration for the same key.
            if (_includeSceneScreens && !_resolved.Contains(UINavigator.Instance))
                _resolved.Add(UINavigator.Instance);
        }

        private void OnSourceScreenOpened(ScreenId screenId, UIScreen screen) => ScreenOpened?.Invoke(screenId, screen);
    }
}

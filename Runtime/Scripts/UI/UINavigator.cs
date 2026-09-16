#region Libraries

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

#endregion

namespace SimpleUIScreensSystem
{
    /// <summary>
    /// Registry of screens placed in a scene, addressed by <see cref="ScreenId"/>.
    /// Screens loaded on demand are owned by their own <see cref="IScreenSource"/> instead.
    /// </summary>
    public sealed class UINavigator : IScreenSource
    {
        private sealed class Registration
        {
            public UnityAction Opening;
            public UnityAction Closed;
            public Action CloseCallback;
        }

        private static UINavigator _instance;

        private readonly Dictionary<ScreenId, UIScreen> _screens = new Dictionary<ScreenId, UIScreen>();
        private readonly Dictionary<UIScreen, Registration> _registrations = new Dictionary<UIScreen, Registration>();

        public static UINavigator Instance => _instance ??= new UINavigator();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;

        /// <summary>Raised once a registered screen becomes active; its opening transition may still run.</summary>
        public event Action<ScreenId, UIScreen> ScreenOpened;

        public int OpenedScreensCount
        {
            get
            {
                var count = 0;
                foreach (var pair in _registrations)
                    if (pair.Key != null && pair.Key.IsOpen) count++;
                return count;
            }
        }

        /// <summary>Registers a screen under its inspector ID. Registering the same screen twice is a no-op.</summary>
        public void Add(UIScreen screen)
        {
            if (screen == null) throw new ArgumentNullException(nameof(screen));
            if (!screen.Id.IsValid)
                throw new ArgumentException($"Screen '{screen.name}' needs a nonempty ID to be registered.", nameof(screen));
            if (_screens.TryGetValue(screen.Id, out var registered))
            {
                if (registered == screen) return;
                if (registered != null)
                    throw new InvalidOperationException(
                        $"Screen ID '{screen.Id}' is already registered by '{registered.name}'.");
                Remove(registered);
            }

            var registration = new Registration
            {
                Opening = () => OnScreenOpening(screen),
                Closed = () => OnScreenClosed(screen)
            };
            _screens.Add(screen.Id, screen);
            _registrations.Add(screen, registration);
            screen.OnOpening.AddListener(registration.Opening);
            screen.OnClosed.AddListener(registration.Closed);
        }

        /// <summary>Forgets a screen and drops its subscriptions. Call before destroying a registered screen.</summary>
        public void Remove(UIScreen screen)
        {
            // A destroyed screen compares equal to null but must still be dropped from every map.
            if (ReferenceEquals(screen, null) || !_registrations.TryGetValue(screen, out var registration)) return;
            _registrations.Remove(screen);
            screen.OnOpening.RemoveListener(registration.Opening);
            screen.OnClosed.RemoveListener(registration.Closed);
            _screens.Remove(screen.Id);
        }

        /// <summary>Opens a registered screen. The callback fires once, when this opening is closed.</summary>
        public void Open(ScreenId screenId, Action closeCallback = null)
        {
            var screen = GetScreen(screenId);
            if (screen.IsOpen && !screen.IsClosing) return;
            if (closeCallback != null) _registrations[screen].CloseCallback = closeCallback;
            screen.Open();
        }

        public void Close(ScreenId screenId)
        {
            var screen = GetScreen(screenId);
            if (screen.IsOpen) screen.Close();
        }

        public void CloseAll()
        {
            // A close callback may open or close other screens, so iterate over a snapshot.
            var snapshot = new UIScreen[_registrations.Count];
            _registrations.Keys.CopyTo(snapshot, 0);
            foreach (var screen in snapshot)
                if (screen != null && screen.IsOpen) screen.Close();
        }

        public bool Contains(ScreenId screenId) => TryGetScreen(screenId, out _);

        public bool TryGetScreen(ScreenId screenId, out UIScreen screen)
        {
            screen = null;
            if (!screenId.IsValid || !_screens.TryGetValue(screenId, out var registered)) return false;
            if (registered == null)
            {
                // The screen was destroyed without Remove(); drop the stale entry.
                Remove(registered);
                return false;
            }

            screen = registered;
            return true;
        }

        void IScreenSource.Open(ScreenId screenId) => Open(screenId);

        private UIScreen GetScreen(ScreenId screenId)
        {
            if (!TryGetScreen(screenId, out var screen))
                throw new ArgumentException($"Unknown UI screen '{screenId}'.", nameof(screenId));
            return screen;
        }

        private void OnScreenOpening(UIScreen screen) => ScreenOpened?.Invoke(screen.Id, screen);

        private void OnScreenClosed(UIScreen screen)
        {
            if (!_registrations.TryGetValue(screen, out var registration)) return;
            var callback = registration.CloseCallback;
            registration.CloseCallback = null;
            callback?.Invoke();
        }
    }
}

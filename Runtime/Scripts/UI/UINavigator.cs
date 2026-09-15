#region Libraries

using System;
using System.Collections.Generic;
using UnityEngine.Events;

#endregion

namespace SimpleUIScreensSystem
{
    /// <summary>
    /// Registry of scene-placed screens addressed by <see cref="ScreenId"/>.
    /// Screens loaded through Addressables are owned by AddressableUIRoot instead.
    /// </summary>
    public sealed class UINavigator
    {
        private static UINavigator _instance;

        private readonly Dictionary<ScreenId, UIScreen> _screens = new Dictionary<ScreenId, UIScreen>();
        private readonly Dictionary<UIScreen, UnityAction> _closedListeners = new Dictionary<UIScreen, UnityAction>();
        private readonly Dictionary<UIScreen, Action> _closeCallbacks = new Dictionary<UIScreen, Action>();
        private readonly List<UIScreen> _openedScreens = new List<UIScreen>();

        public static UINavigator Instance => _instance ??= new UINavigator();

        public int OpenedScreensCount => _openedScreens.Count;

        /// <summary>Registers a screen under its inspector ID. Registering the same screen twice is a no-op.</summary>
        public void Add(UIScreen screen)
        {
            if (screen == null) throw new ArgumentNullException(nameof(screen));
            if (!screen.Id.IsValid)
                throw new ArgumentException($"Screen '{screen.name}' needs a nonempty ID to be registered.", nameof(screen));
            if (_screens.TryGetValue(screen.Id, out var registered))
            {
                if (registered == screen) return;
                throw new InvalidOperationException(
                    $"Screen ID '{screen.Id}' is already registered by '{registered.name}'.");
            }

            _screens.Add(screen.Id, screen);
            UnityAction listener = () => OnScreenClosed(screen);
            _closedListeners.Add(screen, listener);
            screen.OnClosed.AddListener(listener);
        }

        /// <summary>Forgets a screen and drops its subscriptions. Call before destroying a registered screen.</summary>
        public void Remove(UIScreen screen)
        {
            // A destroyed screen compares equal to null but must still be dropped from every map.
            if (ReferenceEquals(screen, null) || !_closedListeners.TryGetValue(screen, out var listener)) return;
            _closedListeners.Remove(screen);
            screen.OnClosed.RemoveListener(listener);
            _screens.Remove(screen.Id);
            _openedScreens.Remove(screen);
            _closeCallbacks.Remove(screen);
        }

        /// <summary>Opens a registered screen. The callback fires once, when this opening is closed.</summary>
        public void Open(ScreenId screenId, Action closeCallback = null)
        {
            var screen = GetScreen(screenId);
            var isOpen = _openedScreens.Contains(screen);
            if (isOpen && !screen.IsClosing) return;

            if (!isOpen) _openedScreens.Add(screen);
            if (closeCallback != null) _closeCallbacks[screen] = closeCallback;
            screen.Open();
        }

        public void Close(ScreenId screenId)
        {
            var screen = GetScreen(screenId);
            if (_openedScreens.Contains(screen)) screen.Close();
        }

        /// <summary>Closes every open screen. Animated screens leave the open list when their close finishes.</summary>
        public void CloseAll()
        {
            // A close callback may open or close other screens, so iterate over a snapshot.
            foreach (var screen in _openedScreens.ToArray())
                if (screen != null && _openedScreens.Contains(screen)) screen.Close();
        }

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

        private UIScreen GetScreen(ScreenId screenId)
        {
            if (!TryGetScreen(screenId, out var screen))
                throw new ArgumentException($"Unknown UI screen '{screenId}'.", nameof(screenId));
            return screen;
        }

        private void OnScreenClosed(UIScreen screen)
        {
            _openedScreens.Remove(screen);
            if (!_closeCallbacks.TryGetValue(screen, out var callback)) return;
            _closeCallbacks.Remove(screen);
            callback();
        }
    }
}

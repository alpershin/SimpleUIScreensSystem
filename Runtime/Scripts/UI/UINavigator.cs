#region Libraries

using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace SimpleUIScreensSystem
{
    public class UINavigator
    {
        private static UINavigator _instance;
        
        private List<UIScreen> _screens = new List<UIScreen>();
        private List<UIScreen> _openedScreens = new List<UIScreen>();

        public int OpenedScreensCount => _openedScreens.Count;

        public static UINavigator Instance => _instance ??= new UINavigator();
        
        public void Add(UIScreen screen)
        {
            if (_screens.Contains(screen)) return;
            _screens.Add(screen);
        }

        public void Open(EScreenType type, Action closeCallback = null)
        {
            var screen = _screens.FirstOrDefault(s => s.Type == type);

            if (_openedScreens.Contains(screen)) return;
            if (screen == null) throw new Exception("Screen not found");

            _openedScreens.Add(screen);
            screen.Open();

            screen.OnClosed.AddListener(() =>
            {
                closeCallback?.Invoke();
                _openedScreens.Remove(screen);
            });
        }

        public void Close(EScreenType type)
        {
            if (_openedScreens.All(s => s.Type != type)) return;

            var screen = _openedScreens.FirstOrDefault(s => s.Type == type);
            _openedScreens.Remove(screen);
            if (screen != null) screen.Close();
        }

        public void CloseAll()
        {
            foreach (var screen in _openedScreens)
                screen.Close();

            _openedScreens.Clear();
        }

        public UIScreen GetScreen(EScreenType type)
        {
            return _screens.FirstOrDefault(s => s.Type == type);
        }
    }
}
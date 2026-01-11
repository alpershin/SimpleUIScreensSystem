#region Libraries

using System;
using R3;
using System.Linq;
using System.Collections.Generic;
using Game.Scripts.UI;

#endregion

namespace SimpleUIScreensSystem
{
    public class UINavigator : IDisposable
    {
        private Dictionary<EScreenType, CompositeDisposable> _disposables = new Dictionary<EScreenType, CompositeDisposable>();
        private List<UIScreen> _screens = new List<UIScreen>();
        private List<UIScreen> _openedScreens = new List<UIScreen>();

        public int OpenedScreensCount => _openedScreens.Count;
        
        public void Add(UIScreen screen)
        {
            if (_screens.Contains(screen)) return;
            _screens.Add(screen);
            _disposables.TryAdd(screen.Type, new CompositeDisposable());
        }
        
        public void Open(EScreenType type, Action closeCallback = null)
        {
            var screen = _screens.FirstOrDefault(s => s.Type == type);

            if (_openedScreens.Contains(screen)) return;
            if (screen == null) throw new Exception("Screen not found");

            _disposables[type] = new CompositeDisposable();
            _openedScreens.Add(screen);
            screen.Open();
            
            screen.OnClosed
                .Subscribe(_ =>
                {
                    closeCallback?.Invoke();
                    _openedScreens.Remove(screen);
                    
                    _disposables[type]?.Dispose();
                })
                .AddTo(_disposables[type]);
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
        
        public void Dispose()
        {
            _disposables.ForEach(pair => pair.Value?.Dispose());
        }
    }
}
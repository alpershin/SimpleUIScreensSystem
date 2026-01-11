#region Libraries

using System;
using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

#endregion

namespace SimpleUIScreensSystem
{
    public class UIScreenOpenCloseAnimation : IDisposable
    {
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        
        private CanvasGroup _screenGroup;
        private Transform _modal;

        private float _screenFadeDuration => 0.1f;
        private float _screenWindowScaleDuration => 0.1f;
        private float _screenWindowScaleTarget => 1f;
        private float _screenWindowScaleStart => 0.1f;

        public void Init(CanvasGroup screen, Transform modalWindow = null)
        {
            _screenGroup = screen;
            _modal = modalWindow;
        }
        
        public void FadeIn(Action onFinish = null)
        {
            Show(0f, 1f, _screenWindowScaleStart, _screenWindowScaleTarget, onFinish);
        }

        public void FadeOut(Action onFinish = null)
        {
            Show(1f, 0f, _screenWindowScaleTarget, _screenWindowScaleStart, onFinish);
        }
        
        private void Show(float screenAlphaFrom, float screenAlphaTo, float modalWindowFrom, float modalWindowTo, Action onFinish = null)
        {
            if (_screenGroup == null) return;
            
            Dispose();
            _cancellationTokenSource = new CancellationTokenSource();

            FadeAnimation(screenAlphaFrom, screenAlphaTo, onFinish).Forget();

            if (_modal == null) return;
               
            ScaleAnimation(modalWindowFrom, modalWindowTo).Forget();
        }
        
        private async UniTask FadeAnimation(float from, float to, Action onFinish = null)
        {
            _screenGroup.alpha = from;
            for (float i = 0f; i < 1f; i += Time.deltaTime / _screenFadeDuration)
            {
                _screenGroup.alpha = Easing.easeInCubic(from, to, i);
                await UniTask.Yield(_cancellationTokenSource.Token);
            }

            _screenGroup.alpha = to;
            onFinish?.Invoke();
        }
        
        private async UniTask ScaleAnimation(float from, float to)
        {
            _modal.localScale = Vector3.one * from;
            for (float i = 0f; i < 1f; i += Time.deltaTime / _screenWindowScaleDuration)
            {
                _modal.localScale = Vector3.one * Easing.easeInSine(from, to, i);
                await UniTask.Yield(_cancellationTokenSource.Token);
            }

            _modal.localScale = Vector3.one * to;
        }

        public void Dispose()
        {
            if (_cancellationTokenSource == null) return;

            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
        }
    }
}
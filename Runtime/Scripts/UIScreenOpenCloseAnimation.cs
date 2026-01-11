#region Libraries

using System;
using System.Collections;
using UnityEngine;

#endregion

namespace SimpleUIScreensSystem
{
    public class UIScreenOpenCloseAnimation : IDisposable
    {
        private MonoBehaviour _monoBehaviour;
        private Coroutine _fadeCoroutine;
        private Coroutine _scaleCoroutine;

        private CanvasGroup _screenGroup;
        private Transform _modal;

        private float _screenFadeDuration => 0.1f;
        private float _screenWindowScaleDuration => 0.1f;
        private float _screenWindowScaleTarget => 1f;
        private float _screenWindowScaleStart => 0.1f;

        public void Init(CanvasGroup screen, Transform modalWindow = null, MonoBehaviour monoBehaviour = null)
        {
            _screenGroup = screen;
            _modal = modalWindow;
            _monoBehaviour = monoBehaviour;
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
            if (_screenGroup == null || _monoBehaviour == null) return;

            Dispose();

            _fadeCoroutine = _monoBehaviour.StartCoroutine(FadeAnimation(screenAlphaFrom, screenAlphaTo, onFinish));

            if (_modal == null) return;

            _scaleCoroutine = _monoBehaviour.StartCoroutine(ScaleAnimation(modalWindowFrom, modalWindowTo));
        }

        private IEnumerator FadeAnimation(float from, float to, Action onFinish = null)
        {
            _screenGroup.alpha = from;
            for (float i = 0f; i < 1f; i += Time.deltaTime / _screenFadeDuration)
            {
                _screenGroup.alpha = Easing.easeInCubic(from, to, i);
                yield return null;
            }

            _screenGroup.alpha = to;
            onFinish?.Invoke();
        }

        private IEnumerator ScaleAnimation(float from, float to)
        {
            _modal.localScale = Vector3.one * from;
            for (float i = 0f; i < 1f; i += Time.deltaTime / _screenWindowScaleDuration)
            {
                _modal.localScale = Vector3.one * Easing.easeInSine(from, to, i);
                yield return null;
            }

            _modal.localScale = Vector3.one * to;
        }

        public void Dispose()
        {
            if (_monoBehaviour == null) return;

            if (_fadeCoroutine != null)
            {
                _monoBehaviour.StopCoroutine(_fadeCoroutine);
                _fadeCoroutine = null;
            }

            if (_scaleCoroutine != null)
            {
                _monoBehaviour.StopCoroutine(_scaleCoroutine);
                _scaleCoroutine = null;
            }
        }
    }
}

#region Libraries

using System;
using System.Collections;
using UnityEngine;

#endregion

namespace SimpleUIScreensSystem
{
    /// <summary>Fades a CanvasGroup and scales an optional modal window on the screen's own coroutine.</summary>
    public class UIScreenOpenCloseAnimation : IDisposable
    {
        private MonoBehaviour _host;
        private ScreenTransitionProfile _profile;
        private Coroutine _routine;
        private CanvasGroup _screenGroup;
        private Transform _modal;

        public bool IsReady => _screenGroup != null && _host != null;
        public bool IsRunning => _routine != null;

        /// <summary>Timing and curves. Null selects <see cref="ScreenTransitionProfile.Default"/>.</summary>
        public ScreenTransitionProfile Profile
        {
            get => _profile != null ? _profile : ScreenTransitionProfile.Default;
            set => _profile = value;
        }

        public void Init(CanvasGroup screen, Transform modalWindow = null, MonoBehaviour host = null)
        {
            _screenGroup = screen;
            _modal = modalWindow;
            _host = host;
        }

        public void SetHidden() => Apply(0f, Profile.ModalHiddenScale);
        public void SetVisible() => Apply(1f, 1f);

        public void FadeIn(Action onFinish = null) => Play(1f, 1f, onFinish);
        public void FadeOut(Action onFinish = null) => Play(0f, Profile.ModalHiddenScale, onFinish);

        /// <summary>Stops a running transition where it is. Its onFinish callback is not invoked.</summary>
        public void Dispose()
        {
            if (_routine == null) return;
            if (_host != null) _host.StopCoroutine(_routine);
            _routine = null;
        }

        private void Play(float alphaTo, float scaleTo, Action onFinish)
        {
            Dispose();
            if (!IsReady || !_host.gameObject.activeInHierarchy)
            {
                Apply(alphaTo, scaleTo);
                onFinish?.Invoke();
                return;
            }

            _routine = _host.StartCoroutine(Animate(alphaTo, scaleTo, onFinish));
        }

        private IEnumerator Animate(float alphaTo, float scaleTo, Action onFinish)
        {
            var profile = Profile;
            var alphaFrom = _screenGroup.alpha;
            var scaleFrom = _modal != null ? _modal.localScale.x : scaleTo;
            // The remaining distance sets the duration, so an interrupted transition reverses from where it is.
            var duration = profile.FadeDuration * Mathf.Abs(alphaTo - alphaFrom);
            for (var t = 0f; duration > 0f && t < 1f; t += Time.unscaledDeltaTime / duration)
            {
                _screenGroup.alpha = Mathf.Lerp(alphaFrom, alphaTo, profile.EvaluateFade(t));
                if (_modal != null)
                    _modal.localScale = Vector3.one * Mathf.Lerp(scaleFrom, scaleTo, profile.EvaluateScale(t));
                yield return null;
            }

            Apply(alphaTo, scaleTo);
            _routine = null;
            onFinish?.Invoke();
        }

        private void Apply(float alpha, float scale)
        {
            if (_screenGroup != null) _screenGroup.alpha = alpha;
            if (_modal != null) _modal.localScale = Vector3.one * scale;
        }
    }
}

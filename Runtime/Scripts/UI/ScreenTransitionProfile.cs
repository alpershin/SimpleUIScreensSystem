using UnityEngine;

namespace SimpleUIScreensSystem
{
    /// <summary>Designer-tunable timing and curves for a screen's open and close transition.</summary>
    [CreateAssetMenu(menuName = "Simple UI/Screen Transition Profile")]
    public sealed class ScreenTransitionProfile : ScriptableObject
    {
        private static ScreenTransitionProfile _default;

        [SerializeField, Min(0f), Tooltip("Seconds for a full fade between hidden and visible. Interrupted transitions take proportionally less.")]
        private float _fadeDuration = 0.1f;
        [SerializeField] private AnimationCurve _fadeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField, Range(0f, 1f), Tooltip("Scale of the modal window while hidden.")]
        private float _modalHiddenScale = 0.1f;
        [SerializeField] private AnimationCurve _scaleCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        /// <summary>Values used by screens that enable animation without assigning a profile.</summary>
        public static ScreenTransitionProfile Default
        {
            get
            {
                if (_default == null)
                {
                    _default = CreateInstance<ScreenTransitionProfile>();
                    _default.hideFlags = HideFlags.HideAndDontSave;
                }

                return _default;
            }
        }

        public float FadeDuration => _fadeDuration;
        public float ModalHiddenScale => _modalHiddenScale;

        public float EvaluateFade(float progress) => Evaluate(_fadeCurve, progress);
        public float EvaluateScale(float progress) => Evaluate(_scaleCurve, progress);

        private static float Evaluate(AnimationCurve curve, float progress)
        {
            return curve == null || curve.length == 0 ? progress : Mathf.Clamp01(curve.Evaluate(progress));
        }

        private void OnValidate()
        {
            _fadeDuration = Mathf.Max(0f, _fadeDuration);
            _modalHiddenScale = Mathf.Clamp01(_modalHiddenScale);
        }
    }
}

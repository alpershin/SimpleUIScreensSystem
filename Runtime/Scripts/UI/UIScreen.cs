#region Libraries

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

#endregion

namespace SimpleUIScreensSystem
{
    /// <summary>
    /// A screen built by hand in uGUI. Transitions run on this component, and the state follows the
    /// GameObject's active flag, so activating or deactivating the object from outside stays consistent.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class UIScreen : MonoBehaviour
    {
        [SerializeField, Tooltip("Stable key used by UINavigator for scene screens. Screens created by AddressableUIRoot are keyed by the catalog.")]
        private string _id;
        [SerializeField] private Button[] _closeButton;
        [SerializeField] protected Transform _modalWindow;
        [SerializeField] private bool _withAnimation;
        [SerializeField, Tooltip("Optional. Timing and curves for the transition; the built-in default is used when empty.")]
        private ScreenTransitionProfile _transition;

        private readonly UnityEvent _onOpening = new UnityEvent();
        private readonly UnityEvent _onOpened = new UnityEvent();
        private readonly UnityEvent _onClosing = new UnityEvent();
        private readonly UnityEvent _onClosed = new UnityEvent();
        private readonly UIScreenOpenCloseAnimation _animation = new UIScreenOpenCloseAnimation();
        private bool _initialized;
        private bool _openRequested;

        public ScreenId Id => ScreenId.FromSerialized(_id);
        public ScreenState State { get; private set; }

        /// <summary>The screen just became active; its opening transition may still run.</summary>
        public UnityEvent OnOpening => _onOpening;
        /// <summary>The opening transition has finished and the screen is fully visible.</summary>
        public UnityEvent OnOpened => _onOpened;
        /// <summary>Closing was requested; the closing transition may still run.</summary>
        public UnityEvent OnClosing => _onClosing;
        /// <summary>The screen is inactive again. Also raised when the object is deactivated from outside.</summary>
        public UnityEvent OnClosed => _onClosed;

        public bool IsOpen => State != ScreenState.Hidden;
        public bool IsClosing => State == ScreenState.Closing;

        private bool CanAnimate =>
            _withAnimation && !UIMotion.ReduceMotion && _animation.IsReady && gameObject.activeInHierarchy;

        protected virtual void Awake()
        {
            if (!_initialized) Init();
            if (_closeButton == null) return;
            foreach (var button in _closeButton)
                if (button != null) button.onClick.AddListener(Close);
        }

        private void OnEnable()
        {
            // Open() drives its own transition. Any other activation is an instant open.
            if (_openRequested || State != ScreenState.Hidden) return;
            BeginOpening(false);
        }

        private void OnDisable()
        {
            _animation.Dispose();
            if (State == ScreenState.Hidden) return;
            if (State != ScreenState.Closing)
            {
                State = ScreenState.Closing;
                _onClosing.Invoke();
            }

            State = ScreenState.Hidden;
            _onClosed.Invoke();
        }

        protected virtual void OnDestroy()
        {
            _animation.Dispose();
            if (_closeButton == null) return;
            foreach (var button in _closeButton)
                if (button != null) button.onClick.RemoveListener(Close);
        }

        /// <summary>Binds the transition to this screen. Runs once; Awake calls it if nobody did earlier.</summary>
        public virtual void Init()
        {
            if (_initialized) return;
            _initialized = true;
            _animation.Init(GetComponent<CanvasGroup>(), _modalWindow, this);
            _animation.Profile = _transition;
        }

        public virtual void Open()
        {
            switch (State)
            {
                case ScreenState.Opening:
                case ScreenState.Open:
                    return;
                case ScreenState.Closing:
                    // Reverse the running transition from its current values.
                    BeginOpening(CanAnimate);
                    return;
            }

            _openRequested = true;
            gameObject.SetActive(true);
            _openRequested = false;
            if (!gameObject.activeSelf) return;

            var animated = CanAnimate;
            if (animated) _animation.SetHidden();
            BeginOpening(animated);
        }

        public virtual void Close() => CloseWith(CanAnimate);

        /// <summary>Deactivates immediately, skipping or cutting short the closing transition.</summary>
        public void Hide() => CloseWith(false);

        private void BeginOpening(bool animated)
        {
            State = ScreenState.Opening;
            _onOpening.Invoke();
            // A listener may have closed or deactivated the screen already.
            if (State != ScreenState.Opening) return;
            if (animated)
            {
                _animation.FadeIn(CompleteOpening);
                return;
            }

            _animation.SetVisible();
            CompleteOpening();
        }

        private void CompleteOpening()
        {
            if (State != ScreenState.Opening) return;
            State = ScreenState.Open;
            _onOpened.Invoke();
        }

        private void CloseWith(bool animated)
        {
            switch (State)
            {
                case ScreenState.Closing:
                    if (!animated) Deactivate();
                    return;
                case ScreenState.Hidden:
                    // Active in the scene but OnEnable has not run yet (startup ordering): hide silently.
                    if (gameObject.activeSelf) Deactivate();
                    return;
            }

            State = ScreenState.Closing;
            _onClosing.Invoke();
            if (State != ScreenState.Closing) return;
            if (animated) _animation.FadeOut(Deactivate);
            else Deactivate();
        }

        private void Deactivate() => gameObject.SetActive(false);
    }
}

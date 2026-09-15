#region Libraries

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

#endregion

namespace SimpleUIScreensSystem
{
    [RequireComponent(typeof(CanvasGroup))]
    public class UIScreen : MonoBehaviour
    {
        [SerializeField, Tooltip("Stable key used by UINavigator for scene screens. Screens created by AddressableUIRoot are keyed by the catalog.")]
        private string _id;
        [SerializeField] private Button[] _closeButton;
        [SerializeField] protected Transform _modalWindow;
        [SerializeField] private bool _withAnimation;

        private UnityEvent _onClosed = new UnityEvent();
        private UnityEvent _onOpened = new UnityEvent();

        private UIScreenOpenCloseAnimation _animation = new UIScreenOpenCloseAnimation();

        public ScreenId Id => ScreenId.FromSerialized(_id);

        public UnityEvent OnClosed => _onClosed;
        public UnityEvent OnOpened => _onOpened;

        public bool IsOpen => gameObject.activeSelf;
        public bool IsClosing { get; private set; }

        protected virtual void Awake()
        {
            if (_closeButton is not { Length: > 0 }) return;

            foreach (var button in _closeButton)
            {
                button.onClick.AddListener(Close);
            }
        }

        private void OnEnable()
        {
            _onOpened?.Invoke();
        }

        private void OnDisable()
        {
            IsClosing = false;
            _animation.Dispose();
            _onClosed?.Invoke();
        }

        protected virtual void OnDestroy()
        {
            _animation.Dispose();
            if (_closeButton == null) return;
            foreach (var button in _closeButton)
                if (button != null) button.onClick.RemoveListener(Close);
        }

        public virtual void Init()
        {
            _animation.Init(GetComponent<CanvasGroup>(), _modalWindow, Coroutines.Runner);
        }

        public virtual void Open()
        {
            IsClosing = false;
            if (_animation == null || !_withAnimation)
            {
                gameObject.SetActive(true);
                return;
            }

            gameObject.SetActive(true);
            // An OnOpened listener may immediately request closing or deactivate this object.
            if (IsClosing || !gameObject.activeInHierarchy) return;
            _animation.FadeIn();
        }

        public virtual void Close()
        {
            if (!gameObject.activeSelf) return;
            // Skip the fade when it could not be seen; OnDisable resets IsClosing only if it runs.
            if (_animation == null || !_withAnimation || !gameObject.activeInHierarchy)
            {
                gameObject.SetActive(false);
                return;
            }

            IsClosing = true;
            _animation.FadeOut(() => gameObject.SetActive(false));
        }
    }
}

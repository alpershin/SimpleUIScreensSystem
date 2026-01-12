#region Libraries

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

#endregion

namespace SimpleUIScreensSystem
{
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class UIScreen : MonoBehaviour
    {
        [SerializeField] private EScreenType _type;
        [SerializeField] private Button[] _closeButton;
        [SerializeField] protected Transform _modalWindow;
        [SerializeField] private bool _withAnimation;

        private UnityEvent _onClosed = new UnityEvent();
        private UnityEvent _onOpened = new UnityEvent();

        private UIScreenOpenCloseAnimation _animation = new UIScreenOpenCloseAnimation();

        public EScreenType Type => _type;

        public UnityEvent OnClosed => _onClosed;
        public UnityEvent OnOpened => _onOpened;

        public bool IsOpen => gameObject.activeSelf;

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
            _onClosed?.Invoke();
        }

        public virtual void Init()
        {
            _animation.Init(GetComponent<CanvasGroup>(), _modalWindow, Coroutines.Runner);
        }

        public virtual void Open()
        {
            if (_animation == null || !_withAnimation)
            {
                gameObject.SetActive(true);
                return;
            }

            gameObject.SetActive(true);
            _animation.FadeIn();
        }

        public virtual void Close()
        {
            if (_animation == null || !_withAnimation)
            {
                gameObject.SetActive(false);
                return;
            }

            _animation.FadeOut(() => gameObject.SetActive(false));
        }
    }
}
#region Libraries

using System;
using System.Linq;
using Game.Scripts.UI;
using R3;
using UnityEngine;
using UnityEngine.UI;

#endregion

namespace SimpleUIScreensSystem
{
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class UIScreen : MonoBehaviour, IDisposable
    {
        private readonly CompositeDisposable _lifetimeDisposable = new CompositeDisposable();
        
        [SerializeField] private EScreenType _type;
        [SerializeField] private Button[] _closeButton;
        [SerializeField] protected Transform _modalWindow;
        [SerializeField] private bool _withAnimation;
        
        private ReactiveCommand<Unit> _onClosed = new ReactiveCommand<Unit>();
        private ReactiveCommand<Unit> _onOpen = new ReactiveCommand<Unit>();

        private UIScreenOpenCloseAnimation _animation = new UIScreenOpenCloseAnimation();
        
        public EScreenType Type => _type;
         
        public Observable<Unit> OnClosed => _onClosed;
        public Observable<Unit> OnOpen => _onOpen;
        
        public bool IsOpen => gameObject.activeSelf;

        protected virtual void Awake()
        {
            if (_closeButton is not { Length: > 0 }) return;

            _closeButton.Select(b => b.OnClickAsObservable())
                .Merge()
                .Subscribe(_ => Close())
                .AddTo(_lifetimeDisposable);
        }

        private void OnEnable()
        {
            _onOpen?.Execute(Unit.Default);
        }

        private void OnDisable()
        {
            _onClosed?.Execute(Unit.Default);
        }

        public virtual void Init()
        {
            _animation.Init(GetComponent<CanvasGroup>(), _modalWindow);
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

        public virtual void Dispose()
        {
            _lifetimeDisposable?.Dispose();
            _onClosed?.Dispose();
            _onOpen?.Dispose();
        }
    }
}
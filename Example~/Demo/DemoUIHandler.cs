#region Libraries

using UnityEngine;
using UnityEngine.UI;

#endregion

namespace SimpleUIScreensSystem.Demo
{
    public class DemoUIHandler : MonoBehaviour
    {
        [SerializeField] protected Button _openDemoScreenButton;

        private UINavigator _navigator => UINavigator.Instance;
        
        private void Awake()
        {
            var demoScreen = _navigator.GetScreen(EScreenType.DemoScreen);
            
            demoScreen.OnOpened.AddListener(() => _openDemoScreenButton.gameObject.SetActive(false));
            demoScreen.OnClosed.AddListener(() => _openDemoScreenButton.gameObject.SetActive(true));
            
            _openDemoScreenButton.onClick.AddListener(() => _navigator.Open(EScreenType.DemoScreen));
        }
    }
}
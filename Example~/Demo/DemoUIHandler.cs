#region Libraries

using System;
using UnityEngine;
using UnityEngine.UI;

#endregion

namespace SimpleUIScreensSystem.Demo
{
    public class DemoUIHandler : MonoBehaviour
    {
        [SerializeField] protected Button _openDemoScreenButton;

        private UIScreen _demoScreen;

        private static UINavigator Navigator => UINavigator.Instance;

        private void Awake()
        {
            if (!Navigator.TryGetScreen(DemoScreens.Demo, out _demoScreen))
                throw new InvalidOperationException("The demo scene needs a UIScreen with ID 'demo'.");

            _demoScreen.OnOpened.AddListener(HideOpenButton);
            _demoScreen.OnClosed.AddListener(ShowOpenButton);
            _openDemoScreenButton.onClick.AddListener(OpenDemoScreen);
        }

        private void OnDestroy()
        {
            if (_demoScreen != null)
            {
                _demoScreen.OnOpened.RemoveListener(HideOpenButton);
                _demoScreen.OnClosed.RemoveListener(ShowOpenButton);
            }

            if (_openDemoScreenButton != null) _openDemoScreenButton.onClick.RemoveListener(OpenDemoScreen);
        }

        private void OpenDemoScreen() => Navigator.Open(DemoScreens.Demo);
        private void HideOpenButton() => _openDemoScreenButton.gameObject.SetActive(false);
        private void ShowOpenButton() => _openDemoScreenButton.gameObject.SetActive(true);
    }
}

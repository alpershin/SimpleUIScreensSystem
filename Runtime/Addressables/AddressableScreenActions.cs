using System;
using UnityEngine;

namespace SimpleUIScreensSystem.AddressableUI
{
    /// <summary>Inspector bridge for UnityEvents. Selects the same stable key as generated C# code.</summary>
    public sealed class AddressableScreenActions : MonoBehaviour
    {
        [SerializeField] private AddressableUIRoot _navigator;
        [SerializeField, HideInInspector] private string _screenId;

        public void Open()
        {
            RequireNavigator();
            _navigator.Open(new ScreenId(_screenId));
        }

        public void Close()
        {
            RequireNavigator();
            _navigator.Close(new ScreenId(_screenId));
        }

        private void RequireNavigator()
        {
            if (_navigator == null)
                throw new InvalidOperationException("Assign a navigator and screen in AddressableScreenActions.");
        }
    }
}

using System;

namespace SimpleUIScreensSystem.AddressableUI.Tests
{
    public sealed class ScreenFixture : UIScreen
    {
        public static Action<ScreenFixture> Initialized;

        public override void Init()
        {
            base.Init();
            Initialized?.Invoke(this);
        }
    }
}

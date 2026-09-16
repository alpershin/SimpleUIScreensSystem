#region Libraries

using System;

#endregion

namespace SimpleUIScreensSystem
{
    /// <summary>
    /// A place screens come from: already present in a scene, or loaded on demand.
    /// <see cref="UIRoot"/> routes a request to the first source that owns the key, so calling
    /// code never has to know where a screen lives.
    /// </summary>
    public interface IScreenSource
    {
        /// <summary>Raised once the screen is active. Its opening transition may still be running.</summary>
        event Action<ScreenId, UIScreen> ScreenOpened;

        /// <summary>True when this source can serve the key. Unknown and invalid keys return false.</summary>
        bool Contains(ScreenId screenId);

        /// <summary>Opens the screen, loading it first if this source loads on demand.</summary>
        void Open(ScreenId screenId);

        /// <summary>Closes the screen and cancels a pending open.</summary>
        void Close(ScreenId screenId);

        /// <summary>Closes every screen this source has open.</summary>
        void CloseAll();

        /// <summary>Returns the live instance, if this source has one right now.</summary>
        bool TryGetScreen(ScreenId screenId, out UIScreen screen);
    }
}

namespace SimpleUIScreensSystem
{
    /// <summary>Lifecycle of a <see cref="UIScreen"/>. Opening and Closing last for the duration of the transition.</summary>
    public enum ScreenState
    {
        Hidden,
        Opening,
        Open,
        Closing
    }
}

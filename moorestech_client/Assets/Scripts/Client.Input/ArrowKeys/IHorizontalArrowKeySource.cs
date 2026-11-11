using System;

namespace Client.Input.ArrowKeys
{
    public enum HorizontalArrowKey
    {
        Left,
        Right,
    }

    // ←/→ の押下を流す口（画面のテストは偽物に差し替える）
    // Port that publishes ← / → presses; screen tests swap in a fake
    public interface IHorizontalArrowKeySource
    {
        IObservable<HorizontalArrowKey> OnArrowPressed { get; }
    }
}

using System.Collections.Generic;
using Client.Game.InGame.Control;

namespace Client.Tests.UIState.Fakes
{
    public class FakePlayerCameraInteractionApplier : IPlayerCameraInteractionApplier
    {
        public readonly List<string> Calls = new();

        public void SetInteractionMode(CameraInteractionMode mode, CursorCenterWarp warp)
        {
            // 中央寄せはモード適用のあとに起きるため呼び出し順どおり2件に分けて記録する
            // The centering happens after the mode is applied, so it is recorded as a second entry in call order
            Calls.Add($"Mode:{mode}");
            if (warp == CursorCenterWarp.ToScreenCenter) Calls.Add("Warp");
        }
    }
}

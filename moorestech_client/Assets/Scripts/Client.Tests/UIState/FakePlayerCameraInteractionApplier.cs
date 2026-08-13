using System.Collections.Generic;
using Client.Game.InGame.Control;

namespace Client.Tests.UIState
{
    public class FakePlayerCameraInteractionApplier : IPlayerCameraInteractionApplier
    {
        public readonly List<string> Calls = new();

        public void SetInteractionMode(CameraInteractionMode mode)
        {
            Calls.Add($"Mode:{mode}");
        }

        public void WarpCursorToScreenCenter()
        {
            Calls.Add("Warp");
        }
    }
}

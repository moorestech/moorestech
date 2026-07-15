using System.Collections.Generic;
using Client.Game.InGame.Control;

namespace Client.Tests.UIState
{
    public class FakePlayerCameraInteractionApplier : IPlayerCameraInteractionApplier
    {
        public readonly List<string> Calls = new();

        public void SetCursorVisible(bool visible)
        {
            Calls.Add($"Cursor:{visible}");
        }

        public void SetCameraRotatable(bool rotatable)
        {
            Calls.Add($"Rotatable:{rotatable}");
        }
    }
}

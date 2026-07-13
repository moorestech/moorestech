using System.Collections.Generic;
using Client.Game.InGame.Control.ViewMode;

namespace Client.Tests.ViewMode
{
    /// <summary>
    ///     副作用呼び出しを記録するテスト用Applier
    ///     Test applier recording side-effect calls
    /// </summary>
    public class FakePlayerViewApplier : IPlayerViewApplier
    {
        public readonly List<string> Calls = new();
        public bool? LastFirstPersonCamera { get; private set; }
        public bool? LastCursorVisible { get; private set; }
        public bool? LastCrosshairVisible { get; private set; }
        public bool? LastCameraRotatable { get; private set; }

        public void SetFirstPersonCamera(bool enabled)
        {
            Calls.Add($"Fps:{enabled}");
            LastFirstPersonCamera = enabled;
        }

        public void SetCursorVisible(bool visible)
        {
            Calls.Add($"Cursor:{visible}");
            LastCursorVisible = visible;
        }

        public void SetCrosshairVisible(bool visible)
        {
            Calls.Add($"Crosshair:{visible}");
            LastCrosshairVisible = visible;
        }

        public void SetCameraRotatable(bool rotatable)
        {
            Calls.Add($"Rotatable:{rotatable}");
            LastCameraRotatable = rotatable;
        }
    }
}

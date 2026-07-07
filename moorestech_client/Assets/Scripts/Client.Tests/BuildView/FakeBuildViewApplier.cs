using System.Collections.Generic;
using Client.Game.InGame.Control;
using Client.Game.InGame.Control.BuildView;
using UnityEngine;

namespace Client.Tests.BuildView
{
    /// <summary>
    ///     副作用呼び出しを記録するテスト用Applier
    ///     Test applier recording side-effect calls
    /// </summary>
    public class FakeBuildViewApplier : IBuildViewApplier
    {
        public readonly List<string> Calls = new();
        public TweenCameraInfo CapturedCamera { get; } = new(Vector3.zero, 5f);
        public TweenCameraInfo LastRestoredCamera { get; private set; }
        public bool? LastFirstPersonCamera { get; private set; }
        public bool? LastCursorVisible { get; private set; }
        public bool? LastCrosshairVisible { get; private set; }

        public TweenCameraInfo CaptureCurrentCamera()
        {
            Calls.Add("Capture");
            return CapturedCamera;
        }

        public void ApplyTopDownCamera()
        {
            Calls.Add("TopDown");
        }

        public void RestoreCamera(TweenCameraInfo saved)
        {
            Calls.Add("Restore");
            LastRestoredCamera = saved;
        }

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
        }
    }
}

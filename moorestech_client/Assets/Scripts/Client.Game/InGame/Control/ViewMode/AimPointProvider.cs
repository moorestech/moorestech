using Client.Input;
using UnityEngine;

namespace Client.Game.InGame.Control.ViewMode
{
    public enum AimPointMode
    {
        Mouse,
        ScreenCenter,
    }

    /// <summary>
    ///     設置・削除・操作用の照準座標を方式別に返す
    ///     Provides aim points for placement, deletion, and interaction by aim mode
    /// </summary>
    public static class AimPointProvider
    {
        public static AimPointMode CurrentMode { get; private set; } = AimPointMode.Mouse;

        public static void SetMode(AimPointMode mode)
        {
            CurrentMode = mode;
        }

        public static Vector3 GetAimScreenPoint()
        {
            if (CurrentMode == AimPointMode.ScreenCenter) return new Vector3(Screen.width / 2f, Screen.height / 2f, 0f);

            return HybridInput.GetMousePosition();
        }
    }
}

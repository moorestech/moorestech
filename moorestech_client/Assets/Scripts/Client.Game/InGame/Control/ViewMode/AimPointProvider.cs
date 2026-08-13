using Client.Input;
using UnityEngine;

namespace Client.Game.InGame.Control.ViewMode
{
    public enum AimPointMode
    {
        Mouse,
        ScreenCenter,
    }

    // 三人称時の照準ソース。基盤側では判断せず具体側がプッシュする
    // Third-person aim source; the concrete side pushes it, not this base
    public enum ThirdPersonAimSource
    {
        ScreenCenter,
        Cursor,
    }

    /// <summary>
    ///     視点モードと三人称照準ソースの2入力から照準座標を方式別に返す
    ///     Provides aim points from view mode and third-person aim source inputs
    /// </summary>
    public static class AimPointProvider
    {
        private static PlayerViewMode _viewMode = PlayerViewMode.ThirdPerson;
        private static ThirdPersonAimSource _thirdPersonAimSource = ThirdPersonAimSource.ScreenCenter;

        public static void SetViewMode(PlayerViewMode viewMode)
        {
            _viewMode = viewMode;
        }

        public static void SetThirdPersonAimSource(ThirdPersonAimSource aimSource)
        {
            _thirdPersonAimSource = aimSource;
        }

        public static AimPointMode GetCurrentMode()
        {
            // 一人称は照準ソースに関わらず常に画面中央
            // First person always aims at screen center regardless of aim source
            if (_viewMode == PlayerViewMode.FirstPerson) return AimPointMode.ScreenCenter;

            return _thirdPersonAimSource == ThirdPersonAimSource.Cursor
                ? AimPointMode.Mouse
                : AimPointMode.ScreenCenter;
        }

        public static Vector3 GetAimScreenPoint()
        {
            if (GetCurrentMode() == AimPointMode.ScreenCenter) return new Vector3(Screen.width / 2f, Screen.height / 2f, 0f);

            return HybridInput.GetMousePosition();
        }
    }
}

using Client.Input;
using UnityEngine;

namespace Client.Game.InGame.Control.ViewMode
{
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

        /// <summary>
        ///     2入力から実際に適用される照準ソースを返す（観測用）
        ///     Returns the aim source actually applied from the two inputs (for observation)
        /// </summary>
        public static ThirdPersonAimSource GetEffectiveAimSource()
        {
            // 一人称は照準ソースに関わらず常に画面中央
            // First person always aims at screen center regardless of aim source
            if (_viewMode == PlayerViewMode.FirstPerson) return ThirdPersonAimSource.ScreenCenter;

            return _thirdPersonAimSource;
        }

        public static Vector3 GetAimScreenPoint()
        {
            if (GetEffectiveAimSource() == ThirdPersonAimSource.ScreenCenter) return ScreenCenter.GetPosition();

            return HybridInput.GetMousePosition();
        }
    }
}

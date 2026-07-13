using Client.Input;
using UnityEngine;

namespace Client.Game.InGame.Control.ViewMode
{
    /// <summary>
    ///     設置・削除・インタラクトの照準スクリーン座標を視点モードに応じて提供する
    ///     Provides the aim screen point for placement, deletion, and interaction based on the view mode
    /// </summary>
    public static class AimPointProvider
    {
        public static PlayerViewMode CurrentMode { get; private set; } = PlayerViewMode.ThirdPerson;

        public static void SetMode(PlayerViewMode mode)
        {
            CurrentMode = mode;
        }

        public static Vector3 GetAimScreenPoint()
        {
            // FPSはカーソルロックのため画面中央を照準にする
            // FPS locks the cursor, so aim at the screen center
            if (CurrentMode == PlayerViewMode.FirstPerson) return new Vector3(Screen.width / 2f, Screen.height / 2f, 0f);

            return HybridInput.GetMousePosition();
        }
    }
}

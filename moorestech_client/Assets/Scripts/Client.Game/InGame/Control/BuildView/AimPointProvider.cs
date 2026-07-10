using Client.Input;
using UnityEngine;

namespace Client.Game.InGame.Control.BuildView
{
    /// <summary>
    ///     設置・削除の照準スクリーン座標を視点モードに応じて提供する
    ///     Provides the aim screen point for placement and deletion based on the view mode
    /// </summary>
    public static class AimPointProvider
    {
        public static BuildViewMode CurrentMode { get; private set; } = BuildViewMode.TopDown;

        public static void SetMode(BuildViewMode mode)
        {
            CurrentMode = mode;
        }

        public static Vector3 GetAimScreenPoint()
        {
            // FPSはカーソルロックのため画面中央を照準にする
            // FPS locks the cursor, so aim at the screen center
            if (CurrentMode == BuildViewMode.FirstPerson) return new Vector3(Screen.width / 2f, Screen.height / 2f, 0f);

            return HybridInput.GetMousePosition();
        }
    }
}

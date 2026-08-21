using UnityEngine;

namespace Client.Input
{
    /// <summary>
    ///     画面中央の唯一の定義。Altワープ先と非Alt時の照準点が同じ点を指す
    ///     Single definition of the screen center: the Alt warp target and the non-Alt aim point share it
    /// </summary>
    public static class ScreenCenter
    {
        // 画面サイズに対する中央の比率
        // Center ratio against the screen size
        private const float CenterRatio = 0.5f;

        public static Vector2 GetPosition()
        {
            return new Vector2(Screen.width * CenterRatio, Screen.height * CenterRatio);
        }
    }
}

using System;
using UnityEngine;

namespace Client.Ui.Foundation.Input.OsCursor
{
    /// <summary>
    /// 本番の OS カーソル。手の絵を Resources から読み UnityEngine.Cursor.SetCursor で当てる
    /// Production OS cursor; loads the hand pictures from Resources and applies them with UnityEngine.Cursor.SetCursor
    /// </summary>
    public sealed class UnityUiOsCursor : IUiOsCursor
    {
        // 32×32 の絵の中心を指す点にする（Web の cursor: grab と同じく手の中央）
        // The hotspot is the center of the 32×32 picture (the middle of the hand, like the Web cursor: grab)
        private static readonly Vector2 HandHotspot = new(16f, 16f);

        private readonly Texture2D _grab;
        private readonly Texture2D _grabbing;

        public UnityUiOsCursor()
        {
            _grab = LoadRequired("UiOsCursor/grab");
            _grabbing = LoadRequired("UiOsCursor/grabbing");
        }

        public void SetShape(UiOsCursorShape shape)
        {
            switch (shape)
            {
                case UiOsCursorShape.Grab:
                    UnityEngine.Cursor.SetCursor(_grab, HandHotspot, CursorMode.Auto);
                    return;
                case UiOsCursorShape.Grabbing:
                    UnityEngine.Cursor.SetCursor(_grabbing, HandHotspot, CursorMode.Auto);
                    return;
                default:
                    UnityEngine.Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                    return;
            }
        }

        private static Texture2D LoadRequired(string resourcePath)
        {
            // 絵が欠けたら最初に使う時点で止める（矢印のまま黙って続けない）
            // Stop on first use when a picture is missing (never silently stay on the arrow)
            var texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null) throw new InvalidOperationException($"[UiOsCursor] missing cursor texture Resources/{resourcePath}.png");
            return texture;
        }
    }
}

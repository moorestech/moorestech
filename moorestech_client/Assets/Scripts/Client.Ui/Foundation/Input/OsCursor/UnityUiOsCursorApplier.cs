using System;
using UnityEngine;

namespace Client.Ui.Foundation.Input.OsCursor
{
    /// <summary>
    /// 手の絵を Resources から読み UnityEngine.Cursor.SetCursor で当てる。Unity には OS 標準の指・手が無いので自作の 32×32 の絵を使う
    /// Loads the hand pictures from Resources and applies them with UnityEngine.Cursor.SetCursor; Unity has no OS pointer or hand cursors, so self-made 32×32 pictures are used
    /// </summary>
    public sealed class UnityUiOsCursorApplier : IUiOsCursorApplier
    {
        private readonly CursorPicture _pointer;
        private readonly CursorPicture _grab;
        private readonly CursorPicture _grabbing;

        public UnityUiOsCursorApplier()
        {
            // ホットスポットは絵の大きさに対する割合で持つ（絵を描き直しても同じ所を指す）。指は人差し指の先（32×32 の (10, 2)）、手は中央
            // Hotspots are fractions of the picture size so redrawn pictures point at the same place; the pointer at the index fingertip ((10, 2) of 32×32), hands at the center
            _pointer = Load("UiOsCursor/pointer", new Vector2(10f / 32f, 2f / 32f));
            _grab = Load("UiOsCursor/grab", new Vector2(0.5f, 0.5f));
            _grabbing = Load("UiOsCursor/grabbing", new Vector2(0.5f, 0.5f));

            #region Internal

            CursorPicture Load(string resourcePath, Vector2 hotspotFraction)
            {
                // 絵が欠けたら最初に使う時点で止める（矢印のまま黙って続けない）
                // Stop on first use when a picture is missing (never silently stay on the arrow)
                var texture = Resources.Load<Texture2D>(resourcePath);
                if (texture == null) throw new InvalidOperationException($"[UiOsCursor] missing cursor texture Resources/{resourcePath}.png");
                return new CursorPicture(texture, new Vector2(texture.width * hotspotFraction.x, texture.height * hotspotFraction.y));
            }

            #endregion
        }

        public void Apply(UiOsCursorShape shape)
        {
            switch (shape)
            {
                case UiOsCursorShape.Pointer:
                    UnityEngine.Cursor.SetCursor(_pointer.Texture, _pointer.Hotspot, CursorMode.Auto);
                    return;
                case UiOsCursorShape.Grab:
                    UnityEngine.Cursor.SetCursor(_grab.Texture, _grab.Hotspot, CursorMode.Auto);
                    return;
                case UiOsCursorShape.Grabbing:
                    UnityEngine.Cursor.SetCursor(_grabbing.Texture, _grabbing.Hotspot, CursorMode.Auto);
                    return;
                case UiOsCursorShape.Default:
                    UnityEngine.Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                    return;
                default:
                    // 未対応の形は黙って矢印にせず止める
                    // Stop on an unhandled shape instead of silently falling back to the arrow
                    throw new ArgumentOutOfRangeException(nameof(shape), shape, "[UiOsCursor] unhandled cursor shape");
            }
        }

        private readonly struct CursorPicture
        {
            public readonly Texture2D Texture;
            public readonly Vector2 Hotspot;

            public CursorPicture(Texture2D texture, Vector2 hotspot)
            {
                Texture = texture;
                Hotspot = hotspot;
            }
        }
    }
}

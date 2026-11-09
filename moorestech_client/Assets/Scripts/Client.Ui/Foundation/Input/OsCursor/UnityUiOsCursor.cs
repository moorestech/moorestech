using System;
using System.Collections.Generic;
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

        private readonly List<ShapeRequest> _requests = new();
        private readonly Texture2D _grab;
        private readonly Texture2D _grabbing;

        public UnityUiOsCursor()
        {
            _grab = LoadRequired("UiOsCursor/grab");
            _grabbing = LoadRequired("UiOsCursor/grabbing");

            #region Internal

            Texture2D LoadRequired(string resourcePath)
            {
                // 絵が欠けたら最初に使う時点で止める（矢印のまま黙って続けない）
                // Stop on first use when a picture is missing (never silently stay on the arrow)
                var texture = Resources.Load<Texture2D>(resourcePath);
                if (texture == null) throw new InvalidOperationException($"[UiOsCursor] missing cursor texture Resources/{resourcePath}.png");
                return texture;
            }

            #endregion
        }

        public IDisposable Request(UiOsCursorShape shape)
        {
            // 要求を積み、一番上の形を当てる
            // Push the request and apply the top shape
            var request = new ShapeRequest(this, shape);
            _requests.Add(request);
            ApplyTop();
            return request;
        }

        private void Withdraw(ShapeRequest request)
        {
            // 外した後の一番上へ戻る（空なら既定）
            // Fall back to the new top after withdrawing (the default when empty)
            _requests.Remove(request);
            ApplyTop();
        }

        private void ApplyTop()
        {
            var shape = 0 < _requests.Count ? _requests[_requests.Count - 1].Shape : UiOsCursorShape.Default;
            switch (shape)
            {
                case UiOsCursorShape.Grab:
                    UnityEngine.Cursor.SetCursor(_grab, HandHotspot, CursorMode.Auto);
                    return;
                case UiOsCursorShape.Grabbing:
                    UnityEngine.Cursor.SetCursor(_grabbing, HandHotspot, CursorMode.Auto);
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

        private sealed class ShapeRequest : IDisposable
        {
            public UiOsCursorShape Shape { get; }
            private readonly UnityUiOsCursor _owner;
            private bool _disposed;

            public ShapeRequest(UnityUiOsCursor owner, UiOsCursorShape shape)
            {
                _owner = owner;
                Shape = shape;
            }

            public void Dispose()
            {
                // silent-ok: 二重の Dispose は 2 回目を無視する / 正常系
                if (_disposed) return;
                _disposed = true;
                _owner.Withdraw(this);
            }
        }
    }
}

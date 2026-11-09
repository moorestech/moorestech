using System;
using System.Collections.Generic;

namespace Client.Ui.Foundation.Input.OsCursor
{
    /// <summary>
    /// OS カーソルの要求を積み、一番上の層・その層で最後の要求の形を当て先へ流す（要求が無ければ既定）。形が変わった時だけ当てる
    /// Stacks OS cursor requests and sends the shape of the latest request in the highest layer to the applier (the default when none); applies only on change
    /// </summary>
    public sealed class UiOsCursorRequestStack : IUiOsCursor
    {
        private readonly IUiOsCursorApplier _applier;
        private readonly List<ShapeRequest> _requests = new();
        private UiOsCursorShape _applied = UiOsCursorShape.Default;

        public UiOsCursorRequestStack(IUiOsCursorApplier applier)
        {
            _applier = applier;
        }

        public IDisposable Request(UiOsCursorShape shape, UiOsCursorLayer layer)
        {
            var request = new ShapeRequest(this, shape, layer);
            _requests.Add(request);
            ApplyTopIfChanged();
            return request;
        }

        private void Withdraw(ShapeRequest request)
        {
            _requests.Remove(request);
            ApplyTopIfChanged();
        }

        private void ApplyTopIfChanged()
        {
            var top = FindTopShape();
            // silent-ok: 同じ形の当て直しは不要 / 当たっている形は変わらないので知らせる相手はいない
            if (top == _applied) return;
            _applied = top;
            _applier.Apply(top);
        }

        private UiOsCursorShape FindTopShape()
        {
            // 高い層が勝ち、同じ層は後から積んだ方が勝つ（>= で後ろを選ぶ）
            // A higher layer wins and within a layer the later request wins (>= picks the later one)
            ShapeRequest top = null;
            foreach (var request in _requests)
            {
                if (top == null || top.Layer <= request.Layer) top = request;
            }
            return top?.Shape ?? UiOsCursorShape.Default;
        }

        private sealed class ShapeRequest : IDisposable
        {
            public UiOsCursorShape Shape { get; }
            public UiOsCursorLayer Layer { get; }
            private readonly UiOsCursorRequestStack _owner;
            private bool _disposed;

            public ShapeRequest(UiOsCursorRequestStack owner, UiOsCursorShape shape, UiOsCursorLayer layer)
            {
                _owner = owner;
                Shape = shape;
                Layer = layer;
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

using System;
using System.Collections.Generic;
using Client.Ui.Foundation.Input.OsCursor;

namespace Client.Tests.Ui.Components.NodeGraph
{
    /// <summary>
    /// 要求を積み、当たっている形（一番上）が変わるたびに記録する偽物（本番の UnityUiOsCursor と同じ後入れ先出し）
    /// Fake that stacks requests and records the effective (top) shape on every change, like the production UnityUiOsCursor
    /// </summary>
    public sealed class FakeUiOsCursor : IUiOsCursor
    {
        private readonly List<ShapeRequest> _requests = new();

        public List<UiOsCursorShape> AppliedShapes { get; } = new();
        public int LiveRequestCount => _requests.Count;
        public UiOsCursorShape Current => 0 < _requests.Count ? _requests[_requests.Count - 1].Shape : UiOsCursorShape.Default;

        public IDisposable Request(UiOsCursorShape shape)
        {
            var before = Current;
            var request = new ShapeRequest(this, shape);
            _requests.Add(request);
            RecordIfChanged(before);
            return request;
        }

        private void Withdraw(ShapeRequest request)
        {
            var before = Current;
            _requests.Remove(request);
            RecordIfChanged(before);
        }

        private void RecordIfChanged(UiOsCursorShape before)
        {
            if (Current != before) AppliedShapes.Add(Current);
        }

        private sealed class ShapeRequest : IDisposable
        {
            public UiOsCursorShape Shape { get; }
            private readonly FakeUiOsCursor _owner;

            public ShapeRequest(FakeUiOsCursor owner, UiOsCursorShape shape)
            {
                _owner = owner;
                Shape = shape;
            }

            public void Dispose()
            {
                _owner.Withdraw(this);
            }
        }
    }
}

using System.Collections.Generic;
using Client.Ui.Foundation.Input.OsCursor;

namespace Client.Tests.Ui.Components.NodeGraph
{
    /// <summary>
    /// 当てられた OS カーソルの形を順に記録する偽物
    /// Fake that records the applied OS cursor shapes in order
    /// </summary>
    public sealed class FakeUiOsCursor : IUiOsCursor
    {
        public List<UiOsCursorShape> AppliedShapes { get; } = new();

        public void SetShape(UiOsCursorShape shape)
        {
            AppliedShapes.Add(shape);
        }
    }
}

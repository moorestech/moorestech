using System.Collections.Generic;
using Client.Ui.Foundation.Input.OsCursor;

namespace Client.Tests.Ui.TestDoubles
{
    /// <summary>
    /// 本物の UiOsCursorRequestStack の当て先に置き、当てられた形を順に記録する偽物（調停は本物が行う）
    /// Fake applier placed under the real UiOsCursorRequestStack that records applied shapes in order (the real stack arbitrates)
    /// </summary>
    public sealed class FakeUiOsCursorApplier : IUiOsCursorApplier
    {
        public List<UiOsCursorShape> AppliedShapes { get; } = new();
        public UiOsCursorShape Current => 0 < AppliedShapes.Count ? AppliedShapes[AppliedShapes.Count - 1] : UiOsCursorShape.Default;

        public void Apply(UiOsCursorShape shape)
        {
            AppliedShapes.Add(shape);
        }
    }
}

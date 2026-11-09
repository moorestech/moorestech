using Client.Tests.Ui.TestDoubles;
using Client.Ui.Foundation.Input.OsCursor;
using NUnit.Framework;

namespace Client.Tests.Ui.Input.OsCursor
{
    /// <summary>
    /// プロセスで 1 本のスタックを捨てた後の挙動（生き残りの Request が落ちない・古い世代の Dispose が新しい記録を変えない）を固定する
    /// Pins behavior after the process stack is dropped (a surviving Request does not throw; a stale-generation Dispose does not change the new record)
    /// </summary>
    public class ProcessUiOsCursorTest
    {
        [TearDown]
        public void ResetProcessStack()
        {
            ProcessUiOsCursor.UseApplierForNextProcessStack(null);
            ProcessUiOsCursor.DropProcessStack();
        }

        [Test]
        public void スタックを捨てた後に生き残ったインスタンスがRequestしても落ちず形が当たる()
        {
            ProcessUiOsCursor.DropProcessStack();
            var cursor = new ProcessUiOsCursor();
            ProcessUiOsCursor.DropProcessStack();
            var applier = new FakeUiOsCursorApplier();
            ProcessUiOsCursor.UseApplierForNextProcessStack(applier);

            using var request = cursor.Request(UiOsCursorShape.Pointer, UiOsCursorLayer.Element);

            Assert.AreEqual(UiOsCursorShape.Pointer, applier.Current);
        }

        [Test]
        public void 古い世代の要求をDisposeしても新しい記録は変わらない()
        {
            ProcessUiOsCursor.DropProcessStack();
            ProcessUiOsCursor.UseApplierForNextProcessStack(new FakeUiOsCursorApplier());
            var cursor = new ProcessUiOsCursor();
            var stale = cursor.Request(UiOsCursorShape.Grab, UiOsCursorLayer.Surface);
            ProcessUiOsCursor.DropProcessStack();
            var newApplier = new FakeUiOsCursorApplier();
            ProcessUiOsCursor.UseApplierForNextProcessStack(newApplier);
            using var fresh = cursor.Request(UiOsCursorShape.Pointer, UiOsCursorLayer.Element);
            var appliedBefore = newApplier.AppliedShapes.Count;

            stale.Dispose();

            Assert.AreEqual(appliedBefore, newApplier.AppliedShapes.Count);
            Assert.AreEqual(UiOsCursorShape.Pointer, newApplier.Current);
        }
    }
}

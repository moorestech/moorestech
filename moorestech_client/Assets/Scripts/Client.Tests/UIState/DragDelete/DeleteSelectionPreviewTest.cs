using Client.Game.InGame.BlockSystem.PlaceSystem.Undo;
using Client.Game.InGame.Train.RailGraph;
using Client.Game.InGame.UI.UIState.State.DragDelete;
using Client.Tests.BuildUndo;
using Client.Tests.UIState.Fakes;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.UIState.DragDelete
{
    public class DeleteSelectionPreviewTest
    {
        [Test]
        public void 破棄済みレールの旧ホバーがあっても別対象を箱へ追加できる()
        {
            var selection = new DeleteSelection(new BuildOperationHistory(), new FakeRemovalRestoreSender());
            var oldGo = new GameObject("DestroyedRailHover");
            oldGo.AddComponent<RailObjectIdCarrier>().SetRailObjectId(1);
            var chain = oldGo.AddComponent<BezierRailChain>();
            chain.SetUseGpuDeform(true);
            var hovered = oldGo.AddComponent<DeleteTargetRail>();
            hovered.SetParentBezierRailChain(chain);

            // 実コンポーネントを破棄し、前フレームのホバー参照を残す
            // Destroy the actual component while retaining the previous frame's hover
            selection.SetHoverPreview(hovered);
            Object.DestroyImmediate(oldGo);
            var selected = new FakeDeleteTarget { Removable = true };
            LogAssert.Expect(LogType.Warning, "[DragDelete] target was destroyed before hover");

            // 例外なく追加され、破棄済み参照が解放される
            // Addition succeeds without exceptions and clears the destroyed reference
            var added = 0;
            Assert.DoesNotThrow(() => added = selection.AddBoxTargets(new[] { selected }));
            Assert.AreEqual(1, added);
            Assert.AreEqual(1, selection.Count);
            Assert.IsNull(selection.HoverPreview);
            selection.CancelSelection();
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void 同じキーの別ラッパーを選んでも旧ホバー要求が残らない(bool box, bool commit)
        {
            var selection = new DeleteSelection(new BuildOperationHistory(), new FakeRemovalRestoreSender());
            var key = new object();
            var hovered = new FakeDeleteTarget { Key = key, Removable = true };
            var selected = new FakeDeleteTarget { Key = key, Removable = true };

            // 箱となぞりの両方で実際の要求者を引き継ぐ
            // Transfer the actual requester for both box and stroke selection
            selection.SetHoverPreview(hovered);
            if (box) selection.AddBoxTargets(new[] { selected });
            else
            {
                selection.BeginStroke();
                selection.TryAddTarget(selected);
            }
            Assert.AreEqual(1, hovered.ResetCount);
            Assert.IsNull(selection.HoverPreview);
            selection.SetHoverPreview(null);
            if (commit) selection.CommitDelete();
            else selection.CancelSelection();

            Assert.AreEqual(hovered.SetPreviewCount, hovered.ResetCount);
            Assert.AreEqual(selected.SetPreviewCount, selected.ResetCount);
            Assert.AreEqual(0, selection.Count);
        }
    }
}

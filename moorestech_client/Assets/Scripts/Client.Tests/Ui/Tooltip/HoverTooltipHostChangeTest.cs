using Client.Localization;
using Client.Tests.Ui.Components.Text;
using Client.Tests.Ui.TestHost;
using Client.Ui.Foundation.Tooltip;
using Client.Ui.State.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Tooltip
{
    /// <summary>
    /// 静止中の変化への追従を確かめる（L1）
    /// Verifies following changes while the pointer rests (L1)
    /// </summary>
    public class HoverTooltipHostChangeTest : UiTestHost
    {
        private HoverTooltipTestStage _stage;

        [SetUp]
        public void MountStage()
        {
            Localize.Initialize();
            _stage = new HoverTooltipTestStage(rootVisualElement);
            simulate.FrameUpdate();
        }

        [Test]
        public void 押した場所で捕捉されたら移動を待たずに引っ込める()
        {
            // UiPressGesture と同じく押下の受け手が捕捉する。押したまま動かさない
            // The press handler captures, as UiPressGesture does; the pointer stays still while held
            var wood = _stage.Wood;
            wood.RegisterCallback<PointerDownEvent>(evt => wood.CapturePointer(evt.pointerId));
            Hover(wood.worldBound.center);
            Assert.IsTrue(_stage.IsShown());

            simulate.MouseDown(wood.worldBound.center, MouseButton.LeftMouse);
            simulate.FrameUpdate();
            Assert.IsFalse(_stage.IsShown(), "hidden on capture without a move");
            wood.ReleasePointer(PointerId.mousePointerId);
            simulate.MouseUp(wood.worldBound.center, MouseButton.LeftMouse);
        }

        [Test]
        public void ホバー中に印の文が変われば移動なしで差し替え印が外れれば消す()
        {
            Hover(_stage.Wood.worldBound.center);
            HoverTooltipMark.Mark(_stage.Wood, UiText.FromVerbatim("Oak"));
            simulate.FrameUpdate();
            Assert.AreEqual(TooltipFaceTest.LineHeightTag + "Oak", _stage.Face.Q<Label>().text, "text swapped without a move");
            Assert.IsTrue(_stage.IsShown());

            // スロットの最後の 1 個が減って印が外れた（C3-a の ItemSlot が Unmark する）
            // The last item in the slot ran out and the mark was removed (C3-a ItemSlot unmarks)
            HoverTooltipMark.Unmark(_stage.Wood);
            simulate.FrameUpdate();
            Assert.IsFalse(_stage.IsShown(), "hidden without a move");
        }

        [Test]
        public void 静止したまま印が外れて付け直されると移動なしで再び出る()
        {
            // スロットの最後の 1 個が減って Unmark、同じスロットへ品が入って Mark（Web の ItemSlot は disabled を切り替えるだけ）
            // The last item runs out (Unmark), then an item enters the same slot (Mark); the Web ItemSlot only toggles disabled
            Hover(_stage.Wood.worldBound.center);
            HoverTooltipMark.Unmark(_stage.Wood);
            simulate.FrameUpdate();
            Assert.IsFalse(_stage.IsShown(), "hidden after unmark");
            HoverTooltipMark.Mark(_stage.Wood, UiText.FromVerbatim("Wood"));
            simulate.FrameUpdate();
            Assert.IsTrue(_stage.IsShown(), "shown again without a move");
            Assert.AreEqual(TooltipFaceTest.LineHeightTag + "Wood", _stage.Face.Q<Label>().text);

            // スクロールの引っ込めは印の付け直しでは解けない（座標の実変化でだけ開き直す）
            // A scroll retraction is not cleared by re-marking (only a real coordinate change reopens)
            _stage.List.scrollOffset = new Vector2(0f, 1f);
            _stage.List.scrollOffset = Vector2.zero;
            simulate.FrameUpdate();
            HoverTooltipMark.Unmark(_stage.Wood);
            HoverTooltipMark.Mark(_stage.Wood, UiText.FromVerbatim("Wood"));
            simulate.FrameUpdate();
            Assert.IsFalse(_stage.IsShown(), "scroll retraction kept across re-marking");
        }

        [Test]
        public void ポインタが先に乗った空のセルに初めて印が付くと移動なしで出る()
        {
            var emptyCell = _stage.List.Q("item-0");
            Hover(emptyCell.worldBound.center);
            Assert.IsFalse(_stage.IsShown(), "no tooltip over an empty cell");

            HoverTooltipMark.Mark(emptyCell, UiText.FromVerbatim("Stone"));
            simulate.FrameUpdate();
            Assert.IsTrue(_stage.IsShown(), "shown on the first mark without a move");
            Assert.AreEqual(TooltipFaceTest.LineHeightTag + "Stone", _stage.Face.Q<Label>().text);
        }

        [Test]
        public void 空のセルをホバー中に祖先がスクロールすると移動なしで付いた印は出さず実移動で出す()
        {
            // 印の無い間のスクロールも引っ込めに数える（Web は disabled 中もホバーの状態で祖先のスクロールを購読する）
            // A scroll while unmarked still counts as a retraction (the Web subscribes to ancestor scrolls by hover state even while disabled)
            var emptyCell = _stage.List.Q("item-0");
            var point = emptyCell.worldBound.center;
            Hover(point);
            _stage.List.scrollOffset = new Vector2(0f, 1f);
            _stage.List.scrollOffset = Vector2.zero;
            simulate.FrameUpdate();
            HoverTooltipMark.Mark(emptyCell, UiText.FromVerbatim("Stone"));
            simulate.FrameUpdate();
            Assert.IsFalse(_stage.IsShown(), "scroll retraction kept for a mark added without a move");

            // 座標が実際に変わる移動で開き直す
            // A real coordinate change reopens it
            Hover(point + new Vector2(5f, 5f));
            Assert.IsTrue(_stage.IsShown(), "reopened by a real move");
            Assert.AreEqual(TooltipFaceTest.LineHeightTag + "Stone", _stage.Face.Q<Label>().text);
        }

        [Test]
        public void ホバー中の要素が木から外れたら消す()
        {
            Hover(_stage.Wood.worldBound.center);
            Assert.IsTrue(_stage.IsShown());

            _stage.List.Remove(_stage.Wood);
            simulate.FrameUpdate();
            Assert.IsFalse(_stage.IsShown());
        }

        [Test]
        public void 印の親を残して下の子だけが外れたら消す()
        {
            var child = new VisualElement { name = "wood-child" };
            child.style.width = 20;
            child.style.height = 20;
            _stage.Wood.Add(child);
            simulate.FrameUpdate();
            Hover(child.worldBound.center);
            Assert.IsTrue(_stage.IsShown(), "shown over the marked parent via its child");

            child.RemoveFromHierarchy();
            simulate.FrameUpdate();
            Assert.IsFalse(_stage.IsShown(), "hidden when only the pointed child detached");
        }

        private void Hover(Vector2 point)
        {
            simulate.MouseMove(point);
            simulate.FrameUpdate();
        }
    }
}

using Client.Localization;
using Client.Tests.Ui.Components.Text;
using Client.Tests.Ui.TestHost;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Tooltip
{
    /// <summary>
    /// ホバー宿主の出し入れを確かめる（L1）
    /// Verifies hover host show and hide (L1)
    /// </summary>
    public class HoverTooltipHostTest : UiTestHost
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
        public void 印の付いた要素を含むScrollViewのスクロールで引っ込める()
        {
            Hover(_stage.Wood.worldBound.center);
            Assert.IsTrue(_stage.IsShown());
            Assert.AreEqual(TooltipFaceTest.LineHeightTag + "Wood", _stage.Face.Q<Label>().text);

            _stage.List.scrollOffset = new Vector2(0f, 120f);
            simulate.FrameUpdate();
            Assert.IsFalse(_stage.IsShown());
        }

        [Test]
        public void 無関係なパネルのスクロールでは引っ込めない()
        {
            Hover(_stage.Wood.worldBound.center);
            _stage.OtherPanel.scrollOffset = new Vector2(0f, 120f);
            simulate.FrameUpdate();
            Assert.IsTrue(_stage.IsShown());
        }

        [Test]
        public void 引っ込めた後はポインタの座標が実際に変わった移動でだけ開き直す()
        {
            var point = _stage.Wood.worldBound.center;
            Hover(point);
            _stage.List.scrollOffset = new Vector2(0f, 1f);
            _stage.List.scrollOffset = Vector2.zero;
            simulate.FrameUpdate();
            Assert.IsFalse(_stage.IsShown());

            // 座標が据え置きの移動（Web でスクロール中にブラウザが出す mousemove）では開き直さない
            // A move at the same coordinates (the browser's stationary mousemove during a scroll on the Web) does not reopen
            Hover(point);
            Assert.IsFalse(_stage.IsShown());
            Hover(point + new Vector2(5f, 5f));
            Assert.IsTrue(_stage.IsShown());
        }

        [Test]
        public void ポインタの捕捉中は出さず印の無い場所では消える()
        {
            var wood = _stage.Wood.worldBound.center;
            Hover(wood);
            _stage.Plain.CapturePointer(PointerId.mousePointerId);
            Hover(wood + new Vector2(2f, 0f));
            Assert.IsFalse(_stage.IsShown(), "hidden while captured");

            _stage.Plain.ReleasePointer(PointerId.mousePointerId);
            Hover(wood + new Vector2(4f, 0f));
            Assert.IsTrue(_stage.IsShown(), "back after release");
            Hover(_stage.Plain.worldBound.center);
            Assert.IsFalse(_stage.IsShown(), "hidden over an unmarked area");
        }

        [Test]
        public void 対象との間隔は配られた倍率で実画面の5pxになる()
        {
            // 倍率 1.5 では実画面 5px がパネルの 5/1.5px になる。置き直しは顔の寸法が決まった次のフレーム
            // At scale 1.5 the 5 real-screen px are 5/1.5 panel px; the face is re-placed on the frame after its size settles
            _stage.StageScale.Value = 1.5f;
            Hover(_stage.Wood.worldBound.center);
            simulate.FrameUpdate();
            Assert.IsTrue(_stage.IsShown());

            // 一覧の先頭のセルは上に入らなければ下へ反転するので、開いている側の間隔を見る
            // The first cell may flip below when there is no room above, so measure the gap on the open side
            // テストのパネルは倍率 1 で、レイアウトは整数のパネル px に丸められる（実測 top 51.333 → layout 51）。丸め前の置き場所（style.top）で測る
            // The test panel has scale 1 and layout snaps to whole panel px (measured top 51.333 -> layout 51), so measure the placement before snapping (style.top)
            var placedTop = _stage.Anchor.style.top.value.value;
            var gapAbove = _stage.Wood.worldBound.yMin - (placedTop + _stage.Anchor.layout.height);
            var gapBelow = placedTop - _stage.Wood.worldBound.yMax;
            Assert.AreEqual(5f / 1.5f, Mathf.Max(gapAbove, gapBelow), 0.05f, "offset in panel px at stage scale 1.5");
        }

        private void Hover(Vector2 point)
        {
            simulate.MouseMove(point);
            simulate.FrameUpdate();
        }
    }
}

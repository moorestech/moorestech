using System.Collections.Generic;
using Client.Tests.Ui.TestHost;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Traits
{
    /// <summary>
    /// ポインタの性質を実測（L1）
    /// Measures pointer traits (L1)
    /// </summary>
    public class UiPointerTraitTest : UiTestHost
    {
        private readonly List<string> _entered = new();
        private VisualElement _row;
        private VisualElement _left;
        private VisualElement _middle;
        private VisualElement _right;

        [SetUp]
        public void MountThreeCells()
        {
            // 3 マスを並べ進入を記録する
            // Lay three cells in a row and record entries
            _entered.Clear();
            _row = new VisualElement();
            _row.style.flexDirection = FlexDirection.Row;
            _left = Cell("trait-left");
            _middle = Cell("trait-middle");
            _right = Cell("trait-right");
            _row.Add(_left);
            _row.Add(_middle);
            _row.Add(_right);
            rootVisualElement.Add(_row);
            simulate.FrameUpdate();
        }

        [Test]
        public void 一回の移動で飛び越えた要素には進入が来ず行き先にだけ来る()
        {
            simulate.MouseMove(_left.worldBound.center);
            _entered.Clear();
            simulate.MouseMove(_right.worldBound.center);
            simulate.FrameUpdate();
            CollectionAssert.AreEqual(new[] { "trait-right" }, _entered);
        }

        [Test]
        public void 捕捉中も下の要素へ進入が来る()
        {
            simulate.MouseDown(_left.worldBound.center, MouseButton.LeftMouse);
            _left.CapturePointer(PointerId.mousePointerId);
            simulate.FrameUpdate();
            _entered.Clear();
            simulate.MouseMove(_right.worldBound.center);
            simulate.FrameUpdate();
            // 仮説: 捕捉しても進入は下の要素へ届く（PoC の罠）。宿主は進入でなく捕捉の有無で出し入れする
            // Hypothesis: entries still reach the element underneath during capture (the PoC trap); the host decides by capture, not entry
            CollectionAssert.Contains(_entered, "trait-right");
            _left.ReleasePointer(PointerId.mousePointerId);
            simulate.MouseUp(_right.worldBound.center, MouseButton.LeftMouse);
        }

        [Test]
        public void 押下の受け手が捕捉すると祖先のTrickleDownに捕捉の事象が届く()
        {
            // 受け口は根でなく行の箱にする（根の登録はテスト間の Clear で外れない）
            // Listen on the row box, not the root (root registrations survive the per-test Clear)
            var captured = new List<string>();
            _row.RegisterCallback<PointerCaptureEvent>(evt => captured.Add(((VisualElement)evt.target).name), TrickleDown.TrickleDown);
            _left.RegisterCallback<PointerDownEvent>(evt => _left.CapturePointer(evt.pointerId));
            simulate.MouseDown(_left.worldBound.center, MouseButton.LeftMouse);
            simulate.FrameUpdate();
            // 仮説: 捕捉の事象は捕捉した要素へ配られ、祖先の TrickleDown を通る。宿主は移動を待たずに捕捉の始まりで引っ込められる
            // Hypothesis: the capture event is dispatched to the capturer through the ancestors' TrickleDown; the host can retract on capture start without a move
            CollectionAssert.AreEqual(new[] { "trait-left" }, captured);
            _left.ReleasePointer(PointerId.mousePointerId);
            simulate.MouseUp(_left.worldBound.center, MouseButton.LeftMouse);
        }

        private VisualElement Cell(string cellName)
        {
            var cell = new VisualElement { name = cellName };
            cell.style.width = 100;
            cell.style.height = 100;
            cell.RegisterCallback<PointerEnterEvent>(_ => _entered.Add(cellName));
            return cell;
        }
    }
}

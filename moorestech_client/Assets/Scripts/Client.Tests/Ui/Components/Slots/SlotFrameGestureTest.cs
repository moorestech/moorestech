using System.Collections.Generic;
using System.Linq;
using Client.Tests.Ui.TestHost;
using Client.Ui.Components.Slots;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Components.Slots
{
    /// <summary>
    /// SlotFrame の状態のクラス・中身の置き場・身振りを確かめる（L1）
    /// Verifies SlotFrame state classes, content placement and gestures (L1)
    /// </summary>
    public class SlotFrameGestureTest : UiTestHost
    {
        private SlotFrame _frame;
        private readonly List<SlotGesture> _gestures = new();

        [SetUp]
        public void AddFrame()
        {
            _gestures.Clear();
            _frame = new SlotFrame("slot-frame");
            _frame.style.position = Position.Absolute;
            _frame.style.left = 100;
            _frame.style.top = 100;
            _frame.OnSlotGestured.Subscribe(_gestures.Add);
            rootVisualElement.Add(_frame);
            simulate.FrameUpdate();
        }

        [TearDown]
        public void NoUnexpectedLogs()
        {
            LogAssert.NoUnexpectedReceived();
        }

        /// <web-test file="src/shared/ui/SlotFrame/index.test.ts">data属性と子要素を同じdivへ設定する</web-test>
        [Test]
        public void 状態のクラスと子要素を同じ枠へ付ける()
        {
            var child = new Label("icon");
            _frame.Add(child);
            _frame.SetState(new SlotFrameState(true, true, true, true, true));

            Assert.AreEqual("slot-frame", _frame.name);
            foreach (var state in new[] { "selected", "filled", "catalog", "insufficient", "ghost" }) Assert.IsTrue(_frame.ClassListContains($"slot-frame--{state}"), state);
            Assert.AreSame(_frame.contentContainer, child.hierarchy.parent);
            Assert.IsTrue(_frame.Contains(child));

            _frame.SetState(new SlotFrameState(false, false, false, false, false));
            Assert.IsFalse(_frame.ClassListContains("slot-frame--selected"));
        }

        /// <web-test file="src/shared/ui/SlotFrame/index.test.ts">左右押下とダブルクリックを既存契約どおり振り分ける</web-test>
        [Test]
        public void 左右押下とダブルクリックを振り分ける()
        {
            simulate.Click(_frame, MouseButton.LeftMouse, EventModifiers.Shift);
            simulate.Click(_frame, MouseButton.RightMouse);
            simulate.DoubleClick(_frame);

            var presses = _gestures.Where(g => g.Kind is not (SlotGestureKind.HoverEnter or SlotGestureKind.HoverLeave or SlotGestureKind.LeftEnter or SlotGestureKind.RightEnter)).ToList();
            Assert.AreEqual(SlotGestureKind.LeftDown, presses[0].Kind);
            Assert.IsTrue(presses[0].Shift);
            Assert.AreEqual(1, presses.Count(g => g.Kind == SlotGestureKind.RightDown));
            Assert.AreEqual(1, presses.Count(g => g.Kind == SlotGestureKind.DoubleClick));
        }

        [Test]
        public void ホバーの出入りを通知する()
        {
            var outside = new Vector2(20, 20);
            var inside = _frame.worldBound.center;
            simulate.MouseMove(outside, inside);
            simulate.MouseMove(inside, outside);

            CollectionAssert.AreEqual(new[] { SlotGestureKind.HoverEnter, SlotGestureKind.HoverLeave }, _gestures.Select(g => g.Kind));
        }

        [Test]
        public void 左を押したまま右を押すと右の押下を出す()
        {
            // PanelSimulator が追加押下を PointerMoveEvent で運ぶかは実測（Step 7 の分岐 3）
            // Whether PanelSimulator carries an extra press as a PointerMoveEvent is measured (Step 7 branch 3)
            var inside = _frame.worldBound.center;
            simulate.MouseDown(inside, MouseButton.LeftMouse);
            _gestures.Clear();
            simulate.MouseDown(inside, MouseButton.RightMouse);

            CollectionAssert.AreEqual(new[] { SlotGestureKind.RightDown }, _gestures.Select(g => g.Kind));
        }
    }
}

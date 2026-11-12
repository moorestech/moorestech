using System.Text.RegularExpressions;
using Client.Tests.Ui.TestHost;
using Client.Ui.Foundation.DragDrop;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.DragDrop
{
    /// <summary>
    /// ドラッグの源と落とし先を確かめる（L1）
    /// Verifies drag sources and drop targets (L1)
    /// </summary>
    public class UiDragDropTest : UiTestHost
    {
        private VisualElement _source;
        private VisualElement _row;
        private VisualElement _slot;
        private VisualElement _slotIcon;
        private VisualElement _outside;
        private RecordingDragPayload _payload;

        [SetUp]
        public void MountSourceAndTargets()
        {
            // 上に源、下に入れ子の落とし先を置く
            // Put the source on top, a nested target below
            _source = Box("drag-source", 60, 60);
            _row = Box("drop-row", 300, 80);
            _row.style.flexDirection = FlexDirection.Row;
            _row.style.paddingLeft = 100;
            _slot = Box("drop-slot", 60, 60);
            _slotIcon = Box("drop-slot-icon", 40, 40);
            _slotIcon.pickingMode = PickingMode.Ignore;
            _slot.Add(_slotIcon);
            _row.Add(_slot);
            _outside = Box("drop-outside", 100, 100);
            rootVisualElement.Add(_source);
            rootVisualElement.Add(_row);
            rootVisualElement.Add(_outside);

            _payload = new RecordingDragPayload();
            UiDragDrop.MarkSource(_source, _payload);
            UiDragDrop.MarkTarget(_row, new NamedDropTarget("row"));
            UiDragDrop.MarkTarget(_slot, new NamedDropTarget("slot"));
            simulate.FrameUpdate();
        }

        [Test]
        public void 閾値未満で離すとタップになる()
        {
            Press(_source.worldBound.center, _source.worldBound.center + new Vector2(3f, 0f));
            CollectionAssert.AreEqual(new[] { "tap" }, _payload.Calls);
        }

        [Test]
        public void 落とし先の中の絵の上で離すとその落とし先を渡す()
        {
            Press(_source.worldBound.center, _slotIcon.worldBound.center);
            CollectionAssert.AreEqual(new[] { "drop:slot" }, _payload.Calls);
        }

        [Test]
        public void 入れ子では最寄りの印が勝ち枠の外の列の隙間は列の落とし先になる()
        {
            Press(_source.worldBound.center, new Vector2(_row.worldBound.xMin + 20f, _row.worldBound.center.y));
            CollectionAssert.AreEqual(new[] { "drop:row" }, _payload.Calls);
        }

        [Test]
        public void 印の無い場所で離すと枠外を渡す()
        {
            Press(_source.worldBound.center, _outside.worldBound.center);
            CollectionAssert.AreEqual(new[] { "outside" }, _payload.Calls);
        }

        [Test]
        public void 右ボタンでは何も起こさず源を差し替えると新しい値へ渡す()
        {
            simulate.MouseDown(_source.worldBound.center, MouseButton.RightMouse);
            simulate.MouseUp(_slot.worldBound.center, MouseButton.RightMouse);
            CollectionAssert.IsEmpty(_payload.Calls);

            var replaced = new RecordingDragPayload();
            UiDragDrop.MarkSource(_source, replaced);
            Press(_source.worldBound.center, _slot.worldBound.center);
            CollectionAssert.IsEmpty(_payload.Calls);
            CollectionAssert.AreEqual(new[] { "drop:slot" }, replaced.Calls);
        }

        [Test]
        public void 源と違う型の落とし先は飛ばして同じ型の祖先へ落とす()
        {
            // 別の型の印は同型の祖先へ落ちる
            // Another-type mark falls to same-type ancestor
            var otherSlot = AddSlotMarkedWith(new OtherKindDropTarget());

            Press(_source.worldBound.center, otherSlot.worldBound.center);
            CollectionAssert.AreEqual(new[] { "drop:row" }, _payload.Calls);
        }

        [Test]
        public void 受け口の派生型で付けた印は飛ばしてエラーログで知らせる()
        {
            // 型引数を省くと派生型の表に入るので、受け口の型では見つからない
            // Omitting the type argument files the mark under the subtype, so the receiver type cannot find it
            var derivedSlot = AddSlotMarkedWith(new DerivedNamedDropTarget("slot"));
            LogAssert.Expect(LogType.Error, new Regex(@"\[UiDragDrop\] .* is marked as DerivedNamedDropTarget"));

            Press(_source.worldBound.center, derivedSlot.worldBound.center);
            CollectionAssert.AreEqual(new[] { "drop:row" }, _payload.Calls);
        }

        private VisualElement AddSlotMarkedWith<THandler>(THandler handler) where THandler : class
        {
            var extra = Box("drop-extra", 60, 60);
            _row.Add(extra);
            UiDragDrop.MarkTarget(extra, handler);
            simulate.FrameUpdate();
            return extra;
        }

        private void Press(Vector2 from, Vector2 to)
        {
            simulate.MouseDown(from, MouseButton.LeftMouse);
            simulate.MouseMove(Vector2.Lerp(from, to, 0.5f));
            simulate.MouseMove(to);
            simulate.MouseUp(to, MouseButton.LeftMouse);
            simulate.FrameUpdate();
        }

        private static VisualElement Box(string boxName, float width, float height)
        {
            var box = new VisualElement { name = boxName };
            box.style.width = width;
            box.style.height = height;
            box.style.flexShrink = 0;
            return box;
        }
    }
}

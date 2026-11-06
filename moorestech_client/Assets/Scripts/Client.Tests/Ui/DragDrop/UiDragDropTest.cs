using Client.Tests.Ui.TestHost;
using Client.Ui.Foundation.DragDrop;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.DragDrop
{
    /// <summary>
    /// ドラッグの源と落とし先を実パネルで確かめる（L1。Web features/hotbar/useHotbarDragSource の一般化。ホットバーの解決は S3-hotbar-modal）
    /// Verifies drag sources and drop targets on a real panel (L1; generalises Web useHotbarDragSource; hotbar resolution is S3-hotbar-modal)
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
            // 上段に源、下段に「列（隙間）＞枠＞絵」の入れ子の落とし先、右下に印の無い場所を置く
            // Put the source on top, a nested target "row (gap) > slot > icon" below, and an unmarked area at the bottom right
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

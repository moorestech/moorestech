using System;
using System.Collections.Generic;
using Client.Tests.Ui.Content;
using Client.Tests.Ui.Screens.BuildMenu.Support;
using Client.Ui.Content;
using Client.Ui.Foundation.DragDrop;
using Client.Ui.Screens.BuildMenu.Parts;
using Client.Ui.State.BuildMenu.Entries;
using Client.Ui.State.Hotbar.Logic;
using Client.Ui.State.PlacementTarget;
using Game.PlacementTarget;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BuildMenu.Views
{
    /// <summary>
    /// エントリのマスの押下・ホバー・ドラッグの振り分け（L1）
    /// Routing of an entry slot's presses, hover and drags (L1)
    /// </summary>
    public class BuildMenuDragSourceTest : ScreenTestHost
    {
        private GameContentPresentation _content;
        private RecordingBuildMenuEntryHandler _handler;

        [SetUp]
        public void SetUpContent()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _content = new GameContentPresentation(new FakeContentTextureSource(Texture2D.whiteTexture, Texture2D.whiteTexture, Texture2D.whiteTexture));
            _handler = new RecordingBuildMenuEntryHandler();
        }

        [Test]
        public void 左のタップは選択を要求する()
        {
            var id = Guid.NewGuid();
            var slot = MountSlot(Block(id));

            // 閾値未満で離す（UiDragDropTest のタップと同じ手順）
            // Release under the threshold (same steps as the UiDragDropTest tap)
            var center = slot.Frame.worldBound.center;
            Press(center, center + new Vector2(3f, 0f));

            CollectionAssert.AreEqual(new[] { id }, _handler.Selected);
            CollectionAssert.IsEmpty(_handler.DeleteRequested);
        }

        [Test]
        public void BPの右押下だけ削除を要求する()
        {
            var blueprintId = Guid.NewGuid();
            var blockId = Guid.NewGuid();
            var blueprint = MountSlot(new BuildMenuEntryViewModel(blueprintId, PlacementTargetKind.Blueprint, BuildMenuSamples.Blueprint, BuildMenuSamples.Sub, "スターター拠点", Array.Empty<BuildMenuMaterialRow>(), false, PlacementTargetNameOnlyIcon.Instance, BuildMenuSetPlacementNone.Instance));
            var block = MountSlot(Block(blockId));

            simulate.Click(blueprint.Frame, MouseButton.RightMouse);
            simulate.Click(block.Frame, MouseButton.RightMouse);

            CollectionAssert.AreEqual(new[] { blueprintId }, _handler.DeleteRequested);
            CollectionAssert.IsEmpty(_handler.Selected);
        }

        [Test]
        public void ホバーで詳細の対象を知らせる()
        {
            var id = Guid.NewGuid();
            var slot = MountSlot(Block(id));

            // 外から中へ動かす（SlotFrameGestureTest のホバーと同じ流し方）
            // Move from outside into the slot (same as the SlotFrameGestureTest hover)
            var outside = new Vector2(1000f, 600f);
            simulate.MouseMove(outside, slot.Frame.worldBound.center);

            CollectionAssert.AreEqual(new[] { id }, _handler.Hovered);
        }

        [Test]
        public void ホットバーの受け口へ落とすと源を1回渡す()
        {
            var id = Guid.NewGuid();
            var receiver = new RecordingHotbarReceiver();

            new BuildMenuEntryDragPayload(id, _handler).OnDropped(receiver);

            Assert.AreEqual(1, receiver.Received.Count);
            Assert.AreEqual(id, ((BuildMenuEntryDragSource)receiver.Received[0]).TargetId);
            CollectionAssert.IsEmpty(_handler.Selected);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void 受け口でない落とし先と断られた落としは何もしない()
        {
            var slot = MountSlot(Block(Guid.NewGuid()));

            // マスの横に別の型の印を付けた箱を置く。源の型（IHotbarDropReceiver）と違うので Foundation が飛ばす
            // Put a box marked with another type beside the slot; Foundation skips it since it differs from the source type (IHotbarDropReceiver)
            var box = new VisualElement { name = "plain-target" };
            box.style.position = Position.Absolute;
            box.style.left = 400;
            box.style.top = 300;
            box.style.width = 100;
            box.style.height = 100;
            rootVisualElement.Add(box);
            UiDragDrop.MarkTarget(box, new PlainTarget());
            simulate.FrameUpdate();

            Press(slot.Frame.worldBound.center, box.worldBound.center);

            CollectionAssert.IsEmpty(_handler.Selected);
            CollectionAssert.IsEmpty(_handler.DeleteRequested);
            LogAssert.NoUnexpectedReceived();
        }

        private BuildMenuEntryViewModel Block(Guid id)
        {
            return BuildMenuSamples.Block(id, BuildMenuSamples.Logistics, "木のチェスト", Array.Empty<BuildMenuMaterialRow>(), false, BuildMenuSetPlacementNone.Instance);
        }

        private BuildMenuEntrySlot MountSlot(BuildMenuEntryViewModel entry)
        {
            var slot = new BuildMenuEntrySlot(entry, _content, BuildMenuSamples.Icons(_content), _handler);
            Mount(slot.Frame);
            return slot;
        }

        private void Press(Vector2 from, Vector2 to)
        {
            simulate.MouseDown(from, MouseButton.LeftMouse);
            simulate.MouseMove(Vector2.Lerp(from, to, 0.5f));
            simulate.MouseMove(to);
            simulate.MouseUp(to, MouseButton.LeftMouse);
            simulate.FrameUpdate();
        }

        private sealed class RecordingHotbarReceiver : IHotbarDropReceiver
        {
            public List<HotbarDragSource> Received { get; } = new();

            public void Receive(HotbarDragSource source)
            {
                Received.Add(source);
            }
        }

        private sealed class PlainTarget
        {
        }
    }
}

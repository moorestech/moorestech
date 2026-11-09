using System.Linq;
using System.Text.RegularExpressions;
using Client.Tests.Ui.Content;
using Client.Ui.Components.Slots;
using Client.Ui.Content;
using Client.Ui.Screens.BlockInventory.Parts;
using Client.Ui.State.Items;
using Client.Ui.State.Toast;
using Core.Master;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.BlockInventory.Parts
{
    /// <summary>
    /// ブロックの枠の身振りが枠番号つきで IBlockSlotCommands の正しい口へ渡ることを確かめる（L1）
    /// Verifies block-slot gestures reach the right IBlockSlotCommands entry with the slot index (L1)
    /// </summary>
    public class BlockItemGridGestureTest : BlockInventoryScreenTestHost
    {
        private static readonly Texture2D Texture = new(4, 4) { hideFlags = HideFlags.DontSave };
        private RecordingBlockSlotCommands _commands;
        private BlockItemGrid _grid;

        [SetUp]
        public void AddGrid()
        {
            // 並ぶ枠を 0 と 2 にし、並び順と枠番号が食い違っても捕捉した番号で渡ることを見る
            // Lay out slots 0 and 2 so the captured index is checked even when it differs from the layout position
            _commands = new RecordingBlockSlotCommands();
            var content = new GameContentPresentation(new FakeContentTextureSource(Texture, Texture, Texture));
            _grid = new BlockItemGrid("chest-grid", _commands, content, new UiCommandResultToaster(new GameUiToastFeed()));
            rootVisualElement.Add(_grid);
            var wood = Wood();
            var slots = new[] { new SlotItem(wood, 3), new SlotItem(wood, 4), new SlotItem(wood, 5) };
            _grid.Render(slots, new[] { 0, 2 });
            simulate.FrameUpdate();

            // 実ゲームと同じく押す前にポインタを舞台へ入れる（入る前の最初の押下は枠へ届かないことを実測した）
            // Move the pointer into the stage before pressing, as in the game (measured: the very first press before any move never reaches a frame)
            simulate.MouseMove(new Vector2(600, 400), Slot(2).worldBound.center);
        }

        [Test]
        public void 左押下はShiftの有無と枠番号つきでLeftDownへ渡る()
        {
            simulate.Click(Slot(2), MouseButton.LeftMouse, EventModifiers.Shift);
            simulate.Click(Slot(0), MouseButton.LeftMouse);
            CollectionAssert.AreEqual(new[] { "LeftDown:2:True", "LeftDown:0:False" }, _commands.Issued);
        }

        [Test]
        public void 右押下は枠番号つきでRightDownへ渡る()
        {
            simulate.Click(Slot(2), MouseButton.RightMouse);
            CollectionAssert.AreEqual(new[] { "RightDown:2" }, _commands.Issued);
        }

        [Test]
        public void ダブルクリックは枠番号つきでDoubleClickへ渡る()
        {
            simulate.DoubleClick(Slot(2));
            Assert.AreEqual(1, _commands.Issued.Count(c => c == "DoubleClick:2"));
            CollectionAssert.IsEmpty(_commands.Issued.Where(c => !c.EndsWith(":2") && !c.Contains(":2:")));
        }

        [Test]
        public void 枠数を超える番号はLogErrorで知らせ空きで描く()
        {
            LogAssert.Expect(LogType.Error, new Regex(@"\[BlockItemGrid\] chest-grid: slot index 3 is out of range \(slot count 1\)"));
            _grid.Render(new[] { new SlotItem(Wood(), 3) }, new[] { 0, 3 });
            Assert.IsNotNull(Slot(3));
        }

        private static ItemId Wood()
        {
            return MasterHolder.ItemMaster.GetItemId(System.Guid.Parse("00000000-0000-0000-1234-000000000001"));
        }

        // testid は Web と同じく枠（SlotFrame）に付く
        // The testid sits on the frame (SlotFrame), as on the Web
        private SlotFrame Slot(int index)
        {
            return rootVisualElement.Q<SlotFrame>($"chest-grid-slot-{index}");
        }
    }
}

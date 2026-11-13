using Client.Input;
using Client.Tests.Ui.State.BlockInventory.PanelModel;
using Client.Tests.Ui.State.Train.Fakes;
using Client.Ui.Foundation.Input;
using Client.Ui.Screens.BlockInventory.Registry;
using Client.Ui.State.BlockInventory.Logic.Common;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.Items;
using Client.Ui.State.Train.Platform;
using Core.Master;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using static Mooresmaster.Model.BlocksModule.BlockMasterElement;

namespace Client.Tests.Ui.Screens.BlockInventory.Train
{
    /// <summary>
    /// 駅名欄を根の入力規則の下に置き、実キーの Esc と View ごとの取り外しで「入力中」の旗が下りるかを確かめる（L1）
    /// Puts the station name field under the root input rules and checks the text-input flag drops on a real-key Esc and on removing the whole view (L1)
    /// </summary>
    public class TrainStationNameInputRulesTest : BlockInventoryScreenTestHost
    {
        private const string InputTestId = "train-station-name-input";

        private VisualElement _rulesRoot;
        private IBlockInventoryPart _body;

        [SetUp]
        public void MountStationUnderRules()
        {
            // 本番同様に層の根へ規則を付ける
            // Attach the rules at a layer root as in production and mount the station body below it
            _rulesRoot = new VisualElement { name = "rules-root", pickingMode = PickingMode.Ignore };
            _rulesRoot.style.position = Position.Absolute;
            _rulesRoot.style.left = 0;
            _rulesRoot.style.top = 0;
            _rulesRoot.style.right = 0;
            _rulesRoot.style.bottom = 0;
            Mount(_rulesRoot);
            GameUiInputRules.Attach(_rulesRoot);

            var panel = BlockInventoryTestPanels.OfType(BlockTypeConst.TrainStation);
            var bodies = new TrainTestBodies(Toaster, panel);
            _body = bodies.CreatePlatformBody(BlockInventoryTestParts.Grids(), BlockInventoryTestParts.Content());
            _rulesRoot.Add(_body.Root);
            _body.Attach((OpenBlockInventoryPanel)panel);
            var slots = new[] { new SlotItem(new ItemId(3), 12) };
            bodies.Sources.SetPlatform(TrainTestPlatforms.StationBody("北駅", slots, new[] { BlockFluidSlotState.Empty(1000) }, TrainPlatformTransferMode.LoadToTrain));
            simulate.FrameUpdate();
        }

        [TearDown]
        public void DetachAndReset()
        {
            _body.Detach();
            UiInputExclusivity.SetGameUiTextInputFocused(false);
            UiInputExclusivity.ClearEscapeLatchForTest();
        }

        [Test]
        public void 駅名欄はEsc1回でフォーカスが外れ値を保つ()
        {
            var input = FocusAndType("中央");

            // 押下の KeyDown だけで外れる（2 回目を要しない）
            // A single KeyDown drops it; no second press is needed
            simulate.KeyDown(KeyCode.Escape);
            Assert.IsNull(panel.focusController.focusedElement);
            Assert.IsFalse(UiInputExclusivity.IsTextInputFocused);
            Assert.IsTrue(UiInputExclusivity.IsEscapeConsumedThisFrame);
            simulate.KeyUp(KeyCode.Escape);
            simulate.FrameUpdate();

            // 同期済みの名前「北駅」の後ろに打った文字が残り、巻き戻らない
            // The typed text stays after the synced name "北駅" and is not reverted
            Assert.AreEqual("北駅中央", input.value);
            Assert.IsNull(panel.focusController.focusedElement);
        }

        [Test]
        public void 入力中の駅の本文をフォーカスごと外しても旗が残らない()
        {
            FocusAndType("中央");

            _body.Root.RemoveFromHierarchy();
            simulate.FrameUpdate();
            Assert.IsFalse(UiInputExclusivity.IsTextInputFocused);
            Assert.IsNull(panel.focusController.focusedElement);
        }

        [Test]
        public void 駅名欄にフォーカスしたまま本文を隠してもWASDが効く()
        {
            // パネルの SetShown(false) と同じく表示だけ切る。外すのは画面でなく根の入力規則（A-275）
            // Hide only the display as the panel's SetShown(false) does; the root input rules blur, not the screen (A-275)
            FocusAndType("中央");
            _body.Root.style.display = DisplayStyle.None;
            simulate.FrameUpdate();

            Assert.IsFalse(UiInputExclusivity.IsSuppressed(InputSuppressionScope.Keyboard));
            Assert.IsNull(panel.focusController.focusedElement);
        }

        private TextField FocusAndType(string text)
        {
            // クリックでフォーカスし打鍵で入力
            // Focus by clicking and enter the value by typing, as a person does
            var input = Find<TextField>(_body.Root, InputTestId);
            simulate.Click(input.worldBound.center);
            simulate.TypingText(text);
            Assert.IsTrue(UiInputExclusivity.IsTextInputFocused);
            return input;
        }
    }
}

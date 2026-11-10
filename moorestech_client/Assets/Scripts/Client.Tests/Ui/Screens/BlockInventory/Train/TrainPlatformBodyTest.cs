using System.Collections.Generic;
using System.Text.RegularExpressions;
using Client.Tests.Ui.State.BlockInventory.PanelModel;
using Client.Tests.Ui.State.Train.Fakes;
using Client.Ui.Screens.BlockInventory.Parts;
using Client.Ui.Screens.BlockInventory.Registry;
using Client.Ui.State.BlockInventory.Logic.Common;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.Commands;
using Client.Ui.State.Items;
using Client.Ui.State.Train.Platform;
using Core.Master;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static Mooresmaster.Model.BlocksModule.BlockMasterElement;

namespace Client.Tests.Ui.Screens.BlockInventory.Train
{
    public class TrainPlatformBodyTest : BlockInventoryScreenTestHost
    {
        private static readonly IReadOnlyList<BlockFluidSlotState> NoFluids = new List<BlockFluidSlotState>();
        private TrainTestBodies _bodies;
        private IBlockInventoryPart _body;

        [SetUp]
        public void MountBody()
        {
            var panel = BlockInventoryTestPanels.OfType(BlockTypeConst.TrainItemPlatform);
            _bodies = new TrainTestBodies(Toaster, panel);
            _body = _bodies.CreatePlatformBody(BlockInventoryTestParts.Grids(), BlockInventoryTestParts.Content());
            Mount(_body.Root);
            _body.Attach((OpenBlockInventoryPanel)panel);
        }

        [TearDown]
        public void DetachBody()
        {
            _body.Detach();
        }

        /// <web-test file="e2e/tests/block/trainPlatform.spec.ts">PFスロットを表示し、積込から卸しへ目標モードActionを送る</web-test>
        [Test]
        public void PFスロットを出し積込から卸しを送る()
        {
            // Web のモック blockTrainPlatform: 2 枠（12 個と空）・積込
            // Web mock blockTrainPlatform: two slots (12 and empty), load mode
            var slots = new[] { new SlotItem(new ItemId(3), 12), new SlotItem(ItemMaster.EmptyItemId, 0) };
            _bodies.Sources.SetPlatform(TrainTestPlatforms.ItemPlatformBody(slots, NoFluids, TrainPlatformTransferMode.LoadToTrain));
            simulate.FrameUpdate();
            Assert.IsNotNull(_body.Root.Q("train-platform-item-slots-slot-1"));
            Assert.IsTrue(IsSelected(_body.Root, "train-platform-mode-load"));

            // 押しても選択は替えず、目標のモードを送るだけ
            // Pressing does not change the selection; it only sends the target mode
            simulate.Click(Find<Button>(_body.Root, "train-platform-mode-unload"));
            CollectionAssert.AreEqual(new[] { TrainPlatformTransferMode.UnloadToPlatform }, _bodies.BlockCommands.SentModes);
            Assert.IsTrue(IsSelected(_body.Root, "train-platform-mode-load"));
        }

        /// <web-test file="e2e/tests/block/trainPlatform.spec.ts">液体PFは液体名・アイコン・量容量と卸しモードを表示する</web-test>
        [Test]
        public void 液体PFは液体と卸しモードを出す()
        {
            // 格子の testid は中の SlotGrid が持ち、出し入れは外の BlockItemGrid が持つので型で引く
            // The grid testid sits on the inner SlotGrid while the outer BlockItemGrid toggles, so query by type
            var water = MasterHolder.FluidMaster.GetFluidId(TrainTestText.WaterFluidGuid);
            _bodies.Sources.SetPlatform(TrainTestPlatforms.FluidPlatformBody(new[] { BlockFluidSlotState.Filled(water, 250, 1000) }, TrainPlatformTransferMode.UnloadToPlatform));
            simulate.FrameUpdate();
            Assert.IsFalse(IsShown(_body.Root.Q<BlockItemGrid>()));
            StringAssert.Contains(TrainTestText.FluidName(water), FluidTexts(_body));
            StringAssert.Contains("250 / 1,000", FluidTexts(_body));
            Assert.IsTrue(TrainTestText.HasFluidIcon(_body.Root.Q("train-platform-fluid-slots-slot-0")));
            Assert.IsTrue(IsSelected(_body.Root, "train-platform-mode-unload"));
        }

        /// <web-test file="src/features/blockInventory/views/__tests__/TrainPlatformInventory.test.tsx">PF自身の充填スロット・常時テキストを更新し、空でも実スロットを残す</web-test>
        [Test]
        public void 充填スロットを描き直し空でも実枠を残す()
        {
            // Web と同じく液体 PF の本文で、水 250 → 700 → 空
            // As in Web, a fluid platform body going water 250, then 700, then empty
            var bodies = new TrainTestBodies(Toaster, BlockInventoryTestPanels.OfType(BlockTypeConst.TrainFluidPlatform));
            var body = bodies.CreatePlatformBody(BlockInventoryTestParts.Grids(), BlockInventoryTestParts.Content());
            Mount(body.Root);
            body.Attach((OpenBlockInventoryPanel)BlockInventoryTestPanels.OfType(BlockTypeConst.TrainFluidPlatform));
            var water = MasterHolder.FluidMaster.GetFluidId(TrainTestText.WaterFluidGuid);
            bodies.Sources.SetPlatform(TrainTestPlatforms.FluidPlatformBody(new[] { BlockFluidSlotState.Filled(water, 250, 1000) }, TrainPlatformTransferMode.UnloadToPlatform));
            simulate.FrameUpdate();
            StringAssert.Contains(TrainTestText.FluidName(water), FluidTexts(body));
            StringAssert.Contains("250 / 1,000", FluidTexts(body));
            Assert.IsTrue(TrainTestText.HasFluidIcon(body.Root.Q("train-platform-fluid-slots-slot-0")));

            bodies.Sources.SetPlatform(TrainTestPlatforms.FluidPlatformBody(new[] { BlockFluidSlotState.Filled(water, 700, 1000) }, TrainPlatformTransferMode.UnloadToPlatform));
            simulate.FrameUpdate();
            StringAssert.Contains("700 / 1,000", FluidTexts(body));
            StringAssert.Contains("700", TrainTestText.TextsUnder(body.Root.Q("train-platform-fluid-slots-slot-0")));

            // 空でも枠は 1 つ残り、アイコンと文は消える
            // Even when empty one slot stays while the icon and texts disappear
            bodies.Sources.SetPlatform(TrainTestPlatforms.FluidPlatformBody(new[] { BlockFluidSlotState.Empty(1000) }, TrainPlatformTransferMode.UnloadToPlatform));
            simulate.FrameUpdate();
            Assert.IsNotNull(body.Root.Q("train-platform-fluid-slots-slot-0"));
            Assert.IsNull(body.Root.Q("train-platform-fluid-slots-slot-1"));
            Assert.IsFalse(TrainTestText.HasFluidIcon(body.Root.Q("train-platform-fluid-slots-slot-0")));
            StringAssert.DoesNotContain(" / ", FluidTexts(body));
            StringAssert.DoesNotContain(TrainTestText.FluidName(water), FluidTexts(body));
            body.Detach();
        }

        /// <web-test file="src/features/blockInventory/train/TrainTimetableSection.test.ts">sends station names only through Set and hides the field for platforms</web-test>
        [Test]
        public void 駅名は決定でだけ送りPFでは欄を出さない()
        {
            // PF では駅名の行が無い
            // Platforms have no station name row
            _bodies.Sources.SetPlatform(TrainTestPlatforms.ItemPlatformBody(new List<SlotItem>(), NoFluids, TrainPlatformTransferMode.LoadToTrain));
            simulate.FrameUpdate();
            Assert.IsFalse(IsShown(_body.Root.Q("train-station-name-section")));

            // 駅では打つだけでは送らず、決定で 1 回送る。拒否は同期名へ戻す
            // On a station typing sends nothing, Set sends once, and a refusal reverts to the synced name
            _bodies.Sources.SetPlatform(TrainTestPlatforms.StationBody("北駅", new List<SlotItem>(), NoFluids, TrainPlatformTransferMode.LoadToTrain));
            simulate.FrameUpdate();
            var field = _body.Root.Q<TextField>("train-station-name-input");
            Assert.AreEqual("北駅", field.value);
            field.value = "南駅";
            Assert.IsEmpty(_bodies.BlockCommands.SentNames);
            simulate.Click(Find<Button>(_body.Root, "train-station-name-apply"));
            CollectionAssert.AreEqual(new[] { "南駅" }, _bodies.BlockCommands.SentNames);
            LogAssert.Expect(LogType.Warning, new Regex(@"\[UiCommandResultToaster\] train_station.set_name failed"));
            _bodies.BlockCommands.Reject(UiCommandRejectReason.TrainStationNameFailed, "duplicate");
            Assert.AreEqual("北駅", field.value);
        }

        // 液体名と量・容量の文は枠の兄弟なので、FluidInventoryContents の器（testid）から読む
        // Name and amount texts are siblings of the slot, so read them from the FluidInventoryContents container (its testid)
        private static string FluidTexts(IBlockInventoryPart body)
        {
            return TrainTestText.TextsUnder(body.Root.Q("train-platform-fluid-slots"));
        }
    }
}

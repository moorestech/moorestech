using System.Collections.Generic;
using Client.Tests.Ui.State.BlockInventory.PanelModel;
using Client.Ui.Screens.BlockInventory.Parts;
using Client.Ui.Screens.BlockInventory.Registry;
using Client.Ui.State.BlockInventory.Logic.Common;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.Items;
using Client.Ui.State.Train.Inventory;
using Client.Ui.State.Train.Timetable;
using Core.Master;
using NUnit.Framework;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static Client.Tests.Ui.State.Train.Fakes.TrainTestData;

namespace Client.Tests.Ui.Screens.BlockInventory.Train
{
    public class TrainInventoryBodyTest : BlockInventoryScreenTestHost
    {
        private TrainTestBodies _bodies;
        private ITrainInventoryPart _body;

        [SetUp]
        public void MountBody()
        {
            _bodies = new TrainTestBodies(Toaster, BlockInventoryTestPanels.Train(BlockInventoryPanelError.None));
            _body = _bodies.CreateTrainBody(BlockInventoryTestParts.Grids(), BlockInventoryTestParts.Content());
            Mount(_body.Root);
            _body.Attach((OpenTrainInventoryPanel)BlockInventoryTestPanels.Train(BlockInventoryPanelError.None));
            simulate.FrameUpdate();
        }

        [TearDown]
        public void DetachBody()
        {
            _body.Detach();
        }

        /// <web-test file="src/features/blockInventory/train/TrainTimetableSection.test.ts">asks C# to fetch only when the timetable tab is selected</web-test>
        [Test]
        public void 時刻表タブを選んだときだけ取得を頼む()
        {
            // 持ち物タブのあいだは読込中が届いても頼まない
            // On the inventory tab a loading state does not ask
            _bodies.Sources.SetTimetable(TrainTimetableViewState.Loading);
            Assert.AreEqual(0, _bodies.TimetableCommands.OpenRequestCount);
            Assert.IsTrue(IsSelected(_body.Root, "train-tab-inventory"));

            // 持ち物の器は子ごと隠れる（IsShown は要素自身の display を見るので器で確かめる）
            // The inventory container hides with its children (IsShown reads the element's own display, so check the container)
            simulate.Click(Find<Button>(_body.Root, "train-tab-timetable"));
            simulate.FrameUpdate();
            Assert.AreEqual(1, _bodies.TimetableCommands.OpenRequestCount);
            Assert.IsTrue(IsShown(_body.Root.Q("train-timetable-loading")));
            Assert.IsFalse(IsShown(_body.Root.Q("train-inventory-contents")));

            // 持ち物タブへ戻しても頼まない
            // Going back to the inventory tab does not ask
            simulate.Click(Find<Button>(_body.Root, "train-tab-inventory"));
            Assert.AreEqual(1, _bodies.TimetableCommands.OpenRequestCount);
        }

        /// <web-test file="src/features/blockInventory/train/TrainTimetableSection.test.ts">asks C# to fetch on tab select even when the timetable is already ready</web-test>
        [Test]
        public void 用意済みでもタブ選択では取得を頼む()
        {
            _bodies.Sources.SetTimetable(TrainTimetableViewState.Ready(NorthOnly(Unit1)));
            simulate.Click(Find<Button>(_body.Root, "train-tab-timetable"));
            simulate.FrameUpdate();
            Assert.AreEqual(1, _bodies.TimetableCommands.OpenRequestCount);
            Assert.IsTrue(IsShown(_body.Root.Q("train-timetable-section")));
        }

        /// <web-test file="src/features/blockInventory/train/TrainTimetableSection.test.ts">re-asks C# when a reset fetch arrives as loading while the timetable tab stays selected</web-test>
        [Test]
        public void 時刻表タブのあいだに読込中へ戻れば頼み直す()
        {
            _bodies.Sources.SetTimetable(TrainTimetableViewState.Ready(NorthOnly(Unit1)));
            simulate.Click(Find<Button>(_body.Root, "train-tab-timetable"));
            _bodies.Sources.SetTimetable(TrainTimetableViewState.Loading);
            Assert.AreEqual(2, _bodies.TimetableCommands.OpenRequestCount);
        }

        /// <web-test file="src/features/blockInventory/train/TrainTimetableSection.test.ts">shows loading and unavailable as distinct states without warning</web-test>
        [Test]
        public void 読込中と使えないを別の文で出し警告しない()
        {
            simulate.Click(Find<Button>(_body.Root, "train-tab-timetable"));
            _bodies.Sources.SetTimetable(TrainTimetableViewState.Loading);
            simulate.FrameUpdate();
            Assert.IsTrue(IsShown(_body.Root.Q("train-timetable-loading")));
            Assert.IsFalse(IsShown(_body.Root.Q("train-timetable-unavailable")));

            _bodies.Sources.SetTimetable(TrainTimetableViewState.Unavailable);
            simulate.FrameUpdate();
            Assert.IsFalse(IsShown(_body.Root.Q("train-timetable-loading")));
            Assert.IsTrue(IsShown(_body.Root.Q("train-timetable-unavailable")));
            LogAssert.NoUnexpectedReceived();
        }

        /// <web-test file="src/features/blockInventory/train/TrainInventoryBody.test.tsx">液体貨車は名前と量容量を表示し、再取得した量へ更新する</web-test>
        [Test]
        public void 液体貨車は名前と量容量を出し再取得で描き直す()
        {
            // 格子の testid は中の SlotGrid が持ち、出し入れは外の BlockItemGrid が持つので型で引く
            // The grid testid sits on the inner SlotGrid while the outer BlockItemGrid toggles, so query by type
            var water = MasterHolder.FluidMaster.GetFluidId(TrainTestText.WaterFluidGuid);
            _bodies.Sources.SetBody(new TrainInventoryBodyViewModel(new List<SlotItem>(), new[] { BlockFluidSlotState.Filled(water, 10, 1000) }));
            simulate.FrameUpdate();
            Assert.IsFalse(IsShown(_body.Root.Q<BlockItemGrid>()));
            StringAssert.Contains(TrainTestText.FluidName(water), FluidTexts());
            StringAssert.Contains("10 / 1,000", FluidTexts());
            Assert.IsTrue(TrainTestText.HasFluidIcon(_body.Root.Q("train-inventory-fluids-slot-0")));

            // 再取得した量へ描き直す
            // Redraws with the refetched amount
            _bodies.Sources.SetBody(new TrainInventoryBodyViewModel(new List<SlotItem>(), new[] { BlockFluidSlotState.Filled(water, 90, 1000) }));
            simulate.FrameUpdate();
            StringAssert.Contains("90 / 1,000", FluidTexts());
            StringAssert.Contains("90", TrainTestText.TextsUnder(_body.Root.Q("train-inventory-fluids-slot-0")));

            // 空になると枠は残り、名前・量・アイコンは消える
            // When emptied the slot stays while name, amount and icon disappear
            _bodies.Sources.SetBody(new TrainInventoryBodyViewModel(new List<SlotItem>(), new[] { BlockFluidSlotState.Empty(1000) }));
            simulate.FrameUpdate();
            Assert.IsNotNull(_body.Root.Q("train-inventory-fluids-slot-0"));
            Assert.IsFalse(TrainTestText.HasFluidIcon(_body.Root.Q("train-inventory-fluids-slot-0")));
            StringAssert.DoesNotContain(" / ", FluidTexts());
            StringAssert.DoesNotContain(TrainTestText.FluidName(water), FluidTexts());
        }

        [Test]
        public void 時刻表タブの間の中身の更新は描かず持ち物へ戻すと最新を描く()
        {
            // 非表示の持ち物タブは購読しないので、時刻表タブの間の更新は木に出ない（D-27）
            // The hidden inventory tab is not subscribed, so updates during the timetable tab do not reach the tree (D-27)
            var water = MasterHolder.FluidMaster.GetFluidId(TrainTestText.WaterFluidGuid);
            simulate.Click(Find<Button>(_body.Root, "train-tab-timetable"));
            _bodies.Sources.SetBody(new TrainInventoryBodyViewModel(new List<SlotItem>(), new[] { BlockFluidSlotState.Filled(water, 10, 1000) }));
            simulate.FrameUpdate();
            Assert.IsNull(_body.Root.Q("train-inventory-fluids-slot-0"));

            // 戻すと購読の頭で最新値を 1 回描く
            // Switching back draws the latest value once on subscribe
            simulate.Click(Find<Button>(_body.Root, "train-tab-inventory"));
            simulate.FrameUpdate();
            Assert.IsNotNull(_body.Root.Q("train-inventory-fluids-slot-0"));
            StringAssert.Contains("10 / 1,000", FluidTexts());
        }

        [Test]
        public void 使えないの間でもタブを選ぶと取得を1回頼む()
        {
            // 取得中の表示は Fetcher の開始の合図を TrainTimetableModel が拾って出す（Hostless のプローブで確かめる）。ここは頼む回数だけ
            // Loading comes from TrainTimetableModel picking up the Fetcher start signal (checked in the Hostless probe); here only the request count
            _bodies.Sources.SetTimetable(TrainTimetableViewState.Unavailable);
            simulate.Click(Find<Button>(_body.Root, "train-tab-timetable"));
            Assert.AreEqual(1, _bodies.TimetableCommands.OpenRequestCount);
        }

        [Test]
        public void 隠して戻した同じ車両はタブを保ち別のパネルでは持ち物タブから()
        {
            // 画面が隠れると Detach、戻ると同じ Presenter へ Attach（alpha の呼ばれ方）。同じ車両ならタブを保つ
            // Hiding the screen detaches and showing it attaches the same Presenter again (alpha's call order); the same car keeps its tab
            simulate.Click(Find<Button>(_body.Root, "train-tab-timetable"));
            _body.Detach();
            _body.Attach((OpenTrainInventoryPanel)BlockInventoryTestPanels.Train(BlockInventoryPanelError.None));
            Assert.IsTrue(IsSelected(_body.Root, "train-tab-timetable"));

            // 識別の違うパネル（エラーが違う）が来たら持ち物タブから（Web は本文の mount ごとに初期化）
            // A panel with a different identity (different error) starts on the inventory tab (Web resets per body mount)
            _body.Detach();
            _body.Attach((OpenTrainInventoryPanel)BlockInventoryTestPanels.Train(BlockInventoryPanelError.TrainCarMissing));
            Assert.IsTrue(IsSelected(_body.Root, "train-tab-inventory"));
        }

        // 液体名と量・容量の文は枠の兄弟なので、FluidInventoryContents の器（testid）から読む
        // Name and amount texts are siblings of the slot, so read them from the FluidInventoryContents container (its testid)
        private string FluidTexts()
        {
            return TrainTestText.TextsUnder(_body.Root.Q("train-inventory-fluids"));
        }
    }
}

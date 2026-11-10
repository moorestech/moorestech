using System;
using System.Collections.Generic;
using System.IO;
using Client.Tests.Ui.Conventions;
using Client.Tests.Ui.State.BlockInventory.PanelModel;
using Client.Tests.Ui.State.Train.Fakes;
using Client.Ui.Components.Controls;
using Client.Ui.Screens.BlockInventory.Fluid;
using Client.Ui.Screens.BlockInventory.Parts;
using Client.Ui.State.BlockInventory.Logic.Common;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.Items;
using Client.Ui.State.Train.Inventory;
using Client.Ui.State.Train.Platform;
using Client.Ui.State.Train.Timetable;
using NUnit.Framework;
using UnityEngine.UIElements;
using Mooresmaster.Model.BlocksModule;
using static Client.Tests.Ui.State.Train.Fakes.TrainTestData;

namespace Client.Tests.Ui.Screens.BlockInventory.Train
{
    public class TrainDesignTest : BlockInventoryScreenTestHost
    {
        private static readonly string[] TrainUssPaths =
        {
            "Assets/Scripts/Client.Ui/Screens/BlockInventory/Train/Train.uss",
            "Assets/Scripts/Client.Ui/Screens/BlockInventory/Train/Timetable/Timetable.uss",
        };
        private static readonly Type[] RawControlTypes = { typeof(Button), typeof(Toggle), typeof(RadioButton), typeof(Slider), typeof(ProgressBar), typeof(TextField) };

        /// <web-test file="src/features/blockInventory/blockInventoryDesign.test.ts">ゲージとモード切替を用途別に共通部品へ置換する</web-test>
        [Test]
        public void 列車のモード切替は共通部品ModeSwitchで出す()
        {
            // タブ・自動運転・積込/卸しの 3 つとも共通の ModeSwitch
            // Tabs, auto-run and load/unload all use the shared ModeSwitch
            var (train, platform) = MountAllParts();
            Assert.IsInstanceOf<ModeSwitch<TrainInventoryTab>>(train.Q("train-tabs"));
            Assert.IsInstanceOf<ModeSwitch<bool>>(train.Q("train-timetable-auto-run"));
            Assert.IsInstanceOf<ModeSwitch<TrainPlatformTransferMode>>(platform.Q("train-platform-mode"));
        }

        /// <web-test file="src/features/blockInventory/blockInventoryDesign.test.ts">%sから禁止Mantine部品とテーマ色を除去する</web-test>
        [Test]
        public void 列車の本文は共通部品とトークンの色だけで組む()
        {
            // (a) USS に色の直書きが無い。判定は規約検査 UiStyleConventionRules と同じもの
            // (a) No literal colors in the USS, judged by the same UiStyleConventionRules as the convention check
            foreach (var path in TrainUssPaths)
            {
                var violations = UiStyleConventionRules.CheckUss(File.ReadAllText(path), false);
                Assert.IsFalse(violations.Exists(v => v.Rule == UiStyleRule.ColorLiteral), $"{path} に色の直書きがある");
            }

            // (b) 列車・駅の本文の操作の要素はどれも共通部品で、素の Button・Toggle 等を画面が作らない
            // (b) Every control in the train and station bodies is a shared part; the screen builds no raw Button, Toggle, etc.
            var (train, platform) = MountAllParts();
            var sharedControls = new List<VisualElement>();
            CollectControls(train, sharedControls);
            CollectControls(platform, sharedControls);
            Assert.Less(0, sharedControls.Count);
        }

        // 列車の本文（時刻表タブを開いた区画つき）と、駅名・枠・液体・モードを全部出した駅の本文を組む
        // Builds the train body with the timetable section open and a station body showing name, slots, fluids and mode
        private (VisualElement train, VisualElement platform) MountAllParts()
        {
            var bodies = new TrainTestBodies(Toaster, BlockInventoryTestPanels.Train(BlockInventoryPanelError.None));
            var train = bodies.CreateTrainBody(BlockInventoryTestParts.Grids(), BlockInventoryTestParts.Content());
            Mount(train.Root);
            train.Attach((OpenTrainInventoryPanel)BlockInventoryTestPanels.Train(BlockInventoryPanelError.None));
            bodies.Sources.SetTimetable(TrainTimetableViewState.Ready(NorthOnly(Unit1)));
            simulate.Click(Find<Button>(train.Root, "train-tab-timetable"));

            var platform = bodies.CreatePlatformBody(BlockInventoryTestParts.Grids(), BlockInventoryTestParts.Content());
            Mount(platform.Root);
            platform.Attach((OpenBlockInventoryPanel)BlockInventoryTestPanels.OfType(BlockMasterElement.BlockTypeConst.TrainStation));
            bodies.Sources.SetPlatform(TrainTestPlatforms.StationBody("北駅", new[] { new SlotItem(new Core.Master.ItemId(3), 12) }, new[] { BlockFluidSlotState.Empty(1000) }, TrainPlatformTransferMode.LoadToTrain));
            simulate.FrameUpdate();
            return (train.Root, platform.Root);
        }

        // 共通部品の中は部品の責任なので下りない。それ以外に素の操作の要素があれば落とす
        // Shared parts own their insides, so do not descend; any other raw control fails
        private static void CollectControls(VisualElement element, List<VisualElement> sharedControls)
        {
            if (IsSharedControl(element))
            {
                sharedControls.Add(element);
                return;
            }
            Assert.IsFalse(Array.IndexOf(RawControlTypes, element.GetType()) >= 0, $"素の {element.GetType().Name} '{element.name}' を画面が作っている");
            foreach (var child in element.Children()) CollectControls(child, sharedControls);
        }

        private static bool IsSharedControl(VisualElement element)
        {
            var type = element.GetType();
            return (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ModeSwitch<>))
                   || element is PanelActionButton || element is IconButton || element is GameTextField || element is BlockItemGrid || element is FluidInventoryContents;
        }
    }
}

using System.Collections.Generic;
using System.Text.RegularExpressions;
using Client.Localization;
using Client.Tests.Ui.State.BlockInventory.PanelModel;
using Client.Tests.Ui.State.Train.Fakes;
using Client.Ui.State.BlockInventory.Logic.Common;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.Commands;
using Client.Ui.State.Items;
using Client.Ui.State.Toast;
using Client.Ui.State.Train.Platform;
using Client.Ui.State.Train.Platform.Edit;
using NUnit.Framework;
using UniRx;
using UnityEngine;
using UnityEngine.TestTools;
using static Mooresmaster.Model.BlocksModule.BlockMasterElement;

namespace Client.Tests.Ui.State.Train.Platform
{
    public class TrainPlatformEditModelTest
    {
        [SetUp]
        public void SetUp()
        {
            // 拒否のトーストが文言を引くので、単独実行でも辞書を用意する
            // Refusal toasts look up text, so prepare the dictionary even when run alone
            Localize.Initialize();
        }

        [Test]
        public void 閉じて同じ駅を開き直した後に戻った拒否は新しい入力を消さない()
        {
            var panels = new FakePanelSource(new ReactiveProperty<BlockInventoryPanelState>(BlockInventoryTestPanels.OfType(BlockTypeConst.TrainStation)));
            var sources = new FakeTrainSources();
            sources.SetPlatform(TrainTestPlatforms.StationBody("北駅", new List<SlotItem>(), new List<BlockFluidSlotState>(), TrainPlatformTransferMode.LoadToTrain));
            var commands = new FakeTrainBlockCommands();
            var state = new TrainStationNameEditState(panels, sources);
            var model = new TrainPlatformEditModel(state);
            var edit = new TrainPlatformEditCommands(state, commands, new UiCommandResultToaster(new GameUiToastFeed()));
            state.Initialize();

            // 駅で送ってから閉じ、同じ駅を開き直して打ち直す（パネルの識別は同じ駅なので IsSamePanel では見分けられない）
            // Send, close, reopen the same station and type again (IsSamePanel cannot tell it apart: same station)
            edit.EditStationName("南駅");
            Assert.IsInstanceOf<UiCommandAccepted>(edit.ApplyStationName());
            panels.State.Value = BlockInventoryPanelState.Closed;
            panels.State.Value = BlockInventoryTestPanels.OfType(BlockTypeConst.TrainStation);
            edit.EditStationName("東駅");

            // 前の回の拒否はトーストとログだけで、新しい入力は打った値のまま
            // The earlier refusal only toasts and logs; the new input keeps the typed value
            LogAssert.Expect(LogType.Warning, new Regex(@"train_station\.set_name failed"));
            LogAssert.Expect(LogType.Warning, new Regex(@"\[TrainStationName\] set_name refused after the draft was replaced"));
            commands.Reject(UiCommandRejectReason.TrainStationNameFailed, "busy");
            Assert.AreEqual("東駅", model.StationNameInput.Value);
        }
    }
}

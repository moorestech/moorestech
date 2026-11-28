using System.Linq;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Client.Network.API;
using Client.Tests.Common;
using Client.Tests.PlaceSystem.TrainCostIntegration;
using Core.Master;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Mooresmaster.Model.BlocksModule;
using Tests.Module.TestMod;
using Tests.Util;
using Tests.Util.PlayerIdentity;
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
using S = Mooresmaster.Model.BlocksModule.BlockMasterElement.BlockScreenConst;

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
            var panels = new FakePanelSource(new ReactiveProperty<BlockInventoryPanelState>(BlockInventoryTestPanels.OfType(BlockTypeConst.TrainStation, S.TrainPlatform)));
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
            edit.ApplyStationName();
            panels.State.Value = BlockInventoryPanelState.Closed;
            panels.State.Value = BlockInventoryTestPanels.OfType(BlockTypeConst.TrainStation, S.TrainPlatform);
            edit.EditStationName("東駅");

            // 前の回の拒否はトーストとログだけで、新しい入力は打った値のまま
            // The earlier refusal only toasts and logs; the new input keeps the typed value
            LogAssert.Expect(LogType.Warning, new Regex(@"train_station\.set_name failed"));
            LogAssert.Expect(LogType.Warning, new Regex(@"\[TrainStationName\] set_name refused after the draft was replaced"));
            commands.Reject(UiCommandRejectReason.TrainStationNameFailed, "busy");
            Assert.AreEqual("東駅", model.StationNameInput.Value);
        }

        [Test]
        public void 貨物ホームの転送は現在の画面に従い拒否時はモードを保持する()
        {
            var environment = TrainTestHelper.CreateEnvironment();
            var position = new Vector3Int(2, 0, 3);
            var blockId = ForUnitTestModBlockId.TestTrainItemPlatform;
            var (_, transfer) = TrainTestHelper.PlaceBlockWithComponent<TrainPlatformTransferComponent>(environment, blockId, position, BlockDirection.North);
            var master = MasterHolder.BlockMaster.GetBlockMaster(blockId);
            var previousApi = ClientContext.VanillaApi;
            using var packets = new PlacementPacketCapture();
            var root = new GameObject(nameof(TrainPlatformEditModelTest));
            try
            {
                // 同じ座標で表示マスタだけ変更
                // Change only the view’s master at the same position.
                TestReflection.SetStaticProperty(typeof(ClientContext), nameof(ClientContext.VanillaApi), packets.Api);
                var store = root.AddComponent<BlockGameObjectDataStore>();
                var view = root.AddComponent<BlockGameObject>();
                var views = TestReflection.GetField<Dictionary<Vector3Int, BlockGameObject>>(store, "_blockObjectsDictionary");
                views.Add(position, view);
                var state = new SubInventoryState(null, null);
                var source = new BlockSubInventorySource(store, position);
                TestReflection.SetField(state, "<CurrentSubInventorySource>k__BackingField", source);
                var commands = new TrainBlockCommands(state);

                // 送信後のサーバー側ホームを確認
                // Check the same platform after the server handles the transmitted request.
                SetScreen(S.TrainPlatform);
                Assert.AreEqual(TrainPlatformTransferComponent.TransferMode.LoadToTrain, transfer.Mode);
                var pending = commands.SetTransferMode(TrainPlatformTransferMode.UnloadToPlatform);
                var response = environment.PacketResponseCreator.GetPacketResponse(packets.ReadRequest(), BoundPacketContext.Bind(1)).Single();
                packets.Waiters.Values.Single().WaitSubject.OnNext((response, PacketWaitCompletionReason.Received));
                Assert.AreSame(UiCommandAccepted.Instance, pending.GetAwaiter().GetResult());
                Assert.AreEqual(TrainPlatformTransferComponent.TransferMode.UnloadToPlatform, transfer.Mode);

                // 開ける別画面と閉鎖は区別し、どちらでも送らない
                // Distinguish a different open screen from closure and send in neither case
                AssertRejected(S.Generic, UiCommandRejectReason.BlockTypeMismatch);
                AssertRejected(S.None, UiCommandRejectReason.BlockNotOpen);

                #region Internal

                void AssertRejected(string screen, UiCommandRejectReason reason)
                {
                    SetScreen(screen);
                    var request = commands.SetTransferMode(TrainPlatformTransferMode.LoadToTrain);
                    packets.AssertNoRequest();
                    var rejected = (UiCommandRejected)request.GetAwaiter().GetResult();
                    Assert.AreEqual(reason, rejected.Reason);
                    Assert.AreEqual(TrainPlatformTransferComponent.TransferMode.UnloadToPlatform, transfer.Mode);
                }

                void SetScreen(string screen)
                {
                    BlockMasterElement replacement = BlockInventoryTestPanels.WithScreen(master, screen);
                    TestReflection.SetField(view, "<BlockMasterElement>k__BackingField", replacement);
                }

                #endregion
            }
            finally
            {
                TestReflection.SetStaticProperty(typeof(ClientContext), nameof(ClientContext.VanillaApi), previousApi);
                Object.DestroyImmediate(root);
            }
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState.State;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Client.Network.API;
using Client.Tests.Common;
using Client.Tests.PlaceSystem.TrainCostIntegration;
using Client.Tests.Ui.State.BlockInventory.PanelModel;
using Client.Ui.State.Commands;
using Client.Ui.State.Train.Platform;
using Client.Ui.State.Train.Platform.Edit;
using Core.Master;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Tests.Module.TestMod;
using Tests.Util;
using Tests.Util.PlayerIdentity;
using UnityEngine;
using S = Mooresmaster.Model.BlocksModule.BlockMasterElement.BlockScreenConst;

namespace Client.Tests.Ui.State.Train.Platform
{
    public class TrainBlockCommandsTest
    {
        [Test]
        public void 貨物ホームの転送は現在の画面に従い拒否時はモードを保持する()
        {
            var environment = TrainTestHelper.CreateEnvironment();
            var position = new Vector3Int(2, 0, 3);
            var blockId = ForUnitTestModBlockId.TestTrainItemPlatform;
            var (_, transfer) = TrainTestHelper.PlaceBlockWithComponent<TrainPlatformTransferComponent>(environment, blockId, position, BlockDirection.North);
            var master = MasterHolder.BlockMaster.GetBlockMaster(blockId);
            var root = new GameObject(nameof(TrainBlockCommandsTest));
            var previousApi = ClientContext.VanillaApi;
            using var packets = new PlacementPacketCapture();
            try
            {
                // 開いた座標を固定し、表示側のマスタだけを差し替える
                // Keep the open position fixed and replace only the view's master
                TestReflection.SetStaticProperty(typeof(ClientContext), nameof(ClientContext.VanillaApi), packets.Api);
                var store = root.AddComponent<BlockGameObjectDataStore>();
                var view = root.AddComponent<BlockGameObject>();
                var views = TestReflection.GetField<Dictionary<Vector3Int, BlockGameObject>>(store, "_blockObjectsDictionary");
                views.Add(position, view);
                var state = new SubInventoryState(null, null);
                var source = new BlockSubInventorySource(store, position);
                TestReflection.SetField(state, "<CurrentSubInventorySource>k__BackingField", source);
                var commands = new TrainBlockCommands(state);

                // 実際に送った要求をサーバーで処理し、同じホームのモードを読む
                // Process the actual request on the server and read that platform's mode
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

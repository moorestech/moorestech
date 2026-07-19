using System;
using System.Linq;
using Core.Master;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.UnlockState;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class RailConnectWithPlacePierProtocolTest
    {
        private const int PlayerId = 1;
        private static readonly Vector3Int FromRailPosition = Vector3Int.zero;
        private static readonly Vector3Int PierPosition = new(10, 0, 0);

        // レール種はrail connectTool（lengthPerUnit=5, 補強棒材x12＋鉄板x5/単位）を使う
        // Rail type uses the rail connectTool (lengthPerUnit=5, reinforce x12 + plate x5 per unit)
        private static readonly Guid RailConnectToolGuid = Guid.Parse("c0000000-0000-0000-0000-000000000002");
        private static readonly Guid ReinforceGuid = Guid.Parse("00000000-0000-0000-1234-000000000002");
        private static readonly Guid PlateGuid = Guid.Parse("00000000-0000-0000-1234-000000000003");
        private const int ReinforcePerUnit = 12;
        private const int PlatePerUnit = 5;
        private const float LengthPerUnit = 5f;

        // TestTrainRail橋脚の建設コストは鉄板(Plate)x2。レール素材の鉄板と重なる
        // The TestTrainRail pier construction cost is plate x2, which overlaps the rail plate material
        private const int PierPlateCost = 2;

        // 潤沢量は各素材のMaxStack以内に収める（補強棒材50・鉄板300）
        // Plentiful amounts stay within each material MaxStack (reinforce 50, plate 300)
        private const int ReinforcePlenty = 50;
        private const int PlatePlenty = 300;

        private TrainTestEnvironment _environment;
        private global::Game.Train.RailGraph.RailNode _fromNode;
        private global::Core.Inventory.IOpenableInventory _inventory;
        private ItemId _reinforceItemId;
        private ItemId _plateItemId;

        [SetUp]
        public void SetUp()
        {
            // 起点レールを直接設置しfromNodeとインベントリを準備する
            // Place the from rail directly and prepare fromNode and the inventory
            _environment = TrainTestHelper.CreateEnvironment();
            var fromRailComponent = TrainTestHelper.PlaceRail(_environment, FromRailPosition, BlockDirection.North);
            _fromNode = fromRailComponent.FrontNode;
            _inventory = _environment.ServiceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;
            _reinforceItemId = MasterHolder.ItemMaster.GetItemId(ReinforceGuid);
            _plateItemId = MasterHolder.ItemMaster.GetItemId(PlateGuid);
        }

        [Test]
        public void 橋脚設置と接続でレールが複数素材の距離比例で消費される()
        {
            UnlockRailConnectTool();
            SetInventory(reinforce: ReinforcePlenty, plate: PlatePlenty);

            var response = Send(ForUnitTestModBlockId.TestTrainRail);

            // 成功応答と橋脚の設置を検証する
            // Verify the success response and the placed pier
            Assert.IsTrue(response.Success, "設置は成功するべき / Placement should succeed");
            Assert.IsTrue(ServerContext.WorldBlockDatastore.Exists(PierPosition), "橋脚が設置されるべき / Pier should be placed");
            Assert.IsTrue(_environment.GetRailGraphDatastore().TryGetRailNode(response.ToNodeId, out var toNode), "toNodeが存在するべき / toNode should exist");
            TrainTestHelper.Node2NodeCheckAndAssert(_fromNode, toNode, "fromNode", "toNode");

            // units×各素材count＋橋脚コスト(鉄板x2)が消費されることを検証する
            // Verify units×each material count plus the pier cost (plate x2) are consumed
            var units = UnitsFor(toNode);
            Assert.Greater(units, 1, "距離比例で2単位以上消費されるべき / Two or more units should be consumed by distance");
            Assert.AreEqual(ReinforcePlenty - units * ReinforcePerUnit, CountItem(_reinforceItemId), "補強棒材がunits×12消費されるべき / Reinforce should be consumed units×12");
            Assert.AreEqual(PlatePlenty - units * PlatePerUnit - PierPlateCost, CountItem(_plateItemId), "鉄板がunits×5＋橋脚コスト消費されるべき / Plate should be consumed units×5 plus the pier cost");
        }

        [Test]
        public void 補強棒材が不足なら設置されず失敗応答を返す()
        {
            UnlockRailConnectTool();
            // 補強棒材を1単位分未満だけ所持する（鉄板は潤沢）
            // Hold less than one unit of reinforce (plate is plentiful)
            SetInventory(reinforce: ReinforcePerUnit - 1, plate: PlatePlenty);

            var response = Send(ForUnitTestModBlockId.TestTrainRail);

            AssertFailedWithoutStateChange(response, expectedReinforce: ReinforcePerUnit - 1, expectedPlate: PlatePlenty);
        }

        [Test]
        public void 鉄板が橋脚コストと敷設分の合算で不足なら失敗しロールバックされる()
        {
            UnlockRailConnectTool();
            // 鉄板は橋脚コスト(2)分のみ所持する。敷設分(5×units≧5)には足りず合算で失敗する（補強棒材は潤沢）
            // Hold plate only for the pier cost (2); insufficient for laying (5×units≥5), so the combined check fails (reinforce is plentiful)
            SetInventory(reinforce: ReinforcePlenty, plate: PierPlateCost);

            var response = Send(ForUnitTestModBlockId.TestTrainRail);

            AssertFailedWithoutStateChange(response, expectedReinforce: ReinforcePlenty, expectedPlate: PierPlateCost);
        }

        [Test]
        public void 未解放橋脚は設置されず失敗応答を返す()
        {
            UnlockRailConnectTool();
            SetInventory(reinforce: ReinforcePlenty, plate: PlatePlenty);

            var response = Send(ForUnitTestModBlockId.LockedTrainRail);

            AssertFailedWithoutStateChange(response, expectedReinforce: ReinforcePlenty, expectedPlate: PlatePlenty);
        }

        [Test]
        public void 未解放connectToolでは設置されず失敗応答を返す()
        {
            // connectToolを解放しないまま接続を試みる
            // Attempt the connection without unlocking the connectTool
            SetInventory(reinforce: ReinforcePlenty, plate: PlatePlenty);

            var response = Send(ForUnitTestModBlockId.TestTrainRail);

            AssertFailedWithoutStateChange(response, expectedReinforce: ReinforcePlenty, expectedPlate: PlatePlenty);
        }

        [Test]
        public void connectToolGuidがEmptyの接続要求は無料設置扱いされず失敗応答を返す()
        {
            // connectToolを解放し素材も潤沢でも、Empty指定は無料設置扱いされず拒否される
            // Even with an unlocked connectTool and ample materials, an Empty specification is rejected instead of treated as free placement
            UnlockRailConnectTool();
            SetInventory(reinforce: ReinforcePlenty, plate: PlatePlenty);

            var response = Send(ForUnitTestModBlockId.TestTrainRail, Guid.Empty);

            AssertFailedWithoutStateChange(response, expectedReinforce: ReinforcePlenty, expectedPlate: PlatePlenty);
        }

        // 設置後のtoNodeまでのレール長から必要単位数を算出する
        // Compute the required unit count from the rail length up to the placed toNode
        private int UnitsFor(global::Game.Train.RailGraph.RailNode toNode)
        {
            return Mathf.CeilToInt(RailConnectionEditProtocol.GetRailLength(_fromNode, toNode) / LengthPerUnit);
        }

        private void UnlockRailConnectTool()
        {
            _environment.ServiceProvider.GetService<IGameUnlockStateDataController>().UnlockConnectTool(RailConnectToolGuid);
        }

        private void SetInventory(int reinforce, int plate)
        {
            // Create は count<1 で空スタックを返す
            // Create returns an empty stack when count < 1
            _inventory.SetItem(0, ServerContext.ItemStackFactory.Create(_reinforceItemId, reinforce));
            _inventory.SetItem(1, ServerContext.ItemStackFactory.Create(_plateItemId, plate));
        }

        private RailConnectWithPlacePierProtocol.RailConnectWithPlacePierResponse Send(BlockId pierBlockId)
        {
            return Send(pierBlockId, RailConnectToolGuid);
        }

        private RailConnectWithPlacePierProtocol.RailConnectWithPlacePierResponse Send(BlockId pierBlockId, Guid connectToolGuid)
        {
            // クライアント同様にレール向きの生成パラメータを付与する
            // Attach the rail direction create param just like the client does
            var stateDetail = new RailBridgePierComponentStateDetail(Vector3.forward);
            var createParams = new[] { new BlockCreateParam(RailBridgePierComponentStateDetail.StateDetailKey, MessagePackSerializer.Serialize(stateDetail)) };
            var placeInfo = new PlaceInfo
            {
                Position = PierPosition,
                Direction = BlockDirection.North,
                VerticalDirection = BlockVerticalDirection.Horizontal,
                CreateParams = createParams,
            };
            var request = RailConnectWithPlacePierProtocol.RailConnectWithPlacePierRequest.Create(PlayerId, _fromNode.NodeId, _fromNode.Guid, pierBlockId, placeInfo, connectToolGuid);
            var responseBytes = _environment.PacketResponseCreator.GetPacketResponse(MessagePackSerializer.Serialize(request), new PacketResponseContext(null)).First();
            return MessagePackSerializer.Deserialize<RailConnectWithPlacePierProtocol.RailConnectWithPlacePierResponse>(responseBytes.ToArray());
        }

        private void AssertFailedWithoutStateChange(RailConnectWithPlacePierProtocol.RailConnectWithPlacePierResponse response, int expectedReinforce, int expectedPlate)
        {
            // 失敗時に橋脚もアイテム消費も残らないことを検証する
            // Verify failures leave neither a pier nor any item consumption
            Assert.IsFalse(response.Success, "失敗応答を返すべき / Should return a failure response");
            Assert.IsFalse(ServerContext.WorldBlockDatastore.Exists(PierPosition), "橋脚は設置されないべき / Pier should not be placed");
            Assert.AreEqual(expectedReinforce, CountItem(_reinforceItemId), "補強棒材は消費されないべき / Reinforce should not be consumed");
            Assert.AreEqual(expectedPlate, CountItem(_plateItemId), "鉄板は消費されないべき / Plate should not be consumed");
        }

        private int CountItem(ItemId itemId)
        {
            return _inventory.InventoryItems.Where(stack => stack.Id == itemId).Sum(stack => stack.Count);
        }
    }
}

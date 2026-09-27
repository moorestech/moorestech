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
    public abstract class RailConnectWithPlacePierProtocolTestBase
    {
        protected const int PlayerId = 1;
        protected static readonly Vector3Int FromRailPosition = Vector3Int.zero;
        protected static readonly Vector3Int PierPosition = new(10, 0, 0);

        // 橋脚を置く位置。最大接続長の検証だけは既定より遠くへ置く
        // Where the pier goes; only the max-length check moves it beyond the default
        protected Vector3Int _pierPosition = PierPosition;

        // レール種はrail connectTool（lengthPerUnit=5, 補強棒材x12＋鉄板x5/単位）を使う
        // Rail type uses the rail connectTool (lengthPerUnit=5, reinforce x12 + plate x5 per unit)
        protected static readonly Guid RailConnectToolGuid = Guid.Parse("c0000000-0000-0000-0000-000000000002");
        protected static readonly Guid ReinforceGuid = Guid.Parse("00000000-0000-0000-1234-000000000002");
        protected static readonly Guid PlateGuid = Guid.Parse("00000000-0000-0000-1234-000000000003");
        protected const int ReinforcePerUnit = 12;
        protected const int PlatePerUnit = 5;
        protected const float LengthPerUnit = 5f;

        // TestTrainRail橋脚の建設コストは鉄板(Plate)x2。レール素材の鉄板と重なる
        // The TestTrainRail pier construction cost is plate x2, which overlaps the rail plate material
        protected const int PierPlateCost = 2;

        // 潤沢量は各素材のMaxStack以内に収める（補強棒材50・鉄板300）
        // Plentiful amounts stay within each material MaxStack (reinforce 50, plate 300)
        protected const int ReinforcePlenty = 50;
        protected const int PlatePlenty = 300;

        protected TrainTestEnvironment _environment;
        protected global::Game.Train.RailGraph.RailNode _fromNode;
        protected global::Core.Inventory.IOpenableInventory _inventory;
        protected ItemId _reinforceItemId;
        protected ItemId _plateItemId;

        [SetUp]
        public void SetUp()
        {
            // 起点レールを直接設置しfromNodeとインベントリを準備する
            // Place the from rail directly and prepare fromNode and the inventory
            _environment = TrainTestHelper.CreateEnvironment();
            _pierPosition = PierPosition;
            var fromRailComponent = TrainTestHelper.PlaceRail(_environment, FromRailPosition, BlockDirection.North);
            _fromNode = fromRailComponent.FrontNode;
            _inventory = _environment.ServiceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;
            _reinforceItemId = MasterHolder.ItemMaster.GetItemId(ReinforceGuid);
            _plateItemId = MasterHolder.ItemMaster.GetItemId(PlateGuid);
        }

        // 設置後のtoNodeまでのレール長から必要単位数を算出する
        // Compute the required unit count from the rail length up to the placed toNode
        protected int UnitsFor(global::Game.Train.RailGraph.RailNode toNode)
        {
            return Mathf.CeilToInt(RailConnectionEditProtocol.GetRailLength(_fromNode, toNode) / LengthPerUnit);
        }

        protected void UnlockRailConnectTool()
        {
            _environment.ServiceProvider.GetService<IGameUnlockStateDataController>().UnlockConnectTool(RailConnectToolGuid);
        }

        protected void SetInventory(int reinforce, int plate)
        {
            // Create は count<1 で空スタックを返す
            // Create returns an empty stack when count < 1
            _inventory.SetItem(0, ServerContext.ItemStackFactory.Create(_reinforceItemId, reinforce));
            _inventory.SetItem(1, ServerContext.ItemStackFactory.Create(_plateItemId, plate));
        }

        protected RailConnectWithPlacePierProtocol.RailConnectWithPlacePierResponse Send(BlockId pierBlockId)
        {
            return Send(pierBlockId, RailConnectToolGuid);
        }

        protected RailConnectWithPlacePierProtocol.RailConnectWithPlacePierResponse Send(BlockId pierBlockId, Guid connectToolGuid)
        {
            // クライアント同様にレール向きの生成パラメータを付与する
            // Attach the rail direction create param just like the client does
            var stateDetail = new RailBridgePierComponentStateDetail(Vector3.forward);
            var createParams = new[] { new BlockCreateParam(RailBridgePierComponentStateDetail.StateDetailKey, MessagePackSerializer.Serialize(stateDetail)) };
            var placeInfo = new PlaceInfo
            {
                Position = _pierPosition,
                Direction = BlockDirection.North,
                VerticalDirection = BlockVerticalDirection.Horizontal,
                CreateParams = createParams,
            };
            var request = RailConnectWithPlacePierProtocol.RailConnectWithPlacePierRequest.Create(_fromNode.NodeId, _fromNode.Guid, pierBlockId, placeInfo, connectToolGuid);
            var responseBytes = _environment.PacketResponseCreator.GetPacketResponse(MessagePackSerializer.Serialize(request), Tests.Util.BoundPacketContext.Bind(PlayerId)).First();
            return MessagePackSerializer.Deserialize<RailConnectWithPlacePierProtocol.RailConnectWithPlacePierResponse>(responseBytes.ToArray());
        }

        protected void AssertFailedWithoutStateChange(RailConnectWithPlacePierProtocol.RailConnectWithPlacePierResponse response, int expectedReinforce, int expectedPlate)
        {
            // 失敗時に橋脚もアイテム消費も残らないことを検証する
            // Verify failures leave neither a pier nor any item consumption
            Assert.IsFalse(response.Success, "失敗応答を返すべき / Should return a failure response");
            Assert.IsFalse(ServerContext.WorldBlockDatastore.Exists(_pierPosition), "橋脚は設置されないべき / Pier should not be placed");
            Assert.AreEqual(expectedReinforce, CountItem(_reinforceItemId), "補強棒材は消費されないべき / Reinforce should not be consumed");
            Assert.AreEqual(expectedPlate, CountItem(_plateItemId), "鉄板は消費されないべき / Plate should not be consumed");
        }

        protected int CountItem(ItemId itemId)
        {
            return _inventory.InventoryItems.Where(stack => stack.Id == itemId).Sum(stack => stack.Count);
        }
    }
}

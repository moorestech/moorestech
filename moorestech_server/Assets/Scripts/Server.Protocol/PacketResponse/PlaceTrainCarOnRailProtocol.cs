using System;
using System.Collections.Generic;
using Core.Master;
using Game.Block.Blocks.TrainRail;
using Game.PlayerInventory.Interface;
using Game.Train.RailGraph;
using Game.Train.Train;
using Game.Train.Utility;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Util.MessagePack;
using RailComponentSpecifier = Server.Protocol.PacketResponse.RailConnectionEditProtocol.RailComponentSpecifier;

namespace Server.Protocol.PacketResponse
{
    public class PlaceTrainCarOnRailProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:placeTrainCar";

        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;

        public PlaceTrainCarOnRailProtocol(ServiceProvider serviceProvider)
        {
            _playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
        }

        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            var request = MessagePackSerializer.Deserialize<PlaceTrainOnRailRequestMessagePack>(payload.ToArray());

            // レールがあるかチェック
            // Check if rail component exists
            var railComponent = RailConnectionEditProtocol.ResolveRailComponent(request.RailSpecifier);
            if (railComponent == null) return null;
            
            // 手持ちのアイテム取得
            // Get the item from player's inventory
            var mainInventory = _playerInventoryDataStore.GetInventoryData(request.PlayerId).MainOpenableInventory;
            var item = mainInventory.GetItem(request.InventorySlot);

            // 列車ユニットをスナップショットから生成
            // Build train unit from the rail position snapshot
            var trainUnit = CreateTrainUnit(railComponent, item.Id, request.RailPosition);
            if (trainUnit == null) return null;

            // アイテムを消費
            // Consume the train item from inventory
            mainInventory.SetItem(request.InventorySlot, item.Id, item.Count - 1);

            return null;

            #region Internal

            TrainUnit CreateTrainUnit(RailComponent rail, ItemId trainItemId, RailPositionSnapshotMessagePack railPositionSnapshot)
            {
                // アイテムIDに対応する列車ユニット編成を検索
                // Search for train unit composition matching the item ID
                if (!MasterHolder.TrainUnitMaster.TryGetTrainUnit(trainItemId, out var trainUnitElement))
                {
                    return null;
                }

                // TrainCarElementからTrainCarオブジェクトを生成
                // Create TrainCar objects from TrainCarElement data
                var trainCars = new TrainCar(trainUnitElement, true);

                // クライアントのレール位置スナップショットを復元する
                // Restore rail position from client snapshot
                var expectedTrainLength = TrainLengthConverter.ToRailUnits(trainUnitElement.Length);
                if (!TryRestoreRailPosition(rail, railPositionSnapshot, expectedTrainLength, out var railPosition))
                {
                    return null;
                }

                // TrainUnitを生成して返す
                // Create and return TrainUnit
                return new TrainUnit(railPosition, new List<TrainCar> { trainCars });
                
                #region Internal

                bool TryRestoreRailPosition(RailComponent railComponent, RailPositionSnapshotMessagePack snapshot, int expectedLength, out RailPosition position)
                {
                    position = null;
                    // スナップショットと長さを検証する
                    // Validate snapshot and length
                    if (snapshot == null)
                    {
                        return false;
                    }
                    var saveData = snapshot.ToModel();
                    if (saveData == null || saveData.RailSnapshot == null || saveData.RailSnapshot.Count == 0)
                    {
                        return false;
                    }
                    // 列車長の一致を確認する
                    // Confirm the train length matches
                    if (saveData.TrainLength != expectedLength)
                    {
                        return false;
                    }

                    // 先頭ノードが指定レールに一致するか確認する
                    // Ensure the head node matches the specified rail component
                    if (!IsHeadMatching(railComponent, saveData.RailSnapshot[0]))
                    {
                        return false;
                    }

                    // RailPositionを復元する
                    // Restore rail position instance
                    // グラフプロバイダを使って復元する
                    // Restore via the graph provider
                    position = RailPositionFactory.Restore(saveData, railComponent.FrontNode.GraphProvider);
                    return position != null;
                }

                bool IsHeadMatching(RailComponent railComponent, ConnectionDestination headDestination)
                {
                    // 指定レールのFront/Backに一致するか確認する
                    // Check if the head matches front/back of the rail component
                    return railComponent.FrontNode.ConnectionDestination.Equals(headDestination) || railComponent.BackNode.ConnectionDestination.Equals(headDestination);
                }

                #endregion
            }

            #endregion
        }

        #region MessagePack Classes

        [MessagePackObject]
        public class PlaceTrainOnRailRequestMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public RailComponentSpecifier RailSpecifier { get; set; }
            [Key(3)] public RailPositionSnapshotMessagePack RailPosition { get; set; }
            [Key(4)] public int HotBarSlot { get; set; }
            [IgnoreMember] public int InventorySlot => PlayerInventoryConst.HotBarSlotToInventorySlot(HotBarSlot);
            [Key(5)] public int PlayerId { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public PlaceTrainOnRailRequestMessagePack()
            {
                // タグを既定値に設定
                // Initialize tag with default value
                Tag = ProtocolTag;
            }

            public PlaceTrainOnRailRequestMessagePack(
                RailComponentSpecifier railSpecifier,
                RailPositionSnapshotMessagePack railPosition,
                int hotBarSlot,
                int playerId)
            {
                // 必須情報を格納
                // Store required request information
                Tag = ProtocolTag;
                RailSpecifier = railSpecifier;
                RailPosition = railPosition;
                HotBarSlot = hotBarSlot;
                PlayerId = playerId;
            }
        }

        #endregion
    }
}

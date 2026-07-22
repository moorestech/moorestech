using System;
using System.Collections.Generic;
using System.Linq;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.Train.Event;
using Game.Train.Unit;
using Game.Train.Unit.Containers;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Event.Notification;
using Server.Protocol.PacketResponse.Util.Construction;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    public class RemoveTrainCarProtocol : IPacketResponse
    {
        private readonly ITrainUnitSnapshotNotifyEvent _trainUnitSnapshotNotifyEvent;
        private readonly ITrainUnitLookupDatastore _trainUnitLookupDatastore;
        private readonly ITrainUnitMutationDatastore _trainUnitMutationDatastore;
        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;
        private readonly NotificationService _notificationService;
        public const string ProtocolTag = "va:removeTrainCar";

        public RemoveTrainCarProtocol(ServiceProvider serviceProvider)
        {
            _trainUnitSnapshotNotifyEvent = serviceProvider.GetService<ITrainUnitSnapshotNotifyEvent>();
            _trainUnitLookupDatastore = serviceProvider.GetService<ITrainUnitLookupDatastore>();
            _trainUnitMutationDatastore = serviceProvider.GetService<ITrainUnitMutationDatastore>();
            _playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
            _notificationService = serviceProvider.GetService<NotificationService>();
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload, PacketResponseContext context)
        {
            // リクエストの復元
            // Deserialize request payload
            var request = MessagePackSerializer.Deserialize<RemoveTrainCarRequestMessagePack>(payload);
            var trainCarInstanceId = new TrainCarInstanceId(request.TrainCarInstanceId);

            if (!_trainUnitLookupDatastore.TryGetTrainUnitByCar(trainCarInstanceId, out var requestTrainUnit))
            {
                Debug.LogWarning($"Remove train car failed. Train not found. \ncarId: {trainCarInstanceId}");
                return null;
            }

            // 削除対象の車両を特定し、返却アイテム(車両ブロック本体＋積載アイテム)を組み立てる
            // Resolve the car to remove and build refund items (car block itself + loaded items).
            var targetCar = requestTrainUnit.Cars.FirstOrDefault(car => car.TrainCarInstanceId == trainCarInstanceId);
            if (targetCar == null)
            {
                Debug.LogWarning($"Remove train car failed. Car not found in train unit. \ncarId: {trainCarInstanceId}");
                return null;
            }
            var refundItems = BuildRefundItems(targetCar);

            // 返却先プレイヤーインベントリに空きがあるか確認する。入らない場合は削除自体を中止する
            // Verify the player inventory can hold every refund item; abort the removal otherwise.
            var playerMainInventory = _playerInventoryDataStore.GetInventoryData(request.PlayerId).MainOpenableInventory;
            if (!playerMainInventory.InsertionCheck(refundItems))
            {
                Debug.LogWarning($"Remove train car aborted. Player inventory is full. \ncarId: {trainCarInstanceId}");
                // 高価な車両が無言で消えない事故を防ぐため満杯を通知する
                // Notify inventory-full so an expensive car never silently refuses to be removed
                _notificationService.Notify(request.PlayerId, NotificationMessagePack.CreateOperationDenied("denied.removeTrainCarInventoryFull", Array.Empty<string>()));
                return null;
            }

            // 削除の実行
            // Apply removal
            var createdTrainUnit = requestTrainUnit.RemoveCar(trainCarInstanceId);

            TrainUnitInstanceId delid = requestTrainUnit.TrainUnitInstanceId;//削除するもの
            List<TrainUnit> createList = new List<TrainUnit>();//新規生成か更新するもの

            // requestTrainUnitの中身が変更されて実在しているか
            if (requestTrainUnit.Cars.Count == 0)
            {
                // TrainUnitまるごと通知
                // Notify train unit snapshot updates
                // datastore更新上書き
                _trainUnitMutationDatastore.UnregisterTrain(requestTrainUnit);
                requestTrainUnit.OnDestroy();
                requestTrainUnit = null;
            }
            else
            {
                // datastore更新上書き
                _trainUnitMutationDatastore.RegisterTrain(requestTrainUnit);
                createList.Add(requestTrainUnit);
            }

            if (createdTrainUnit != null)
            {
                // 実在するか
                // is exist?
                if (createdTrainUnit.Cars.Count == 0)
                {
                    createdTrainUnit.OnDestroy();
                    createdTrainUnit = null;
                }
                else
                {
                    // datastore更新
                    _trainUnitMutationDatastore.RegisterTrain(createdTrainUnit);
                    createList.Add(createdTrainUnit);
                }
            }

            // 削除した車両のブロック本体と中身をプレイヤーへ返却する
            // Refund the removed car's block item and its contents to the player.
            playerMainInventory.InsertItem(refundItems);

            // 先に削除をクライアントに送信
            // 1st del
            _trainUnitSnapshotNotifyEvent.NotifyDeleted(delid);
            //
            // 2nd create
            foreach (var trainUnit in createList)
                _trainUnitSnapshotNotifyEvent.NotifySnapshot(trainUnit);
            return null;

            #region Internal

            List<IItemStack> BuildRefundItems(TrainCar car)
            {
                var result = new List<IItemStack>();

                // 建設コスト全額を返却する（コスト未定義マスタは本体返却なし）
                // Refund the full construction cost; masters without cost refund nothing for the body
                var costItemCounts = ConstructionCostService.ToItemCounts(car.TrainCarMasterElement.RequiredItems);
                if (costItemCounts.Length > 0)
                {
                    result.AddRange(ConstructionCostService.CreateRefundItems(costItemCounts));
                }

                // アイテムコンテナを積んでいる場合は中身も返却対象に加える(液体コンテナはアイテム化不可のため対象外)
                // Include loaded items when the car carries an item container (fluid containers cannot be itemized).
                if (car.Container is ItemTrainCarContainer itemContainer)
                {
                    for (var i = 0; i < itemContainer.GetSlotSize(); i++)
                        result.Add(itemContainer.GetItem(i));
                }

                return result;
            }

            #endregion
        }

        [MessagePackObject]
        public class RemoveTrainCarRequestMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public long TrainCarInstanceId { get; set; }
            [Key(3)] public int PlayerId { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public RemoveTrainCarRequestMessagePack()
            {
                Tag = ProtocolTag;
            }

            public RemoveTrainCarRequestMessagePack(long trainCarInstanceId, int playerId)
            {
                Tag = ProtocolTag;
                TrainCarInstanceId = trainCarInstanceId;
                PlayerId = playerId;
            }
        }
    }
}

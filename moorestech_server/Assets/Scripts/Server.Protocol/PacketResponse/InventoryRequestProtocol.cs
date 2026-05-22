using System;
using System.Collections.Generic;
using System.Linq;
using Core.Item.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using Game.Train.Unit;
using Game.Train.Unit.Containers;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Util.MessagePack;
using Game.World.Interface.DataStore;

namespace Server.Protocol.PacketResponse
{
    /// <summary>
    /// インベントリ取得プロトコル
    /// Inventory fetch protocol
    /// </summary>
    public class InventoryRequestProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:invReq";

        private readonly ITrainUnitLookupDatastore _trainUnitLookupDatastore;

        public InventoryRequestProtocol(ServiceProvider serviceProvider)
        {
            _trainUnitLookupDatastore = serviceProvider.GetService<ITrainUnitLookupDatastore>();
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload, PacketResponseContext context)
        {
            // リクエストをデシリアライズ
            // Deserialize request
            var request = MessagePackSerializer.Deserialize<RequestInventoryRequestProtocolMessagePack>(payload);

            // 識別子ごとのインベントリを判定取得
            // Dispatch inventory retrieval by identifier type
            return request.Identifier.InventoryType switch
            {
                InventoryType.Block => CreateBlockResponse(request.Identifier),
                InventoryType.Train => CreateTrainResponse(request.Identifier),
                _ => throw new ArgumentException($"Unknown inventory type: {request.Identifier.InventoryType}")
            };

            #region Internal

            ResponseInventoryRequestProtocolMessagePack CreateBlockResponse(InventoryIdentifierMessagePack identifier)
            {
                // ブロック情報を取得
                // Fetch block information
                var position = identifier.BlockPosition.Vector3Int;
                var datastore = ServerContext.WorldBlockDatastore;
                var block = datastore.GetBlock(position);

                if (block == null) return ResponseInventoryRequestProtocolMessagePack.CreateBlockNotFound(identifier);

                // インベントリ要素を抽出
                // Collect inventory items
                if (!datastore.ExistsComponent<IOpenableBlockInventoryComponent>(position))
                    return ResponseInventoryRequestProtocolMessagePack.CreateContainerNotFound(InventoryType.Block, identifier);

                var items = datastore.GetBlock<IOpenableBlockInventoryComponent>(position).InventoryItems;
                return ResponseInventoryRequestProtocolMessagePack.CreateSuccess(InventoryType.Block, identifier, items);
            }

            ResponseInventoryRequestProtocolMessagePack CreateTrainResponse(InventoryIdentifierMessagePack identifier)
            {
                // 列車カーを探索
                // Find the target train car
                var trainCarInstanceId = new TrainCarInstanceId(long.Parse(identifier.TrainCarInstanceId));
                if (!_trainUnitLookupDatastore.TryGetTrainCar(trainCarInstanceId, out var trainCar))
                    return ResponseInventoryRequestProtocolMessagePack.CreateTrainCarNotFound(identifier);

                // 列車カーのインベントリを生成
                // Build the train car inventory
                if (trainCar.Container is ItemTrainCarContainer container)
                    return ResponseInventoryRequestProtocolMessagePack.CreateSuccess(InventoryType.Train, identifier, container.InventoryItems.ToArray());
                return ResponseInventoryRequestProtocolMessagePack.CreateContainerNotFound(InventoryType.Train, identifier);
            }

            #endregion
        }


        [MessagePackObject]
        public class RequestInventoryRequestProtocolMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public InventoryIdentifierMessagePack Identifier { get; set; }


            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public RequestInventoryRequestProtocolMessagePack() { }

            public RequestInventoryRequestProtocolMessagePack(InventoryIdentifierMessagePack identifier)
            {
                Tag = ProtocolTag;
                Identifier = identifier;
            }
        }

        [MessagePackObject]
        public class ResponseInventoryRequestProtocolMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public InventoryType InventoryType { get; set; }
            [Key(3)] public InventoryIdentifierMessagePack Identifier { get; set; }
            [Key(4)] public ItemMessagePack[] Items { get; set; }
            [Key(5)] public InventoryRequestResult Result { get; set; }


            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public ResponseInventoryRequestProtocolMessagePack() { }

            public ResponseInventoryRequestProtocolMessagePack(InventoryType inventoryType, InventoryIdentifierMessagePack identifier, IReadOnlyList<IItemStack> items, InventoryRequestResult result = InventoryRequestResult.Success)
            {
                Tag = ProtocolTag;
                InventoryType = inventoryType;
                Identifier = identifier;
                Items = items.Select(item => new ItemMessagePack(item)).ToArray();
                Result = result;
            }

            public static ResponseInventoryRequestProtocolMessagePack CreateSuccess(InventoryType inventoryType, InventoryIdentifierMessagePack identifier, IReadOnlyList<IItemStack> items)
            {
                return new ResponseInventoryRequestProtocolMessagePack(inventoryType, identifier, items, InventoryRequestResult.Success);
            }

            public static ResponseInventoryRequestProtocolMessagePack CreateContainerNotFound(InventoryType inventoryType, InventoryIdentifierMessagePack identifier)
            {
                return new ResponseInventoryRequestProtocolMessagePack(inventoryType, identifier, Array.Empty<IItemStack>(), InventoryRequestResult.ContainerNotFound);
            }

            public static ResponseInventoryRequestProtocolMessagePack CreateTrainCarNotFound(InventoryIdentifierMessagePack identifier)
            {
                return new ResponseInventoryRequestProtocolMessagePack(InventoryType.Train, identifier, Array.Empty<IItemStack>(), InventoryRequestResult.TrainCarNotFound);
            }

            public static ResponseInventoryRequestProtocolMessagePack CreateBlockNotFound(InventoryIdentifierMessagePack identifier)
            {
                return new ResponseInventoryRequestProtocolMessagePack(InventoryType.Block, identifier, Array.Empty<IItemStack>(), InventoryRequestResult.BlockNotFound);
            }
        }
    }

    public enum InventoryRequestResult
    {
        Success = 0,
        ContainerNotFound = 1,
        TrainCarNotFound = 2,
        BlockNotFound = 3
    }
}

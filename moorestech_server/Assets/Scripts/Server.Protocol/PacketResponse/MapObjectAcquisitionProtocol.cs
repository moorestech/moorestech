using System;
using System.Collections.Generic;
using Core.Const;
using Core.Item;
using Game.MapObject.Interface;
using Game.PlayerInventory.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;

namespace Server.Protocol.PacketResponse
{
    /// <summary>
    ///     MapObjectを取得するときのプロトコル
    /// </summary>
    public class MapObjectAcquisitionProtocol : IPacketResponse
    {
        public const string Tag = "va:mapObjectInfoAcquisition";
        private readonly ItemStackFactory _itemStackFactory;

        private readonly IMapObjectDatastore _mapObjectDatastore;
        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;

        public MapObjectAcquisitionProtocol(ServiceProvider serviceProvider)
        {
            _mapObjectDatastore = serviceProvider.GetService<IMapObjectDatastore>();
            _itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
            _playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
        }


        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            var data = MessagePackSerializer.Deserialize<GetMapObjectProtocolProtocolMessagePack>(payload.ToArray());

            var mapObject = _mapObjectDatastore.Get(data.InstanceId);
            var itemStack = _itemStackFactory.Create(mapObject.ItemId, mapObject.ItemCount);
            var playerMainInventory =
                _playerInventoryDataStore.GetInventoryData(data.PlayerId).MainOpenableInventory;
            var insertedItem = playerMainInventory.InsertItem(itemStack);

            //アイテムの挿入に成功したらマップオブジェクトを削除
            if (insertedItem.Id == ItemConst.EmptyItemId) mapObject.Destroy();

            return null;
        }
    }


    [MessagePackObject]
    public class GetMapObjectProtocolProtocolMessagePack : ProtocolMessagePackBase
    {
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public GetMapObjectProtocolProtocolMessagePack()
        {
        }

        public GetMapObjectProtocolProtocolMessagePack(int playerId, int instanceId)
        {
            Tag = MapObjectAcquisitionProtocol.Tag;
            PlayerId = playerId;
            InstanceId = instanceId;
        }

        [Key(2)]
        public int PlayerId { get; set; }
        [Key(3)]
        public int InstanceId { get; set; }
    }
}
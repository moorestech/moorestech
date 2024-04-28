using System;
using System.Collections.Generic;
using Core.Const;
using Core.Item.Interface;
using Game.Context;
using Game.Map.Interface;
using Game.PlayerInventory.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    /// <summary>
    ///     MapObjectを取得するときのプロトコル
    /// </summary>
    public class MapObjectAcquisitionProtocol : IPacketResponse
    {
        public const string Tag = "va:mapObjectInfoAcquisition";


        private readonly IMapObjectDatastore _mapObjectDatastore;
        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;

        public MapObjectAcquisitionProtocol(ServiceProvider serviceProvider)
        {
            _mapObjectDatastore = serviceProvider.GetService<IMapObjectDatastore>();
            _playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
        }


        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            var data = MessagePackSerializer.Deserialize<GetMapObjectProtocolProtocolMessagePack>(payload.ToArray());

            var mapObject = _mapObjectDatastore.Get(data.InstanceId);
            var playerMainInventory = _playerInventoryDataStore.GetInventoryData(data.PlayerId).MainOpenableInventory;

            var earnedItem = mapObject.Attack(data.AttackDamage); // ダメージを与える

            foreach (var earnItem in earnedItem)
            {
                playerMainInventory.InsertItem(earnItem);
            }

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

        public GetMapObjectProtocolProtocolMessagePack(int playerId, int instanceId, int attackDamage)
        {
            Tag = MapObjectAcquisitionProtocol.Tag;
            PlayerId = playerId;
            InstanceId = instanceId;
            AttackDamage = attackDamage;
        }

        [Key(2)]
        public int PlayerId { get; set; }
        [Key(3)]
        public int InstanceId { get; set; }
        [Key(4)]
        public int AttackDamage { get; set; }
    }
}
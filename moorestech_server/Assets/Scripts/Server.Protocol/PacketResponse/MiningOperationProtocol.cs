﻿using System;
using System.Collections.Generic;
using Core.Item;
using Core.Ore;
using Game.PlayerInventory.Interface;
using Game.WorldMap;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Util.MessagePack;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    public class MiningOperationProtocol : IPacketResponse
    {
        public const string Tag = "va:miningOre";
        private readonly ItemStackFactory _itemStackFactory;
        private readonly IOreConfig _oreConfig;
        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;

        private readonly VeinGenerator _veinGenerator;
        private Seed _seed;

        public MiningOperationProtocol(ServiceProvider serviceProvider)
        {
            _veinGenerator = serviceProvider.GetService<VeinGenerator>();
            _playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
            _itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
            _oreConfig = serviceProvider.GetService<IOreConfig>();
            _seed = serviceProvider.GetService<Seed>();
        }

        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            var data = MessagePackSerializer.Deserialize<MiningOperationProtocolMessagePack>(payload.ToArray());


            //プレイヤーインベントリーの取得
            var playerMainInventory =
                _playerInventoryDataStore.GetInventoryData(data.PlayerId).MainOpenableInventory;

            //鉱石IDを取得
            var oreId = _veinGenerator.GetOreId(data.Pos);
            //鉱石のアイテムID
            var oreItemId = _oreConfig.OreIdToItemId(oreId);
            //プレイヤーインベントリーに鉱石を挿入
            playerMainInventory.InsertItem(_itemStackFactory.Create(oreItemId, 1));


            return null;
        }
    }


    [MessagePackObject]
    public class MiningOperationProtocolMessagePack : ProtocolMessagePackBase
    {
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public MiningOperationProtocolMessagePack()
        {
        }

        public MiningOperationProtocolMessagePack(int playerId, Vector2Int pos)
        {
            Tag = MiningOperationProtocol.Tag;
            PlayerId = playerId;
            Pos = new Vector2IntMessagePack(pos);
        }

        [Key(2)]
        public int PlayerId { get; set; }
        [Key(3)]
        public Vector2IntMessagePack Pos { get; set; }
    }
}
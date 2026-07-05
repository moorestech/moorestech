using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Protocol.PacketResponse.Util.ElectricWire;

namespace Server.Protocol.PacketResponse
{
    public class PlaceBlockFromHotBarProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:palceHotbarBlock";
        
        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;
        
        public PlaceBlockFromHotBarProtocol(ServiceProvider serviceProvider)
        {
            _playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
        }
        
        public ProtocolMessagePackBase GetResponse(byte[] payload, PacketResponseContext context)
        {
            var data = MessagePackSerializer.Deserialize<SendPlaceHotBarBlockProtocolMessagePack>(payload);
            var inventoryData = _playerInventoryDataStore.GetInventoryData(data.PlayerId);
            
            foreach (var placeInfo in data.PlacePositions)
            {
                PlaceBlock(placeInfo, data, inventoryData);
            }
            
            return null;
        }
        
        #region GetResponse
        
        static void PlaceBlock(PlaceInfoMessagePack placeInfo, SendPlaceHotBarBlockProtocolMessagePack data, PlayerInventoryData inventoryData)
        {
            //すでにブロックがある場合はそもまま処理を終了
            if (ServerContext.WorldBlockDatastore.Exists(placeInfo.Position)) return;
            
            //アイテムIDがブロックIDに変換できない場合はそもまま処理を終了
            var item = inventoryData.MainOpenableInventory.GetItem(data.InventorySlot);
            if (!MasterHolder.BlockMaster.IsBlock(item.Id)) return;
            
            // ブロックIDの設定
            var blockId = MasterHolder.BlockMaster.GetBlockId(item.Id);
            blockId = blockId.GetVerticalOverrideBlockId(placeInfo.VerticalDirection);
            
            // paramsの作成
            var createParams = placeInfo.BlockCreateParams.Select(v => new BlockCreateParam(v.Key, v.Value)).ToArray();

            // 電気系ブロックなら自動接続計画を設置前に検証する。電線不足なら設置しない
            // For electric blocks, validate the auto-connect plan before placement; skip placement when wires are insufficient
            var isElectric = ElectricWireBlockParamResolver.TryGetWireParam(MasterHolder.BlockMaster.GetBlockMaster(blockId).BlockParam, out _, out _);
            var plan = default(ElectricWireAutoConnectPlan);
            if (isElectric)
            {
                plan = ElectricWireAutoConnectService.EvaluateAutoConnect(blockId, placeInfo.Position, placeInfo.Direction, new[] { (item.Id, 1) }, inventoryData.MainOpenableInventory.InventoryItems);
                if (!plan.IsPlaceable) return;
            }

            //ブロックの設置。占有範囲が重なる等で失敗した場合はアイテムを消費しない
            //Place the block. Do not consume the item if placement fails (e.g. overlapping footprint)
            if (!ServerContext.WorldBlockDatastore.TryAddBlock(blockId, placeInfo.Position, placeInfo.Direction, createParams, out var block)) return;

            //アイテムを減らし、セットする
            item = item.SubItem(1);
            inventoryData.MainOpenableInventory.SetItem(data.InventorySlot, item);

            // 検証済みの計画を実行してワイヤーを張り、電線を消費する
            // Execute the validated plan: add wires and consume wire items
            if (isElectric) ElectricWireAutoConnectService.ExecuteAutoConnect(plan, block, inventoryData.MainOpenableInventory);
        }
        
        #endregion
        
        
        [MessagePackObject]
        public class SendPlaceHotBarBlockProtocolMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public int PlayerId { get; set; }
            
            [Key(3)] public int HotBarSlot { get; set; }
            [IgnoreMember] public int InventorySlot => PlayerInventoryConst.HotBarSlotToInventorySlot(HotBarSlot);
            
            [Key(4)] public List<PlaceInfoMessagePack> PlacePositions { get; set; }
            
            public SendPlaceHotBarBlockProtocolMessagePack(int playerId, int hotBarSlot, List<PlaceInfo> placeInfos)
            {
                Tag = ProtocolTag;
                PlayerId = playerId;
                HotBarSlot = hotBarSlot;
                PlacePositions = placeInfos.ConvertAll(v => new PlaceInfoMessagePack(v));
            }
            
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public SendPlaceHotBarBlockProtocolMessagePack() { }
        }
    }
}
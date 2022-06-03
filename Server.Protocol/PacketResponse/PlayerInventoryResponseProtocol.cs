using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Game.PlayerInventory.Interface;
using MessagePack;
using Server.Util;

namespace Server.Protocol.PacketResponse
{
    public class PlayerInventoryResponseProtocol : IPacketResponse
    {
        public const string Tag = "va:playerInvRequest";
        
        private IPlayerInventoryDataStore _playerInventoryDataStore;

        public PlayerInventoryResponseProtocol(IPlayerInventoryDataStore playerInventoryDataStore)
        {
            _playerInventoryDataStore = playerInventoryDataStore;
        }

        public List<List<byte>> GetResponse(List<byte> payload)
        {
            var data = MessagePackSerializer.Deserialize<RequestPlayerInventoryProtocolMessagePack>(payload.ToArray());
            
            var playerInventory = _playerInventoryDataStore.GetInventoryData(data.PlayerId);
            
            

            //メインインベントリのアイテムを設定
            var mainIds = new List<int>();
            var mainCounts = new List<int>();
            for (int i = 0; i < PlayerInventoryConst.MainInventorySize; i++)
            {
                mainIds.Add(playerInventory.MainOpenableInventory.GetItem(i).Id);
                mainCounts.Add(playerInventory.MainOpenableInventory.GetItem(i).Count);
            }
            
            
            //グラブインベントリのアイテムを設定
            var grabId = playerInventory.GrabInventory.GetItem(0).Id;
            var grabCount = playerInventory.GrabInventory.GetItem(0).Count;

            
            //クラフトインベントリのアイテムを設定
            var craftIds = new List<int>();
            var craftCounts = new List<int>();
            for (int i = 0; i < PlayerInventoryConst.CraftingSlotSize; i++)
            {
                craftIds.Add(playerInventory.CraftingOpenableInventory.GetItem(i).Id);
                craftCounts.Add(playerInventory.CraftingOpenableInventory.GetItem(i).Count);
            }
            
            //クラフト結果のアイテムを設定
            var craftResultId = playerInventory.CraftingOpenableInventory.GetItem(0).Id;
            var craftResultCount = playerInventory.CraftingOpenableInventory.GetItem(0).Count;
            
            var isCreatable = playerInventory.CraftingOpenableInventory.IsCreatable();

            var response = MessagePackSerializer.Serialize(new PlayerInventoryResponseProtocolMessagePack()
            {
                Tag = Tag,
                MainIds = mainIds.ToArray(),
                MainCounts = mainCounts.ToArray(),
                GrabId = grabId,
                GrabCount = grabCount,
                CraftIds = craftIds.ToArray(),
                CraftCounts = craftCounts.ToArray(),
                CraftResultId = craftResultId,
                CraftResultCount = craftResultCount,
                IsCreatable = isCreatable
            });

            return new List<List<byte>>() {response.ToList()};
        }
    }
    
    
    [MessagePackObject(keyAsPropertyName :true)]
    public class RequestPlayerInventoryProtocolMessagePack : ProtocolMessagePackBase
    {
        public int PlayerId { get; set; }
    }
    
    
    [MessagePackObject(keyAsPropertyName :true)]
    public class PlayerInventoryResponseProtocolMessagePack : ProtocolMessagePackBase
    {
        public int PlayerId { get; set; }
        public int[] MainIds { get; set; }
        public int[] MainCounts { get; set; }
        public int GrabId { get; set; }
        public int GrabCount { get; set; }
        public int[] CraftIds { get; set; }
        public int[] CraftCounts { get; set; }
        public int CraftResultId { get; set; }
        public int CraftResultCount { get; set; }
        public bool IsCreatable { get; set; }
    }
}
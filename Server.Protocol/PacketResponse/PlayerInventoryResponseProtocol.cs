using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Game.PlayerInventory.Interface;
using MessagePack;
using Server.Event;
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

            //ExportInventoryLog(playerInventory);

            //メインインベントリのアイテムを設定
            var mainItems = new List<ItemMessagePack>();
            for (int i = 0; i < PlayerInventoryConst.MainInventorySize; i++)
            {
                var id = playerInventory.MainOpenableInventory.GetItem(i).Id;
                var count = playerInventory.MainOpenableInventory.GetItem(i).Count;
                mainItems.Add(new ItemMessagePack(id,count));
            }
            
            
            //グラブインベントリのアイテムを設定
            var grabItem = new ItemMessagePack(
                playerInventory.GrabInventory.GetItem(0).Id, 
                playerInventory.GrabInventory.GetItem(0).Count);

            
            //クラフトインベントリのアイテムを設定
            var craftItems = new List<ItemMessagePack>();
            for (int i = 0; i < PlayerInventoryConst.CraftingSlotSize; i++)
            {
                var id = playerInventory.CraftingOpenableInventory.GetItem(i).Id;
                var count = playerInventory.CraftingOpenableInventory.GetItem(i).Count;
                craftItems.Add(new ItemMessagePack(id,count));
            }
            
            //クラフト結果のアイテムを設定
            var craftItem = new ItemMessagePack(
                playerInventory.CraftingOpenableInventory.GetCreatableItem().Id, 
                playerInventory.CraftingOpenableInventory.GetCreatableItem().Count);
            
            var isCreatable = playerInventory.CraftingOpenableInventory.IsCreatable();

            var response = MessagePackSerializer.Serialize(new PlayerInventoryResponseProtocolMessagePack(
                data.PlayerId,mainItems.ToArray(),grabItem,craftItems.ToArray(),craftItem,isCreatable));
            

            return new List<List<byte>>() {response.ToList()};
        }


        private void ExportInventoryLog(PlayerInventoryData playerInventory)
        {
            var inventoryStr = new StringBuilder();
            inventoryStr.AppendLine("Main Inventory");
            

            //メインインベントリのアイテムを設定
            for (int i = 0; i < PlayerInventoryConst.MainInventorySize; i++)
            {
                var id = playerInventory.MainOpenableInventory.GetItem(i).Id;
                var count = playerInventory.MainOpenableInventory.GetItem(i).Count;

                inventoryStr.Append(id + " " + count + "  ");
                if ((i + 1) % PlayerInventoryConst.MainInventoryColumns == 0)
                {
                    inventoryStr.AppendLine();
                }
            }
            
            
            inventoryStr.AppendLine();
            inventoryStr.AppendLine("Grab Inventory");
            inventoryStr.AppendLine(playerInventory.GrabInventory.GetItem(0).Id + " " + playerInventory.GrabInventory.GetItem(0).Count + "  ");
            
            inventoryStr.AppendLine();
            inventoryStr.AppendLine("Craft Inventory");

            
            //クラフトインベントリのアイテムを設定
            for (int i = 0; i < PlayerInventoryConst.CraftingSlotSize; i++)
            {
                var id = playerInventory.CraftingOpenableInventory.GetItem(i).Id;
                var count = playerInventory.CraftingOpenableInventory.GetItem(i).Count;

                inventoryStr.Append(id + " " + count + "  ");
                if ((i + 1) % PlayerInventoryConst.CraftingInventoryColumns == 0)
                {
                    inventoryStr.AppendLine();
                }
            }
            
            
            inventoryStr.AppendLine("Craft Result Item");
            inventoryStr.AppendLine(playerInventory.CraftingOpenableInventory.GetCreatableItem().Id + " " + playerInventory.CraftingOpenableInventory.GetCreatableItem().Count + "  ");
            
        }
    }
    
    
    [MessagePackObject(keyAsPropertyName :true)]
    public class RequestPlayerInventoryProtocolMessagePack : ProtocolMessagePackBase
    {
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public RequestPlayerInventoryProtocolMessagePack() { }

        public RequestPlayerInventoryProtocolMessagePack(int playerId)
        {
            Tag = PlayerInventoryResponseProtocol.Tag;
            PlayerId = playerId;
        }

        public int PlayerId { get; set; }
    }
    
    
    [MessagePackObject(keyAsPropertyName :true)]
    public class PlayerInventoryResponseProtocolMessagePack : ProtocolMessagePackBase
    {
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public PlayerInventoryResponseProtocolMessagePack() { }


        public PlayerInventoryResponseProtocolMessagePack(int playerId, ItemMessagePack[] main, ItemMessagePack grab, ItemMessagePack[] craft, ItemMessagePack craftResult, bool isCreatable)
        {
            Tag = PlayerInventoryResponseProtocol.Tag;
            PlayerId = playerId;
            Main = main;
            Grab = grab;
            Craft = craft;
            CraftResult = craftResult;
            IsCreatable = isCreatable;
        }

        public int PlayerId { get; set; }
        
        public ItemMessagePack[] Main { get; set; }
        public ItemMessagePack Grab { get; set; }
        
        public ItemMessagePack[] Craft { get; set; }
        public ItemMessagePack CraftResult { get; set; }
        public bool IsCreatable { get; set; }
    }
}
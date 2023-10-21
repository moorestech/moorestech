using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Game.PlayerInventory.Interface;
using MessagePack;
using Server.Util.MessagePack;

namespace Server.Protocol.PacketResponse
{
    public class PlayerInventoryResponseProtocol : IPacketResponse
    {
        public const string Tag = "va:playerInvRequest";

        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;

        public PlayerInventoryResponseProtocol(IPlayerInventoryDataStore playerInventoryDataStore)
        {
            _playerInventoryDataStore = playerInventoryDataStore;
        }

        public List<List<byte>> GetResponse(List<byte> payload)
        {
            var data = MessagePackSerializer.Deserialize<RequestPlayerInventoryProtocolMessagePack>(payload.ToArray());

            var playerInventory = _playerInventoryDataStore.GetInventoryData(data.PlayerId);

            //ExportInventoryLog(playerInventory);

            
            var mainItems = new List<ItemMessagePack>();
            for (var i = 0; i < PlayerInventoryConst.MainInventorySize; i++)
            {
                var id = playerInventory.MainOpenableInventory.GetItem(i).Id;
                var count = playerInventory.MainOpenableInventory.GetItem(i).Count;
                mainItems.Add(new ItemMessagePack(id, count));
            }


            
            var grabItem = new ItemMessagePack(
                playerInventory.GrabInventory.GetItem(0).Id,
                playerInventory.GrabInventory.GetItem(0).Count);


            
            var craftItems = new List<ItemMessagePack>();
            for (var i = 0; i < PlayerInventoryConst.CraftingSlotSize; i++)
            {
                var id = playerInventory.CraftingOpenableInventory.GetItem(i).Id;
                var count = playerInventory.CraftingOpenableInventory.GetItem(i).Count;
                craftItems.Add(new ItemMessagePack(id, count));
            }

            
            var craftItem = new ItemMessagePack(
                playerInventory.CraftingOpenableInventory.GetCreatableItem().Id,
                playerInventory.CraftingOpenableInventory.GetCreatableItem().Count);

            var isCreatable = playerInventory.CraftingOpenableInventory.IsCreatable();

            var response = MessagePackSerializer.Serialize(new PlayerInventoryResponseProtocolMessagePack(
                data.PlayerId, mainItems.ToArray(), grabItem, craftItems.ToArray(), craftItem, isCreatable));


            return new List<List<byte>> { response.ToList() };
        }



        ///     

        public static void ExportInventoryLog(PlayerInventoryData playerInventory, bool isExportMain, bool isExportCraft, bool isExportGrab)
        {
            var inventoryStr = new StringBuilder();
            inventoryStr.AppendLine("Main Inventory");


            if (isExportMain)
                
                for (var i = 0; i < PlayerInventoryConst.MainInventorySize; i++)
                {
                    var id = playerInventory.MainOpenableInventory.GetItem(i).Id;
                    var count = playerInventory.MainOpenableInventory.GetItem(i).Count;

                    inventoryStr.Append(id + " " + count + "  ");
                    if ((i + 1) % PlayerInventoryConst.MainInventoryColumns == 0) inventoryStr.AppendLine();
                }

            inventoryStr.AppendLine();

            if (isExportGrab)
            {
                inventoryStr.AppendLine("Grab Inventory");
                inventoryStr.AppendLine(playerInventory.GrabInventory.GetItem(0).Id + " " + playerInventory.GrabInventory.GetItem(0).Count + "  ");
            }


            if (isExportCraft)
            {
                inventoryStr.AppendLine();
                inventoryStr.AppendLine("Craft Inventory");
                
                for (var i = 0; i < PlayerInventoryConst.CraftingSlotSize; i++)
                {
                    var id = playerInventory.CraftingOpenableInventory.GetItem(i).Id;
                    var count = playerInventory.CraftingOpenableInventory.GetItem(i).Count;

                    inventoryStr.Append(id + " " + count + "  ");
                    if ((i + 1) % PlayerInventoryConst.CraftingInventoryColumns == 0) inventoryStr.AppendLine();
                }

                inventoryStr.AppendLine("Craft Result Item");
                inventoryStr.AppendLine(playerInventory.CraftingOpenableInventory.GetCreatableItem().Id + " " + playerInventory.CraftingOpenableInventory.GetCreatableItem().Count + "  ");
            }

            Console.WriteLine(inventoryStr);
        }
    }


    [MessagePackObject(true)]
    public class RequestPlayerInventoryProtocolMessagePack : ProtocolMessagePackBase
    {
        [Obsolete("。。")]
        public RequestPlayerInventoryProtocolMessagePack()
        {
        }

        public RequestPlayerInventoryProtocolMessagePack(int playerId)
        {
            Tag = PlayerInventoryResponseProtocol.Tag;
            PlayerId = playerId;
        }

        public int PlayerId { get; set; }
    }


    [MessagePackObject(true)]
    public class PlayerInventoryResponseProtocolMessagePack : ProtocolMessagePackBase
    {
        [Obsolete("。。")]
        public PlayerInventoryResponseProtocolMessagePack()
        {
        }


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
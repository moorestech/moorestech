using System;
using System.Collections.Generic;
using System.Text;
using Game.PlayerInventory.Interface;
using Server.Util;

namespace Server.Protocol.PacketResponse
{
    public class PlayerInventoryResponseProtocol : IPacketResponse
    {
        private IPlayerInventoryDataStore _playerInventoryDataStore;

        public PlayerInventoryResponseProtocol(IPlayerInventoryDataStore playerInventoryDataStore)
        {
            _playerInventoryDataStore = playerInventoryDataStore;
        }

        public List<List<byte>> GetResponse(List<byte> payload)
        {
            var byteListEnumerator = new ByteListEnumerator(payload);
            byteListEnumerator.MoveNextToGetShort();
            var playerId = byteListEnumerator.MoveNextToGetInt();
            var playerInventory = _playerInventoryDataStore.GetInventoryData(playerId);

            var response = new List<byte>();
            response.AddRange(ToByteList.Convert((short) 4));
            response.AddRange(ToByteList.Convert(playerId));
            response.AddRange(ToByteList.Convert((short) 0));
            
            
            //ログ用のstring
            var inventory = new StringBuilder();
            inventory.Append("Main");
            inventory.Append("\n");

            //メインインベントリのアイテムを設定
            for (int i = 0; i < PlayerInventoryConst.MainInventorySize; i++)
            {
                response.AddRange(ToByteList.Convert(playerInventory.MainOpenableInventory.GetItem(i).Id));
                response.AddRange(ToByteList.Convert(playerInventory.MainOpenableInventory.GetItem(i).Count));
                
                inventory.Append(playerInventory.MainOpenableInventory.GetItem(i).Id + " " + playerInventory.MainOpenableInventory.GetItem(i).Count);
                inventory.Append("　");
                if ((i+1) % PlayerInventoryConst.MainInventoryColumns == 0)
                {
                    inventory.Append("\n");
                }
            }
            
            
            //グラブインベントリのアイテムを設定
            response.AddRange(ToByteList.Convert(playerInventory.GrabInventory.GetItem(0).Id));
            response.AddRange(ToByteList.Convert(playerInventory.GrabInventory.GetItem(0).Count));
            
            
            inventory.Append("\n");
            inventory.Append("Grab");
            inventory.Append("\n");
            inventory.Append(playerInventory.GrabInventory.GetItem(0).Id + " " + playerInventory.GrabInventory.GetItem(0).Count);
            inventory.Append("\n");
            inventory.Append("Craft");
            inventory.Append("\n");
            
            
            //クラフトインベントリのアイテムを設定
            for (int i = 0; i < PlayerInventoryConst.CraftingSlotSize; i++)
            {
                response.AddRange(ToByteList.Convert(playerInventory.CraftingOpenableInventory.GetItem(i).Id));
                response.AddRange(ToByteList.Convert(playerInventory.CraftingOpenableInventory.GetItem(i).Count));
                
                inventory.Append(playerInventory.CraftingOpenableInventory.GetItem(i).Id + " " + playerInventory.CraftingOpenableInventory.GetItem(i).Count);
                inventory.Append("　");
                if ((i+1) % PlayerInventoryConst.CraftingInventoryRows == 0)
                {
                    inventory.Append("\n");
                }
            }
            
            Console.WriteLine(inventory.ToString());
            
            //クラフト結果のアイテムを設定
            response.AddRange(ToByteList.Convert(playerInventory.CraftingOpenableInventory.GetCreatableItem().Id));
            response.AddRange(ToByteList.Convert(playerInventory.CraftingOpenableInventory.GetCreatableItem().Count));
            //クラフト可能かを設定
            if (playerInventory.CraftingOpenableInventory.IsCreatable())
            {
                response.Add(1);
            }
            else
            {
                response.Add(0);
            }

            return new List<List<byte>>() {response};
        }
    }
}
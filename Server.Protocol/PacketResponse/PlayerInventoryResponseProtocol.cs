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

            //メインインベントリのアイテムを設定
            for (int i = 0; i < PlayerInventoryConst.MainInventorySize; i++)
            {
                response.AddRange(ToByteList.Convert(playerInventory.MainOpenableInventory.GetItem(i).Id));
                response.AddRange(ToByteList.Convert(playerInventory.MainOpenableInventory.GetItem(i).Count));
            }
            
            
            //グラブインベントリのアイテムを設定
            response.AddRange(ToByteList.Convert(playerInventory.GrabInventory.GetItem(0).Id));
            response.AddRange(ToByteList.Convert(playerInventory.GrabInventory.GetItem(0).Count));


            //クラフトインベントリのアイテムを設定
            for (int i = 0; i < PlayerInventoryConst.CraftingSlotSize; i++)
            {
                response.AddRange(ToByteList.Convert(playerInventory.CraftingOpenableInventory.GetItem(i).Id));
                response.AddRange(ToByteList.Convert(playerInventory.CraftingOpenableInventory.GetItem(i).Count));
            }
            
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
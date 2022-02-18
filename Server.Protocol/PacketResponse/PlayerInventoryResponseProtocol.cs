using System.Collections.Generic;
using Game.PlayerInventory.Interface;
using Server.PacketHandle.PacketResponse;
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

        public List<byte[]> GetResponse(List<byte> payload)
        {
            var payloadData = new ByteArrayEnumerator(payload);
            payloadData.MoveNextToGetShort();
            var playerId = payloadData.MoveNextToGetInt();
            var playerInventory = _playerInventoryDataStore.GetInventoryData(playerId);

            var response = new List<byte>();
            response.AddRange(ToByteList.Convert((short) 4));
            response.AddRange(ToByteList.Convert(playerId));
            response.AddRange(ToByteList.Convert((short) 0));

            //メインインベントリのアイテムを設定
            for (int i = 0; i < PlayerInventoryConst.MainInventorySize; i++)
            {
                response.AddRange(ToByteList.Convert(playerInventory.MainInventory.GetItem(i).Id));
                response.AddRange(ToByteList.Convert(playerInventory.MainInventory.GetItem(i).Count));
            }
            
            //クラフトインベントリのアイテムを設定
            for (int i = 0; i < PlayerInventoryConst.CraftingInventorySize; i++)
            {
                response.AddRange(ToByteList.Convert(playerInventory.CraftingInventory.GetItem(i).Id));
                response.AddRange(ToByteList.Convert(playerInventory.CraftingInventory.GetItem(i).Count));
            }
            //クラフト結果のアイテムを設定
            response.AddRange(ToByteList.Convert(playerInventory.CraftingInventory.GetCreatableItem().Id));
            response.AddRange(ToByteList.Convert(playerInventory.CraftingInventory.GetCreatableItem().Count));
            //クラフト可能かを設定
            if (playerInventory.CraftingInventory.IsCreatable())
            {
                response.Add(1);
            }
            else
            {
                response.Add(0);
            }

            return new List<byte[]>() {response.ToArray()};
        }
    }
}
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Core.Block.Blocks;
using Core.Block.Blocks.Machine;
using Core.Block.Config;
using Core.Block.Config.LoadConfig.ConfigParamGenerator;
using Core.Block.Config.LoadConfig.Param;
using Core.Inventory;
using Game.World.Interface.DataStore;
using Server.Util;

namespace Server.Protocol.PacketResponse
{
    //TODO BlockInventoryRequestProtocolを作る
    public class BlockInventoryRequestProtocol : IPacketResponse
    {
        private const int ProtocolId = 6;
        private IWorldBlockDatastore _blockDatastore;
        private BlockConfig _blockConfig;

        //データのレスポンスを実行するdelegateを設定する
        private delegate byte[] InventoryResponse(int x, int y,IBlockConfigParam config);
        private Dictionary<string,InventoryResponse> _inventoryResponseDictionary = new();

        public BlockInventoryRequestProtocol(IWorldBlockDatastore blockDatastore, BlockConfig blockConfig)
        {
            _blockDatastore = blockDatastore;
            _blockConfig = blockConfig;
            
            //インベントリがあるブロックのレスポンスの設定
            _inventoryResponseDictionary.Add(VanillaBlockType.Machine,MachineInventoryResponse);
        }

        public List<byte[]> GetResponse(List<byte> payload)
        {
            var enumerator = new ByteArrayEnumerator(payload);
            enumerator.MoveNextToGetShort();
            var x = enumerator.MoveNextToGetInt();
            var y = enumerator.MoveNextToGetInt();
            
            var blockConfig = _blockConfig.GetBlockConfig(_blockDatastore.GetBlock(x, y).GetBlockId());
            
            //そのブロックがDictionaryに登録されてたらdelegateを実行して返す
            if (_inventoryResponseDictionary.ContainsKey(blockConfig.Type))
            {
                var response = _inventoryResponseDictionary[blockConfig.Type](x, y, blockConfig.Param);
                return new List<byte[]> {response.ToArray()};
            }

            return new List<byte[]>();
        }

        //アイテムやスロット数など基本的なデータを返す
        private List<byte> GetResponseBase(int x, int y)
        {
            var response = new List<byte>();
            var block = _blockDatastore.GetBlock(x, y) as IInventory;
            
            response.AddRange(ToByteList.Convert((short) ProtocolId));
            response.AddRange(ToByteList.Convert((short) block.GetSlotSize()));
            
            for (int i = 0; i < block.GetSlotSize(); i++)
            {
                var item = block.GetItem(i);
                
                response.AddRange(ToByteList.Convert(item.Id));
                response.AddRange(ToByteList.Convert(item.Count));
            }

            return response;
        }

        //Machine固有のレスポンスを行う
        private byte[] MachineInventoryResponse(int x,int y,IBlockConfigParam config)
        {
            var param = config as MachineBlockConfigParam;
            
            var response = GetResponseBase(x, y);
            response.AddRange(ToByteList.Convert((short) 1)); //UI type idが1
            response.AddRange(ToByteList.Convert((short)param.InputSlot));
            response.AddRange(ToByteList.Convert((short)param.OutputSlot));
            
            return response.ToArray();
        }
    }
}
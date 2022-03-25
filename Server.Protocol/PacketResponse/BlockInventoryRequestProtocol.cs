using System;
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
using Microsoft.Extensions.DependencyInjection;
using Server.Util;

namespace Server.Protocol.PacketResponse
{
    public class BlockInventoryRequestProtocol : IPacketResponse
    {
        private const int ProtocolId = 6;
        private IWorldBlockDatastore _blockDatastore;
        private IBlockConfig _blockConfig;

        //データのレスポンスを実行するdelegateを設定する
        private delegate byte[] InventoryResponse(int x, int y,IBlockConfigParam config);
        private Dictionary<string,InventoryResponse> _inventoryResponseDictionary = new();

        public BlockInventoryRequestProtocol(ServiceProvider serviceProvider)
        {
            _blockDatastore = serviceProvider.GetService<IWorldBlockDatastore>();
            _blockConfig = serviceProvider.GetService<IBlockConfig>();
            
            //インベントリがあるブロックのレスポンスの設定
            _inventoryResponseDictionary.Add(VanillaBlockType.Machine,MachineInventoryResponse);
            _inventoryResponseDictionary.Add(VanillaBlockType.Generator,GeneratorInventoryResponse);
            _inventoryResponseDictionary.Add(VanillaBlockType.Chest,ChestInventoryResponse);
        }

        public List<byte[]> GetResponse(List<byte> payload)
        {
            var byteListEnumerator = new ByteListEnumerator(payload);
            byteListEnumerator.MoveNextToGetShort();
            var x = byteListEnumerator.MoveNextToGetInt();
            var y = byteListEnumerator.MoveNextToGetInt();

            var blockId = _blockDatastore.GetBlock(x, y).GetBlockId();
            var blockConfig = _blockConfig.GetBlockConfig(blockId);
            
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
            var block = _blockDatastore.GetBlock(x, y) as IOpenableInventory;
            
            response.AddRange(ToByteList.Convert((short) ProtocolId));
            response.AddRange(ToByteList.Convert((short) block.GetSlotSize()));
            response.AddRange(ToByteList.Convert(_blockDatastore.GetBlock(x,y).GetBlockId()));
            
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
        //Generator固有のレスポンスを行う
        private byte[] GeneratorInventoryResponse(int x,int y,IBlockConfigParam config)
        {
            var param = config as PowerGeneratorConfigParam;
            
            var response = GetResponseBase(x, y);
            response.AddRange(ToByteList.Convert((short) 1)); //UI type idが1
            response.AddRange(ToByteList.Convert((short)param.FuelSlot));
            response.AddRange(ToByteList.Convert(0));
            
            return response.ToArray();
        }
        //Chest固有のレスポンスを行う
        private byte[] ChestInventoryResponse(int x,int y,IBlockConfigParam config)
        {
            var param = config as ChestConfigParam;
            
            var response = GetResponseBase(x, y);
            response.AddRange(ToByteList.Convert((short) 1)); //UI type idが1
            response.AddRange(ToByteList.Convert((short)param.ChestItemNum));
            response.AddRange(ToByteList.Convert((short)0));
            
            return response.ToArray();
        }
    }
}
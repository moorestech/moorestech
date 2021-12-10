using System;
using System.Collections.Generic;
using Core.Block;
using Game.World.Interface;
using Game.World.Interface.Event;
using Game.World.Interface.Util;
using World.Event;

namespace World
{
    /// <summary>
    /// ワールドに存在するブロックとその座標の対応づけを行います。
    /// </summary>
    public class WorldBlockDatastore : IWorldBlockDatastore
    {
        //メインのデータストア
        private Dictionary<int, BlockWorldData> _blockMasterDictionary = new Dictionary<int, BlockWorldData>();
        //座標とキーの紐づけ
        private Dictionary<Coordinate,int> _coordinateDictionary = new();

        
        readonly IBlock _nullBlock = new NullBlock(BlockConst.BlockConst.NullBlockId,Int32.MaxValue);
        private readonly BlockPlaceEvent _blockPlaceEvent;

        public WorldBlockDatastore(IBlockPlaceEvent blockPlaceEvent)
        {
            _blockPlaceEvent = (BlockPlaceEvent) blockPlaceEvent;
        }

        public bool AddBlock(IBlock block,int x,int y,IBlockInventory blockInventory)
        {
            //既にキーが登録されてないか、同じ座標にブロックを置こうとしてないかをチェック
            if (!_blockMasterDictionary.ContainsKey(block.GetIntId()) && !_coordinateDictionary.ContainsKey(CoordinateCreator.New(x,y)))
            {
                var c = CoordinateCreator.New(x,y);
                var data = new BlockWorldData(block, x, y,blockInventory);
                _blockMasterDictionary.Add(block.GetIntId(),data);
                _coordinateDictionary.Add(c,block.GetIntId());
                _blockPlaceEvent.OnBlockPutEventInvoke(new BlockPlaceEventProperties(c,data.Block));
                
                return true;
            }
            return false;
        }
        public IBlock GetBlock(int intId) { return _blockMasterDictionary.ContainsKey(intId) ? _blockMasterDictionary[intId].Block : _nullBlock; }
        public IBlockInventory GetBlockInventory(int intId) { return _blockMasterDictionary.ContainsKey(intId) ? _blockMasterDictionary[intId].BlockInventory : new NullIBlockInventory(); }
        public IBlockInventory GetBlockInventory(int x, int y) { return GetBlockInventory(GetIntId(x, y));}
        public IBlock GetBlock(int x,int y)
        {
            var c = CoordinateCreator.New(x,y);
            if (_coordinateDictionary.ContainsKey(c)) return _blockMasterDictionary[_coordinateDictionary[c]].Block;
            return _nullBlock;
        }
        public int GetIntId(int x, int y)
        {
            var c = CoordinateCreator.New(x,y);
            if (_coordinateDictionary.ContainsKey(c)) return _coordinateDictionary[c];
            return Int32.MaxValue;
        }
    }
}
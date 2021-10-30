using System;
using System.Collections.Generic;
using Core.Block;
using World.Event;
using World.Util;

namespace World.DataStore
{
    public class WorldBlockDatastore
    {
        //メインのデータストア
        private Dictionary<int, BlockWorldData> _blockMasterDictionary = new Dictionary<int, BlockWorldData>();
        //座標とキーの紐づけ
        private Dictionary<Coordinate,int> _coordinateDictionary = new();

        
        
        readonly Coordinate _nullCoordinate = CoordinateCreator.New(BlockConst.NullBlockIntId,BlockConst.NullBlockIntId);
        readonly IBlock _nullBlock = new NullBlock(BlockConst.NullBlockId,Int32.MaxValue);
        

        public bool AddBlock(IBlock Block,int x,int y) { return AddBlock(Block, x, y, new NullIBlockInventory()); }
        public bool AddBlock(IBlock Block,int x,int y,IBlockInventory blockInventory)
        {
            //既にキーが登録されてないか、同じ座標にブロックを置こうとしてないかをチェック
            if (!_blockMasterDictionary.ContainsKey(Block.GetIntId()) && !_coordinateDictionary.ContainsKey(CoordinateCreator.New(x,y)))
            {
                var c = CoordinateCreator.New(x,y);
                var data = new BlockWorldData(Block, x, y,blockInventory);
                _blockMasterDictionary.Add(Block.GetIntId(),data);
                _coordinateDictionary.Add(c,Block.GetIntId());
                BlockPlaceEvent.OnBlockPutEventInvoke(new BlockPlaceEventProperties(c,data.Block));
                
                return true;
            }
            return false;
        }
        public Coordinate GetCoordinate(int intId) { return _blockMasterDictionary.ContainsKey(intId) ? _blockMasterDictionary[intId].Coordinate : _nullCoordinate; }
        public IBlock GetBlock(int intId) { return _blockMasterDictionary.ContainsKey(intId) ? _blockMasterDictionary[intId].Block : _nullBlock; }
        public IBlockInventory GetBlockInventory(int intId) { return _blockMasterDictionary.ContainsKey(intId) ? _blockMasterDictionary[intId].BlockInventory : new NullIBlockInventory(); }
        public IBlock GetBlock(int x,int y)
        {
            var c = CoordinateCreator.New(x,y);
            if (_coordinateDictionary.ContainsKey(c)) return _blockMasterDictionary[_coordinateDictionary[c]].Block;
            return _nullBlock;
        }
    }
}
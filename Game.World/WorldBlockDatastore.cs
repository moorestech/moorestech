using System;
using System.Collections.Generic;
using Core.Block;
using Core.Block.BlockFactory;
using Core.Block.BlockInventory;
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
        private readonly Dictionary<int, BlockWorldData> _blockMasterDictionary = new();
        //座標とキーの紐づけ
        private readonly Dictionary<Coordinate,int> _coordinateDictionary = new();

        
        readonly IBlock _nullBlock = new NullBlock();
        private readonly BlockPlaceEvent _blockPlaceEvent;
        private readonly BlockFactory _blockFactory;

        public WorldBlockDatastore(IBlockPlaceEvent blockPlaceEvent, BlockFactory blockFactory)
        {
            _blockFactory = blockFactory;
            _blockPlaceEvent = (BlockPlaceEvent) blockPlaceEvent;
        }

        public bool AddBlock(IBlock block,int x,int y,BlockDirection blockDirection)
        {
            //既にキーが登録されてないか、同じ座標にブロックを置こうとしてないかをチェック
            if (!_blockMasterDictionary.ContainsKey(block.GetIntId()) && !_coordinateDictionary.ContainsKey(CoordinateCreator.New(x,y)))
            {
                var c = CoordinateCreator.New(x,y);
                var data = new BlockWorldData(block, x, y,blockDirection);
                _blockMasterDictionary.Add(block.GetIntId(),data);
                _coordinateDictionary.Add(c,block.GetIntId());
                _blockPlaceEvent.OnBlockPutEventInvoke(new BlockPlaceEventProperties(c,data.Block,blockDirection));
                
                return true;
            }
            return false;
        }
        public IBlock GetBlock(int x,int y)
        {
            var c = CoordinateCreator.New(x,y);
            if (_coordinateDictionary.ContainsKey(c)) return _blockMasterDictionary[_coordinateDictionary[c]].Block;
            return _nullBlock;
        }

        public BlockDirection GetBlockDirection(int x, int y)
        {
            var c = CoordinateCreator.New(x,y);
            if (_coordinateDictionary.ContainsKey(c)) return _blockMasterDictionary[_coordinateDictionary[c]].Direction;
            return BlockDirection.North;
        }

        public List<SaveBlockData> GetSaveBlockDataList()
        {
            var list = new List<SaveBlockData>();
            foreach (var block in _blockMasterDictionary)
            {
                list.Add(new SaveBlockData(
                    block.Value.X,
                    block.Value.Y,
                    block.Value.Block.GetBlockId(),
                    block.Value.Block.GetIntId(),
                    block.Value.Block.GetSaveState(),
                    (int)block.Value.Direction));
            }

            return list;
        }

        public void LoadBlockDataList(List<SaveBlockData> saveBlockDataList)
        {
            foreach (var block in saveBlockDataList)
            {
                AddBlock(
                    _blockFactory.Create(block.BlockId, block.IntId, block.State),
                    block.X,
                    block.Y,
                    (BlockDirection)block.Direction);
            }
        }
    }
}
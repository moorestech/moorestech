using System;
using System.Collections.Generic;
using industrialization.Core.Block;

namespace industrialization.OverallManagement.DataStore
{
    public static class WorldBlockDatastore
    {
        //メインのデータストア
        private static Dictionary<uint, BlockWorldData> _blockMasterDictionary = new Dictionary<uint, BlockWorldData>();
        //座標がキーのデータストア
        private static Dictionary<Coordinate,BlockWorldData> _coordinateDictionary = new Dictionary<Coordinate,BlockWorldData>();


        public static void ClearData()
        {
            _blockMasterDictionary = new Dictionary<uint, BlockWorldData>();
            _coordinateDictionary = new Dictionary<Coordinate,BlockWorldData>();
        }
        
        public static bool AddBlock(BlockBase Block,int x,int y)
        {
            //既にキーが登録されてないか、同じ座標にブロックを置こうとしてないかをチェック
            if (!ContainsKey(Block.IntId) &&
                !ContainsCoordinate(x,y))
            {
                var data = new BlockWorldData(Block, x, y);
                _blockMasterDictionary.Add(Block.IntId,data);
                _coordinateDictionary.Add(new Coordinate {x = x, y = y},data);

                return true;
            }

            return false;
        }

        public static bool ContainsKey(uint intId)
        {
            return _blockMasterDictionary.ContainsKey(intId);
        }

        public static Coordinate GetCoordinate(uint intId)
        {
            if (_blockMasterDictionary.ContainsKey(intId))
            {
                var i = _blockMasterDictionary[intId];
                return new Coordinate {x = i.X, y = i.Y};
            }

            return new Coordinate {x = Int32.MaxValue, y = Int32.MaxValue};
        }
        
        public static BlockBase GetBlock(uint intId)
        {
            if (_blockMasterDictionary.ContainsKey(intId))
            {
                return _blockMasterDictionary[intId].BlockBase;
            }

            return new NullBlock(0,Int32.MaxValue);
        }

        public static bool ContainsCoordinate(int x, int y)
        {
            var c = new Coordinate {x = x, y = y};
            return _coordinateDictionary.ContainsKey(c);
        }
        public static BlockBase GetBlock(int x,int y)
        {
            var c = new Coordinate {x = x, y = y};
            if (_coordinateDictionary.ContainsKey(c))
            {
                return _coordinateDictionary[c].BlockBase;
            }

            return null;
        }

        public static void RemoveBlock(BlockBase block)
        {
            if (_blockMasterDictionary.ContainsKey(block.IntId))
            {
                _blockMasterDictionary.Remove(block.IntId);
                var i = _blockMasterDictionary[block.IntId];
                _coordinateDictionary.Remove(new Coordinate {x=i.X, y = i.Y});
            }
        }
        

        class BlockWorldData
        {
            public BlockWorldData(BlockBase blockBase,int x, int y)
            {
                X = x;
                Y = y;
                BlockBase = blockBase;
            }

            public int X { get; }
            public int Y { get; }
            public BlockBase BlockBase { get; }
        
        
        }
    }

    public struct Coordinate
    {
        public int x;
        public int y;
    }
}
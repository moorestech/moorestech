using System;
using System.Collections.Generic;
using Core.Block;

namespace industrialization.OverallManagement.DataStore
{
    public static class WorldBlockDatastore
    {
        //メインのデータストア
        private static Dictionary<int, BlockWorldData> _blockMasterDictionary = new Dictionary<int, BlockWorldData>();
        //座標がキーのデータストア
        private static Dictionary<Coordinate,BlockWorldData> _coordinateDictionary = new Dictionary<Coordinate,BlockWorldData>();


        public static void ClearData()
        {
            _blockMasterDictionary = new Dictionary<int, BlockWorldData>();
            _coordinateDictionary = new Dictionary<Coordinate,BlockWorldData>();
        }
        
        public static bool AddBlock(IBlock Block,int x,int y)
        {
            //既にキーが登録されてないか、同じ座標にブロックを置こうとしてないかをチェック
            if (!ContainsKey(Block.GetIntId()) &&
                !ContainsCoordinate(x,y))
            {
                var data = new BlockWorldData(Block, x, y);
                _blockMasterDictionary.Add(Block.GetIntId(),data);
                _coordinateDictionary.Add(new Coordinate {x = x, y = y},data);

                return true;
            }

            return false;
        }

        public static bool ContainsKey(int intId)
        {
            return _blockMasterDictionary.ContainsKey(intId);
        }

        public static Coordinate GetCoordinate(int intId)
        {
            if (_blockMasterDictionary.ContainsKey(intId))
            {
                var i = _blockMasterDictionary[intId];
                return new Coordinate {x = i.X, y = i.Y};
            }

            return new Coordinate {x = Int32.MaxValue, y = Int32.MaxValue};
        }
        
        public static IBlock GetBlock(int intId)
        {
            if (_blockMasterDictionary.ContainsKey(intId))
            {
                return _blockMasterDictionary[intId].Block;
            }

            return new NullBlock(BlockConst.NullBlockId,Int32.MaxValue);
        }

        public static bool ContainsCoordinate(int x, int y)
        {
            var c = new Coordinate {x = x, y = y};
            return _coordinateDictionary.ContainsKey(c);
        }
        public static IBlock GetBlock(int x,int y)
        {
            var c = new Coordinate {x = x, y = y};
            if (_coordinateDictionary.ContainsKey(c))
            {
                return _coordinateDictionary[c].Block;
            }

            return new NullBlock(BlockConst.NullBlockId,0);
        }

        public static void RemoveBlock(IBlock block)
        {
            if (_blockMasterDictionary.ContainsKey(block.GetIntId()))
            {
                _blockMasterDictionary.Remove(block.GetIntId());
                var i = _blockMasterDictionary[block.GetIntId()];
                _coordinateDictionary.Remove(new Coordinate {x=i.X, y = i.Y});
            }
        }
        

        class BlockWorldData
        {
            public BlockWorldData(IBlock block,int x, int y)
            {
                X = x;
                Y = y;
                Block = block;
            }

            public int X { get; }
            public int Y { get; }
            public IBlock Block { get; }
        
        
        }
    }

    public struct Coordinate
    {
        public int x;
        public int y;
    }
}
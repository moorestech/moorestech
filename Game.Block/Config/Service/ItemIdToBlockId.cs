using System;
using System.Collections.Generic;
using Core.Const;
using Game.Block.Interface.BlockConfig;

namespace Game.Block.Config.Service
{
    public class ItemIdToBlockId
    {
        private readonly Dictionary<int, int> _idTable = new();


        ///     IDID

        public ItemIdToBlockId(IBlockConfig blockConfig)
        {
            for (var i = 1; i <= blockConfig.GetBlockConfigCount(); i++)
            {
                var itemId = blockConfig.GetBlockConfig(i).ItemId;
                if (itemId == ItemConst.EmptyItemId) continue;

                if (_idTable.ContainsKey(itemId))
                    throw new Exception("IDID。１ID2ID");

                _idTable.Add(itemId, i);
            }
        }

        public bool CanConvert(int itemId)
        {
            return _idTable.ContainsKey(itemId);
        }

        public int Convert(int itemId)
        {
            if (!_idTable.ContainsKey(itemId))
                throw new Exception("ID");

            return _idTable[itemId];
        }
    }
}
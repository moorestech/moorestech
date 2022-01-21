using System;
using System.Collections.Generic;

namespace Core.Block.Config.Service
{
    public class ItemIdToBlockId
    {

        private readonly Dictionary<int, int> _idTable;
        
        /// <summary>
        /// アイテムIDからブロックIDへ変換するテーブルを作成する
        /// </summary>
        public ItemIdToBlockId(BlockConfig blockConfig)
        {
            foreach (var id in blockConfig.GetBlockIds())
            {
                var itemId = blockConfig.GetBlockConfig(id).ItemId;
                
                if (_idTable.ContainsKey(itemId)) 
                    throw new Exception("アイテムIDが重複しています");
                
                _idTable.Add(itemId, id);
            }
            
        }
        
        public bool CanConvert(int itemId)
        {
            return _idTable.ContainsKey(itemId);
        }
        
        public int Convert(int itemId)
        {
            if (!_idTable.ContainsKey(itemId))
                throw new Exception("アイテムIDが存在しません");
            
            return _idTable[itemId];
        }
    }
}
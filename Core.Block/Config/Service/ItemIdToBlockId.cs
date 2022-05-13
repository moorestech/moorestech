using System;
using System.Collections.Generic;

namespace Core.Block.Config.Service
{
    public class ItemIdToBlockId
    {

        private readonly Dictionary<int, int> _idTable = new Dictionary<int, int>();
        
        /// <summary>
        /// アイテムIDからブロックIDへ変換するテーブルを作成する
        /// </summary>
        public ItemIdToBlockId(IBlockConfig blockConfig)
        {
            for (int i = 1; i < blockConfig.GetBlockConfigCount(); i++)
            {
                var itemId = blockConfig.GetBlockConfig(i).ItemId;
                
                if (_idTable.ContainsKey(itemId)) 
                    throw new Exception("アイテムIDからブロックIDへの対応付けに失敗。１つのアイテムIDが2つ以上のブロックが指定したアイテムIDと重複しています");

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
                throw new Exception("アイテムIDが存在しません");
            
            return _idTable[itemId];
        }
    }
}
using System.Collections.Generic;
using Client.Mod.Texture;
using Core.Master;
using UnityEngine;

namespace Client.Game.InGame.Context
{
    /// <summary>
    ///     ブロックのアイコン画像をBlockIdキーで管理するクラス
    ///     Holds block icon images keyed by BlockId
    /// </summary>
    public class BlockImageContainer
    {
        private readonly Dictionary<BlockId, ItemViewData> _blockImageList = new();

        public ItemViewData GetBlockView(BlockId blockId)
        {
            if (_blockImageList.TryGetValue(blockId, out var view)) return view;

            Debug.LogError($"BlockViewData not found. blockId:{blockId}");
            return null;
        }

        public void AddBlockView(BlockId blockId, ItemViewData itemViewData)
        {
            _blockImageList[blockId] = itemViewData;
        }
    }
}

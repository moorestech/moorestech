using System;
using System.Data.HashFunction;
using System.Data.HashFunction.xxHash;
using Core.Item.Interface.Config;

namespace Core.Item.Config
{
    public class ItemConfigData : IItemConfigData
    {
        internal ItemConfigData(ItemConfigJsonData jsonData, string modId, IxxHash xxHash, int itemId)
        {
            ModId = modId;
            ItemId = itemId;
            Name = jsonData.Name;
            MaxStack = jsonData.MaxStack;
            ImagePath = jsonData.ImagePath;
            ItemHash = 1;
            
            ItemHash = BitConverter.ToInt64(xxHash.ComputeHash(modId + "/" + Name).Hash);
        }
        
        /// <summary>
        ///     アイテムが定義されていないとき用のコンストラクタ
        /// </summary>
        public ItemConfigData(string name, int maxStack, string modId, int itemId)
        {
            Name = name;
            MaxStack = maxStack;
            ModId = modId;
            ItemId = itemId;
        }
        
        public long ItemHash { get; }
        public int ItemId { get; }
        
        public string ModId { get; }
        public string Name { get; }
        
        public int MaxStack { get; }
        public string ImagePath { get; }
    }
}
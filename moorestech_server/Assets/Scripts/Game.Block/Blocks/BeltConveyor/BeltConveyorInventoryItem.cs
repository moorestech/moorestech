﻿using Core.Item.Interface;

namespace Game.Block.Blocks.BeltConveyor
{
    public interface IOnBeltConveyorItem
    {
        public float RemainingPercent { get; }
        public int ItemId { get; }
        public ItemInstanceId ItemInstanceId { get; }
    }
    
    public class BeltConveyorInventoryItem : IOnBeltConveyorItem
    {
        public BeltConveyorInventoryItem(int itemId, ItemInstanceId itemInstanceId)
        {
            ItemId = itemId;
            ItemInstanceId = itemInstanceId;
            RemainingPercent = 1;
        }
        public int ItemId { get; }
        public ItemInstanceId ItemInstanceId { get; }
        /// <summary>
        ///     ベルトコンベア内のアイテムが出るまで残り何パーセントか
        /// </summary>
        public float RemainingPercent { get; set; }
    }
}
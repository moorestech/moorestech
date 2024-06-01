﻿namespace Game.Block.Blocks.BeltConveyor
{
    public class BeltConveyorInventoryItem
    {
        /// <summary>
        ///     ベルトコンベア内のアイテムがあと何秒で出るかを入れるプロパティ
        /// </summary>
        public double RemainingTime { get; set; }
        
        public readonly int ItemId;
        public readonly long ItemInstanceId;

        public BeltConveyorInventoryItem(int itemId, double remainingTime, long itemInstanceId)
        {
            ItemId = itemId;
            RemainingTime = remainingTime;
            ItemInstanceId = itemInstanceId;
        }
    }
}
using System;
namespace industrialization.Installation.BeltConveyor.Generally
{
    public class GenerallyBeltConveyorInventoryItem
    {
        public GenerallyBeltConveyorInventoryItem(int itemId,double removalAvailableTime)
        {
            ItemID = itemId;
            InsertTime = DateTime.Now;
            RemovalAvailableTime = DateTime.Now.AddSeconds(removalAvailableTime);
        }

        public DateTime InsertTime { get; }
        public DateTime RemovalAvailableTime { get; }
        public int ItemID { get; }
    }
}
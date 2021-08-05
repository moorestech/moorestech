namespace industrialization.Core.Installation.BeltConveyor.Generally
{
    public class BeltConveyorInventoryItem
    {
        public readonly int ItemId;
        public double LimitTime;
        public double RemainingTime
        {
            get => _remainingTime;
            set
            {
                if (LimitTime < RemainingTime)
                {
                    _remainingTime = value;
                }
            }
        }
        private double _remainingTime;
        public BeltConveyorInventoryItem(int itemId, double remainingTime, double limitTime)
        {
            ItemId = itemId;
            _remainingTime = remainingTime;
            LimitTime = limitTime;
        }
    }
}
namespace Game.Block.Blocks.BeltConveyor
{
    /// <summary>
    ///      <see cref="RemainingTime" /> 
    ///     0
    ///      <see cref="LimitTime" /> 
    ///     
    /// </summary>
    public class BeltConveyorInventoryItem
    {
        public readonly int ItemId;
        public readonly long ItemInstanceId;

        private double _remainingTime;

        public BeltConveyorInventoryItem(int itemId, double remainingTime, double limitTime, long itemInstanceId)
        {
            ItemId = itemId;
            _remainingTime = remainingTime;
            LimitTime = limitTime;
            ItemInstanceId = itemInstanceId;
        }


        ///     
        ///      =  + 
        ///      =  / 

        public double LimitTime { get; set; }


        ///     

        public double RemainingTime
        {
            get => _remainingTime;
            set
            {
                if (LimitTime < RemainingTime) _remainingTime = value;
            }
        }
    }
}
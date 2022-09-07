namespace Core.Block.Blocks.BeltConveyor
{
    /// <summary>
    /// ベルトコンベア内でアイテムがどれくらい進んだか？を表すのに残り時間 <see cref="RemainingTime"/> を使用している
    /// しかし、ただ残り時間を減らすだけでは、例えばベルトコンベア上でアイテムが渋滞しているときに渋滞関係なくそのまま残り時間が0になってしまう
    /// そのため、 <see cref="LimitTime"/> を定義し、「どこまでなら進んでいいか？」を表現する
    /// 残り時間は、リミット時間より時間を引くことは出来ないため、アイテムの渋滞を表現することができる
    /// </summary>
    public class BeltConveyorInventoryItem
    {
        public readonly int ItemId;
        public readonly long ItemInstanceId;
        
        /// <summary>
        /// アイテムの残り時間をどこまで減らしていいか？という制限
        /// アイテム残り時間制限 = 前のアイテムの残り秒数 + ベルトコンベアの挿入可能な間隔の秒数
        /// ベルトコンベアの挿入可能な間隔の秒数 = ベルトコンベアに入れれるアイテム数 / ベルトコンベアでアイテムが入ってから出るまでの秒数
        /// </summary>
        public double LimitTime { get; set; }

        /// <summary>
        /// ベルトコンベア内のアイテムがあと何秒で
        /// </summary>
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

        public BeltConveyorInventoryItem(int itemId, double remainingTime, double limitTime, long itemInstanceId)
        {
            ItemId = itemId;
            _remainingTime = remainingTime;
            LimitTime = limitTime;
            ItemInstanceId = itemInstanceId;
        }
    }
}
namespace Game.Block.Blocks.BeltConveyor
{
    /// <summary>
    ///     ベルトコンベア内でアイテムがどれくらい進んだか？を表すのに残り時間 <see cref="RemainingTime" /> を使用している
    ///     しかし、ただ残り時間を減らすだけでは、例えばベルトコンベア上でアイテムが渋滞しているときに渋滞関係なくそのまま残り時間が0になってしまう
    ///     そのため、 <see cref="LimitTime" /> を定義し、「どこまでなら進んでいいか？」を表現する
    ///     残り時間は、リミット時間より時間を引くことは出来ないため、アイテムの渋滞を表現することができる
    /// </summary>
    public class BeltConveyorInventoryItem
    {
        public readonly int ItemId;
        public readonly long ItemInstanceId;

        /// <summary>
        /// ベルトコンベア内のアイテムがあと何秒で出るかを入れるプロパティ
        /// </summary>
        public double RemainingTime { get; set; }

        public BeltConveyorInventoryItem(int itemId, double remainingTime, long itemInstanceId)
        {
            ItemId = itemId;
            RemainingTime = remainingTime;
            ItemInstanceId = itemInstanceId;
        }
    }
}
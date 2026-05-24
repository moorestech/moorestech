using MessagePack;

namespace Game.PlayerRiding.Interface
{
    /// <summary>
    /// 乗り物識別子を保持するMessagePackクラス
    /// A MessagePack class that holds a ridable identifier.
    /// </summary>
    [MessagePackObject]
    public class RidableIdentifierMessagePack
    {
        [Key(0)] public RidableType RidableType { get; set; }

        [Key(1)] public long TrainCarInstanceId { get; set; }

        public RidableIdentifierMessagePack() { }

        public static RidableIdentifierMessagePack CreateTrainCarMessage(long trainCarInstanceId)
        {
            return new RidableIdentifierMessagePack
            {
                RidableType = RidableType.TrainCar,
                TrainCarInstanceId = trainCarInstanceId,
            };
        }
    }
}

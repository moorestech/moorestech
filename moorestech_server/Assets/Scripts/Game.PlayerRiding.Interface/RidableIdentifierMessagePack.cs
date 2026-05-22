using MessagePack;

namespace Game.PlayerRiding.Interface
{
    /// <summary>
    /// 乗り物識別子を保持するMessagePackクラス。種別判別子＋型別ペイロード方式。
    /// MessagePack class that holds a ridable identifier. Type discriminator plus per-type payload.
    /// </summary>
    [MessagePackObject]
    public class RidableIdentifierMessagePack
    {
        // 乗り物種別の判別子（RidableType の primitive 文字列）。
        // Discriminator of the ridable kind (the RidableType primitive string).
        [Key(0)] public string RidableType { get; set; }

        /// <summary>
        /// 列車車両の場合の TrainCarInstanceId（long を文字列化）。
        /// TrainCarInstanceId for the train-car case (long stored as string).
        /// </summary>
        [Key(1)] public string TrainCarInstanceId { get; set; }

        public RidableIdentifierMessagePack() { }

        public static RidableIdentifierMessagePack CreateTrainCarMessage(long trainCarInstanceId)
        {
            return new RidableIdentifierMessagePack
            {
                RidableType = Game.PlayerRiding.Interface.RidableType.TrainCar.AsPrimitive(),
                TrainCarInstanceId = trainCarInstanceId.ToString(),
            };
        }
    }
}

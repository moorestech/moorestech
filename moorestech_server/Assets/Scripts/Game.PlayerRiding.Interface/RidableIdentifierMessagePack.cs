using MessagePack;

namespace Game.PlayerRiding.Interface
{
    // 乗り物の種類。InventoryType に倣う enum discriminator。
    // Kind of ridable. An enum discriminator, mirroring InventoryType.
    public enum RidableType : byte
    {
        TrainCar,
    }

    /// <summary>
    /// 乗り物識別子を保持するMessagePackクラス。enum discriminator＋型別ペイロード方式。
    /// MessagePack class that holds a ridable identifier. Enum discriminator plus per-type payload.
    /// </summary>
    [MessagePackObject]
    public class RidableIdentifierMessagePack
    {
        [Key(0)] public RidableType RidableType { get; set; }

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
                RidableType = RidableType.TrainCar,
                TrainCarInstanceId = trainCarInstanceId.ToString(),
            };
        }
    }
}

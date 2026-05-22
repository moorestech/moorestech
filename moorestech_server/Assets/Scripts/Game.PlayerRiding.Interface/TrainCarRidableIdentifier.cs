namespace Game.PlayerRiding.Interface
{
    // 列車車両を指す識別子。TrainInventorySubInventoryIdentifier に倣う。
    // Identifier that points at a train car. Mirrors TrainInventorySubInventoryIdentifier.
    public class TrainCarRidableIdentifier : IRidableIdentifier
    {
        public RidableType Type => RidableType.TrainCar;

        // アセンブリ循環を避けるため TrainCarInstanceId 構造体ではなく long を保持する
        // Holds a long instead of the TrainCarInstanceId struct to avoid an assembly cycle.
        public long TrainCarInstanceId { get; }

        public TrainCarRidableIdentifier(long trainCarInstanceId)
        {
            TrainCarInstanceId = trainCarInstanceId;
        }

        // セーブ用ペイロード: 車両インスタンスIDの十進文字列
        // Save payload: the decimal string of the train car instance id.
        public string GetSaveState()
        {
            return TrainCarInstanceId.ToString();
        }

        public override bool Equals(object obj)
        {
            if (obj is TrainCarRidableIdentifier other)
            {
                return TrainCarInstanceId.Equals(other.TrainCarInstanceId);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return TrainCarInstanceId.GetHashCode();
        }
    }
}

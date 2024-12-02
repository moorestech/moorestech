namespace Game.Train.Train
{
    /// <summary>
    /// 列車車両の基本クラス。すべての車両に共通の情報を保持。
    /// </summary>
    public abstract class TrainCarBase
    {
        // 前後のノードと距離情報
        public (RailNode from, RailNode to) FrontWheelPosition { get; set; }
        public (RailNode from, RailNode to) RearWheelPosition { get; set; }
        public int FrontWheelDistanceFromStart { get; set; }
        public int RearWheelDistanceFromStart { get; set; }

        // コンストラクタ
        protected TrainCarBase()
        {
            FrontWheelPosition = (null, null);
            RearWheelPosition = (null, null);
            FrontWheelDistanceFromStart = 0;
            RearWheelDistanceFromStart = 0;
        }
    }
}
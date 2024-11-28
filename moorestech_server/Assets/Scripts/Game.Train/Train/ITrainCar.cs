namespace Game.Train.Train
{
    /// <summary>
    /// 単一の車両を表すインターフェース
    /// </summary>
    public interface ITrainCar
    {
        public TrainCarType TrainCarType { get; }
    }
    
    public enum TrainCarType
    {
        Locomotive, // 動力車
        Freight // 貨車
    }
}
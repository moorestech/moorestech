using Game.Block.Interface.Component;
using Game.Train.Train;
namespace Game.Train.Blocks
{
    public class StationComponent : IBlockComponent
    {
        public string StationName { get; }

        // 駅の長さ（何両分か）
        private int _stationLength;

        // 現在使用中の列車単位
        private TrainUnit _currentTrain;

        // IBlockComponentからのメンバ
        public bool IsDestroy { get; private set; }

        public StationComponent(int stationLength, string stationName = "DefaultStation")
        {
            _stationLength = stationLength;
            _currentTrain = null;
            IsDestroy = false;
            StationName = stationName;
        }


        // 列車が駅に到着したときの処理
        public bool TrainArrived(TrainUnit train)
        {
            // すでに列車がいる場合は何もしない
            if (_currentTrain != null)
            {
                return false;
            }

            // 列車が駅に入る
            _currentTrain = train;
            return true;
        }

        // 列車が駅から出発したときの処理
        public bool TrainDeparted(TrainUnit train)
        {
            // 列車がいない場合は何もしない
            if (_currentTrain == null)
            {
                return false;
            }

            // 列車が駅から出る
            _currentTrain = null;
            return true;
        }


        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}
using System.Collections.Generic;

namespace Game.Train.Train
{
    /// <summary>
    /// 列車一編成を表すクラス
    /// 現在地はRailPositionという抽象的なクラスで表すことに注意
    /// 
    /// </summary>
    public class TrainUnit
    {
        // 列車の編成
        private List<TrainCar> _trainFormation;
        private int _currentSpeed;
        private bool _isRunning;

        // 車両を追加
        public void AddTrainCar(TrainCar trainCar)
        {
        }

        // 列車全体の速度を計算
        public void CalcSpeed(int speed)
        {
            _currentSpeed = speed;
        }

    }
}
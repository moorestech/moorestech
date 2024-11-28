using UnityEngine.Pool;

namespace Game.Train.Train
{
    /// <summary>
    /// 列車一編成を表すクラス
    /// A class that represents a single train
    /// </summary>
    public class TrainUnit
    {
        // この列車の編成
        // The formation of this train
        private ListPool<ITrainCar> _trainFormation = new();
    }
}
using Game.Block.Interface.Component;
using Game.Train.RailGraph;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Game.Block.Blocks.TrainRail
{
    /// <summary>
    /// セーブ・ロード用コンポーネント
    /// レールに関わる複数の RailComponent のセーブ／ロードを一括管理する
    /// </summary>
    public class RailSaverComponent : IBlockComponent, IBlockSaveState
    {
        /// <summary>
        /// RailComponentをまとめてセーブするためのコンポーネント
        /// 1つのブロックに2つのRailComponentを持つ可能性がある(例: 駅, 貨物プラットフォームなど)
        /// </summary>
        public bool IsDestroy { get; private set; }
        public string SaveKey => "RailSaverComponent";

        public RailComponent[] RailComponents { get; private set; }

        public RailSaverComponent(RailComponent[] railComponents)
        {
            RailComponents = railComponents;
        }

        public string GetSaveState()
        {
            var railSaverData = new RailSaverData
            {
                Values = new List<RailComponentInfo>()
            };

            foreach (var railComponent in RailComponents)
            {
                railSaverData.Values.Add(railComponent.GetPartialSaveState());
            }
            return JsonConvert.SerializeObject(railSaverData);
        }

        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}

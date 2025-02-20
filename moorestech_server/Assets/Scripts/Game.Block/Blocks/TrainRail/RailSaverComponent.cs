using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Train.RailGraph;
using Game.Train.Utility;
using UnityEngine;

namespace Game.Block.Blocks.TrainRail
{
    public class RailSaverComponent : IBlockSaveState
    {

        /// <summary>
        /// RailComponentがついてるブロックに必ず付属するコンポーネント
        /// セーブ・ロードに関しては1つのブロックが2つのRailComponentを持つ可能性があるため"RailSaverComponent.cs"が担当
        /// 具体的には駅や貨物プラットフォームブロック
        /// </summary>

        public bool IsDestroy { get; private set; }
        public string SaveKey => "RailSaverComponent";

        public RailSaverComponent(RailComponent[] railComponents)
        {
        }


        public string GetSaveState()
        {
            return "";
        }

        public void Destroy()
        {
            IsDestroy = true;
        }

    }
}

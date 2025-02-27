using Game.Block.Interface.Component;
using Game.Train.RailGraph;
using Newtonsoft.Json;
using System.Collections.Generic;


namespace Game.Block.Blocks.TrainRail
{
    public class RailSaverComponent : IBlockComponent, IBlockSaveState
    {
        /// <summary>
        /// RailComponentがついてるブロックに必ず付属するコンポーネント
        /// セーブ・ロードに関しては1つのブロックが2つのRailComponentを持つ可能性があるため"RailSaverComponent.cs"が担当
        /// 具体的には駅や貨物プラットフォームブロック
        /// </summary>
        public bool IsDestroy { get; private set; }
        public string SaveKey => "RailSaverComponent";
        

        public RailComponent[] railComponents { get; private set; }

        public RailSaverComponent(RailComponent[] railComponents_)
        {
            railComponents = railComponents_;
        }

        public string GetSaveState()
        {
            RailSaverData _saveData = new RailSaverData();
            _saveData.values = new List<RailComponentInfo>();
            foreach (var railComponent in railComponents)
            {
                _saveData.values.Add(railComponent.GetSaveState_Partial());
            }
            return JsonConvert.SerializeObject(_saveData);
        }


        public void Destroy()
        {
            IsDestroy = true;
        }

    }
}

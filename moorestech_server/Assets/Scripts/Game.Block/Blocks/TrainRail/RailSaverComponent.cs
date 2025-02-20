using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Train.RailGraph;
using Game.Train.Utility;
using UnityEngine;

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
            //必要な情報はrailComponentごとに
            //自分のRailComponentID
            //自分のFrontNodeがつながる先のRailNodeに紐づいているRailComponentIDのリスト "_"区切り
            //自分のBackNodeがつながる先のRailNodeに紐づいているRailComponentIDのリスト "_"区切り
            //上3つを":"区切り
            //railComponentの間は";"区切り
            string saveData = "";
            foreach (var railComponent in railComponents)
            {
                saveData += railComponent.railComponentID.GetSaveState();
                saveData += ":";
                var f_nodeList = railComponent.FrontNode.ConnectedNodes;
                foreach (var f_node in f_nodeList)
                {
                    var f_node_railComponent = RailGraphDatastore.GetRailComponentID(f_node);
                    saveData += f_node_railComponent.GetSaveState() + "_";
                }
                saveData += ":";
                var b_nodeList = railComponent.BackNode.ConnectedNodes;
                foreach (var b_node in b_nodeList)
                {
                    var b_node_railComponent = RailGraphDatastore.GetRailComponentID(b_node);
                    saveData += b_node_railComponent.GetSaveState() + "_";
                }
                saveData += ":";
                saveData += ";";
            }
            return saveData;
        }

        public void Destroy()
        {
            IsDestroy = true;
        }

    }
}

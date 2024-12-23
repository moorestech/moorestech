using Game.Block.Interface.Component;
using Game.Train.RailGraph;

namespace Game.Train.Blocks
{
    /// <summary>
    /// レールの基本構成要素を表すクラス。
    /// レールに関連する機能を提供。
    /// </summary>
    public class RailComponent : IBlockComponent
    {
        // レールが破壊されたかどうかを示すフラグ
        public bool IsDestroy { get; private set; }

        // このレールに関連付けられているRailNode（表と裏）
        public RailNode FrontNode { get; private set; }
        public RailNode BackNode { get; private set; }

        // コンストラクタ
        public RailComponent(RailGraphDatastore railGraph)
        {
            // RailGraphにノードを登録
            FrontNode = new RailNode(railGraph);
            BackNode = new RailNode(railGraph);
            FrontNode.SetOppositeNode(BackNode);
            BackNode.SetOppositeNode(FrontNode);

            // RailGraphに登録
            railGraph.AddNode(FrontNode);
            railGraph.AddNode(BackNode);
        }

        /// <summary>
        /// このレールを破壊する処理
        /// </summary>
        public void Destroy()
        {
            IsDestroy = true;
        }

    }
}

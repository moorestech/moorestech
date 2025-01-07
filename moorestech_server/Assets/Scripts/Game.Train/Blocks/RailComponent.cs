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
        //3D上のブロック座標
        public UnityEngine.Vector3Int Position { get; private set; }

        // コンストラクタ
        public RailComponent()
        {
            // RailGraphにノードを登録
            FrontNode = new RailNode();
            BackNode = new RailNode();
            FrontNode.SetOppositeNode(BackNode);
            BackNode.SetOppositeNode(FrontNode);
        }

        //レール接続作業、手動でつなげたときを想定。自分の表or裏を起点に相手の表or裏につながる
        //isFront_this:true 自分の表から相手のxに入る
        //isFront_target:true 相手の表に入る
        public void ConnectRailComponent(RailComponent targetRail, bool isFront_this, bool isFront_target)
        {
            if ((isFront_this == true) & (isFront_target == true))//表自→表相、裏相→表自
            {
                FrontNode.ConnectNode(targetRail.FrontNode, 1);
                targetRail.BackNode.ConnectNode(BackNode, 1);
            }
            else if ((isFront_this == true) & (isFront_target == false))//表自→裏相、表相→裏自
            {
                FrontNode.ConnectNode(targetRail.BackNode, 1);
                targetRail.FrontNode.ConnectNode(BackNode, 1);
            }
            else if ((isFront_this == false) & (isFront_target == true))//裏自→表相、裏相→表自
            {
                BackNode.ConnectNode(targetRail.FrontNode, 1);
                targetRail.BackNode.ConnectNode(FrontNode, 1);
            }
            else if ((isFront_this == false) & (isFront_target == false))//裏自→裏相、表相→表自
            {
                BackNode.ConnectNode(targetRail.BackNode, 1);
                targetRail.FrontNode.ConnectNode(FrontNode, 1);
            }
        }

        // このレールを破壊する処理
        public void Destroy()
        {
            IsDestroy = true;
            FrontNode.Destroy();
            BackNode.Destroy();
            FrontNode = null;
            BackNode = null;
        }

    }
}

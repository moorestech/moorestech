using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Train.RailGraph;
using Game.Train.Utility;
using UnityEngine;

namespace Game.Block.Blocks.TrainRail
{
    /// <summary>
    /// レールの基本構成要素を表すクラス。
    /// レールに関連する機能を提供。
    /// 基本1つのRailComponentが2つのRailNodeを持つ=裏表
    /// </summary>
    public class RailComponent : IBlockComponent
    {
        // レールが破壊されたかどうかを示すフラグ
        public bool IsDestroy { get; private set; }

        // このレールに関連付けられているRailNode（表と裏）
        public RailNode FrontNode { get; private set; }
        public RailNode BackNode { get; private set; }
        //3D上のブロック座標など
        private BlockPositionInfo blockPositionInfo;
        //制御点計算用ベジェ曲線微分強度
        private float bezierStrength { get; set; } = 0.5f;//今後手動でいじれるようにするかも

        // コンストラクタ
        public RailComponent(BlockPositionInfo blockPositionInfo_)
        {
            blockPositionInfo = blockPositionInfo_;
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
            //距離を算出
            var p0 = (Vector3)GetPosition();//自分のアンカーポイント
            var p1 = GetControlPoint(isFront_this);//自分の制御点
            var p2 = targetRail.GetControlPoint(!isFront_target);//相手の制御点
            var p3 = (Vector3)targetRail.GetPosition();//相手のアンカーポイント
            int distance = (int)(BezierUtility.GetBezierCurveLength(p0, p1, p2, p3) * BezierUtility.RAIL_LENGTH_SCALE + 0.5f);


            if ((isFront_this == true) & (isFront_target == true))//表自→表相、裏相→表自
            {
                FrontNode.ConnectNode(targetRail.FrontNode, distance);
                targetRail.BackNode.ConnectNode(BackNode, distance);
            }
            else if ((isFront_this == true) & (isFront_target == false))//表自→裏相、表相→裏自
            {
                FrontNode.ConnectNode(targetRail.BackNode, distance);
                targetRail.FrontNode.ConnectNode(BackNode, distance);
            }
            else if ((isFront_this == false) & (isFront_target == true))//裏自→表相、裏相→表自
            {
                BackNode.ConnectNode(targetRail.FrontNode, distance);
                targetRail.BackNode.ConnectNode(FrontNode, distance);
            }
            else if ((isFront_this == false) & (isFront_target == false))//裏自→裏相、表相→表自
            {
                BackNode.ConnectNode(targetRail.BackNode, distance);
                targetRail.FrontNode.ConnectNode(FrontNode, distance);
            }
        }

        public void DisconnectRailComponent(RailComponent targetRail, bool isFront_this, bool isFront_target)
        {
            if ((isFront_this == true) & (isFront_target == true))//表自→表相、裏相→表自
            {
                FrontNode.DisconnectNode(targetRail.FrontNode);
                targetRail.BackNode.DisconnectNode(BackNode);
            }
            else if ((isFront_this == true) & (isFront_target == false))//表自→裏相、表相→裏自
            {
                FrontNode.DisconnectNode(targetRail.BackNode);
                targetRail.FrontNode.DisconnectNode(BackNode);
            }
            else if ((isFront_this == false) & (isFront_target == true))//裏自→表相、裏相→表自
            {
                BackNode.DisconnectNode(targetRail.FrontNode);
                targetRail.BackNode.DisconnectNode(FrontNode);
            }
            else if ((isFront_this == false) & (isFront_target == false))//裏自→裏相、表相→表自
            {
                BackNode.DisconnectNode(targetRail.BackNode);
                targetRail.FrontNode.DisconnectNode(FrontNode);
            }
        }

        // 自分の座標vector3intを返す
        public Vector3Int GetPosition()
        {
            return blockPositionInfo.OriginalPos;
        }
        // 自分の向いている方角とbezierStrengthから制御点を計算してvector3で返す
        public Vector3 GetControlPoint(bool isFront)
        {
            var dir = blockPositionInfo.BlockDirection;
            //ここではdirがnorth,east,south,westのみを想定
            Vector3 direction = new Vector3();
            switch (dir)
            {
                case BlockDirection.North:
                    direction = new Vector3(0, 0, 1);
                    break;
                case BlockDirection.East:
                    direction = new Vector3(1, 0, 0);
                    break;
                case BlockDirection.South:
                    direction = new Vector3(0, 0, -1);
                    break;
                case BlockDirection.West:
                    direction = new Vector3(-1, 0, 0);
                    break;
            }
            //isFrontがfalseの場合は反対側に制御点を設定
            if (isFront == false)
            {
                return blockPositionInfo.OriginalPos - direction * bezierStrength;//多分vector3(0.5,0.5,0.5)が足されて中心になる
            }
            else
            {
                return blockPositionInfo.OriginalPos + direction * bezierStrength;
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

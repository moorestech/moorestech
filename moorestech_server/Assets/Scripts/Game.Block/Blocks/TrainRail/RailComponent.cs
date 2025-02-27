using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Train.RailGraph;
using Game.Train.Utility;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Block.Blocks.TrainRail
{
    /// <summary>
    /// レールブロック1つにこのコンポーネントが1つ対応する
    /// レールの基本構成要素を表すクラス。
    /// レールに関連する機能を提供。
    /// 基本1つのRailComponentが2つのRailNodeを持つ=裏表
    /// セーブ・ロードに関しては1つのブロックが2つのRailComponentを持つ可能性があるため"RailSaverComponent.cs"が担当
    /// </summary>
    public class RailComponent : IBlockComponent
    {
        // レールが破壊されたかどうかを示すフラグ
        public bool IsDestroy { get; private set; }

        // このレールに関連付けられているRailNode（表と裏）
        public RailNode FrontNode { get; private set; }
        public RailNode BackNode { get; private set; }
        //ブロックではなくこのRailComponentの存在するべきワールド座標
        private BlockPositionInfo componentPositionInfo;
        //制御点計算用ベジェ曲線微分強度
        private float bezierStrength = 0.5f;//今後手動でいじれるようにするかも

        public RailControlPoint Front_railControlPoint { get; }
        public RailControlPoint Back_railControlPoint { get; }
        public RailComponentID railComponentID { get; }//中身はblockの座標とRailSeverComponentからみた自分のID

        // コンストラクタ
        public RailComponent(BlockPositionInfo componentPositionInfo_, RailComponentID railComponentID_ = null)
        {
            railComponentID = railComponentID_;
            componentPositionInfo = componentPositionInfo_;
            Front_railControlPoint = new RailControlPoint(componentPositionInfo.OriginalPos, GetControlPoint(true));
            Back_railControlPoint = new RailControlPoint(componentPositionInfo.OriginalPos, GetControlPoint(false));

            // RailGraphにノードを登録
            FrontNode = new RailNode();
            BackNode = new RailNode();
            
            RailGraphDatastore.AddRailComponentID(FrontNode, new ConnectionDestination(railComponentID, true));
            RailGraphDatastore.AddRailComponentID(BackNode, new ConnectionDestination(railComponentID, false));
            //RailNodeは必ずしも反対Nodeを持つ必要はないが、RailComponentで生成したものに関しては持つ
            FrontNode.SetOppositeNode(BackNode);
            BackNode.SetOppositeNode(FrontNode);
            //RailNodeの座標(RailControlPoint)は必ずしも持つ必要はないが、RailComponentで生成したものに関しては持つ
            FrontNode.SetRailControlPoints(Front_railControlPoint, Back_railControlPoint);
            BackNode.SetRailControlPoints(Back_railControlPoint, Front_railControlPoint);////裏Nodeにとって進行方向の制御点はback_railControlPointである
        }

        //レール接続作業、手動でつなげたときを想定。自分の表or裏を起点に相手の表or裏につながる
        //isFront_this:true 自分の表から相手のxに入る
        //isFront_target:true 相手の表に入る
        //defaultdistanceはデバッグ時以外基本していしない
        public void ConnectRailComponent(RailComponent targetRail, bool isFront_this, bool isFront_target, int defaultdistance = -1)
        {
            //距離を算出
            var myControlPoint = isFront_this ? Front_railControlPoint : Back_railControlPoint;
            var targetControlPoint = isFront_target ? targetRail.Back_railControlPoint : targetRail.Front_railControlPoint;

            int distance;
            if (defaultdistance != -1)
            {
                distance = defaultdistance;
            }
            else 
            {
                distance = (int)(BezierUtility.GetBezierCurveLength(myControlPoint, targetControlPoint) * BezierUtility.RAIL_LENGTH_SCALE + 0.5f);
            }


            if ((isFront_this == true) & (isFront_target == true))//表自→表相、裏相→裏自
            {
                FrontNode.ConnectNode(targetRail.FrontNode, distance);
                targetRail.ConnectRailComponent(this, false, false, distance);
                //targetRail.BackNode.ConnectNode(BackNode, distance);
            }
            else if ((isFront_this == true) & (isFront_target == false))//表自→裏相、表相→裏自
            {
                FrontNode.ConnectNode(targetRail.BackNode, distance);
                targetRail.ConnectRailComponent(this, true, false, distance);
                //targetRail.FrontNode.ConnectNode(BackNode, distance);
            }
            else if ((isFront_this == false) & (isFront_target == true))//裏自→表相、裏相→表自
            {
                BackNode.ConnectNode(targetRail.FrontNode, distance);
                targetRail.ConnectRailComponent(this, false, true, distance);
                //targetRail.BackNode.ConnectNode(FrontNode, distance);
            }
            else if ((isFront_this == false) & (isFront_target == false))//裏自→裏相、表相→表自
            {
                BackNode.ConnectNode(targetRail.BackNode, distance);
                targetRail.ConnectRailComponent(this, true, true, distance);
                //targetRail.FrontNode.ConnectNode(FrontNode, distance);
            }
        }

        public void DisconnectRailComponent(RailComponent targetRail, bool isFront_this, bool isFront_target)
        {
            if ((isFront_this == true) & (isFront_target == true))//表自→表相、裏相→裏自
            {
                FrontNode.DisconnectNode(targetRail.FrontNode);
                targetRail.DisconnectRailComponent(this, false, false);
                //targetRail.BackNode.DisconnectNode(BackNode);
            }
            else if ((isFront_this == true) & (isFront_target == false))//表自→裏相、表相→裏自
            {
                FrontNode.DisconnectNode(targetRail.BackNode);
                targetRail.DisconnectRailComponent(this, true, false);
                //targetRail.FrontNode.DisconnectNode(BackNode);
            }
            else if ((isFront_this == false) & (isFront_target == true))//裏自→表相、裏相→表自
            {
                BackNode.DisconnectNode(targetRail.FrontNode);
                targetRail.DisconnectRailComponent(this, false, true);
                //targetRail.BackNode.DisconnectNode(FrontNode);
            }
            else if ((isFront_this == false) & (isFront_target == false))//裏自→裏相、表相→表自
            {
                BackNode.DisconnectNode(targetRail.BackNode);
                targetRail.DisconnectRailComponent(this, true, true);
                //targetRail.FrontNode.DisconnectNode(FrontNode);
            }
        }


        public void ChangeBezierStrength(float val)
        {
            bezierStrength = val;
            Front_railControlPoint.ControlPointPosition = GetControlPoint(true);//更新
            Back_railControlPoint.ControlPointPosition = GetControlPoint(false);//更新
            //RailComponentが管理するRailNodeの制御点もこれで更新されている
        }

        // 自分の向いている方角とbezierStrengthから制御点を計算してvector3で返す
        // 相対座標を返す
        public Vector3 GetControlPoint(bool isFront)
        {
            var dir = componentPositionInfo.BlockDirection;
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
                return  -direction * bezierStrength;
            }
            else
            {
                return direction * bezierStrength;
            }
        }

        public RailComponentInfo GetSaveState_Partial() 
        {
            var state = new RailComponentInfo();
            state.myID = railComponentID;
            state.bezierStrength = bezierStrength;
            var f = new List<ConnectionDestination>();
            var b = new List<ConnectionDestination>();
            
            
            foreach (var f_node in FrontNode.ConnectedNodes)
            {
                var f_node_railComponent = RailGraphDatastore.GetRailComponentID(f_node);
                f.Add(f_node_railComponent);
            }
            foreach (var b_node in BackNode.ConnectedNodes)
            {
                var b_node_railComponent = RailGraphDatastore.GetRailComponentID(b_node);
                b.Add(b_node_railComponent);
            }
            state.connectMyFrontTo = f;
            state.connectMyBackTo = b;
            return state;
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

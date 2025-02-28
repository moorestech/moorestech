using Game.Block.Interface.Component;
using Game.Train.RailGraph;
using Game.Train.Utility;
using UnityEngine;
using System.Collections.Generic;
using Game.Block.Interface;

namespace Game.Block.Blocks.TrainRail
{
    /// <summary>
    /// 1つのレールブロック内のレール要素を表すコンポーネント。
    /// 基本的に1つのRailComponentが FrontNode と BackNode の2つのRailNodeを持つ。
    /// </summary>
    public class RailComponent : IBlockComponent
    {
        public bool IsDestroy { get; private set; }

        // このRailComponentが保持する2つのRailNode
        public RailNode FrontNode { get; private set; }
        public RailNode BackNode { get; private set; }

        private BlockPositionInfo componentPositionInfo;
        private float bezierStrength = 0.5f;

        public RailControlPoint FrontRailControlPoint { get; }
        public RailControlPoint BackRailControlPoint { get; }

        // RailSaverComponent からみた自分の通し番号を含む識別子
        public RailComponentID RailComponentID { get; }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public RailComponent(BlockPositionInfo positionInfo, RailComponentID railComponentID = null)
        {
            RailComponentID = railComponentID;
            componentPositionInfo = positionInfo;

            // ベジェ曲線の制御点
            FrontRailControlPoint = new RailControlPoint(positionInfo.OriginalPos, GetControlPoint(true));
            BackRailControlPoint = new RailControlPoint(positionInfo.OriginalPos, GetControlPoint(false));

            // RailNode を作成して RailGraph に登録
            FrontNode = new RailNode();
            BackNode = new RailNode();

            RailGraphDatastore.AddRailComponentID(FrontNode, new ConnectionDestination(railComponentID, true));
            RailGraphDatastore.AddRailComponentID(BackNode, new ConnectionDestination(railComponentID, false));

            // お互いを逆方向ノードとしてセット
            FrontNode.SetOppositeNode(BackNode);
            BackNode.SetOppositeNode(FrontNode);

            // RailNodeに制御点を登録（表裏で使う制御点が異なる）
            FrontNode.SetRailControlPoints(FrontRailControlPoint, BackRailControlPoint);
            BackNode.SetRailControlPoints(BackRailControlPoint, FrontRailControlPoint);
        }

        /// <summary>
        /// RailComponent同士を接続する
        /// </summary>
        /// <param name="targetRail">接続先のRailComponent</param>
        /// <param name="isFrontThis">自分側の接続がFrontかどうか</param>
        /// <param name="isFrontTarget">相手側の接続がFrontかどうか</param>
        /// <param name="defaultDistance">明示的に距離を指定したい場合（-1なら自動計算）</param>
        public void ConnectRailComponent(RailComponent targetRail, bool isFrontThis, bool isFrontTarget, int defaultDistance = -1)
        {
            // まず、接続する2つのRailNodeを取得
            var (thisNode, thisOppositeNode) = GetNodes(isFrontThis);
            var (thatNode, thatOppositeNode) = targetRail.GetNodes(isFrontTarget);

            // 距離計算（指定がなければベジェ曲線をもとに推定）
            int distance = defaultDistance >= 0
                ? defaultDistance
                : CalculateDistanceTo(targetRail, isFrontThis, isFrontTarget);

            // 相互接続
            thisNode.ConnectNode(thatNode, distance);
            thatOppositeNode.ConnectNode(thisOppositeNode, distance);
        }

        /// <summary>
        /// RailComponent同士の接続を解除する
        /// </summary>
        /// <param name="targetRail">切断先のRailComponent</param>
        /// <param name="isFrontThis">自分側の接続がFrontかどうか</param>
        /// <param name="isFrontTarget">相手側の接続がFrontかどうか</param>
        public void DisconnectRailComponent(RailComponent targetRail, bool isFrontThis, bool isFrontTarget)
        {
            var (thisNode, thisOppositeNode) = GetNodes(isFrontThis);
            var (thatNode, thatOppositeNode) = targetRail.GetNodes(isFrontTarget);

            thisNode.DisconnectNode(thatNode);
            thatOppositeNode.DisconnectNode(thisOppositeNode);
        }

        /// <summary>
        /// ベジェ曲線の強度を変更し、制御点を更新する
        /// </summary>
        public void ChangeBezierStrength(float val)
        {
            bezierStrength = val;
            FrontRailControlPoint.ControlPointPosition = GetControlPoint(true);
            BackRailControlPoint.ControlPointPosition = GetControlPoint(false);
        }

        /// <summary>
        /// セーブ用の情報を部分的に取得
        /// </summary>
        public RailComponentInfo GetPartialSaveState()
        {
            var state = new RailComponentInfo
            {
                MyID = RailComponentID,
                BezierStrength = bezierStrength,
                ConnectMyFrontTo = new List<ConnectionDestination>(),
                ConnectMyBackTo = new List<ConnectionDestination>()
            };

            // FrontNode の接続リスト
            foreach (var node in FrontNode.ConnectedNodes)
            {
                var destInfo = RailGraphDatastore.GetRailComponentID(node);
                state.ConnectMyFrontTo.Add(destInfo);
            }

            // BackNode の接続リスト
            foreach (var node in BackNode.ConnectedNodes)
            {
                var destInfo = RailGraphDatastore.GetRailComponentID(node);
                state.ConnectMyBackTo.Add(destInfo);
            }

            return state;
        }

        /// <summary>
        /// isFrontフラグに応じてFrontNode/BackNodeを返すヘルパーメソッド
        /// </summary>
        private (RailNode node, RailNode oppositeNode) GetNodes(bool isFront)
        {
            return isFront
                ? (FrontNode, BackNode)
                : (BackNode, FrontNode);
        }

        /// <summary>
        /// 相手RailComponentとの距離をベジェ曲線から自動計算する
        /// </summary>
        private int CalculateDistanceTo(RailComponent targetRail, bool isFrontThis, bool isFrontTarget)
        {
            var myControlPoint = isFrontThis ? FrontRailControlPoint : BackRailControlPoint;
            var targetControlPoint = isFrontTarget ? targetRail.BackRailControlPoint : targetRail.FrontRailControlPoint;

            // 実際にはスケーリング等の係数をかけて距離に変換
            float rawLength = BezierUtility.GetBezierCurveLength(myControlPoint, targetControlPoint);
            float scaled = rawLength * BezierUtility.RAIL_LENGTH_SCALE;
            return (int)(scaled + 0.5f);
        }

        /// <summary>
        /// isFront の値によってベジェ制御点（相対座標）を返す
        /// </summary>
        private Vector3 GetControlPoint(bool isFront)
        {
            // 想定：dirは North, East, South, West のみ
            var dir = componentPositionInfo.BlockDirection;
            Vector3 direction = Vector3.zero;

            switch (dir)
            {
                case BlockDirection.North:
                    direction = Vector3.forward;  // (0,0,1)
                    break;
                case BlockDirection.East:
                    direction = Vector3.right;    // (1,0,0)
                    break;
                case BlockDirection.South:
                    direction = Vector3.back;     // (0,0,-1)
                    break;
                case BlockDirection.West:
                    direction = Vector3.left;     // (-1,0,0)
                    break;
            }

            return isFront
                ? direction * bezierStrength
                : -direction * bezierStrength;
        }

        /// <summary>
        /// レールを破壊し、ノードを破棄する
        /// </summary>
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

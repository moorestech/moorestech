using Game.Block.Interface.Component;
using Game.Train.RailGraph;
using Game.Train.Utility;
using UnityEngine;
using System.Collections.Generic;
using ClassLibrary;
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

        private float controlPointStrength = 0.5f;

        public RailControlPoint FrontControlPoint { get; }
        public RailControlPoint BackControlPoint { get; }

        // ブロック座標とIDが格納されている
        public RailComponentID ComponentID { get; }
        public Vector3 Position { get; }//ブロックではなくレールのつなぎ目としてのこのcomponentの位置
        
        public readonly Vector3 RailDirection;
        
        /// <summary>
        /// レール方向にBlockDirectionを用いるコンストラクタ
        /// </summary>
        public RailComponent(Vector3 position, BlockDirection blockDirection, RailComponentID railComponentID) : this(position, ToVector3(blockDirection), railComponentID) { }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public RailComponent(Vector3 position, Vector3 railDirection ,RailComponentID railComponentID)
        {
            Position = position;
            RailDirection = railDirection;
            ComponentID = railComponentID;

            // ベジェ曲線の制御点を初期化
            FrontControlPoint = new RailControlPoint(position, CalculateControlPointOffset(true));
            BackControlPoint = new RailControlPoint(position, CalculateControlPointOffset(false));

            FrontNode = new RailNode();
            BackNode = new RailNode();

            RailGraphDatastore.AddRailComponentID(FrontNode, new ConnectionDestination(railComponentID, true));
            RailGraphDatastore.AddRailComponentID(BackNode, new ConnectionDestination(railComponentID, false));

            // お互いを逆方向ノードとしてセット
            FrontNode.SetOppositeNode(BackNode);
            BackNode.SetOppositeNode(FrontNode);

            // RailNodeに制御点を登録（表裏で使う制御点が異なる）
            FrontNode.SetRailControlPoints(FrontControlPoint, BackControlPoint);
            BackNode.SetRailControlPoints(BackControlPoint, FrontControlPoint);
        }


        /// <summary>
        /// RailComponent同士を接続する
        /// </summary>
        /// <param name="targetComponent">接続先のRailComponent</param>
        /// <param name="useFrontSideOfThis">自分側の接続がFrontかどうか</param>
        /// <param name="useFrontSideOfTarget">相手側の接続がFrontかどうか</param>
        /// <param name="explicitDistance">明示的に距離を指定したい場合（-1なら自動計算）</param>
        public void ConnectRailComponent(RailComponent targetComponent, bool useFrontSideOfThis, bool useFrontSideOfTarget, int explicitDistance = -1)
        {
            if (this == targetComponent)
            {
                Debug.LogWarning("Attempted to connect a RailComponent to itself. Operation aborted.");
                return;
            }
            // まず、接続する2つのRailNodeを取得
            var (thisNode, thisOppositeNode) = GetRailNodesBySide(useFrontSideOfThis);
            var (targetNode, targetOppositeNode) = targetComponent.GetRailNodesBySide(useFrontSideOfTarget);

            // 距離計算（指定がなければベジェ曲線をもとに推定）
            int distance = explicitDistance >= 0
                ? explicitDistance
                : ComputeDistanceToComponent(targetComponent, useFrontSideOfThis, useFrontSideOfTarget);

            // 相互接続
            thisNode.ConnectNode(targetNode, distance);
            targetOppositeNode.ConnectNode(thisOppositeNode, distance);
        }

        /// <summary>
        /// RailComponent同士の接続を解除する
        /// </summary>
        /// <param name="targetComponent">切断先のRailComponent</param>
        /// <param name="useFrontSideOfThis">自分側の接続がFrontかどうか</param>
        /// <param name="useFrontSideOfTarget">相手側の接続がFrontかどうか</param>
        public void DisconnectRailComponent(RailComponent targetComponent, bool useFrontSideOfThis, bool useFrontSideOfTarget)
        {
            var (thisNode, thisOppositeNode) = GetRailNodesBySide(useFrontSideOfThis);
            var (targetNode, targetOppositeNode) = targetComponent.GetRailNodesBySide(useFrontSideOfTarget);

            thisNode.DisconnectNode(targetNode);
            targetOppositeNode.DisconnectNode(thisOppositeNode);
        }

        /// <summary>
        /// ベジェ曲線の強度を変更し、制御点を更新する
        /// </summary>
        public void UpdateControlPointStrength(float strength)
        {
            controlPointStrength = strength;
            FrontControlPoint.ControlPointPosition = CalculateControlPointOffset(true);
            BackControlPoint.ControlPointPosition = CalculateControlPointOffset(false);
        }

        /// <summary>
        /// セーブ用の情報を部分的に取得
        /// </summary>
        public RailComponentInfo GetPartialSaveState()
        {
            var state = new RailComponentInfo
            {
                MyID = ComponentID,
                BezierStrength = controlPointStrength,
                ConnectMyFrontTo = new List<ConnectionDestination>(),
                ConnectMyBackTo = new List<ConnectionDestination>(),
                RailDirection = new Vector3JsoObjects(RailDirection),
            };

            // FrontNode の接続リスト
            foreach (var node in FrontNode.ConnectedNodes)
            {
                var connectionInfo = RailGraphDatastore.GetRailComponentID(node);
                state.ConnectMyFrontTo.Add(connectionInfo);
            }

            // BackNode の接続リスト
            foreach (var node in BackNode.ConnectedNodes)
            {
                var connectionInfo = RailGraphDatastore.GetRailComponentID(node);
                state.ConnectMyBackTo.Add(connectionInfo);
            }

            return state;
        }

        /// <summary>
        /// 指定されたサイドに応じて FrontNode/BackNodeを返すヘルパーメソッド
        /// </summary>
        private (RailNode node, RailNode oppositeNode) GetRailNodesBySide(bool useFrontSide)
        {
            return useFrontSide
                ? (FrontNode, BackNode)
                : (BackNode, FrontNode);
        }

        /// <summary>
        /// 対象のRailComponentとの距離をベジェ曲線から自動計算する
        /// </summary>
        private int ComputeDistanceToComponent(RailComponent targetComponent, bool useFrontSideOfThis, bool useFrontSideOfTarget)
        {
            var thisControlPoint = useFrontSideOfThis ? FrontControlPoint : BackControlPoint;
            var targetControlPoint = useFrontSideOfTarget ? targetComponent.BackControlPoint : targetComponent.FrontControlPoint;

            float rawLength = BezierUtility.GetBezierCurveLength(thisControlPoint, targetControlPoint);
            float scaledLength = rawLength * BezierUtility.RAIL_LENGTH_SCALE;
            return (int)(scaledLength + 0.5f);
        }

        /// <summary>
        /// 指定されたサイドに応じたベジェ制御点の相対座標を返す
        /// </summary>
        private Vector3 CalculateControlPointOffset(bool useFrontSide)
        {
            return useFrontSide ? RailDirection * controlPointStrength : -RailDirection * controlPointStrength;
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
        
        public static Vector3 ToVector3(BlockDirection blockDirection)
        {
            return blockDirection switch
            {
                BlockDirection.North => Vector3.forward, // (0,0,1)
                BlockDirection.East => Vector3.right, // (1,0,0)
                BlockDirection.South => Vector3.back, // (0,0,-1)
                BlockDirection.West => Vector3.left, // (-1,0,0)
                _ => Vector3.zero
            };
        }
    }
}

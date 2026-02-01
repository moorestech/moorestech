using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Train.RailGraph;
using Game.Train.RailCalc;
using Game.Train.SaveLoad;
using UnityEngine;

namespace Game.Block.Blocks.TrainRail
{
    /// <summary>
    /// 1つのレールブロック内のレール要素を表すコンポーネント。
    /// 基本的に1つのRailComponentが FrontNode と BackNode の2つのRailNodeを持つ。
    /// </summary>
    public class RailComponent : IBlockComponent
    {
        public bool IsDestroy { get; private set; }

        private readonly IRailGraphDatastore _railGraphDatastore;

        // このRailComponentが保持する2つのRailNode
        public RailNode FrontNode { get; private set; }
        public RailNode BackNode { get; private set; }
        public RailControlPoint FrontControlPoint { get; }
        public RailControlPoint BackControlPoint { get; }

        //ブロックではなくレールのつなぎ目としてのこのcomponentの位置
        //Vector3形式であるが、現時点でこの値自体の誤差は許容している。もしrailcomponent.positionを新規に使う場合すでに誤差が含まれていることを考慮すること
        public Vector3 Position { get; }
        public readonly Vector3 RailDirection;
        
        /// <summary>
        /// レール方向にBlockDirectionを用いるコンストラクタ
        /// </summary>
        public RailComponent(IRailGraphDatastore railGraphDatastore, Vector3 position, BlockDirection blockDirection, Vector3Int blockPosition, int componentIndex) : this(railGraphDatastore, position, ToVector3(blockDirection), blockPosition, componentIndex) { }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public RailComponent(IRailGraphDatastore railGraphDatastore, Vector3 position, Vector3 railDirection, Vector3Int blockPosition, int componentIndex)
        {
            _railGraphDatastore = railGraphDatastore;

            Position = position;
            RailDirection = railDirection;

            // ベジェ曲線の制御点を初期化
            FrontControlPoint = new RailControlPoint(position, CalculateControlPointOffset(true));
            BackControlPoint = new RailControlPoint(position, CalculateControlPointOffset(false));

            FrontNode = new RailNode(_railGraphDatastore);
            BackNode = new RailNode(_railGraphDatastore);

            FrontNode.SetRailControlPoints(FrontControlPoint, BackControlPoint);
            BackNode.SetRailControlPoints(BackControlPoint, FrontControlPoint);
            FrontNode.SetConnectionDestination(new ConnectionDestination(blockPosition, componentIndex, true));
            BackNode.SetConnectionDestination(new ConnectionDestination(blockPosition, componentIndex, false));
            _railGraphDatastore.AddNodePair(FrontNode, BackNode);
            return;
            
            Vector3 CalculateControlPointOffset(bool useFrontSide)
            {
                return useFrontSide ? RailDirection : -RailDirection;
            }
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




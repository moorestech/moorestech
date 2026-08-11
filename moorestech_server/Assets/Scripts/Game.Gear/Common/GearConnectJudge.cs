using Game.Block.Interface;
using Game.Block.Interface.Component.ConnectJudge;
using Mooresmaster.Model.GearModule;
using UnityEngine;

namespace Game.Gear.Common
{
    /// <summary>
    ///     歯車ドメインの接続判定。双方の噛み合い軸（meshingAxis）がワールド空間で平行なときのみ接続を許可する
    ///     Gear-domain judge; allows connection only when both meshing axes are parallel in world space
    /// </summary>
    public class GearConnectJudge : IConnectorConnectJudge
    {
        public bool CanConnect(ConnectJudgeContext context)
        {
            // コネクタ未特定（方向無制限経路）は制約なしとして通す
            // Pass when connectors are unresolved (unrestricted-directions path)
            if (context.SelfConnector is not GearConnectsElement selfConnector) return true;
            if (context.TargetConnector is not GearConnectsElement targetConnector) return true;

            // 軸未設定のコネクタは向き制約なし
            // Connectors without a meshing axis have no orientation constraint
            var selfAxis = selfConnector.Option.MeshingAxis;
            var targetAxis = targetConnector.Option.MeshingAxis;
            if (selfAxis == null || targetAxis == null) return true;

            // 双方のローカル軸をワールドへ変換し、外積ゼロ（平行・逆向き含む）なら噛み合う
            // Convert both local axes to world space; mesh when the cross product is zero (parallel or anti-parallel)
            var selfWorldAxis = context.SelfPositionInfo.BlockDirection.GetCoordinateConvertAction()(selfAxis.Value);
            var targetWorldAxis = context.TargetPositionInfo.BlockDirection.GetCoordinateConvertAction()(targetAxis.Value);
            return Vector3Int.RoundToInt(Vector3.Cross(selfWorldAxis, targetWorldAxis)) == Vector3Int.zero;
        }
    }
}

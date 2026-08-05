using Server.Protocol.PacketResponse.Util.ElectricWire.Placement;
using UnityEngine;

namespace Server.Protocol.PacketResponse.Util.ElectricWire
{
    /// <summary>
    /// ElectricWireExtendServiceの実行結果。成功時は終点（次の起点）座標とInstanceIdを持つ
    /// Result of ElectricWireExtendService; on success carries the endpoint (next origin) position and InstanceId
    /// </summary>
    public readonly struct ExtendResult
    {
        public readonly bool IsSuccess;
        public readonly ElectricWirePlacementFailureReason FailureReason;
        public readonly Vector3Int EndpointPos;
        public readonly int EndpointBlockInstanceId;

        private ExtendResult(bool isSuccess, ElectricWirePlacementFailureReason failureReason, Vector3Int endpointPos, int endpointBlockInstanceId)
        {
            IsSuccess = isSuccess;
            FailureReason = failureReason;
            EndpointPos = endpointPos;
            EndpointBlockInstanceId = endpointBlockInstanceId;
        }

        public static ExtendResult Success(Vector3Int endpointPos, int endpointBlockInstanceId)
        {
            return new ExtendResult(true, ElectricWirePlacementFailureReason.None, endpointPos, endpointBlockInstanceId);
        }

        public static ExtendResult Failure(ElectricWirePlacementFailureReason failureReason)
        {
            return new ExtendResult(false, failureReason, Vector3Int.zero, 0);
        }
    }
}

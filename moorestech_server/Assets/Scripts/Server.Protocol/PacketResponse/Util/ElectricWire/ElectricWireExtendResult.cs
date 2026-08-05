using Server.Protocol.PacketResponse.Util.ElectricWire.Placement;
using UnityEngine;

namespace Server.Protocol.PacketResponse.Util.ElectricWire
{
    /// <summary>
    /// ElectricWireExtendServiceの実行結果。成功時は終点（次の起点）座標とInstanceIdを持つ
    /// Result of ElectricWireExtendService; on success carries the endpoint (next origin) position and InstanceId
    /// </summary>
    public readonly struct ElectricWireExtendResult
    {
        public readonly bool IsSuccess;
        public readonly ElectricWirePlacementFailureReason FailureReason;
        public readonly Vector3Int EndpointPos;
        public readonly int EndpointBlockInstanceId;

        private ElectricWireExtendResult(bool isSuccess, ElectricWirePlacementFailureReason failureReason, Vector3Int endpointPos, int endpointBlockInstanceId)
        {
            IsSuccess = isSuccess;
            FailureReason = failureReason;
            EndpointPos = endpointPos;
            EndpointBlockInstanceId = endpointBlockInstanceId;
        }

        public static ElectricWireExtendResult Success(Vector3Int endpointPos, int endpointBlockInstanceId)
        {
            return new ElectricWireExtendResult(true, ElectricWirePlacementFailureReason.None, endpointPos, endpointBlockInstanceId);
        }

        public static ElectricWireExtendResult Failure(ElectricWirePlacementFailureReason failureReason)
        {
            return new ElectricWireExtendResult(false, failureReason, Vector3Int.zero, 0);
        }
    }
}

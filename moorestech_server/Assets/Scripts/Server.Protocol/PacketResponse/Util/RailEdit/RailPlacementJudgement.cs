using System;
using System.Collections.Generic;
using Core.Master;

namespace Server.Protocol.PacketResponse
{
    /// <summary>
    /// レール設置可否の統合判定結果。失敗理由、または採用connectToolと消費素材を保持する
    /// Aggregated rail placement viability result, exposing the failure reason or the selected connectTool with its consumption materials.
    /// </summary>
    public readonly struct RailPlacementJudgement
    {
        public readonly RailConnectionEditProtocol.RailConnectionEditFailureReason FailureReason;
        public readonly Guid ConnectToolGuid;
        public readonly IReadOnlyList<ConnectToolMaterialCost> Materials;

        public bool IsPlaceable => FailureReason == RailConnectionEditProtocol.RailConnectionEditFailureReason.None;

        public Guid SelectedRailTypeGuid => ConnectToolGuid;

        public RailPlacementJudgement(RailConnectionEditProtocol.RailConnectionEditFailureReason failureReason, Guid connectToolGuid, IReadOnlyList<ConnectToolMaterialCost> materials)
        {
            FailureReason = failureReason;
            ConnectToolGuid = connectToolGuid;
            Materials = materials;
        }
    }
}

using System;
using System.Collections.Generic;
using Core.Master;

namespace Server.Protocol.PacketResponse.Util.RailEdit
{
    /// <summary>
    /// レール設置判定結果
    /// Rail placement judgement result
    /// - 失敗理由、または採用connectTool・消費素材のいずれかを保持
    /// - Holds either the failure reason, or the selected connectTool with its consumption materials
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

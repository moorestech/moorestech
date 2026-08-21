using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Core.Master;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect.Parts
{
    /// <summary>
    /// 電柱ゴースト評価の結果をまとめた型。不可理由を個別に持ち、ツールチップ行へ写す
    /// Result of a pole-ghost evaluation, holding each block reason separately so it can be pushed as tooltip lines
    /// </summary>
    public readonly struct ElectricWirePoleGhostEvaluation
    {
        public readonly List<PlaceInfo> PlaceInfos;
        public readonly BlockMasterElement PoleMaster;
        public readonly BlockId PoleBlockId;
        public readonly bool IsGroundClear;
        public readonly bool IsPositionFree;
        public readonly IReadOnlyList<ConstructionMaterialShortage> MaterialShortages;

        // 電柱ゴーストは常に1セルなので、設置情報と電柱パラメータは保持元から都度導出する
        // The pole ghost is always a single cell, so the place info and pole param are derived from their source on demand
        public bool CanAffordPole => MaterialShortages.Count == 0;
        public PlaceInfo PlaceInfo => PlaceInfos[0];
        public ElectricPoleBlockParam PoleParam => (ElectricPoleBlockParam)PoleMaster.BlockParam;

        public ElectricWirePoleGhostEvaluation(List<PlaceInfo> placeInfos, BlockMasterElement poleMaster, BlockId poleBlockId, bool isGroundClear, bool isPositionFree, IReadOnlyList<ConstructionMaterialShortage> materialShortages)
        {
            PlaceInfos = placeInfos;
            PoleMaster = poleMaster;
            PoleBlockId = poleBlockId;
            IsGroundClear = isGroundClear;
            IsPositionFree = isPositionFree;
            MaterialShortages = materialShortages;
        }

        // ゴーストの不可理由をプッシュ順（地形 → 重複 → 素材）でツールチップへ積む
        // Push the ghost's block reasons in order (terrain → overlap → materials) into the tooltip
        public void PushBlockReasons(PlacementFeedback feedback)
        {
            if (!IsGroundClear) feedback.AddBlockedByTerrain();
            if (!IsPositionFree) feedback.AddBlockedByExistingBlock();
            feedback.AddMaterialShortages(MaterialShortages);
        }
    }
}

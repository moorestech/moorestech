using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Core.Master;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect.Parts
{
    /// <summary>
    /// 電柱ゴースト評価結果を保持
    /// 不可理由を個別にツールチップへ
    /// Holds a pole-ghost evaluation result
    /// Each block reason is pushed to the tooltip separately
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

        // 電柱ゴースト自体の設置可否（孤立設置・延長設置の両呼び出し元で共有する判定）
        // The pole ghost's own placeability (the judgement both isolated and extend placement callers shared)
        public bool IsGhostPlaceable => IsGroundClear && IsPositionFree && CanAffordPole;

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

            // 地形干渉・重複で既に不可のセルは「今回の設置セル」に数えないため素材行も出さない（前例: CommonBlockPlaceCostMarker）
            // A cell already blocked by terrain/overlap is not a placing cell, so no material line either (precedent: CommonBlockPlaceCostMarker)
            if (!IsGroundClear || !IsPositionFree) return;
            foreach (var shortage in MaterialShortages) feedback.Add(ConstructionMaterialShortageLine.ToLine(shortage));
        }
    }
}

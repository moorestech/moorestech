using System.Collections.Generic;
using Core.Master;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect.Parts
{
    /// <summary>
    /// 電柱ゴースト評価の結果をまとめた型。out引数の乱立を避けるために導入する
    /// Result of a pole-ghost evaluation, bundled to avoid a sprawl of out parameters
    /// </summary>
    public readonly struct ElectricWirePoleGhostEvaluation
    {
        public readonly List<PlaceInfo> PlaceInfos;
        public readonly BlockMasterElement PoleMaster;
        public readonly BlockId PoleBlockId;
        public readonly bool GroundClear;
        public readonly bool CanAffordPole;

        // 電柱ゴーストは常に1セルなので、設置情報と電柱パラメータは保持元から都度導出する
        // The pole ghost is always a single cell, so the place info and pole param are derived from their source on demand
        public PlaceInfo PlaceInfo => PlaceInfos[0];
        public ElectricPoleBlockParam PoleParam => (ElectricPoleBlockParam)PoleMaster.BlockParam;

        public ElectricWirePoleGhostEvaluation(List<PlaceInfo> placeInfos, BlockMasterElement poleMaster, BlockId poleBlockId, bool groundClear, bool canAffordPole)
        {
            PlaceInfos = placeInfos;
            PoleMaster = poleMaster;
            PoleBlockId = poleBlockId;
            GroundClear = groundClear;
            CanAffordPole = canAffordPole;
        }
    }
}

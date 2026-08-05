using System.Collections.Generic;
using Core.Master;
using Game.Block.Interface;
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
        public readonly PlaceInfo PlaceInfo;
        public readonly BlockMasterElement PoleMaster;
        public readonly BlockId PoleBlockId;
        public readonly ElectricPoleBlockParam PoleParam;
        public readonly bool GroundClear;
        public readonly bool CanAffordPole;

        public ElectricWirePoleGhostEvaluation(List<PlaceInfo> placeInfos, PlaceInfo placeInfo, BlockMasterElement poleMaster, BlockId poleBlockId, ElectricPoleBlockParam poleParam, bool groundClear, bool canAffordPole)
        {
            PlaceInfos = placeInfos;
            PlaceInfo = placeInfo;
            PoleMaster = poleMaster;
            PoleBlockId = poleBlockId;
            PoleParam = poleParam;
            GroundClear = groundClear;
            CanAffordPole = canAffordPole;
        }
    }
}

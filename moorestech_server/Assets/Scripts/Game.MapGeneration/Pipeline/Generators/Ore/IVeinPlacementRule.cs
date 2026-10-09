using System.Collections.Generic;

namespace Game.MapGeneration.Pipeline.Generators
{
    // 鉱脈メンバー候補の採否を1入口で決める。陸地→隣タイル確定済み→同タイル既出の順と採用数の集計は実装の内側に閉じる
    // Decides a vein member candidate at one entry; the land, neighbour-confirmed, same-tile order and the accepted tally stay inside the implementation
    internal interface IVeinPlacementRule
    {
        // クラスタ中心が採用候補になった。0件採用の診断に使う
        // A cluster centre became eligible; used to diagnose zero acceptances
        void BeginCluster();

        bool TryAcceptMember(PlacedVein candidate, IReadOnlyList<PlacedVein> excludedVeins, IReadOnlyList<PlacedVein> confirmedVeins);

        // エントリ1件ぶんの却下理由を記録し集計を閉じる
        // Records one entry's rejection reasons and closes its tally
        void ReportRejections(int seed, int tileX, int tileZ, string entryGuid);
    }
}

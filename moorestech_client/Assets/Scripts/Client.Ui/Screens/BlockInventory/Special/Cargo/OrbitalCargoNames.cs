using System.Collections.Generic;
using System.Globalization;
using Client.Localization;
using Client.Ui.Content;
using Client.Ui.State.BlockInventory.Special.Cargo;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;

namespace Client.Ui.Screens.BlockInventory.Special.Cargo
{
    /// <summary>
    /// 積荷の文。「ICチップLv2 ×20 → 計算データ ×40」「グラフェンケーブル ×1000 → ケーブルプール +1000」（ADR 0106 の 1）
    /// Cargo text: "IC chip Lv2 ×20 → Compute data ×40", "Graphene cable ×1000 → Cable pool +1000" (ADR 0106 §1)
    /// </summary>
    public static class OrbitalCargoNames
    {
        public static UiText Row(OrbitalCargoChoice choice, GameContentPresentation content)
        {
            return new UiText(LocalizationKeys.Ui.BlockInventory.OrbitalCargoRow, new[]
            {
                new LocalizationTextParam("payload", content.ItemName(choice.PayloadItemId).Resolve()),
                new LocalizationTextParam("count", Count(choice.CountPerLaunch)),
                new LocalizationTextParam("outcome", Outcome(choice, content).Resolve()),
            });
        }

        public static UiText NextFlight(OrbitalCargoChoice choice, GameContentPresentation content)
        {
            return new UiText(LocalizationKeys.Ui.BlockInventory.OrbitalCargoNextFlight, new[] { new LocalizationTextParam("cargo", Row(choice, content).Resolve()) });
        }

        // プールへ足す種類は 1 便の効果を先に書き（今の数は拠点の画面で見る。ADR 0106 の 1）、続けて出力の品と数を書く。サーバーは両者を別々に処理するので両方あれば両方
        // Pool-adding kinds state one flight's effect first (the current pool is on the base screen; ADR 0106 §1), then output items and counts; both when both, as the server applies them independently
        private static UiText Outcome(OrbitalCargoChoice choice, GameContentPresentation content)
        {
            var parts = new List<UiText>(choice.ArrivalOutputs.Count + 1);
            if (choice.AddsToCablePool) parts.Add(new UiText(LocalizationKeys.Ui.BlockInventory.OrbitalCargoOutcomeCablePool, new[] { new LocalizationTextParam("count", Count(choice.CountPerLaunch)) }));
            foreach (var output in choice.ArrivalOutputs)
            {
                parts.Add(new UiText(LocalizationKeys.Ui.BlockInventory.OrbitalCargoOutcomeItem, new[]
                {
                    new LocalizationTextParam("item", content.ItemName(output.ItemId).Resolve()),
                    new LocalizationTextParam("count", Count(output.Count)),
                }));
            }
            return UiText.Join(", ", parts);
        }

        private static string Count(int count)
        {
            return count.ToString(CultureInfo.InvariantCulture);
        }
    }
}

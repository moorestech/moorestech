using Client.Game.InGame.UI.Tooltip;
using Mooresmaster.Localization.Generated;
using Server.Protocol.PacketResponse.Util.ElectricWire.Placement;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect.Parts.Feedback
{
    /// <summary>
    /// 電線ドメイン専用のツールチップ行を組み立てる
    /// Builds the tooltip lines that belong to the electric-wire domain
    /// </summary>
    public static class ElectricWireFeedbackLines
    {
        // 電線不足の文言はToKeyが唯一の所有者（自動接続プレビューと接続判定で綴りが割れないようにする）
        // ToKey is the single owner of the wire-shortage wording (auto-connect preview and judgement must not diverge)
        public static TooltipLine WireShortage()
        {
            return new TooltipLine(ElectricWirePlacementFailureTooltipKey.ToKey(ElectricWirePlacementFailureReason.NoWireItem));
        }

        public static TooltipLine WireOutOfRangeNotice()
        {
            return new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceWireOutOfRangeNotice);
        }

        // 消費電線が無いときは案内行を出さない（旧ラベルと同じ）
        // No notice line without wire consumption (same as the old label)
        public static bool TryWireCost(int totalWireCost, out TooltipLine line)
        {
            if (totalWireCost <= 0)
            {
                line = default;
                return false;
            }

            line = new TooltipLine(LocalizationKeys.Ui.Tooltip.PlaceWireCost, new[] { totalWireCost.ToString() });
            return true;
        }
    }
}

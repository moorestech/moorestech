using System.Collections.Generic;
using Mooresmaster.Localization.Generated;

namespace Client.Game.InGame.UI.Tooltip
{
    public interface IMouseCursorTooltip
    {
        // 現在の表示内容を読む（Web UIブリッジが表示行を写すのに使う）
        // Reads the current presentation, which the Web UI bridge mirrors onto its tooltip topic
        public TooltipPresentation GetPresentation();

        // 現在の所有者。書き手が「自分の表示がまだ立っているか」を内容比較なしで判定する
        // The current owner, letting a writer tell whether its own show still stands without comparing content
        public TooltipOwner CurrentOwner { get; }

        // 表示も非表示も所有者トークン付きで呼ぶ（現所有者以外のHideは他者の表示を消さない）
        // Both show and hide carry an owner token, so a Hide from anyone else never clears the current tooltip
        public void Hide(TooltipOwner owner);
        public void Show(TooltipOwner owner, LocalizationKey key);
        public void Show(TooltipOwner owner, LocalizationKey key, IReadOnlyList<string> textParams);
        public void Show(TooltipOwner owner, IReadOnlyList<TooltipLine> lines);
    }
}

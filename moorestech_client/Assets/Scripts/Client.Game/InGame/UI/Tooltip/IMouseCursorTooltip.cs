using System.Collections.Generic;
using Mooresmaster.Localization.Generated;

namespace Client.Game.InGame.UI.Tooltip
{
    public interface IMouseCursorTooltip
    {
        // 現在の表示内容を読む（PlacementFeedbackTooltipPresenterが自分の表示継続を判定するのに使う）
        // Reads the current presentation; PlacementFeedbackTooltipPresenter uses it to detect whether its own show still stands
        public TooltipPresentation GetPresentation();

        // 表示も非表示も所有者トークン付きで呼ぶ（現所有者以外のHideは他者の表示を消さない）
        // Both show and hide carry an owner token, so a Hide from anyone else never clears the current tooltip
        public void Hide(TooltipOwner owner);
        public void Show(TooltipOwner owner, LocalizationKey key);
        public void Show(TooltipOwner owner, LocalizationKey key, IReadOnlyList<string> textParams);
        public void Show(TooltipOwner owner, IReadOnlyList<TooltipLine> lines);
    }
}

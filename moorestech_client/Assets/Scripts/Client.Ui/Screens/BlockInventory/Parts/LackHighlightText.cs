using Client.Ui.Foundation.Text;
using Client.Ui.State.Text;

namespace Client.Ui.Screens.BlockInventory.Parts
{
    public enum LackHighlightTextSize
    {
        Xs,
        Sm,
    }

    /// <summary>
    /// 現在値と要求値の文。不足のときだけ警告色にする（Web details/LackHighlightText の写し）
    /// Current-versus-required text that turns the warning colour only when lacking (port of Web details/LackHighlightText)
    /// </summary>
    public sealed class LackHighlightText : LocalizedLabel
    {
        public const string ClassName = "lack-highlight-text";
        public const string InsufficientClassName = "lack-highlight-text--insufficient";

        public LackHighlightText(string testid, LackHighlightTextSize size)
        {
            // testid は Web の任意 prop と同じく省略できる
            // The testid is optional, as the Web prop is
            if (testid != null) name = testid;
            AddToClassList(ClassName);
            AddToClassList(size == LackHighlightTextSize.Xs ? UiTypographyClass.TextXs : UiTypographyClass.TextSm);
        }

        public void Render(UiText text, bool insufficient)
        {
            SetText(text);
            EnableInClassList(InsufficientClassName, insufficient);
        }
    }
}

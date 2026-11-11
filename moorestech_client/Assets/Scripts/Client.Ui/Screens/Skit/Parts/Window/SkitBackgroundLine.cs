// S/Client.Ui/Screens/Skit/Parts/Window/SkitBackgroundLine.cs
using Client.Ui.Foundation.Text;
using Client.Ui.State.Text;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Skit.Parts.Window
{
    /// <summary>
    /// 背景スキットの面を持たない 1 行。入力は必ず素通しする（Web .backgroundLine）
    /// The faceless single line of a background skit; it always passes input through (Web .backgroundLine)
    /// </summary>
    public sealed class SkitBackgroundLine : LocalizedLabel
    {
        public SkitBackgroundLine()
        {
            name = "background-skit";
            AddToClassList("skit-background-line");
            pickingMode = PickingMode.Ignore;
            SetText(UiText.FromVerbatim(string.Empty));
            style.display = DisplayStyle.None;
        }

        public void SetLine(string speaker, string body, int visibleCodePoints)
        {
            SetText(UiText.FromVerbatim(SkitRichText.BackgroundLine(speaker, body, visibleCodePoints)));
        }

        public void SetShown(bool shown)
        {
            style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}

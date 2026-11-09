using Client.Ui.Foundation.Theme;
using UnityEngine.UIElements;

namespace Client.Ui.Components.Panels
{
    /// <summary>
    /// Mantine Paper の写し。暗色テーマの既定の面と角丸だけを持つ（Web の ChallengeNodeCard・TrainRidingHud・CursorTooltip が使う）
    /// Copy of Mantine Paper with only the dark-theme default face and radius (used by Web ChallengeNodeCard, TrainRidingHud and CursorTooltip)
    /// </summary>
    /// <remarks>
    /// 画面が面や角丸を変えるときは祖先でつまみ --paper-face・--paper-radius を上書きする
    /// Screens change the face or radius by overriding the --paper-face and --paper-radius knobs on an ancestor
    /// </remarks>
    public sealed class PaperSurface : VisualElement
    {
        public PaperSurface(string testid)
        {
            name = testid;
            AddToClassList("paper-surface");
            UiSurface.Mark(this);
        }
    }
}

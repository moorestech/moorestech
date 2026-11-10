using Client.Ui.Foundation.Text;
using Client.Ui.State.Text;
using UnityEngine.UIElements;

namespace Client.Ui.Components.Recipe
{
    /// <summary>
    /// レベル継承のバッジ（共有部品）
    /// Level inheritance badge (shared component)
    /// </summary>
    /// <remarks>
    /// Web は継承しないとき testid の要素ごと描かない。testid は文のラベルに付け、ラベルは Show で作り Hide で外す（文の無い LocalizedLabel は 1 フレーム後にエラーを出すため）。
    /// The Web renders no testid element without inheritance. The testid sits on the text label, created by Show and removed by Hide (a LocalizedLabel without text reports an error a frame later).
    /// </remarks>
    public sealed class LevelInheritanceBadge : VisualElement
    {
        public const string UssClassName = "level-inheritance-badge";
        private const string ShownUssClassName = "level-inheritance-badge--shown";
        private const string TextUssClassName = "level-inheritance-badge__text";
        private readonly string _testid;

        public LevelInheritanceBadge(string testid)
        {
            _testid = testid;
            AddToClassList(UssClassName);
            pickingMode = PickingMode.Ignore;
        }

        public void Show(UiText text)
        {
            // Mantine の Text size="xs" c="dimmed" と同じ字の組
            // Same typography as the Mantine Text size="xs" c="dimmed"
            Clear();
            var label = new LocalizedLabel { name = _testid, pickingMode = PickingMode.Ignore };
            label.AddToClassList(UiTypographyClass.TextXs);
            label.AddToClassList(UiTypographyClass.TextDimmed);
            label.AddToClassList(TextUssClassName);
            label.SetText(text);
            Add(label);
            AddToClassList(ShownUssClassName);
        }

        public void Hide()
        {
            Clear();
            RemoveFromClassList(ShownUssClassName);
        }
    }
}

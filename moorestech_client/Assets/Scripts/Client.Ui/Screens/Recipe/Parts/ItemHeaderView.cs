using Client.Ui.Foundation.Text;
using Client.Ui.State.Text;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Recipe.Parts
{
    /// <summary>
    /// 選んだアイテムの名前と装飾線の見出し（Web ItemHeader の写し）
    /// Header with the selected item's name and decoration line (port of the Web ItemHeader)
    /// </summary>
    public sealed class ItemHeaderView : VisualElement
    {
        private readonly LocalizedLabel _name;

        public ItemHeaderView()
        {
            AddToClassList("item-header");

            // 名前は Web の font-weight 500（ラテンと CJK の両方を持つ .text--medium。D-35）
            // The name uses the Web font-weight 500 (.text--medium covers Latin and CJK; D-35)
            _name = new LocalizedLabel();
            _name.AddToClassList("item-header__name");
            _name.AddToClassList(UiTypographyClass.TextMedium);
            Add(_name);
            Add(new ItemHeaderRule());
        }

        public void SetName(UiText name)
        {
            _name.SetText(name);
        }
    }
}

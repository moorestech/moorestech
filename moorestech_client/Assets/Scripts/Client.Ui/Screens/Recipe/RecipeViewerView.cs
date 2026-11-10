using Client.Ui.Components.Panels;
using Client.Ui.Foundation.Text;
using Client.Ui.Screens.Recipe.Parts;
using Client.Ui.State.Recipe.Screen;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Recipe
{
    /// <summary>
    /// レシピビューアのパネル（Web RecipeViewer・RecipeContent の写し）。選択が変わると中身を作り直してスクロールを先頭へ戻す
    /// Recipe viewer panel (port of the Web RecipeViewer and RecipeContent); a new selection rebuilds the content and resets the scroll
    /// </summary>
    public sealed class RecipeViewerView : VisualElement
    {
        private const string SelectedClass = "recipe-viewer--selected";
        private readonly RecipeEntryPartsContext _context;
        private readonly LocalizedLabel _hint;
        private readonly VisualElement _content;
        private RecipeSelection _shownSelection = RecipeSelection.None;
        private bool _shownHasRecipes;
        private RecipeViewerViewModel _latest = RecipeViewerNoSelection.Instance;
        private bool _shown = true;
        private RecipeEntryList _list;

        public RecipeViewerView(RecipeEntryPartsContext context)
        {
            _context = context;
            AddToClassList("recipe-viewer");
            var panel = new GamePanel("recipe-viewer", GamePanelFrame.Craft);

            // 未選択の案内（Web <Text size="sm" c="dimmed" ta="center" m="auto">）と、選択ありの中身
            // The no-selection hint (Web <Text size="sm" c="dimmed" ta="center" m="auto">) and the selected content
            _hint = new LocalizedLabel { name = "recipe-viewer-hint" };
            _hint.AddToClassList(UiTypographyClass.TextSm);
            _hint.AddToClassList(UiTypographyClass.TextDimmed);
            _hint.AddToClassList("recipe-viewer__hint");
            _hint.SetText(new UiText(LocalizationKeys.Ui.Recipe.SelectItemHint));
            _content = new VisualElement();
            _content.AddToClassList("recipe-viewer__content");
            panel.Add(_hint);
            panel.Add(_content);
            Add(panel);
        }

        public void Render(RecipeViewerViewModel vm)
        {
            // 最新の VM を覚え、隠れている間は組まない（出し直すときに覚えた VM で組む＝Web の再マウント。D-33）
            // Remember the latest VM and skip building while hidden (showing again builds from it, like the Web remount; D-33)
            _latest = vm;
            // silent-ok: 隠れている間は覚えるだけ（出し直しで組む）/ RecipeViewerViewTest の隠して出し直す件が見る
            if (!_shown) return;

            // 未選択は案内だけ、選択ありは高さを切り替えて中身を出す（Web height 300 / 432.983）
            // No selection shows the hint only; a selection switches the height and shows the content (Web height 300 / 432.983)
            var selected = vm as RecipeViewerItemSelected;
            EnableInClassList(SelectedClass, selected != null);
            _hint.style.display = selected == null ? DisplayStyle.Flex : DisplayStyle.None;
            _content.style.display = selected == null ? DisplayStyle.None : DisplayStyle.Flex;
            if (selected == null)
            {
                ForgetShownContent();
                return;
            }

            // 別のアイテム、または「レシピ無し」と列の境をまたいだら作り直す（Web は key の再マウントと entries.length の枝の決め直し）
            // Rebuild for a different item or when crossing between "no recipes" and the list (Web remounts by key and re-picks the entries.length branch)
            var hasRecipes = selected.Entries.Count > 0;
            if (!_shownSelection.SameAs(RecipeSelection.Of(selected.ItemId)) || hasRecipes != _shownHasRecipes) RebuildContent(selected, hasRecipes);
            if (hasRecipes) _list.Render(selected.Entries);
        }

        public void SetShown(bool shown)
        {
            _shown = shown;
            style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
            if (shown)
            {
                // 出し直したら覚えた VM で組む（持ち物の再取得など別の契機を待たない）
                // Build from the remembered VM on show (without waiting for another trigger such as an inventory refetch)
                Render(_latest);
                return;
            }

            // 閉じたら中身を捨てる（Web は画面を出るとアンマウント＝長押しが止まり、開き直すとスクロールが先頭）
            // Drop the content on hide (Web unmounts: the hold stops and the scroll is at the top on reopen)
            ForgetShownContent();
        }

        // 表示済みの中身と、それを表す状態をまとめて捨てる（書き手を 1 本にする）
        // Drop the shown content and the state describing it together (single writer)
        private void ForgetShownContent()
        {
            _content.Clear();
            _list = null;
            _shownSelection = RecipeSelection.None;
            _shownHasRecipes = false;
        }

        private void RebuildContent(RecipeViewerItemSelected selected, bool hasRecipes)
        {
            ForgetShownContent();
            _shownSelection = RecipeSelection.Of(selected.ItemId);
            _shownHasRecipes = hasRecipes;
            var header = new ItemHeaderView();
            header.SetName(_context.Content.ItemName(selected.ItemId));
            _content.Add(header);

            // レシピが無ければ「レシピ無し」、あれば列（Web の Stack gap sm）
            // "No recipes" when empty, otherwise the list (the Web Stack gap sm)
            if (!hasRecipes)
            {
                var noRecipes = new LocalizedLabel { name = "recipe-viewer-no-recipes" };
                noRecipes.AddToClassList(UiTypographyClass.TextSm);
                noRecipes.AddToClassList(UiTypographyClass.TextDimmed);
                noRecipes.AddToClassList("recipe-viewer__no-recipes");
                noRecipes.SetText(new UiText(LocalizationKeys.Ui.Recipe.NoRecipes));
                _content.Add(noRecipes);
                return;
            }
            _list = new RecipeEntryList(_context);
            _content.Add(_list);
        }
    }
}

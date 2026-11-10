using Core.Master;
using UniRx;

namespace Client.Ui.State.Recipe.Screen
{
    /// <summary>
    /// 選択の持ち主（Web selectionStore の写し）。同じ選択の再設定は通知しない
    /// Owner of the selection (port of the Web selectionStore); re-setting the same selection does not notify
    /// </summary>
    public sealed class RecipeItemSelection
    {
        private readonly ReactiveProperty<RecipeSelection> _current = new(RecipeSelection.None);
        public IReadOnlyReactiveProperty<RecipeSelection> Current => _current;

        public void Select(ItemId itemId)
        {
            Apply(RecipeSelection.Of(itemId));
        }

        public void Clear()
        {
            Apply(RecipeSelection.None);
        }

        private void Apply(RecipeSelection next)
        {
            // 判別 union は参照が毎回違うので値で比べて重複通知を避ける
            // Union values are new references each time, so compare by value to avoid duplicate notifications
            if (_current.Value.SameAs(next)) return;
            _current.Value = next;
        }
    }
}

using System.Collections.Generic;
using Client.Ui.Components.Gauges;
using Client.Ui.Foundation.Text;
using Client.Ui.State.Text;
using UnityEngine.UIElements;

namespace Client.Ui.Components.Recipe
{
    /// <summary>
    /// レシピ行。素材・中央列（秒数→矢印→操作）・結果の 3 列と、外周の輪と四隅の括弧
    /// Recipe row: materials, the center column (duration, arrow, action) and results, with the outer ring and corner brackets
    /// </summary>
    /// <remarks>
    /// スロットは画面が作った要素を受けて並べるだけで、操作は画面がスロットを直接購読する（Web は ReactNode[] を受ける）。
    /// It only lays out slot elements the screen built; the screen subscribes to the slots directly (the Web takes ReactNode[]).
    /// </remarks>
    public sealed class RecipeRow : VisualElement
    {
        public const string UssClassName = "recipe-row";
        private const string ActionShownUssClassName = "recipe-row__action--shown";

        private readonly RecipeSlotColumn _materials;
        private readonly RecipeSlotColumn _results;
        private readonly LocalizedLabel _duration;
        private readonly ProgressArrowGlyph _arrow;
        private readonly VisualElement _action;

        public RecipeRow(string testid, string arrowTestid)
        {
            name = testid;
            AddToClassList(UssClassName);

            // 枠の飾りは最初の子にして中身より下に描く
            // The frame decoration is the first child so it draws below the contents
            Add(new RecipeRowFrame());

            // 素材・中央列・結果の 3 列（Web の grid の 1fr・ボタン幅・1fr）
            // Three columns: materials, center and results (the Web grid's 1fr, button width, 1fr)
            _materials = new RecipeSlotColumn(testid, "recipe-row__materials");
            Add(_materials);
            var center = new VisualElement();
            center.AddToClassList("recipe-row__center");
            Add(center);
            _results = new RecipeSlotColumn(testid, "recipe-row__results");
            Add(_results);

            // 中央列は秒数→矢印→操作の縦積み（裁定 2026-08-20）。操作が無ければ操作段を描かない（裁定 2026-09-05）
            // The center stacks duration, arrow and action (ruling 2026-08-20); no action means no action slot (ruling 2026-09-05)
            _duration = new LocalizedLabel { name = $"{testid}-duration", pickingMode = PickingMode.Ignore };
            _duration.AddToClassList("recipe-row__duration");
            center.Add(_duration);
            _arrow = new ProgressArrowGlyph(arrowTestid);
            _arrow.AddToClassList("recipe-row__arrow");
            center.Add(_arrow);
            _action = new VisualElement();
            _action.AddToClassList("recipe-row__action");
            center.Add(_action);
        }

        public void SetSlots(IReadOnlyList<VisualElement> materials, IReadOnlyList<VisualElement> results)
        {
            _materials.SetSlots(materials);
            _results.SetSlots(results);
        }

        public void SetDuration(UiText duration)
        {
            _duration.SetText(duration);
        }

        public void SetProgress(float ratio01)
        {
            _arrow.SetProgress(ratio01);
        }

        public void SetIdle()
        {
            _arrow.SetIdle();
        }

        public void SetAction(VisualElement action)
        {
            _action.Clear();
            _action.Add(action);
            _action.AddToClassList(ActionShownUssClassName);
        }

        public void ClearAction()
        {
            _action.Clear();
            _action.RemoveFromClassList(ActionShownUssClassName);
        }
    }
}

using System;
using Client.Ui.Foundation.Text;
using Client.Ui.State.Text;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Components.Controls
{
    /// <summary>
    /// 主要アクションの青グラデのボタン（クラフト・研究する）。面の 3 色・寸法・字・縁はつまみで受ける
    /// Primary action button with the blue gradient (craft, research); face colors, size, type and outline come from knobs
    /// </summary>
    /// <remarks>
    /// 既定はクラフトの見た目。研究は画面が祖先で --recipe-action-* を上書きする。長押しは置く側が Foundation の UiPressGesture を重ねる。
    /// Defaults to the craft look; research overrides --recipe-action-* on an ancestor. Hold-to-craft is layered on by the caller with the Foundation UiPressGesture.
    /// </remarks>
    public sealed class RecipeActionButton : Button
    {
        public const string UssClassName = "recipe-action-button";
        public const string LabelUssClassName = "recipe-action-button__label";
        public const string FaceStartProperty = "--recipe-action-button-face-start";
        public const string FaceMiddleProperty = "--recipe-action-button-face-middle";
        public const string FaceEndProperty = "--recipe-action-button-face-end";
        public const string FaceBorderWidthProperty = "--recipe-action-button-face-border-width";

        private readonly Subject<Unit> _clicked = new();
        private readonly LocalizedLabel _label;

        public IObservable<Unit> OnClicked => _clicked;

        public RecipeActionButton(string testid, UiText label, RecipeActionLabelWeight weight)
        {
            name = testid;
            AddToClassList(UssClassName);

            // GameUi は入力欄以外にフォーカスを持たせない
            // GameUi never lets anything but text fields hold focus
            focusable = false;

            // 面を先に、文言をその上に重ねる（どちらも押下を横取りしない）
            // Face first, then the label on top (neither steals the press)
            Add(new RecipeActionButtonFace());
            _label = new LocalizedLabel { pickingMode = PickingMode.Ignore };
            _label.AddToClassList(LabelUssClassName);
            // 字の太さは Web と同じ字形を選ぶ（クラフト 500、研究 400 は付けない既定の字）
            // Pick the same weight as the Web (craft 500; research 400 is the default face with no class)
            if (weight == RecipeActionLabelWeight.Medium) _label.AddToClassList(UiTypographyClass.TextMedium);
            SetLabel(label);
            Add(_label);

            // 押下は UniRx で外へ出す
            // Presses leave through UniRx
            clicked += () => _clicked.OnNext(Unit.Default);
        }

        public void SetLabel(UiText label)
        {
            _label.SetText(label);
        }
    }
}

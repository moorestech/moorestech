using System;
using Client.Ui.Components.Panels;
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.Foundation.Text;
using Client.Ui.Screens.Skit.Parts.Window;
using Client.Ui.State.Skit;
using Client.Ui.State.Text;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Skit.Parts.Choices
{
    /// <summary>
    /// 選択肢の板 1 枚。面は左右だけ水平フェード、上下の線と菱形は内側に全不透明で載せ、文は中央に 1 行で置く（Web .choice）
    /// One choice plate; the face fades only at its left and right ends, the rules and diamonds sit inset at full opacity, and the label is one centered line (Web .choice)
    /// </summary>
    public sealed class SkitChoiceButton : Button
    {
        private readonly Subject<Unit> _onClicked = new();
        private readonly UiOsCursorHoverRequest _hover;

        public IObservable<Unit> OnClicked => _onClicked;

        public SkitChoiceButton(SkitChoiceItem item, bool lead, IUiOsCursor osCursor)
        {
            name = $"skit-choice-{item.ChoiceId}";
            focusable = false;
            text = string.Empty;
            AddToClassList("skit-choice");
            EnableInClassList("skit-choice--lead", lead);

            // 面は部品の種別 SkitChoice（左右だけの水平フェードと押下の面）。入力はこのボタンが受けるので板と本文の箱は拾わせない（D-37）
            // The face is the SkitChoice kind (side-only fade and pressed face); this button takes input, so neither the plate nor its body box is pickable (D-37)
            var plate = new GamePanelSurface($"skit-choice-{item.ChoiceId}-plate", GamePanelSurfaceKind.SkitChoice);
            plate.pickingMode = PickingMode.Ignore;
            plate.contentContainer.pickingMode = PickingMode.Ignore;
            Add(plate);
            Add(CreateDecor("skit-choice__rule"));
            Add(new SkitChoiceMarker("skit-choice__marker--start"));
            Add(new SkitChoiceMarker("skit-choice__marker--end"));

            // 文は菱形を避けた中央に置き、はみ出しは省略
            // Center the label clear of the diamonds; ellipsis on overflow
            var labelBox = CreateDecor("skit-choice__label-box");
            var label = new LocalizedLabel();
            label.AddToClassList("skit-choice__label");
            label.pickingMode = PickingMode.Ignore;
            label.SetText(UiText.FromVerbatim(SkitRichText.Literal(item.Label)));
            labelBox.Add(label);
            Add(labelBox);

            clicked += () => _onClicked.OnNext(Unit.Default);

            // 押せる印の指は要素の層で要求する（Web .choice の cursor: pointer。ポーズ中は Ignore で PointerEnter が来ず出ない）
            // The pointer finger is requested on the element layer (Web .choice cursor: pointer; while paused Ignore sends no PointerEnter so none shows)
            _hover = new UiOsCursorHoverRequest(this, osCursor, UiOsCursorShape.Pointer, UiOsCursorLayer.Element);
        }

        // ポーズ中は拾わない（Web の pointer-events: none）。乗っていた指は PointerLeave を待たずに外す
        // Not pickable while paused (Web pointer-events: none); a held finger is withdrawn without waiting for PointerLeave
        public void SetInteractive(bool interactive)
        {
            pickingMode = interactive ? PickingMode.Position : PickingMode.Ignore;
            if (!interactive) _hover.Release();
        }

        private static VisualElement CreateDecor(string className)
        {
            var element = new VisualElement { pickingMode = PickingMode.Ignore };
            element.AddToClassList(className);
            return element;
        }
    }
}

using System;
using Client.Ui.Components.Controls;
using Client.Ui.Components.Panels;
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.Foundation.Layout;
using Client.Ui.Foundation.Stage.ScaleReading;
using Client.Ui.Foundation.Text;
using Client.Ui.State.Modal;
using Client.Ui.State.Modal.Logic;
using Client.Ui.State.Text;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Modal
{
    /// <summary>
    /// 項目名と値の行を見せるモーダル
    /// - 面はレシピ詳細と同じ Craft 枠
    /// - 題の罫線なし（visual-vocabulary §3）
    /// A modal showing label-and-values rows
    /// - Face is the recipe viewer's Craft frame
    /// - No title rules (visual-vocabulary §3)
    /// </summary>
    public sealed class ModalTableView : VisualElement
    {
        private readonly ModalRequest _request;
        private readonly IModalCommands _commands;
        private readonly IObservable<Unit> _backdropClicked;
        private readonly StageScaleSource _stageScale;
        private IDisposable _backdropSubscription;
        private IDisposable _stageScaleSubscription;

        public ModalTableView(ModalRequest request, IModalCommands commands, IObservable<Unit> backdropClicked, IUiOsCursor osCursor, StageScaleSource stageScale)
        {
            _request = request;
            _commands = commands;
            _backdropClicked = backdropClicked;
            _stageScale = stageScale;
            AddToClassList("modal-table-view");

            // Craft 枠へ縦に積む
            // Stack vertically inside the Craft frame
            var panel = new GamePanel("modal", GamePanelFrame.Craft);
            panel.Add(TextLabel("modal-title", request.Title, UiTypographyClass.TitleOrder4, "modal-table-view__title"));
            panel.Add(TextLabel("modal-message", request.Message, UiTypographyClass.TextSm, "modal-table-view__message"));
            panel.Add(CreateTable());
            panel.Add(CreateActions());
            Add(panel);

            // 幕の押下と舞台の倍率は載っている間だけ聞く
            // Listen for backdrop presses and the stage scale only while attached
            RegisterCallback<AttachToPanelEvent>(OnAttached);
            RegisterCallback<DetachFromPanelEvent>(OnDetached);

            #region Internal

            VisualElement CreateTable()
            {
                // 1行は項目名の行と、その下に字下げした値の行の2行1組。罫線・列そろえは作らない
                // Each row is a label line with an indented values line below; no rules or column alignment
                var table = new UiStack(UiGap.Sm) { name = "modal-table", pickingMode = PickingMode.Ignore };
                table.AddToClassList("modal-table-view__table");
                for (var i = 0; i < request.Rows.Count; i++)
                {
                    var row = new UiStack(UiGap.Px2) { name = $"modal-table-row-{i}", pickingMode = PickingMode.Ignore };
                    row.AddToClassList("modal-table-view__row");
                    row.AddItem(TextLabel($"modal-table-row-{i}-label", request.Rows[i].Label, UiTypographyClass.TextSm, "modal-table-view__row-label"));
                    row.AddItem(TextLabel($"modal-table-row-{i}-values", request.Rows[i].Values, UiTypographyClass.TextSm, "modal-table-view__row-values"));
                    table.AddItem(row);
                }
                return table;
            }

            VisualElement CreateActions()
            {
                // 閉じるは副次ボタンで右へ
                // Close is a secondary button, aligned right
                var actions = new VisualElement { pickingMode = PickingMode.Ignore };
                actions.AddToClassList("modal-table-view__actions");
                var button = new PanelActionButton("modal-button", UiText.FromVerbatim(request.ButtonText), osCursor);
                button.OnClicked.Subscribe(_ => _commands.Respond(ModalResponse.Confirm(_request, null)));
                actions.Add(button);
                return actions;
            }

            LocalizedLabel TextLabel(string testid, string text, string typographyClass, string className)
            {
                var label = new LocalizedLabel { name = testid, pickingMode = PickingMode.Ignore };
                label.AddToClassList(typographyClass);
                label.AddToClassList(className);
                label.SetText(UiText.FromVerbatim(text));
                return label;
            }

            #endregion
        }

        private void OnAttached(AttachToPanelEvent evt)
        {
            _backdropSubscription = _backdropClicked.Subscribe(_ => _commands.Respond(ModalResponse.Cancel(_request)));

            // 宿主は実画面 px なので、インベントリと同じ大きさになるよう舞台の倍率を掛け直す
            // The host is in real-screen px, so reapply the stage scale to match the inventory's size
            _stageScaleSubscription = _stageScale.Scale.Subscribe(scale => style.scale = new Scale(new Vector3(scale, scale, 1f)));
        }

        private void OnDetached(DetachFromPanelEvent evt)
        {
            _backdropSubscription?.Dispose();
            _backdropSubscription = null;
            _stageScaleSubscription?.Dispose();
            _stageScaleSubscription = null;
        }
    }
}

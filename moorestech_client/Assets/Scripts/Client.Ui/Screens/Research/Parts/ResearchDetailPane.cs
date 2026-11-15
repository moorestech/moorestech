using System;
using Client.Ui.Components.Controls;
using Client.Ui.Components.Panels;
using Client.Ui.Content;
using Client.Ui.Foundation.Layout;
using Client.Ui.Foundation.Text;
using Client.Ui.State.Research.Commands;
using Client.Ui.State.Research.Tree;
using Client.Ui.State.Text;
using Client.Ui.State.Toast;
using Mooresmaster.Localization.Generated;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Research.Parts
{
    /// <summary>
    /// 選択ノードの詳細ペイン
    /// Detail pane of the selected node
    /// </summary>
    public sealed class ResearchDetailPane : VisualElement
    {
        private readonly LocalizedLabel _name;
        private readonly LocalizedLabel _description;
        private readonly GameScrollView _scroll;
        private readonly ResearchConsumeItemsSection _consume;
        private readonly ResearchUnlockSections _unlocks;
        private readonly ResearchDetailActions _actions;
        private bool _hasRendered;
        private Guid _renderedGuid;

        public ResearchDetailPane(IResearchTreeCommands commands, GameContentPresentation content, UiCommandResultToaster toaster)
        {
            name = "research-detail-pane";
            AddToClassList("research-tree__detail");
            pickingMode = PickingMode.Ignore;
            var panel = new GamePanel("research-detail-panel", GamePanelFrame.Craft);
            panel.AddToClassList("research-tree__detail-panel");
            var body = new VisualElement();
            body.AddToClassList("research-tree__detail-body");

            // 見出し: 名前と閉じる
            // Header: name and close
            var header = new VisualElement();
            header.AddToClassList("research-tree__detail-header");
            _name = new LocalizedLabel();
            _name.AddToClassList("research-tree__detail-name");
            header.Add(_name);
            header.Add(CloseButton());

            // 見出しとボタンの間だけスクロール
            // Scroll only between header and button
            _scroll = new GameScrollView("research-detail-scroll", GameScrollAxis.Vertical);
            _scroll.AddToClassList("research-tree__detail-scroll");
            var scrollContent = new VisualElement();
            scrollContent.AddToClassList("research-tree__detail-scroll-content");
            _description = new LocalizedLabel();
            _description.AddToClassList("research-tree__detail-description");
            _consume = new ResearchConsumeItemsSection(content);
            _unlocks = new ResearchUnlockSections(content);
            scrollContent.Add(_description);
            scrollContent.Add(_consume);
            scrollContent.Add(_unlocks);
            _scroll.Add(scrollContent);
            ResearchCalcKnobs.ApplyScrollBleed(_scroll, scrollContent);

            _actions = new ResearchDetailActions(commands, toaster);
            body.Add(header);
            body.Add(_scroll);
            body.Add(_actions);
            panel.Add(body);
            Add(panel);

            #region Internal

            // 閉じるは素の要素＋クリック
            // Close is a plain element with a click
            VisualElement CloseButton()
            {
                var close = new VisualElement { name = "research-detail-close" };
                close.AddToClassList("research-tree__detail-close");
                var symbol = new LocalizedLabel { pickingMode = PickingMode.Ignore };
                symbol.AddToClassList("research-tree__detail-close-symbol");
                symbol.SetText(new UiText(LocalizationKeys.Ui.Research.CloseSymbol));
                close.Add(symbol);
                close.RegisterCallback<ClickEvent>(_ => commands.CloseDetail());
                return close;
            }

            #endregion
        }

        public void Render(ResearchDetailViewModel detail)
        {
            // 隠した後の最初か、ノードが替わったらスクロールを頭へ戻す（Web は閉じるとペインを捨て、ScrollArea は key={guid}）
            // Reset the scroll on the first render after hiding or when the node changes (Web discards the pane on close; ScrollArea key={guid})
            if (!_hasRendered || _renderedGuid != detail.Guid) _scroll.SetScrollOffset(0f);
            _hasRendered = true;
            _renderedGuid = detail.Guid;
            UiDisplay.SetShown(this, true);
            _name.SetText(UiText.FromContent(ContentLocalizationKeys.ResearchName(detail.Guid)));
            _description.SetText(UiText.FromContent(ContentLocalizationKeys.ResearchDescription(detail.Guid)));
            _consume.Render(detail.ConsumeSlots);
            _unlocks.Render(detail.Unlocks);
            _actions.Render(detail.Guid, detail.Button, detail.NodeStatesReceived);
        }

        public void Hide()
        {
            // 次に出すときは頭から（同じノードでも）
            // The next show starts from the top, even for the same node
            UiDisplay.SetShown(this, false);
            _hasRendered = false;
        }
    }
}

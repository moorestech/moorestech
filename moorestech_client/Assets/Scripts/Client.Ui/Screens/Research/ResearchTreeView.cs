using System;
using Client.Ui.Components.NodeGraph;
using Client.Ui.Components.Panels;
using Client.Ui.Content;
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.Screens.Research.Parts;
using Client.Ui.State.Research.Commands;
using Client.Ui.State.Research.Tree;
using Client.Ui.State.Text;
using Client.Ui.State.Toast;
using Mooresmaster.Localization.Generated;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Research
{
    /// <summary>
    /// 研究エリア（カード＋詳細ペイン）
    /// Research area (cards + detail pane)
    /// </summary>
    public sealed class ResearchTreeView : VisualElement
    {
        private readonly NodeGraphView _graph;
        private readonly ResearchNodeCardSet _cards;
        private readonly VisualElement _detailHost;
        private readonly ResearchDetailPane _detailPane;
        private bool _hasGraph;
        private bool _hasFocus;
        private Vector2 _focus;

        public ResearchTreeView(IResearchTreeCommands commands, GameContentPresentation content, UiCommandResultToaster toaster, IUiOsCursor osCursor)
        {
            name = "research-tree";
            AddToClassList("research-tree");
            pickingMode = PickingMode.Ignore;
            // leftのcalcは要素ごとに1回登録
            // Register the left calc once per element
            ResearchCalcKnobs.ApplyAreaLeft(this);

            // パネルとツリー。タップで選択切替
            // Panel and tree; taps toggle selection
            var panel = new GamePanel("research-panel", GamePanelFrame.Default);
            panel.AddToClassList("research-tree__panel");
            panel.SetTitle(new UiText(LocalizationKeys.Ui.Research.Title));
            _graph = new NodeGraphView("research", osCursor);
            _graph.AddToClassList("research-tree__graph");
            _graph.OnNodeTapped.Subscribe(key => commands.ToggleNodeSelection(Guid.Parse(key)));
            panel.Add(_graph);
            Add(panel);

            // 詳細ペインは視野の外に浮かべる
            // Detail pane floats outside pan and zoom
            _detailHost = new VisualElement { pickingMode = PickingMode.Ignore };
            _detailHost.AddToClassList("research-tree__detail-host");
            Add(_detailHost);
            _detailPane = new ResearchDetailPane(commands, content, toaster);
            _cards = new ResearchNodeCardSet(content);
        }

        public void Render(ResearchTreeViewModel model)
        {
            // 並びか注目点の変化時だけ組み直す
            // Rebuild only when order or focus changes
            var orderChanged = _cards.RenderCards(model.Nodes);
            var hasFocus = model.TryGetInitialFocus(out var focus);
            var focusChanged = hasFocus != _hasFocus || (hasFocus && focus != _focus);
            if (!_hasGraph || orderChanged || focusChanged)
            {
                _graph.SetGraph(_cards.BuildContent(model.Nodes, hasFocus ? NodeGraphFocus.At(focus) : NodeGraphFocus.None));
                _hasGraph = true;
                _hasFocus = hasFocus;
                _focus = focus;
            }

            // 詳細は選択の有無で出し分ける
            // Details show or hide by the selection
            if (!model.TryGetDetail(out var detail))
            {
                _detailPane.Hide();
                return;
            }
            _detailPane.Render(detail);
            if (_detailPane.parent == null) _detailHost.Add(_detailPane);
        }

        public void SetShown(bool shown)
        {
            // 隠す間も要素を残し視野を保つ
            // Keep the element hidden to keep the viewport
            style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}

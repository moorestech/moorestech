using System;
using Client.Ui.Components.NodeGraph;
using Client.Ui.Components.Panels;
using Client.Ui.Content;
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.Foundation.Layout;
using Client.Ui.Screens.Research.Parts;
using Client.Ui.State.Research.Commands;
using Client.Ui.State.Research.Tree;
using Client.Ui.State.Text;
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
        private ResearchTreeViewModel _shown;

        public ResearchTreeView(IResearchTreeCommands commands, GameContentPresentation content, IUiOsCursor osCursor)
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
            _detailPane = new ResearchDetailPane(commands, content);
            _cards = new ResearchNodeCardSet(content);
        }

        public void Render(ResearchTreeViewModel model)
        {
            // カードは毎回描き直し、グラフの形（並び・座標・前提・注目点）が変わったときだけ渡し直す
            // Cards are redrawn every time; content is handed over only when the graph shape changes (order, positions, edges, focus)
            _cards.RenderCards(model.Nodes);
            if (_shown == null || !model.HasSameGraphAs(_shown))
            {
                var hasFocus = model.TryGetInitialFocus(out var focus);
                _graph.SetGraph(_cards.BuildContent(model.Nodes, hasFocus ? NodeGraphFocus.At(focus) : NodeGraphFocus.None));
            }
            _shown = model;

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
            UiDisplay.SetShown(this, shown);
        }
    }
}

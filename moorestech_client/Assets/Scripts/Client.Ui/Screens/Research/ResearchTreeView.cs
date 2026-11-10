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
    /// 研究エリア（Web ResearchTreePanel）。常駐の NodeGraphView にカードを使い回して渡し、選択があれば詳細ペインを浮かべる
    /// The research area (Web ResearchTreePanel); hands reused cards to a resident NodeGraphView and floats the detail pane while selected
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
            // left の calc は要素ごとに 1 回だけ登録する（コールバックが重なるため）
            // Register the left calc once per element (callbacks would stack)
            ResearchCalcKnobs.ApplyAreaLeft(this);

            // 題「研究」のパネルとツリー。タップは選択の切り替えへ
            // The "Research" panel and the tree; taps toggle the selection
            var panel = new GamePanel("research-panel", GamePanelFrame.Default);
            panel.AddToClassList("research-tree__panel");
            panel.SetTitle(new UiText(LocalizationKeys.Ui.Research.Title));
            _graph = new NodeGraphView("research", osCursor);
            _graph.AddToClassList("research-tree__graph");
            _graph.OnNodeTapped.Subscribe(key => commands.ToggleNodeSelection(Guid.Parse(key)));
            panel.Add(_graph);
            Add(panel);

            // 詳細ペインはパン・ズームの外に浮かべ、最初の詳細で載せる（キーの無いラベルを載せない）
            // The detail pane floats outside pan and zoom and mounts on the first details (no keyless labels attached)
            _detailHost = new VisualElement { pickingMode = PickingMode.Ignore };
            _detailHost.AddToClassList("research-tree__detail-host");
            Add(_detailHost);
            _detailPane = new ResearchDetailPane(commands, content, toaster);
            _cards = new ResearchNodeCardSet(content);
        }

        public void Render(ResearchTreeViewModel model)
        {
            // グラフの組み直しはノードの並びか注目点が変わったときだけ（視野を残すため）
            // Rebuild the graph only when node order or focus changes (to keep the viewport)
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
            // 隠す間も要素は残し、NodeGraphView の視野を保つ（C4-d D-23(3)）
            // Keep the element while hidden so NodeGraphView keeps its viewport (C4-d D-23(3))
            style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}

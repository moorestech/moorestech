using System.Collections.Generic;
using System.Linq;
using Client.UitkPoc.RecipeGraph.Layout;
using Client.UitkPoc.RecipeGraph.Model;
using Client.UitkPoc.RecipeGraph.View;
using Client.UitkPoc.Shell;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.UitkPoc.RecipeGraph
{
    // Satisfactory計算機風のレシピチェーン。目標と毎分量を選ぶと、DAGを解いて自動レイアウトする
    // Satisfactory-calculator-like chain: pick a target and rate, solve the DAG and auto-layout it
    public class RecipeGraphDemo : IUitkPocDemo
    {
        private readonly UitkPocContext _context;
        private readonly Dictionary<string, int> _recipeChoice = new();
        private RecipeBook _book;
        private RecipeGraphView _graphView;
        private RecipeSummaryPanel _summary;
        private string _targetGuid;
        private float _targetPerMinute = 5f;

        public string Title => "レシピチェーン";

        public RecipeGraphDemo(UitkPocContext context)
        {
            _context = context;
        }

        public VisualElement CreateView()
        {
            _book = new RecipeBook(_context.ModDirectory);
            var root = new VisualElement();
            root.styleSheets.Add(Resources.Load<StyleSheet>("UitkPoc/RecipeGraph"));
            root.AddToClassList("rg-root");

            // グラフ領域の左上にツールバーを浮かせ、右に集計を置く
            // Float a toolbar over the graph's top-left and put the summary on the right
            var graphArea = new VisualElement();
            graphArea.AddToClassList("rg-graph-area");
            _graphView = new RecipeGraphView(_context);
            _graphView.RecipeCycleRequested.Subscribe(CycleRecipe);
            graphArea.Add(_graphView);
            graphArea.Add(CreateToolbar());
            root.Add(graphArea);
            _summary = new RecipeSummaryPanel(_context);
            root.Add(_summary);
            Recompute();
            return root;

            #region Internal

            VisualElement CreateToolbar()
            {
                var toolbar = new VisualElement();
                toolbar.AddToClassList("rg-toolbar");
                var targets = _book.RecipesByOutput.Keys.Where(g => !_book.FluidGuids.Contains(g)).OrderBy(g => _book.ResourceNames[g]).ToList();
                _targetGuid = targets.First(g => _book.ResourceNames[g] == "コンピュータ");
                var targetField = new DropdownField("目標", targets.Select(g => _book.ResourceNames[g]).ToList(), targets.IndexOf(_targetGuid));
                targetField.RegisterValueChangedCallback(evt => { _targetGuid = targets[targetField.index]; Recompute(); });
                var rateLabel = new Label($"{_targetPerMinute:0}/分");
                var rateSlider = new Slider("毎分", 1f, 120f) { value = _targetPerMinute };
                rateSlider.RegisterValueChangedCallback(evt => { _targetPerMinute = Mathf.Round(evt.newValue); rateLabel.text = $"{_targetPerMinute:0}/分"; Recompute(); });
                var flowToggle = new Toggle("流れ") { value = true };
                flowToggle.RegisterValueChangedCallback(evt => _graphView.SetFlowAnimation(evt.newValue));
                toolbar.Add(targetField);
                toolbar.Add(rateSlider);
                toolbar.Add(rateLabel);
                toolbar.Add(flowToggle);
                toolbar.Add(new Button(_graphView.FitToView) { text = "全体表示" });
                toolbar.Add(new Button(_graphView.ResetManualPositions) { text = "整列" });
                return toolbar;
            }

            #endregion
        }

        private void CycleRecipe(string resourceGuid)
        {
            // 代替レシピを順送りし、構造ごと解き直す
            // Rotate to the next alternative recipe and re-solve the structure
            var count = _book.RecipesByOutput[resourceGuid].Count;
            var current = _recipeChoice.TryGetValue(resourceGuid, out var index) ? index : _book.DefaultRecipeIndex(resourceGuid);
            _recipeChoice[resourceGuid] = (current + 1) % count;
            Recompute();
        }

        private void Recompute()
        {
            var startTime = Time.realtimeSinceStartupAsDouble;
            var graph = RecipeChainSolver.Solve(_book, _targetGuid, _targetPerMinute, _recipeChoice);
            var heights = Enumerable.Repeat(RecipeNodeElement.Height, graph.Nodes.Count).ToArray();
            var layout = LayeredGraphLayout.Compute(heights, graph.Edges.Select(e => (e.FromNode, e.ToNode)).ToList(), RecipeNodeElement.Width, 110f, 28f);
            var layoutMilliseconds = (Time.realtimeSinceStartupAsDouble - startTime) * 1000.0;
            _graphView.SetGraph(graph, layout);
            _summary.SetGraph(graph, layoutMilliseconds);
        }
    }
}

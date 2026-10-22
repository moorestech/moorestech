using System.Linq;
using Client.UitkPoc.RecipeGraph.Model;
using Client.UitkPoc.RecipeGraph.View;
using Client.UitkPoc.Shell;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.UitkPoc.RecipeGraph
{
    // 右側の集計パネル。機械種別の合計台数と原料の毎分量を出す
    // Right summary panel: machine totals by type and raw inputs per minute
    public class RecipeSummaryPanel : ScrollView
    {
        private readonly UitkPocContext _context;

        public RecipeSummaryPanel(UitkPocContext context)
        {
            _context = context;
            AddToClassList("rg-summary");
        }

        public void SetGraph(ProductionGraph graph, double layoutMilliseconds)
        {
            Clear();
            AddHeading($"ノード {graph.Nodes.Count} / 辺 {graph.Edges.Count} / レイアウト {layoutMilliseconds:0.0}ms");

            // 機械ごとに台数を合算し、切り上げ台数で並べる
            // Sum machines per type and order by rounded-up count
            AddHeading("機械");
            var machines = graph.Nodes.Where(n => n.MachineCount > 0).GroupBy(n => n.MachineName)
                .Select(g => (name: g.Key, exact: g.Sum(n => n.MachineCount), rounded: g.Sum(n => Mathf.CeilToInt(n.MachineCount - 0.0001f))))
                .OrderByDescending(m => m.rounded);
            foreach (var machine in machines) AddRow(null, machine.name, $"{machine.rounded}台", $"(×{machine.exact:0.##})");

            AddHeading("原料");
            foreach (var raw in graph.Nodes.Where(n => n.IsRaw).OrderByDescending(n => n.RatePerMinute))
            {
                AddRow(_context.LoadItemIcon(raw.ResourceName), raw.ResourceName, $"{RecipeNodeElement.FormatRate(raw.RatePerMinute)}/分", raw.MachineName);
            }

            #region Internal

            void AddHeading(string text)
            {
                var heading = new Label(text);
                heading.AddToClassList("rg-summary__heading");
                Add(heading);
            }

            void AddRow(Texture2D icon, string name, string value, string note)
            {
                var row = new VisualElement();
                row.AddToClassList("rg-summary__row");
                var iconElement = new VisualElement();
                iconElement.AddToClassList("rg-summary__icon");
                if (icon != null) iconElement.style.backgroundImage = icon;
                row.Add(iconElement);
                var nameColumn = new VisualElement();
                nameColumn.AddToClassList("rg-summary__name-column");
                nameColumn.Add(new Label(name));
                var noteLabel = new Label(note);
                noteLabel.AddToClassList("rg-summary__note");
                nameColumn.Add(noteLabel);
                row.Add(nameColumn);
                var valueLabel = new Label(value);
                valueLabel.AddToClassList("rg-summary__value");
                row.Add(valueLabel);
                Add(row);
            }

            #endregion
        }
    }
}

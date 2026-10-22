using Client.UitkPoc.RecipeGraph.Model;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.UitkPoc.RecipeGraph.View
{
    // 1ノード分のカード。アイコン・資源名・毎分量・機械と台数・代替レシピ数を出す
    // One node card: icon, resource name, per-minute rate, machine with count, alternative recipe count
    public class RecipeNodeElement : VisualElement
    {
        public const float Width = 220f;
        public const float Height = 96f;

        public readonly string ResourceGuid;
        public readonly Label AlternativeBadge;
        private readonly Label _rateLabel;
        private readonly Label _machineLabel;
        private readonly Label _countBadge;

        public RecipeNodeElement(ProductionNode node, Texture2D icon)
        {
            ResourceGuid = node.ResourceGuid;
            AddToClassList("rg-node");
            if (node.IsRaw) AddToClassList("rg-node--raw");
            if (node.IsFluid) AddToClassList("rg-node--fluid");
            style.width = Width;
            style.height = Height;

            // 上段: アイコンと名前・毎分量
            // Top row: icon with name and per-minute rate
            var top = new VisualElement();
            top.AddToClassList("rg-node__top");
            var iconElement = new VisualElement();
            iconElement.AddToClassList("rg-node__icon");
            if (icon != null) iconElement.style.backgroundImage = icon;
            else iconElement.Add(new Label(node.ResourceName.Substring(0, 1)) { pickingMode = PickingMode.Ignore });
            top.Add(iconElement);
            var titleColumn = new VisualElement();
            titleColumn.AddToClassList("rg-node__title-column");
            var title = new Label(node.ResourceName);
            title.AddToClassList("rg-node__title");
            _rateLabel = new Label();
            _rateLabel.AddToClassList("rg-node__rate");
            titleColumn.Add(title);
            titleColumn.Add(_rateLabel);
            top.Add(titleColumn);
            Add(top);

            // 下段: 機械名と台数バッジ、代替レシピの切替バッジ
            // Bottom row: machine name, count badge, alternative recipe badge
            var bottom = new VisualElement();
            bottom.AddToClassList("rg-node__bottom");
            _machineLabel = new Label();
            _machineLabel.AddToClassList("rg-node__machine");
            _countBadge = new Label();
            _countBadge.AddToClassList("rg-node__count");
            AlternativeBadge = new Label();
            AlternativeBadge.AddToClassList("rg-node__alt");
            bottom.Add(_machineLabel);
            bottom.Add(_countBadge);
            bottom.Add(AlternativeBadge);
            Add(bottom);

            foreach (var child in this.Query<VisualElement>().Build()) if (child != this && child != AlternativeBadge) child.pickingMode = PickingMode.Ignore;
            SetNumbers(node);
        }

        // 構造が同じまま量だけ変わった時はテキストだけ差し替える
        // When only amounts change, swap texts and keep the element
        public void SetNumbers(ProductionNode node)
        {
            _rateLabel.text = $"{FormatRate(node.RatePerMinute)} /分";
            _machineLabel.text = node.MachineName;
            _countBadge.text = node.MachineCount <= 0 ? "-" : $"×{node.MachineCount:0.##}";
            _countBadge.tooltip = $"必要台数 {Mathf.CeilToInt(node.MachineCount - 0.0001f)} 台";
            AlternativeBadge.text = node.AlternativeRecipeCount > 1 ? $"⇄{node.AlternativeRecipeCount}" : "";
            AlternativeBadge.style.display = node.AlternativeRecipeCount > 1 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public static string FormatRate(float rate)
        {
            return rate >= 100 ? rate.ToString("0") : rate >= 10 ? rate.ToString("0.#") : rate.ToString("0.##");
        }
    }
}

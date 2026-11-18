using System;
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Stage.Columns
{
    /// <summary>
    /// 舞台の上段 3 列の入れ物。画面は列へ Add するだけで、位置と余白はここが持つ
    /// Container of the three top-row stage columns; screens only Add into a column and this owns position and padding
    /// </summary>
    public sealed class StageColumnsLayout : VisualElement
    {
        private const string RootClass = "stage-columns";
        private const string LeftPaddingCollapsedClass = "stage-columns--left-padding-collapsed";
        private const string ColumnClass = "stage-columns__column";

        private readonly VisualElement _inv;
        private readonly VisualElement _viewer;
        private readonly VisualElement _items;

        public StageColumnsLayout()
        {
            name = "stage-columns";
            AddToClassList(RootClass);

            // 入れ物も列も拾わない（Web の .stage は透明な grid で、拾うのは中のパネルだけ）
            // Neither the container nor the columns pick (Web .stage is a transparent grid; only panels inside pick)
            pickingMode = PickingMode.Ignore;
            _inv = CreateColumn("stage-column-inv", "inv");
            _viewer = CreateColumn("stage-column-viewer", "viewer");
            _items = CreateColumn("stage-column-items", "items");
        }

        public VisualElement Get(StageColumn column)
        {
            return column switch
            {
                StageColumn.Inv => _inv,
                StageColumn.Viewer => _viewer,
                StageColumn.Items => _items,
                _ => throw new ArgumentOutOfRangeException(nameof(column), column, null),
            };
        }

        public void SetLeftPaddingCollapsed(bool collapsed)
        {
            EnableInClassList(LeftPaddingCollapsedClass, collapsed);
        }

        private VisualElement CreateColumn(string columnName, string modifier)
        {
            var column = new VisualElement { name = columnName, pickingMode = PickingMode.Ignore };
            column.AddToClassList(ColumnClass);
            column.AddToClassList($"{ColumnClass}--{modifier}");
            Add(column);
            return column;
        }
    }
}

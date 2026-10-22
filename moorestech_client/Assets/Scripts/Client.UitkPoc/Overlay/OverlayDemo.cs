using System.Collections.Generic;
using System.IO;
using System.Linq;
using Client.UitkPoc.Shell;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.UitkPoc.Overlay
{
    // 重なり・操作系（ツールチップ・右クリックメニュー・DnD・仮想化リスト・浮動ウィンドウ・モーダル）の検証
    // Overlay and interaction checks: tooltip, context menu, DnD, virtualized list, floating windows, modal
    public class OverlayDemo : IUitkPocDemo
    {
        private const int VirtualRowCount = 10000;

        private readonly UitkPocContext _context;

        public string Title => "重ね・操作";

        public OverlayDemo(UitkPocContext context)
        {
            _context = context;
        }

        public VisualElement CreateView()
        {
            var root = new VisualElement();
            root.styleSheets.Add(Resources.Load<StyleSheet>("UitkPoc/Overlay"));
            root.AddToClassList("ov-root");
            var itemNames = JObject.Parse(File.ReadAllText(Path.Combine(_context.ModDirectory, "master", "items.json")))["data"]
                .Select(i => (string)i["name"]).Where(n => _context.LoadItemIcon(n) != null).ToList();

            // 最前面の層（ゴースト・ツールチップ・モーダル）は全列より後ろに足して兄弟順で上に置く
            // Top layers (ghost, tooltip, modal) are appended last so sibling order puts them above
            var dragLayer = new VisualElement { pickingMode = PickingMode.Ignore };
            dragLayer.AddToClassList("ov-layer");
            var tooltips = new TooltipLayer();
            var modal = new ModalDialogElement();

            var inventoryColumn = Column(root, "インベントリ（ホバーでツールチップ・ドラッグで入れ替え・右クリックでメニュー）");
            var contents = Enumerable.Range(0, 40).Select(i => i % 3 == 2 ? ((string)null, 0) : (itemNames[(i * 7) % itemNames.Count], (i * 37) % 120 + 1)).ToList();
            inventoryColumn.Add(new InventoryGridElement(_context, dragLayer, tooltips, contents));
            inventoryColumn.Add(new Button(modal.Open) { text = "モーダルを開く" });

            var listColumn = Column(root, $"仮想化リスト（{VirtualRowCount:N0}行）");
            listColumn.Add(new VirtualizedItemList(_context, itemNames, VirtualRowCount));

            var windowArea = Column(root, "浮動ウィンドウ");
            windowArea.AddToClassList("ov-window-area");
            foreach (var (title, position) in new[] { ("ウィンドウA", new Vector2(20, 60)), ("ウィンドウB", new Vector2(120, 180)), ("ウィンドウC", new Vector2(220, 300)) })
            {
                var window = new FloatingWindowElement(title, position, new Vector2(300, 200));
                tooltips.Attach(window);
                windowArea.Add(window);
            }

            root.Add(dragLayer);
            root.Add(tooltips);
            root.Add(modal);
            return root;

            #region Internal

            VisualElement Column(VisualElement parent, string title)
            {
                var column = new VisualElement();
                column.AddToClassList("ov-column");
                var heading = new Label(title);
                heading.AddToClassList("ov-column__title");
                column.Add(heading);
                parent.Add(column);
                return column;
            }

            #endregion
        }
    }
}

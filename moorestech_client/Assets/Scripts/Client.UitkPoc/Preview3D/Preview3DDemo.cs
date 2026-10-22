using System.Collections.Generic;
using System.IO;
using System.Linq;
using Client.Common.Asset;
using Client.UitkPoc.Shell;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.UitkPoc.Preview3D
{
    // three.js相当の検証。実ブロックのモデルをUI内で回し、小窓を大量に並べたときの負荷も見る
    // three.js-equivalent check: spin real block models inside UI and measure many small previews
    public class Preview3DDemo : IUitkPocDemo
    {
        private static readonly int[] GridCounts = { 0, 12, 24, 48 };

        private readonly UitkPocContext _context;
        private readonly List<(string name, string address)> _blocks = new();
        private readonly Dictionary<string, GameObject> _prefabCache = new();
        private readonly List<ModelPreviewElement> _gridPreviews = new();
        private ModelPreviewElement _mainPreview;
        private Label _mainTitle;
        private VisualElement _grid;
        private int _layer;

        public string Title => "3D";

        public Preview3DDemo(UitkPocContext context)
        {
            _context = context;
        }

        public VisualElement CreateView()
        {
            _layer = LayerMask.NameToLayer("IconCapture");
            var blocksJson = JObject.Parse(File.ReadAllText(Path.Combine(_context.ModDirectory, "master", "blocks.json")))["data"];
            foreach (var block in blocksJson) _blocks.Add(((string)block["name"], (string)block["blockPrefabAddressablesPath"]));

            var root = new VisualElement();
            root.styleSheets.Add(Resources.Load<StyleSheet>("UitkPoc/Preview3D"));
            root.AddToClassList("p3-root");
            root.Add(CreateBlockList());

            // 中央: 大きなプレビューの上にUIを重ねる
            // Center: UI layered over the large preview
            var stage = new VisualElement();
            stage.AddToClassList("p3-stage");
            _mainPreview = new ModelPreviewElement(new Vector3(0f, -10000f, 0f), 1024, _layer);
            _mainPreview.AddToClassList("p3-preview--main");
            _mainTitle = new Label();
            _mainTitle.AddToClassList("p3-stage__title");
            var hint = new Label("ドラッグで回転 / ホイールで拡大");
            hint.AddToClassList("p3-stage__hint");
            var autoRotate = new Toggle("自動回転") { value = true };
            autoRotate.AddToClassList("p3-stage__toggle");
            autoRotate.RegisterValueChangedCallback(evt => _mainPreview.SetAutoRotate(evt.newValue));
            _mainPreview.SetAutoRotate(true);
            _mainPreview.Add(_mainTitle);
            _mainPreview.Add(hint);
            _mainPreview.Add(autoRotate);
            stage.Add(_mainPreview);
            root.Add(stage);
            root.Add(CreateGridColumn());
            ShowMain(0);
            return root;
        }

        private VisualElement CreateBlockList()
        {
            // 仮想化ListViewと検索欄。表示中の行だけ要素を作る
            // Virtualized ListView with a search box; rows exist only while visible
            var column = new VisualElement();
            column.AddToClassList("p3-list-column");
            var search = new TextField { value = "" };
            search.AddToClassList("p3-search");
            var filtered = Enumerable.Range(0, _blocks.Count).ToList();
            var list = new ListView(filtered, 30, () => new Label(), (element, index) => ((Label)element).text = _blocks[filtered[index]].name);
            list.AddToClassList("p3-list");
            list.selectionChanged += _ => { if (list.selectedIndex >= 0) ShowMain(filtered[list.selectedIndex]); };
            search.RegisterValueChangedCallback(evt =>
            {
                filtered.Clear();
                filtered.AddRange(Enumerable.Range(0, _blocks.Count).Where(i => _blocks[i].name.Contains(evt.newValue)));
                list.RefreshItems();
            });
            column.Add(search);
            column.Add(list);
            return column;
        }

        private VisualElement CreateGridColumn()
        {
            var column = new VisualElement();
            column.AddToClassList("p3-grid-column");
            var countField = new DropdownField("小窓の数", GridCounts.Select(c => c.ToString()).ToList(), 2);
            countField.RegisterValueChangedCallback(_ => SetGridCount(GridCounts[countField.index]));
            column.Add(countField);
            _grid = new VisualElement();
            _grid.AddToClassList("p3-grid");
            column.Add(_grid);
            SetGridCount(GridCounts[2]);
            return column;
        }

        public void SetGridCount(int count)
        {
            // 必要数まで増やし、余りは木から外す（外れたカメラは止まる）
            // Grow to the requested count and detach extras (detached cameras stop)
            while (_gridPreviews.Count < count)
            {
                var index = _gridPreviews.Count;
                var preview = new ModelPreviewElement(new Vector3(index * 40f, -10000f, 200f), 192, _layer);
                preview.AddToClassList("p3-preview--small");
                preview.SetAutoRotate(true);
                preview.SetModel(BuildModel((index * 5 + 3) % _blocks.Count));
                _gridPreviews.Add(preview);
            }
            _grid.Clear();
            for (var i = 0; i < count; i++) _grid.Add(_gridPreviews[i]);
        }

        private void ShowMain(int blockIndex)
        {
            _mainTitle.text = _blocks[blockIndex].name;
            _mainPreview.SetModel(BuildModel(blockIndex));
        }

        private GameObject BuildModel(int blockIndex)
        {
            var (name, address) = _blocks[blockIndex];
            if (!_prefabCache.TryGetValue(address, out var prefab))
            {
                prefab = AddressableLoader.LoadDefault<GameObject>(address);
                _prefabCache[address] = prefab;
            }
            // 読めないブロックは空の立方体で代用し、ログで気づけるようにする
            // Unloadable blocks fall back to a cube and are logged
            if (prefab != null) return RenderOnlyModelBuilder.Build(prefab, _layer);
            Debug.LogWarning($"[UitkPoc] ブロックのプレハブを読めない: {name} ({address})");
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.layer = _layer;
            return cube;
        }
    }
}

using System.IO;
using System.Linq;
using Client.UitkPoc.Shell;
using Newtonsoft.Json.Linq;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.UitkPoc.TextAndInput
{
    // 文字まわり（マークダウン・文中アイコン・リンク・和文の画質・入力欄・省略記号）の検証
    // Text checks: markdown, inline icons, links, Japanese glyph quality, inputs and ellipsis
    public class TextAndInputDemo : IUitkPocDemo
    {
        private const string HelpMarkdown = @"# 歯車ネットワーク
歯車は**回転数（RPM）**と**トルク**を伝える。:item[鉄インゴット]: を延ばした :item[鉄板]: で作る歯車は、:item[木の板]: のものより多くのトルクを運べる。
## 組み立て
1. [蒸気機関](block:蒸気機関) を置き、:item[石炭]: を入れる
2. `シャフト` でつなぎ、[歯車ベルトコンベア](block:直線歯車ベルトコンベア) を回す
3. 必要な力が足りないと **すべての機械が遅くなる**
- 分岐は [歯車コンベア分岐機](block:歯車コンベア分岐機) で行う
- 長い説明文は自動で折り返され、文中の :item[鉄板]: や :item[銅のワイヤー]: も行と一緒に流れる
> 注意: 回転方向が逆の歯車をつなぐと止まる
---
```
rpm = 入力rpm × (入力歯数 / 出力歯数)
```";

        private readonly UitkPocContext _context;

        public string Title => "文字と入力";

        public TextAndInputDemo(UitkPocContext context)
        {
            _context = context;
        }

        public VisualElement CreateView()
        {
            var root = new VisualElement();
            root.styleSheets.Add(Resources.Load<StyleSheet>("UitkPoc/TextAndInput"));
            root.AddToClassList("tx-root");

            // 1列目: マークダウン風の説明とリンクのポップアップ
            // Column 1: markdown-like help with a link popup
            var helpColumn = Column(root, "マークダウン風の説明（文中アイコン・リンク・選択可）");
            var markdown = new MiniMarkdownView(HelpMarkdown);
            helpColumn.Add(markdown);
            var popup = new Label();
            popup.AddToClassList("tx-link-popup");
            popup.style.display = DisplayStyle.None;
            root.Add(popup);
            markdown.LinkClicked.Subscribe(click =>
            {
                var local = root.WorldToLocal(click.position);
                popup.text = $"<b>{click.linkId.Replace("block:", "")}</b>\nリンクID: {click.linkId}（ここでブロック詳細を開く想定）";
                popup.style.left = local.x + 12;
                popup.style.top = local.y + 12;
                popup.style.display = DisplayStyle.Flex;
            });
            root.RegisterCallback<PointerDownEvent>(_ => popup.style.display = DisplayStyle.None, TrickleDown.TrickleDown);

            // 2列目: 和文の画質比較
            // Column 2: Japanese glyph quality
            var fontColumn = Column(root, "和文の画質（SDF / ビットマップ）");
            fontColumn.Add(new FontQualityPanel(_context.JapaneseFont, _context.JapaneseSourceFont));

            // 3列目: 入力欄と省略記号
            // Column 3: inputs and ellipsis
            var inputColumn = Column(root, "入力・検索ハイライト・省略記号");
            inputColumn.Add(CreateSearch());
            var multiLine = new TextField("複数行") { multiline = true, value = "1行目\n2行目は長い文章で折り返しを確認するための文字列です。" };
            multiLine.AddToClassList("tx-multiline");
            inputColumn.Add(multiLine);
            inputColumn.Add(Note("省略（1行）"));
            inputColumn.Add(Text("tx-ellipsis-single", "非常に長いアイテム名の例：高圧蒸気タービン用の強化複合鉄板フレームアセンブリ（改良型）"));
            inputColumn.Add(Note("省略（3行で打ち切り。CSSのline-clamp相当）"));
            inputColumn.Add(Text("tx-ellipsis-multi", "説明文：この機械は電力を消費して鉱石を粉砕する。粉砕速度は電力の充足率に比例し、充足率が50%を下回ると停止する。上位機種では副産物として石の粉も得られる。長い文章がどこで切られるかを確認する。"));
            return root;

            #region Internal

            VisualElement Column(VisualElement parent, string title)
            {
                var column = new ScrollView();
                column.AddToClassList("tx-column");
                var heading = new Label(title);
                heading.AddToClassList("tx-column__title");
                column.Add(heading);
                parent.Add(column);
                return column;
            }

            Label Note(string text)
            {
                var label = new Label(text);
                label.AddToClassList("tx-note");
                return label;
            }

            Label Text(string className, string text)
            {
                var label = new Label(text);
                label.AddToClassList(className);
                return label;
            }

            #endregion
        }

        private VisualElement CreateSearch()
        {
            // 入力のたびに絞り込み、一致部分を<mark>で強調する
            // Filter on every keystroke and highlight the match with <mark>
            var names = JObject.Parse(File.ReadAllText(Path.Combine(_context.ModDirectory, "master", "items.json")))["data"].Select(i => (string)i["name"]).ToList();
            var box = new VisualElement();
            var field = new TextField("検索") { value = "鉄" };
            field.textEdition.placeholder = "アイテム名で検索";
            var results = new VisualElement();
            results.AddToClassList("tx-search-results");
            box.Add(field);
            box.Add(results);
            field.RegisterValueChangedCallback(evt => Render(evt.newValue));
            Render(field.value);
            return box;

            #region Internal

            void Render(string query)
            {
                results.Clear();
                foreach (var name in names.Where(n => query.Length > 0 && n.Contains(query)).Take(8))
                {
                    var highlighted = name.Replace(query, $"<mark=#f2a64066><b>{query}</b></mark>");
                    // 存在しないsprite名はタグが生の文字で出るため、アイコンのある名前だけタグを付ける
                    // Unknown sprite names print the raw tag, so only names with icons get one
                    var icon = _context.LoadItemIcon(name) != null ? $"<sprite name=\"{name}\"> " : "□ ";
                    results.Add(new Label(icon + highlighted));
                }
            }

            #endregion
        }
    }
}

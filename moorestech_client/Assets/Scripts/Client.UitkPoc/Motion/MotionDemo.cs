using System.Collections.Generic;
using System.Linq;
using Client.UitkPoc.Shell;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.UIElements.Experimental;

namespace Client.UitkPoc.Motion
{
    // framer-motion/CSS animationで書く類の動きを、USS transition・experimental.animation・自前の積分で再現する
    // Recreates framer-motion/CSS-animation style motion with USS transitions, experimental.animation and hand integration
    public class MotionDemo : IUitkPocDemo
    {
        private static readonly string[] ItemNames = { "ICチップ", "ガラス", "ゴム", "コンクリート", "ガラス管", "アスファルト", "Hビーム鋼材", "ゲルマニウムインゴット" };

        private readonly UitkPocContext _context;

        public string Title => "動き";

        public MotionDemo(UitkPocContext context)
        {
            _context = context;
        }

        public VisualElement CreateView()
        {
            var root = new VisualElement();
            root.styleSheets.Add(Resources.Load<StyleSheet>("UitkPoc/Motion"));
            root.AddToClassList("mo-root");
            var toasts = new ToastStack();

            // 1列目: 通知とFLIP並べ替え
            // Column 1: toasts and FLIP reordering
            var column1 = Section(root, "通知の出入り（高さも畳む）と FLIP 並べ替え");
            var toastButton = new Button(() => toasts.Push("鉄板を獲得しました", _context.LoadItemIcon(ItemNames[Random.Range(0, ItemNames.Length)]))) { text = "通知を出す" };
            column1.Add(toastButton);
            var flipList = new FlipReorderList(ItemNames.Select((n, i) => (n, _context.LoadItemIcon(n), (i * 37 + 11) % 97)).ToList());
            var flipButtons = Row(column1);
            flipButtons.Add(new Button(flipList.Shuffle) { text = "シャッフル" });
            flipButtons.Add(new Button(flipList.SortByValue) { text = "値で並べる" });
            column1.Add(flipList);

            // 2列目: ばね・揺れ・カウントアップ
            // Column 2: spring, shake, count-up
            var column2 = Section(root, "ばね・揺れ・カウントアップ（@keyframes の代替）");
            var springArea = new VisualElement();
            springArea.AddToClassList("mo-spring-area");
            springArea.Add(new SpringDragCard("掴んで離すと戻る"));
            column2.Add(springArea);
            var shakeTarget = new VisualElement();
            shakeTarget.AddToClassList("mo-shake-target");
            shakeTarget.style.backgroundImage = _context.LoadItemIcon("ICチップ");
            var countLabel = new Label("0");
            countLabel.AddToClassList("mo-count");
            var effectRow = Row(column2);
            effectRow.Add(new Button(() => Shake(shakeTarget)) { text = "揺らす" });
            effectRow.Add(shakeTarget);
            effectRow.Add(new Button(() => CountUp(countLabel, Random.Range(1000, 99999))) { text = "数える" });
            effectRow.Add(countLabel);

            // 3列目: 時間差フェードインとアコーディオン
            // Column 3: staggered fade-in and accordion
            var column3 = Section(root, "時間差フェードイン・アコーディオン（height:auto の遷移）");
            var grid = new VisualElement();
            grid.AddToClassList("mo-stagger-grid");
            for (var i = 0; i < 24; i++)
            {
                var tile = new VisualElement();
                tile.AddToClassList("mo-tile");
                tile.style.backgroundImage = _context.LoadItemIcon(ItemNames[i % ItemNames.Length]);
                tile.style.transitionDelay = new List<TimeValue> { new TimeValue(i * 35, TimeUnit.Millisecond) };
                grid.Add(tile);
            }
            column3.Add(new Button(() => ReplayStagger(grid)) { text = "もう一度" });
            column3.Add(grid);
            column3.Add(CreateAccordion());
            root.Add(toasts);
            ReplayStagger(grid);
            return root;

            #region Internal

            VisualElement Section(VisualElement parent, string title)
            {
                var section = new VisualElement();
                section.AddToClassList("mo-section");
                var heading = new Label(title);
                heading.AddToClassList("mo-section__title");
                section.Add(heading);
                parent.Add(section);
                return section;
            }

            VisualElement Row(VisualElement parent)
            {
                var row = new VisualElement();
                row.AddToClassList("mo-row");
                parent.Add(row);
                return row;
            }

            #endregion
        }

        private static void Shake(VisualElement target)
        {
            // 減衰する正弦波で左右に揺らす
            // Shake sideways with a decaying sine
            target.experimental.animation.Start(0f, 1f, 600, (element, t) => element.style.translate = new Translate(Mathf.Sin(t * 50f) * (1f - t) * 14f, 0));
        }

        private static void CountUp(Label label, int target)
        {
            label.experimental.animation.Start(0f, 1f, 900, (_, t) => label.text = Mathf.RoundToInt(t * target).ToString("N0")).Ease(Easing.OutCubic);
        }

        private static void ReplayStagger(VisualElement grid)
        {
            foreach (var tile in grid.Children()) tile.AddToClassList("mo-tile--hidden");
            grid.schedule.Execute(() => { foreach (var tile in grid.Children()) tile.RemoveFromClassList("mo-tile--hidden"); }).ExecuteLater(50);
        }

        private static VisualElement CreateAccordion()
        {
            var accordion = new VisualElement();
            accordion.AddToClassList("mo-accordion");
            var body = new VisualElement();
            body.AddToClassList("mo-accordion__body");
            // 高さ0の親の中では子の文字も高さ0で測られて潰れるため、中身をabsoluteにして実寸を測れるようにする
            // Inside a 0-height parent children get measured at 0, so the inner box is absolute to keep its natural height
            var inner = new VisualElement();
            inner.AddToClassList("mo-accordion__inner");
            for (var i = 0; i < 5; i++) inner.Add(new Label($"詳細の行 {i + 1}: 中身の高さは事前に分からない"));
            body.Add(inner);
            var expanded = false;
            var header = new Button(() =>
            {
                // 中身の実測高さへ向けて数値の高さをtransitionさせる
                // Transition a numeric height toward the measured inner height
                expanded = !expanded;
                body.style.height = expanded ? inner.layout.height : 0f;
            }) { text = "▼ 詳細を開く／閉じる" };
            accordion.Add(header);
            accordion.Add(body);
            return accordion;
        }
    }
}

using System.Collections.Generic;
using System.Text.RegularExpressions;
using Client.Localization;
using Client.Tests.Ui.TestHost;
using Client.Ui.Components.Gauges;
using Client.Ui.Components.Recipe;
using Client.Ui.Components.Slots;
using Client.Ui.State.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static Client.Tests.Ui.Components.ThemeTokenFile;

namespace Client.Tests.Ui.Components.Recipe
{
    /// <summary>
    /// 名前・折り返し・列幅・操作段（L1）
    /// Names, wrapping, column widths and action slot (L1)
    /// </summary>
    public class RecipeRowLayoutTest : UiTestHost
    {
        private const string ProbePath = "Assets/Scripts/Client.Tests/Ui/Components/Recipe/RecipeKnobOverrides.uss";
        private const string TestId = "recipe-row-test";
        private const string ArrowTestId = "recipe-row-test-arrow";
        private const string IdleClassName = "progress-arrow-glyph--idle";

        [SetUp]
        public void LoadDictionary()
        {
            Localize.Initialize();
        }

        [Test]
        public void 名前と秒数と矢印はWebのtestidで矢印は静止から始まり進捗で切り替わる()
        {
            var row = Mount(rootVisualElement, 1, 1, 600f);
            Assert.AreEqual(TestId, row.name);
            Assert.AreEqual("5秒", row.Q<Label>($"{TestId}-duration").text);
            var arrow = row.Q<ProgressArrowGlyph>(ArrowTestId);
            Assert.IsTrue(arrow.ClassListContains(IdleClassName));
            row.SetProgress(0.5f);
            Assert.IsFalse(arrow.ClassListContains(IdleClassName));
            row.SetIdle();
            Assert.IsTrue(arrow.ClassListContains(IdleClassName));
        }

        [Test]
        public void 素材と結果は2列で折り返し素材は左へ結果は右へ寄る()
        {
            var row = Mount(rootVisualElement, 3, 3, 600f);
            var pitch = ResolvePixels("--recipe-slot-size-max") + ResolvePixels("--recipe-slot-gap");

            // 素材: 1 行目に 2 点、2 行目の 1 点は左の列。列の左端から並ぶ
            // Materials: two on the first line, the third in the left column below, starting at the column's left edge
            Assert.AreEqual(row.Q(className: "recipe-row__materials").worldBound.xMin, Bound(row, "m-0").xMin, 0.5f);
            Assert.AreEqual(Bound(row, "m-0").xMin + pitch, Bound(row, "m-1").xMin, 0.5f);
            Assert.AreEqual(Bound(row, "m-0").yMin, Bound(row, "m-1").yMin, 0.5f);
            Assert.AreEqual(Bound(row, "m-0").yMin + pitch, Bound(row, "m-2").yMin, 0.5f);
            Assert.AreEqual(Bound(row, "m-0").xMin, Bound(row, "m-2").xMin, 0.5f);

            // 結果: 格子ごと右へ寄り、2 行目の 1 点は格子の左の列（Web の justify-content: end の grid）
            // Results: the whole grid aligns right and the third sits in the grid's left column (the Web justify-content: end grid)
            Assert.AreEqual(row.Q(className: "recipe-row__results").worldBound.xMax, Bound(row, "r-1").xMax, 0.5f);
            Assert.AreEqual(Bound(row, "r-0").xMin, Bound(row, "r-2").xMin, 0.5f);
        }

        [Test]
        public void 中央列はクラフトボタンの幅で左右に列の間隔をとり矢印は素材の点数によらず行の中央()
        {
            var row = Mount(rootVisualElement, 1, 2, 600f);
            var center = row.Q(className: "recipe-row__center");
            Assert.AreEqual(ResolvePixels("--recipe-craft-button-width"), center.layout.width, 0.5f);
            Assert.AreEqual(ResolvePixels("--recipe-column-gap"), center.resolvedStyle.marginLeft, 0.01f);
            Assert.AreEqual(ResolvePixels("--recipe-column-gap"), center.resolvedStyle.marginRight, 0.01f);
            Assert.AreEqual(row.Q(className: "recipe-row__materials").layout.width, row.Q(className: "recipe-row__results").layout.width, 0.5f);
            Assert.AreEqual(row.worldBound.center.x, row.Q(ArrowTestId).worldBound.center.x, 0.5f);
        }

        [Test]
        public void スロットは上限の寸法で機械のレシピ選択の上書きでは32pxと狭いpaddingになる()
        {
            var plain = Mount(rootVisualElement, 1, 1, 600f);
            Assert.AreEqual(ResolvePixels("--recipe-slot-size-max"), Bound(plain, "m-0").width, 0.5f);
            Assert.AreEqual(ResolvePixels("--recipe-row-padding-block"), plain.resolvedStyle.paddingTop, 0.01f);
            plain.RemoveFromHierarchy();

            var probe = AssetDatabase.LoadAssetAtPath<StyleSheet>(ProbePath);
            Assert.IsNotNull(probe, ProbePath);
            var list = new VisualElement();
            list.styleSheets.Add(probe);
            list.AddToClassList("recipe-machine-selection-probe");
            rootVisualElement.Add(list);
            var row = Mount(list, 1, 1, 600f);
            Assert.AreEqual(32f, Bound(row, "m-0").width, 0.5f);

            // 5.6px の padding はレイアウトが倍率 1 の物理画素へ丸めて 6px になる（台帳「実測の結論」C2-a craft の枠・LX-42 と同じ丸め）
            // The 5.6px padding is rounded by layout to physical pixels at scale 1, giving 6px (same rounding as the ledger's C2-a craft frame and LX-42)
            Assert.AreEqual(Mathf.Round(5.6f), row.resolvedStyle.paddingTop, 0.01f);
        }

        [Test]
        public void 操作が無ければ操作段を描かずSetActionで矢印の下2pxに置く()
        {
            var row = Mount(rootVisualElement, 1, 1, 600f);
            var action = row.Q(className: "recipe-row__action");
            Assert.AreEqual(DisplayStyle.None, action.resolvedStyle.display);

            // 操作の高さ 10px でも段はボタンの高さを最小に持つ
            // Even a 10px action keeps the slot at the button height minimum
            var probe = new VisualElement { name = "action-probe" };
            probe.style.width = 40f;
            probe.style.height = 10f;
            row.SetAction(probe);
            simulate.FrameUpdate();
            Assert.AreEqual(DisplayStyle.Flex, action.resolvedStyle.display);
            Assert.AreEqual(ResolvePixels("--recipe-action-button-height"), action.layout.height, 0.5f);
            Assert.AreEqual(row.Q(ArrowTestId).worldBound.yMax + 2f, action.worldBound.yMin, 0.5f);

            row.ClearAction();
            simulate.FrameUpdate();
            Assert.AreEqual(DisplayStyle.None, action.resolvedStyle.display);
            Assert.IsNull(row.Q("action-probe"));
        }

        [Test]
        public void 列に収まらないスロットは縮めず開発者へエラーで知らせる()
        {
            // 幅 200 では列が約 38.8px。素材 1 点は収まり、結果 2 点（83.6px）がはみ出す
            // At width 200 each column is about 38.8px; one material fits and two results (83.6px) overflow
            LogAssert.Expect(LogType.Error, new Regex(@"\[RecipeRow\] recipe-row-test: slots overflow"));
            var row = Mount(rootVisualElement, 1, 2, 200f);
            simulate.FrameUpdate();
            Assert.AreEqual(ResolvePixels("--recipe-slot-size-max"), Bound(row, "r-1").width, 0.5f);
        }

        private RecipeRow Mount(VisualElement parent, int materialCount, int resultCount, float width)
        {
            // 秒数は載せる前に渡す（文の無い LocalizedLabel は 1 フレーム後に LogError を出す）
            // Pass the duration before mounting (a LocalizedLabel without text logs an error one frame later)
            var row = new RecipeRow(TestId, ArrowTestId);
            row.style.width = width;
            row.SetDuration(UiText.FromVerbatim("5秒"));
            row.SetSlots(Slots("m", materialCount), Slots("r", resultCount));
            parent.Add(row);
            simulate.FrameUpdate();
            return row;
        }

        private static IReadOnlyList<VisualElement> Slots(string prefix, int count)
        {
            var slots = new List<VisualElement>();
            // 空のスロットでも枠の寸法は --slot-size で決まる
            // An empty slot still takes its frame size from --slot-size
            for (var i = 0; i < count; i++) slots.Add(new ItemSlot($"{prefix}-{i}"));
            return slots;
        }

        private static Rect Bound(VisualElement row, string slotTestid)
        {
            return row.Q(slotTestid).worldBound;
        }
    }
}

using Client.Localization;
using Client.Tests.Ui.TestHost;
using Client.Ui.Components.Recipe;
using Client.Ui.Components.Slots;
using Client.Ui.Foundation.Icons;
using Client.Ui.State.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static Client.Tests.Ui.Components.ThemeTokenFile;

namespace Client.Tests.Ui.Components.Recipe
{
    /// <summary>
    /// 液体・線と面と枠の色・描画（L1）
    /// Fluids, border/face/frame colors and drawing (L1)
    /// </summary>
    public class RecipeRowContentTest : UiTestHost
    {
        private const string RecipeGuid = "84000000-0000-4000-8000-000000000001";

        [SetUp]
        public void LoadDictionary()
        {
            Localize.Initialize();
        }

        [Test]
        public void 入出力の液体は素材と結果の列にFluidAmountSlotの枠として量付きで並ぶ()
        {
            // 機械のレシピ選択の行と同じ組み方: 入力の液体 10・出力の液体 1000 を Web と同じ testid の枠で渡す
            // Built like the machine recipe selection row: input fluid 10 and output fluid 1000 in frames with the Web testids
            var inputId = $"machine-recipe-{RecipeGuid}-input-fluid-0";
            var outputId = $"machine-recipe-{RecipeGuid}-output-fluid-0";
            var input = new FluidAmountSlot(inputId);
            input.SetFluid(Icon("87000000-0000-4000-8000-000000000001"), SlotAmountBadge.Show(10));
            var output = new FluidAmountSlot(outputId);
            output.SetFluid(Icon("87000000-0000-4000-8000-000000000002"), SlotAmountBadge.Show(1000));
            var row = new RecipeRow($"machine-recipe-{RecipeGuid}-row", $"machine-recipe-{RecipeGuid}-arrow");
            row.style.width = 600f;
            row.SetDuration(UiText.FromVerbatim("7秒"));
            row.SetSlots(new VisualElement[] { input }, new VisualElement[] { output });
            rootVisualElement.Add(row);
            simulate.FrameUpdate();

            var materials = row.Q(className: "recipe-row__materials");
            var results = row.Q(className: "recipe-row__results");
            Assert.IsNotNull(materials.Q(inputId), inputId);
            Assert.IsNotNull(results.Q(outputId), outputId);
            Assert.AreEqual(SlotAmountFormat.Format(10), AmountText(materials, inputId));
            Assert.AreEqual(SlotAmountFormat.Format(1000), AmountText(results, outputId));
        }

        [Test]
        public void 線と面はトークンの色で枠は線の箱から2px外へ広がり入力を拾わない()
        {
            var row = MountPlain();
            Assert.AreEqual(1f, row.resolvedStyle.borderTopWidth, 0.01f);
            AssertSameColor(ResolveColor("--recipe-row-border"), row.resolvedStyle.borderTopColor, "border");
            AssertSameColor(ResolveColor("--recipe-row-face"), row.resolvedStyle.backgroundColor, "face");

            // 枠の箱は padding の箱から 3px 外＝線の箱から 2px 外（Web の ::before/::after）
            // The frame box is 3px outside the padding box, i.e. 2px outside the border box (Web ::before/::after)
            var frame = row.Q(className: "recipe-row__frame");
            Assert.AreEqual(PickingMode.Ignore, frame.pickingMode);
            Assert.AreEqual(row.worldBound.xMin - 2f, frame.worldBound.xMin, 0.5f);
            Assert.AreEqual(row.worldBound.yMin - 2f, frame.worldBound.yMin, 0.5f);
            Assert.AreEqual(row.worldBound.xMax + 2f, frame.worldBound.xMax, 0.5f);
            Assert.AreEqual(row.worldBound.yMax + 2f, frame.worldBound.yMax, 0.5f);
            AssertSameColor(ResolveColor("--recipe-row-ring"), FrameColor(frame, "--recipe-row-frame-ring"), "ring");
            AssertSameColor(ResolveColor("--recipe-row-bracket"), FrameColor(frame, "--recipe-row-frame-bracket"), "bracket");
            AssertSameColor(ResolveColor("--recipe-row-glow"), FrameColor(frame, "--recipe-row-frame-glow"), "glow");
            AssertSameColor(ResolveColor("--recipe-row-bracket-shadow"), FrameColor(frame, "--recipe-row-frame-bracket-shadow"), "bracket shadow");
        }

        [Test]
        public void 描画でエラーを出さない()
        {
            var row = MountPlain();
            row.SetProgress(0.42f);
            simulate.FrameUpdate();
            LogAssert.NoUnexpectedReceived();
        }

        private RecipeRow MountPlain()
        {
            var row = new RecipeRow("recipe-row-content-test", "recipe-row-content-test-arrow");
            row.style.width = 600f;
            row.SetDuration(UiText.FromVerbatim("5秒"));
            row.SetSlots(new VisualElement[] { new ItemSlot("m-0") }, new VisualElement[] { new ItemSlot("r-0") });
            rootVisualElement.Add(row);
            simulate.FrameUpdate();
            simulate.FrameUpdate();
            return row;
        }

        private static UiIcon Icon(string idText)
        {
            return new UiIcon(Texture2D.whiteTexture, idText, UiText.FromVerbatim(idText));
        }

        private static string AmountText(VisualElement column, string frameTestid)
        {
            // 量の帯は FluidAmountSlot が name = 枠の testid + "-amount" で置き、文字は縁でなく字の層を読む（FluidAmountSlotTest と同じ）
            // FluidAmountSlot names the amount band frame testid + "-amount"; read the glyph layer, not the stroke (as FluidAmountSlotTest does)
            var amount = column.Q($"{frameTestid}-amount");
            Assert.IsNotNull(amount, $"{frameTestid}-amount");
            return amount.Q<Label>(null, "fluid-amount-slot__amount-layer", "outlined-count-label--glyph").text;
        }

        private static Color FrameColor(VisualElement frame, string property)
        {
            Assert.IsTrue(frame.customStyle.TryGetValue(new CustomStyleProperty<Color>(property), out var color), property);
            return color;
        }
    }
}

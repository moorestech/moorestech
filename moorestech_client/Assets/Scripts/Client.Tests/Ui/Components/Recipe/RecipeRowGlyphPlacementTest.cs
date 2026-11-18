using System.Collections.Generic;
using Client.Localization;
using Client.Tests.Ui.TestHost;
using Client.Ui.Components.Recipe;
using Client.Ui.Components.Slots;
using Client.Ui.State.Text;
using NUnit.Framework;
using UnityEngine.UIElements;
using static Client.Tests.Ui.Components.ThemeTokenFile;

namespace Client.Tests.Ui.Components.Recipe
{
    /// <summary>
    /// 秒数の字が切れない逃げと、スロットの個数の末尾の字間（L1）
    /// The duration's clip headroom and the slot count's trailing letter gap (L1)
    /// </summary>
    public class RecipeRowGlyphPlacementTest : UiTestHost
    {
        private const string TestId = "recipe-row-glyph-test";

        [SetUp]
        public void LoadDictionary()
        {
            Localize.Initialize();
        }

        [Test]
        public void 秒数は流れでは字の大きさの高さのまま上に字のはみ出しより広い逃げを持つ()
        {
            var row = Mount();
            var duration = row.Q<Label>($"{TestId}-duration");
            var center = row.Q(className: "recipe-row__center");
            var fontSize = ResolvePixels("--recipe-duration-text-size");

            // 流れの上の箱（逃げを除く）は Web の line-height: 1 と同じ字の大きさの高さで、矢印は従来どおりその 2px 下
            // The flow box (minus the headroom) is font-size tall like the Web line-height: 1, and the arrow stays 2px below it
            Assert.AreEqual(fontSize, duration.contentRect.height, 0.5f);
            Assert.AreEqual(fontSize, duration.layout.yMax, 0.5f);
            Assert.AreEqual(duration.layout.yMax + 2f, row.Q(className: "recipe-row__arrow").layout.yMin, 0.5f);

            // 字の上端は箱より約 2px 上に出る（2560×1080 の撮影で 3 物理画素）ので、切る範囲はそれより上まで広い
            // The glyph top rises about 2px above the box (3 device px in the 2560x1080 shot), so the clip reaches above it
            Assert.Greater(duration.resolvedStyle.paddingTop, 2f);
            Assert.AreEqual(duration.layout.yMin + duration.resolvedStyle.paddingTop, duration.layout.yMax - fontSize, 0.5f);
            Assert.AreEqual(TextOverflow.Ellipsis, duration.resolvedStyle.textOverflow);

            // 描く位置は Web の基線へ字の大きさの 18.1% 下げる（逃げの高さに引きずられない）
            // The drawn position drops by 18.1% of the font size to the Web baseline (independent of the headroom)
            Assert.AreEqual(center.worldBound.yMin + fontSize * 1.181f, duration.worldBound.yMax, 0.5f);
        }

        [Test]
        public void スロットの個数は字間12の末尾の字間のぶん内へ寄る()
        {
            var row = Mount();
            var count = row.Q("r-0").Q<Label>(null, "slot-content__count", "outlined-count-label--glyph");

            // Web の letter-spacing 0.12em × 16px を Chrome は最後の字の後ろにも足す
            // Chrome adds the Web letter-spacing 0.12em x 16px after the last glyph too
            Assert.AreEqual(12f, count.resolvedStyle.letterSpacing, 0.01f);
            Assert.AreEqual(0.12f * 16f, count.resolvedStyle.paddingRight, 0.1f);
        }

        private RecipeRow Mount()
        {
            var row = new RecipeRow(TestId, $"{TestId}-arrow", UiText.FromVerbatim("20秒"));
            row.style.width = 600f;
            var result = new ItemSlot("r-0");
            row.SetSlots(new List<VisualElement> { new ItemSlot("m-0") }, new List<VisualElement> { result });
            rootVisualElement.Add(row);
            simulate.FrameUpdate();
            result.Q<SlotContent>().SetCount(7);
            simulate.FrameUpdate();
            return row;
        }
    }
}

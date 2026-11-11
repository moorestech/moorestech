using Client.Ui.State.BlockInventory.Logic.Special;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;

namespace Client.Tests.Ui.State.BlockInventory.Special
{
    /// <summary>
    /// Web の filterSplitterLogic.test.ts の写し（L0）
    /// Port of Web filterSplitterLogic.test.ts (L0)
    /// </summary>
    public class FilterSplitterLogicTest
    {
        /// <web-test file="src/features/blockInventory/filterSplitterLogic.test.ts">3モードを型付き翻訳キーへ対応づける</web-test>
        [Test]
        public void 三つのモードを文言キーへ対応づける()
        {
            Assert.AreEqual(LocalizationKeys.Ui.BlockInventory.FilterDefault.Key, FilterSplitterLogic.ModeKey(FilterSplitterMode.Default).Key);
            Assert.AreEqual(LocalizationKeys.Ui.BlockInventory.FilterWhitelist.Key, FilterSplitterLogic.ModeKey(FilterSplitterMode.Whitelist).Key);
            Assert.AreEqual(LocalizationKeys.Ui.BlockInventory.FilterBlacklist.Key, FilterSplitterLogic.ModeKey(FilterSplitterMode.Blacklist).Key);
        }

        /// <web-test file="src/features/blockInventory/filterSplitterLogic.test.ts">grabCount=0 かつ clear=false は noop</web-test>
        [Test]
        public void 何も掴まず消去でもなければ無操作()
        {
            Assert.AreEqual(FilterSlotClickAction.Noop, FilterSplitterLogic.ClickAction(0, false));
        }

        /// <web-test file="src/features/blockInventory/filterSplitterLogic.test.ts">grabCount>0 かつ clear=false は set</web-test>
        [Test]
        public void 掴んでいて消去でなければ設定()
        {
            Assert.AreEqual(FilterSlotClickAction.Set, FilterSplitterLogic.ClickAction(1, false));
        }

        /// <web-test file="src/features/blockInventory/filterSplitterLogic.test.ts">clear=true は grabCount に関わらず clear</web-test>
        [Test]
        public void 消去は掴みに関わらず消去()
        {
            Assert.AreEqual(FilterSlotClickAction.Clear, FilterSplitterLogic.ClickAction(0, true));
            Assert.AreEqual(FilterSlotClickAction.Clear, FilterSplitterLogic.ClickAction(1, true));
        }
    }
}

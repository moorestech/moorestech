using Client.Localization;
using Client.Ui.State.MaterialTooltip;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;

namespace Client.Tests.Ui.State.MaterialTooltip
{
    public class MaterialTooltipTextTest
    {
        [TestCase(MaterialTooltipKind.RecipeMaterial)]
        [TestCase(MaterialTooltipKind.ResearchConsumeItem)]
        [TestCase(MaterialTooltipKind.BuildMenuMaterial)]
        [TestCase(MaterialTooltipKind.BuildMenuShortageLine)]
        public void 素材ツールチップの種類ごとに決まった辞書キーを使う(MaterialTooltipKind kind)
        {
            // 期待のキーと同じ引数で解いた文が Resolve の結果と一致する
            // Resolve() must equal the expected key resolved with the same params
            var expectedParams = new[]
            {
                new LocalizationTextParam("itemName", "鉄板"),
                new LocalizationTextParam("ownedCount", "1"),
                new LocalizationTextParam("requiredCount", "1"),
            };
            Assert.AreEqual(Localize.GetFormatted(ExpectedKey(kind), expectedParams), MaterialTooltipText.Create(kind, "鉄板", 1, 1).Resolve());

            #region Internal

            LocalizationKey ExpectedKey(MaterialTooltipKind materialKind)
            {
                return materialKind switch
                {
                    MaterialTooltipKind.RecipeMaterial => LocalizationKeys.Ui.Recipe.MaterialTooltip,
                    MaterialTooltipKind.ResearchConsumeItem => LocalizationKeys.Ui.Research.ConsumeItemTooltip,
                    MaterialTooltipKind.BuildMenuMaterial => LocalizationKeys.Ui.BuildMenu.MaterialTooltip,
                    _ => LocalizationKeys.Ui.BuildMenu.MaterialShortageLine,
                };
            }

            #endregion
        }

        [Test]
        public void 名前と所持数と必要数を量バッジと同じ表記で渡す()
        {
            var text = MaterialTooltipText.Create(MaterialTooltipKind.BuildMenuMaterial, "鉄板", 2000, 1234.5);
            var expectedParams = new[]
            {
                new LocalizationTextParam("itemName", "鉄板"),
                new LocalizationTextParam("ownedCount", "1,234.5"),
                new LocalizationTextParam("requiredCount", "2,000"),
            };
            Assert.AreEqual(Localize.GetFormatted(LocalizationKeys.Ui.BuildMenu.MaterialTooltip, expectedParams), text.Resolve());
        }
    }
}

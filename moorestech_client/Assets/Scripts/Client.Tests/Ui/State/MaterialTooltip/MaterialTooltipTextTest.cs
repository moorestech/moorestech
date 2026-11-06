using System.Linq;
using Client.Ui.State.MaterialTooltip;
using NUnit.Framework;

namespace Client.Tests.Ui.State.MaterialTooltip
{
    public class MaterialTooltipTextTest
    {
        [TestCase(MaterialTooltipKind.RecipeMaterial, "ui.recipe.materialTooltip")]
        [TestCase(MaterialTooltipKind.ResearchConsumeItem, "ui.research.consumeItemTooltip")]
        [TestCase(MaterialTooltipKind.BuildMenuMaterial, "ui.buildMenu.materialTooltip")]
        [TestCase(MaterialTooltipKind.BuildMenuShortageLine, "ui.buildMenu.materialShortageLine")]
        public void 素材ツールチップの種類ごとに決まった辞書キーを使う(MaterialTooltipKind kind, string expectedKey)
        {
            Assert.AreEqual(expectedKey, MaterialTooltipText.Create(kind, "鉄板", 1, 1).Key.Key);
        }

        [Test]
        public void 名前と所持数と必要数を量バッジと同じ表記で渡す()
        {
            var text = MaterialTooltipText.Create(MaterialTooltipKind.BuildMenuMaterial, "鉄板", 2000, 1234.5);
            var values = text.Params.ToDictionary(param => param.Name, param => param.Value);
            Assert.AreEqual("鉄板", values["itemName"]);
            Assert.AreEqual("1,234.5", values["ownedCount"]);
            Assert.AreEqual("2,000", values["requiredCount"]);
        }
    }
}

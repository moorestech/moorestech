using System;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using NUnit.Framework;

namespace Client.Tests.Localization
{
    public class ClientGameLocalizedDisplayContractTest
    {
        [Test]
        public void BlockSubInventorySourceは表示名ではなくGuidを公開する()
        {
            var sourceType = typeof(BlockSubInventorySource);

            Assert.AreEqual(
                typeof(Guid),
                sourceType.GetProperty("BlockGuid")?.PropertyType);
            Assert.IsNull(sourceType.GetProperty("BlockName"));
        }
    }
}

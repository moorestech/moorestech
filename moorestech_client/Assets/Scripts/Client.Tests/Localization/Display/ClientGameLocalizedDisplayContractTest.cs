using Mooresmaster.Model.BlocksModule;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using NUnit.Framework;

namespace Client.Tests.Localization.Display
{
    public class ClientGameLocalizedDisplayContractTest
    {
        [Test]
        public void BlockSubInventorySourceは現在のブロックマスタを取得できる()
        {
            var sourceType = typeof(BlockSubInventorySource);

            // 撤去済みの座標を識別できる取得契約を固定する
            // Preserve the lookup contract that distinguishes a removed block
            var method = sourceType.GetMethod(nameof(BlockSubInventorySource.TryGetBlockMaster), new[] { typeof(BlockMasterElement).MakeByRefType() });
            Assert.IsNotNull(method);
            Assert.AreEqual(typeof(bool), method.ReturnType);
            Assert.IsTrue(method.GetParameters()[0].IsOut);
            Assert.IsNull(sourceType.GetProperty("BlockName"));
        }
    }
}

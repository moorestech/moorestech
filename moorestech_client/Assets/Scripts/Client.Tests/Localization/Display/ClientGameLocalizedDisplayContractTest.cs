using System;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using NUnit.Framework;

namespace Client.Tests.Localization.Display
{
    public class ClientGameLocalizedDisplayContractTest
    {
        [Test]
        public void BlockSubInventorySourceは表示名ではなくGuidを公開する()
        {
            var sourceType = typeof(BlockSubInventorySource);

            // 撤去後は識別子を返せないため、現在のTry取得契約を検査する
            // Check the current Try contract because a removed block has no identity to return
            var identityMethod = sourceType.GetMethod(nameof(BlockSubInventorySource.TryGetBlockIdentity));
            Assert.IsNotNull(identityMethod);
            Assert.AreEqual(typeof(bool), identityMethod.ReturnType);
            var parameters = identityMethod.GetParameters();
            Assert.AreEqual(2, parameters.Length);
            Assert.AreEqual(typeof(Guid).MakeByRefType(), parameters[0].ParameterType);
            Assert.IsTrue(parameters[0].IsOut);
            Assert.AreEqual(typeof(string).MakeByRefType(), parameters[1].ParameterType);
            Assert.IsTrue(parameters[1].IsOut);
            Assert.IsNull(sourceType.GetProperty("BlockName"));
        }
    }
}

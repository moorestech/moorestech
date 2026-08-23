using System.Collections.Generic;
using Client.Game.InGame.Construction;
using Core.Item.Interface;
using Core.Master;
using Game.Construction;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Client.Tests.PlaceSystem.ElectricWireConnect
{
    /// <summary>
    /// 電柱ゴーストの建設コスト判定が財布を通ることの契約試験
    /// Contract tests proving the pole ghost's construction-cost judgement goes through the wallet
    /// </summary>
    public class ElectricWirePoleAffordabilityTest
    {
        [Test]
        public void 素材ゼロでも財布に残りがあれば1セル置ける()
        {
            CreateServer();

            var mirror = new ClientRemainingPlacementCountDatastore();
            mirror.ApplyAll(new Dictionary<BlockId, int> { { ForUnitTestModBlockId.GearBeltConveyor, 1 } });
            var walletQuery = new ConstructionWalletQuery(mirror);

            // 電柱ゴーストと同じ判定式。所持素材ゼロでも財布が1セル分を賄う
            // The very expression the pole ghost uses; with zero materials held the wallet still covers one cell
            Assert.IsTrue(1 <= walletQuery.GetAffordablePlacementCount(ForUnitTestModBlockId.GearBeltConveyor, new List<IItemStack>()));
        }

        [Test]
        public void 財布も素材も空なら1セルも置けない()
        {
            CreateServer();

            var walletQuery = new ConstructionWalletQuery(new ClientRemainingPlacementCountDatastore());

            Assert.IsFalse(1 <= walletQuery.GetAffordablePlacementCount(ForUnitTestModBlockId.GearBeltConveyor, new List<IItemStack>()));
        }

        private static void CreateServer()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }
    }
}

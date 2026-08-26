using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.Map.MapVein;
using Core.Master;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Client.Tests.Map
{
    /// <summary>
    ///     設置対象ごとに表示する鉱脈種別が切り替わることを検証する
    ///     Verifies the displayed vein kind switches per placement target
    /// </summary>
    public class PlacementVeinViewKindResolverTest
    {
        [Test]
        public void 採掘機はアイテム鉱脈それ以外のブロックは非表示になる()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var minerGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.ElectricMinerId).BlockGuid;
            var chestGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.ChestId).BlockGuid;

            Assert.AreEqual(MapVeinKind.Item, PlacementVeinViewKindResolver.Resolve(new BlockPlacementTarget(minerGuid, null)));
            Assert.IsNull(PlacementVeinViewKindResolver.Resolve(new BlockPlacementTarget(chestGuid, null)));

            // 対象未選択でも落ちずに非表示を返す。滞在中のnullプッシュがここを通る
            // An absent target must return "hidden" instead of throwing; the null push during the stay goes through here
            Assert.IsNull(PlacementVeinViewKindResolver.Resolve(null));
        }
    }
}

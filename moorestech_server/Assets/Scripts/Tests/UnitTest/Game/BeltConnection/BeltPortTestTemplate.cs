using System.Collections.Generic;
using System.Reflection;
using Game.Context;
using Game.Block.Factory;
using Game.Block.Blocks;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Blocks.BeltConveyor.Connection;
using Game.Block.Factory.BlockTemplate;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.InventoryConnectsModule;
using Tests.Module;

namespace Tests.UnitTest.Game.BeltConnection
{
    internal sealed class BeltPortTestTemplate : IBlockTemplate
    {
        private readonly Queue<InventoryConnects> _ports;

        internal BeltPortTestTemplate(params InventoryConnects[] ports)
        {
            _ports = new Queue<InventoryConnects>(ports);
        }

        internal static void Install(BeltPortTestTemplate template)
        {
            // テスト専用APIを本番へ増やさず、既存factoryのtemplateだけを差し替える
            // Replace only the existing factory template without adding a production API for tests
            var field = typeof(BlockFactory).GetField("_vanillaIBlockTemplates", BindingFlags.Instance | BindingFlags.NonPublic);
            var templates = (VanillaIBlockTemplates)field.GetValue(ServerContext.BlockFactory);
            templates.BlockTypesDictionary["BeltConveyor"] = template;
        }

        public IBlock New(BlockMasterElement master, BlockInstanceId id, BlockPositionInfo position, BlockCreateParam[] createParams)
        {
            // 合法なマスタport定義を実Worldの登録境界へ渡す
            // Pass legal master port definitions through the real world registration boundary
            var connector = BeltInventoryConnectionContext.Create(_ports.Dequeue(), position, BeltConveyorSlopeType.Straight);
            return new BlockSystem(id, master.BlockGuid, new List<IBlockComponent> { new DummyBlockInventory(1, 4), connector }, position);
        }

        public IBlock Load(Dictionary<string, object> states, BlockMasterElement master, BlockInstanceId id, BlockPositionInfo position)
        {
            return New(master, id, position, System.Array.Empty<BlockCreateParam>());
        }
    }
}

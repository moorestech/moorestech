using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Core.Master;
using Game.Block.Blocks;
using Game.Block.Factory;
using Game.Block.Factory.BlockTemplate;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.InventoryConnectsModule;
using Mod.Config;
using Newtonsoft.Json.Linq;
using Tests.Module;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.UnitTest.Game.BeltConnection.Machine
{
    internal sealed class MachinePortTestTemplate : IBlockTemplate
    {
        private readonly InventoryConnects _ports;
        private readonly Vector3Int _size;

        private MachinePortTestTemplate(InventoryConnects ports, Vector3Int size)
        {
            _ports = ports;
            _size = size;
        }

        internal static void Install(InventoryConnects ports, Vector3Int size)
        {
            // 占有セル通知も同じマスタサイズで検証する
            // Exercise occupied-cell notifications with the same master size
            var container = ServerContext.GetService<MasterJsonFileContainer>();
            var fileName = new JsonFileName("blocks");
            var json = JObject.Parse(container.ConfigJsons[0].JsonContents[fileName]);
            var guid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.ChestId).BlockGuid;
            json["data"].Single(block => (Guid)block["blockGuid"] == guid)["blockSize"] = new JArray(size.x, size.y, size.z);
            container.ConfigJsons[0].JsonContents[fileName] = json.ToString();
            MasterHolder.Load(container);

            // 実マスタを変更せず、任意の面を持つ機械を登録境界で試す
            // Exercise machine faces at the registration boundary without editing production masters
            var field = typeof(BlockFactory).GetField("_vanillaIBlockTemplates", BindingFlags.Instance | BindingFlags.NonPublic);
            var templates = (VanillaIBlockTemplates)field.GetValue(ServerContext.BlockFactory);
            templates.BlockTypesDictionary["Chest"] = new MachinePortTestTemplate(ports, size);
        }

        internal static IBlock Place(BeltEdgeTestWorld world, Vector3Int cell, BlockDirection direction)
        {
            NUnit.Framework.Assert.IsTrue(world.World.TryAddBlock(ForUnitTestModBlockId.ChestId, cell, direction,
                Array.Empty<BlockCreateParam>(), out var block));
            return block;
        }

        public IBlock New(BlockMasterElement master, BlockInstanceId id, BlockPositionInfo position, BlockCreateParam[] createParams)
        {
            position = new BlockPositionInfo(position.OriginalPos, position.BlockDirection, _size);
            var connector = BlockTemplateUtil.CreateInventoryConnector(_ports, position);
            return new BlockSystem(id, master.BlockGuid, new List<IBlockComponent> { new DummyBlockInventory(1, 4), connector }, position);
        }

        public IBlock Load(Dictionary<string, object> states, BlockMasterElement master, BlockInstanceId id, BlockPositionInfo position) =>
            New(master, id, position, Array.Empty<BlockCreateParam>());

        internal static InputConnectsElement Input(Vector3Int offset, Vector3Int[] directions, Guid? shape) =>
            new InputConnectsElement(0, Guid.NewGuid(), shape, offset, directions);
        internal static OutputConnectsElement Output(Vector3Int offset, Vector3Int[] directions, Guid? shape) =>
            new OutputConnectsElement(0, Guid.NewGuid(), shape, offset, directions);
    }
}

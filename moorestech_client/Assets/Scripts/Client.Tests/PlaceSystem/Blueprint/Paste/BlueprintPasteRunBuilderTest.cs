using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Paste;
using Core.Master;
using Game.Block.Interface;
using Game.Blueprint;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.PlaceSystem
{
    public class BlueprintPasteRunBuilderTest
    {
        [Test]
        public void 外形幅の刻みでアンカーが並ぶ()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var chestGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.ChestId).BlockGuid.ToString();
            var blueprint = new BlueprintJsonObject("pair", new List<BlueprintBlockJsonObject>
            {
                new(new Vector3Int(0, 0, 0), chestGuid, (int)BlockDirection.North, new Dictionary<string, string>()),
                new(new Vector3Int(1, 0, 0), chestGuid, (int)BlockDirection.North, new Dictionary<string, string>()),
            }, Guid.NewGuid());

            // 外形幅2でアンカー0,2,4へ展開する
            // A width-two footprint expands at anchors 0, 2 and 4
            var elements = BlueprintPasteRunBuilder.Build(blueprint, Vector3Int.zero, new Vector3Int(5, 0, 0), new Vector3Int(2, 1, 1), 0);
            Assert.AreEqual(6, elements.Count);
            CollectionAssert.AreEquivalent(new[] { 0, 1, 2, 3, 4, 5 }, elements.Select(e => e.Position.x).ToArray());
        }
    }
}

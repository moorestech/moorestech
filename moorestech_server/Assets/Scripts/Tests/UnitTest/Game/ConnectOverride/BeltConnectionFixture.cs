using System;
using System.Collections.Generic;
using System.IO;
using Core.Master;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Component.ConnectJudge;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.World.Interface.DataStore;
using Newtonsoft.Json;
using NUnit.Framework;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.UnitTest.Game.ConnectOverride
{
    internal sealed class BeltCase
    {
        public string UpperSource;
        public string LowerSource;
        public string UpperTarget;
        public string LowerTarget;
        public string Source;
        public string Target;
    }

    internal sealed class BeltCases
    {
        public string Source;
        public List<BeltCase> Cases;
        public List<BeltCase> DiagramCases;
    }

    internal static class BeltConnectionFixture
    {
        internal static BeltCases Load()
        {
            // Frozen oracle: E:/Dropbox/seg/images/08_vertex_patterns_generator/
            // generate_vertex_patterns.py classify_pattern. Empty bypasses the oracle.
            var path = Path.Combine(Environment.CurrentDirectory, "..", "moorestech_server",
                "Assets", "Scripts", "Tests", "UnitTest", "Game", "ConnectOverride",
                "BeltConnectionExpectedCases.json");
            return JsonConvert.DeserializeObject<BeltCases>(File.ReadAllText(path));
        }

        internal static void Check(BeltCase row, BlockDirection direction, bool sourceFirst,
            bool sourceGear, bool targetGear, string label)
        {
            var world = ServerContext.WorldBlockDatastore;
            var forward = direction.ConvertLocalCell(Vector3Int.forward);
            var cells = new Dictionary<string, Vector3Int>
            {
                ["UpperSource"] = Vector3Int.up,
                ["LowerSource"] = Vector3Int.zero,
                ["UpperTarget"] = Vector3Int.up + forward,
                ["LowerTarget"] = forward
            };
            var kinds = new Dictionary<string, string>
            {
                ["UpperSource"] = row.UpperSource,
                ["LowerSource"] = row.LowerSource,
                ["UpperTarget"] = row.UpperTarget,
                ["LowerTarget"] = row.LowerTarget
            };
            var order = sourceFirst
                ? new[] { "UpperSource", "LowerSource", "UpperTarget", "LowerTarget" }
                : new[] { "UpperTarget", "LowerTarget", "UpperSource", "LowerSource" };
            var blocks = new Dictionary<string, IBlock>();
            try
            {
                foreach (var name in order)
                {
                    if (kinds[name] == "Empty") continue;
                    var gear = name.EndsWith("Source") ? sourceGear : targetGear;
                    var id = Id(kinds[name], gear);
                    Assert.IsTrue(world.TryAddBlock(id, cells[name], direction,
                        Array.Empty<BlockCreateParam>(), out var block), label + " place " + name);
                    blocks.Add(name, block);
                }
                foreach (var sourceName in new[] { "UpperSource", "LowerSource" })
                foreach (var targetName in new[] { "UpperTarget", "LowerTarget" })
                {
                    if (!blocks.TryGetValue(sourceName, out var source) ||
                        !blocks.TryGetValue(targetName, out var target) ||
                        !CentralSource(sourceName, kinds[sourceName]) ||
                        !CentralTarget(targetName, kinds[targetName])) continue;
                    var connector = source.GetComponent<BlockConnectorComponent<IBlockInventory, DefaultConnectJudge>>();
                    var inventory = target.GetComponent<VanillaBeltConveyorComponent>();
                    var actual = connector.ConnectedTargets.ContainsKey(inventory);
                    var expected = row.Source == (sourceName == "UpperSource" ? "Upper" : "Lower") &&
                                   row.Target == (targetName == "UpperTarget" ? "Upper" : "Lower");
                    Assert.AreEqual(expected, actual, label + " " + sourceName + " -> " + targetName);
                }
            }
            finally
            {
                foreach (var name in order)
                    if (blocks.ContainsKey(name)) world.RemoveBlock(cells[name], BlockRemoveReason.ManualRemove);
            }
        }

        private static bool CentralSource(string name, string slope)
        {
            return name == "UpperSource" ? slope == "Flat" || slope == "Down" : slope == "Up";
        }

        private static bool CentralTarget(string name, string slope)
        {
            return name == "UpperTarget" ? slope == "Flat" || slope == "Up" : slope == "Down";
        }

        private static BlockId Id(string slope, bool gear)
        {
            if (gear)
                return slope switch
                {
                    "Flat" => ForUnitTestModBlockId.GearBeltConveyor,
                    "Up" => ForUnitTestModBlockId.TestGearBeltConveyorUp,
                    _ => ForUnitTestModBlockId.TestGearBeltConveyorDown
                };
            return slope switch
            {
                "Flat" => ForUnitTestModBlockId.BeltConveyorId,
                "Up" => ForUnitTestModBlockId.TestBeltConveyorUp,
                _ => ForUnitTestModBlockId.TestBeltConveyorDown
            };
        }
    }
}

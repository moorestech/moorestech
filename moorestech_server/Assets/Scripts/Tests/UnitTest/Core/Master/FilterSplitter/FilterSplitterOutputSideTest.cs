using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Core.Master;
using Core.Master.Validator;
using Game.Block.Interface.Component;
using Mooresmaster.Model.BlocksModule;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Core.Master.FilterSplitter
{
    /// <summary>
    /// フィルター分岐器の出口の側（左・正面・右）を決める計算と、決まらないマスタを読込時に止める検証が、同じ接続の組で食い違わないこと
    /// The exit side calculation (left, front, right) and the load-time validator that stops undecidable masters agree on the same connector sets
    /// </summary>
    public class FilterSplitterOutputSideTest
    {
        private const string SplitterName = "TestFilterSplitter";
        private const string Throws = "Throws";
        private const string DuplicateSide = "DuplicateSide";

        [Test]
        public void テストマスタの出口は右左正面の並び()
        {
            // テストマスタは入口が背面(0,0,-1)、出口が +X・-X・+Z の順。流れる向き +Z の右手が +X
            // The test master has the entry at the back (0,0,-1) and exits +X, -X, +Z; +X is the right hand of the +Z flow
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var param = (FilterSplitterBlockParam)MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.FilterSplitter).BlockParam;

            var sides = FilterSplitterOutputSideResolver.ResolveAll(param.InventoryConnectors);

            CollectionAssert.AreEqual(new[] { FilterSplitterOutputSide.Right, FilterSplitterOutputSide.Left, FilterSplitterOutputSide.Front }, sides.ToArray());
        }

        // 列: 入口コネクタ（1要素=1コネクタ、向きは ; 区切り、空文字は向き0個）・出口の向き・検証の文（null は通る）・計算の結果（側の並び / Throws / DuplicateSide）
        // Columns: input connectors (one element per connector, directions split by ;, empty means no direction), exit directions, validator text (null passes), calculation (sides / Throws / DuplicateSide)
        private static IEnumerable<TestCaseData> ConnectorCases()
        {
            yield return new TestCaseData(new[] { "0,0,-1" }, new[] { "1,0,0", "-1,0,0", "0,0,1" }, null, "Right,Left,Front").SetName("テストマスタの並びは通り右左正面");
            yield return new TestCaseData(new[] { "0,0,-1" }, new[] { "-1,0,0", "0,0,1", "1,0,0" }, null, "Left,Front,Right").SetName("出荷マスタの並びは通り左正面右");
            yield return new TestCaseData(new[] { "1,0,0" }, new[] { "0,0,1", "0,0,-1", "-1,0,0" }, null, "Right,Left,Front").SetName("入口が横向きでも入口から見た側で決まる");
            yield return new TestCaseData(new[] { "0,0,-1" }, new[] { "1,0,0", "-1,0,0" }, null, "Right,Left").SetName("出口が左と右の2つだけなら止めない");
            yield return new TestCaseData(new[] { "0,0,-1" }, new[] { "1,0,0", "-1,0,0", "0,0,-1" }, "is not left, front or right of its input", Throws).SetName("入口と同じ面の出口を止める");
            yield return new TestCaseData(new[] { "0,0,-1" }, new[] { "1,0,0", "-1,0,0", "1,0,1" }, "is not left, front or right of its input", Throws).SetName("斜めの出口を止める");
            yield return new TestCaseData(new[] { "0,0,-1" }, new[] { "1,0,0", "-1,0,0", "0,1,0" }, "is not left, front or right of its input", Throws).SetName("上向きの出口を止める");
            yield return new TestCaseData(new[] { "0,0,-1" }, new[] { "1,0,0", "-1,0,0", "0,-1,0" }, "is not left, front or right of its input", Throws).SetName("下向きの出口を止める");
            yield return new TestCaseData(new[] { "0,0,-1" }, new[] { "1,0,0", "-1,0,0", "0,0,1;1,0,0" }, "is not left, front or right of its input", Throws).SetName("向きが2つの出口を止める");
            yield return new TestCaseData(new[] { "0,0,-1" }, new[] { "1,0,0", "-1,0,0", "" }, "is not left, front or right of its input", Throws).SetName("向きの無い出口を止める");
            yield return new TestCaseData(new[] { "0,0,-1" }, new[] { "-1,0,0", "-1,0,0", "0,0,1" }, "has two outputs on the same side", DuplicateSide).SetName("同じ側の出口が2つあれば止める");
            yield return new TestCaseData(new[] { "0,0,-1;1,0,0" }, new[] { "1,0,0", "-1,0,0", "0,0,1" }, "does not point to a single horizontal direction", Throws).SetName("入口の向きが2つあれば止める");
            yield return new TestCaseData(new[] { "0,1,0" }, new[] { "1,0,0", "-1,0,0", "0,0,1" }, "does not point to a single horizontal direction", Throws).SetName("入口が上向きなら止める");
            yield return new TestCaseData(new[] { "0,0,-1", "1,0,0" }, new[] { "1,0,0", "-1,0,0", "0,0,1" }, "does not have exactly one input connector", Throws).SetName("入口コネクタが2つあれば止める");
            yield return new TestCaseData(Array.Empty<string>(), new[] { "1,0,0", "-1,0,0", "0,0,1" }, "does not have exactly one input connector", Throws).SetName("入口コネクタが無ければ止める");
            yield return new TestCaseData(new[] { "0,0,-1" }, Array.Empty<string>(), "has no output connectors", Throws).SetName("出口が無ければ止める");
        }

        [TestCaseSource(nameof(ConnectorCases))]
        public void 検証と側の計算は同じ接続の組で食い違わない(string[] inputConnectors, string[] exitDirections, string validatorText, string calculation)
        {
            // 表の約束: 検証を通る組は計算が全部の側を決め、止める組は計算が決められないか同じ側が重なる
            // Table contract: a passing set gets every side decided; a stopped set throws or repeats a side
            Assert.AreEqual(validatorText == null, calculation != Throws && calculation != DuplicateSide, "表の行が約束に反する");

            var blocks = PrepareBlocksJson();
            var connectors = FindBlockParam(blocks)["inventoryConnectors"];
            var inputTemplate = connectors["inputConnects"][0];
            var outputTemplate = connectors["outputConnects"][0];
            connectors["inputConnects"] = new JArray(inputConnectors.Select((directions, index) => Connector(inputTemplate, directions, $"f1110000-0000-0000-0000-0000000001{index:D2}")));
            connectors["outputConnects"] = new JArray(exitDirections.Select((direction, index) => Connector(outputTemplate, direction, $"f1110000-0000-0000-0000-0000000002{index:D2}")));
            var blockMaster = new BlockMaster(blocks);

            // 検証の側
            // Validator side
            var passed = BlockMasterUtil.Validate(blockMaster.Blocks, out var errors);
            Assert.AreEqual(validatorText == null, passed, errors);
            if (validatorText != null) StringAssert.Contains(validatorText, errors);

            // 計算の側
            // Calculation side
            var param = (FilterSplitterBlockParam)blockMaster.Blocks.Data.Single(block => block.Name == SplitterName).BlockParam;
            if (calculation == Throws)
            {
                Assert.Throws<InvalidOperationException>(() => FilterSplitterOutputSideResolver.ResolveAll(param.InventoryConnectors));
                return;
            }
            var sides = FilterSplitterOutputSideResolver.ResolveAll(param.InventoryConnectors);
            if (calculation == DuplicateSide)
            {
                Assert.Less(sides.Distinct().Count(), sides.Count);
                return;
            }
            CollectionAssert.AreEqual(calculation.Split(',').Select(side => (FilterSplitterOutputSide)Enum.Parse(typeof(FilterSplitterOutputSide), side)).ToArray(), sides.ToArray());
        }

        private static JToken Connector(JToken template, string directions, string connectorGuid)
        {
            // 空文字は向き0個（directions を空の配列にする）
            // An empty string means no direction (an empty directions array)
            var connector = template.DeepClone();
            var parsed = directions == "" ? Array.Empty<string>() : directions.Split(';');
            connector["directions"] = new JArray(parsed.Select(direction => new JArray(direction.Split(',').Select(value => int.Parse(value)))));
            connector["connectorGuid"] = connectorGuid;
            return connector;
        }

        private static JToken PrepareBlocksJson()
        {
            // 依存マスタを初期化し、テスト内の JToken だけを書き換える（前例 RocketLaunchPadParamValidatorTest）
            // Initialize dependency masters, then edit only the in-test JToken (precedent RocketLaunchPadParamValidatorTest)
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var blocksJsonPath = Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods", "forUnitTest", "master", "blocks.json");
            return JToken.Parse(File.ReadAllText(blocksJsonPath));
        }

        private static JToken FindBlockParam(JToken blocksJToken)
        {
            return blocksJToken["data"].Children<JObject>().Single(block => (string)block["name"] == SplitterName)["blockParam"];
        }
    }
}

using System;
using System.Text;
using Game.Block.Blocks.FilterSplitter;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Blueprint;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Game
{
    /// <summary>
    /// フィルタスプリッタ設定がBP抽出→BlockCreateParam設置で再現されることを検証する。
    /// Verifies filter splitter settings round-trip through blueprint extraction and placement.
    /// </summary>
    public class BlueprintFilterSplitterSettingsTest
    {
        [Test]
        public void SettingsRoundTripThroughBlueprintTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // 元のフィルタスプリッタに設定を入れる
            // Configure the source filter splitter
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FilterSplitter, new Vector3Int(0, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var source);
            var sourceComponent = source.GetComponent<VanillaFilterSplitterComponent>();
            sourceComponent.SetMode(0, FilterSplitterMode.Blacklist);
            sourceComponent.SetFilterItem(0, 0, ForUnitTestItemId.ItemId1);

            // 範囲抽出でBPを作り、設定JSONが入っていることを確認
            // Extract a blueprint and verify settings JSON is captured
            var created = BlueprintCreateService.TryCreateFromArea("filter", new Vector3Int(0, 0, 0), new Vector3Int(0, 0, 0), out var blueprint);
            Assert.IsTrue(created);
            var settingsJson = blueprint.Blocks[0].Settings[VanillaFilterSplitterComponent.BlueprintSettingsSaveKey];
            Assert.IsNotNull(settingsJson);

            // 設定JSONをBlockCreateParamに載せて別座標に設置し、設定が再現されることを確認
            // Place a new splitter with the settings param and verify reproduction
            var createParams = new[] { new BlockCreateParam(VanillaFilterSplitterComponent.BlueprintSettingsSaveKey, Encoding.UTF8.GetBytes(settingsJson)) };
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.FilterSplitter, new Vector3Int(10, 0, 10), BlockDirection.North, createParams, out var pasted);
            var pastedComponent = pasted.GetComponent<VanillaFilterSplitterComponent>();

            Assert.AreEqual(FilterSplitterMode.Blacklist, pastedComponent.GetMode(0));
            Assert.AreEqual(ForUnitTestItemId.ItemId1, pastedComponent.GetFilterItems(0)[0]);
        }
    }
}

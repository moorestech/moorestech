using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Client.Tests.Support;
using Core.Master;
using Core.Update;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.ChallengesModule;
using NUnit.Framework;
using Server.Boot;
using UnityEngine;

namespace Client.Tests.EarlyGame
{
    /// <summary>
    ///     接続チュートリアルの相対座標が動力を伝えるか確かめる
    ///     Proves the connection tutorial's relative layout (windmill → shaft → crusher) really carries gear power on the pinned v8 master
    /// </summary>
    public class EarlyGameGearTutorialLayoutTest
    {
        private const string ServerDirectoryName = "server_v8";
        private const string MapDirectoryPath = "server_v8/map";
        private const string MasterDirectoryPath = "server_v8/mods/moorestechAlphaMod_8/master";

        // 既存ブロックと衝突しない空き座標。値そのものに意味は無い
        // A free coordinate that collides with nothing; the value itself carries no meaning
        private static readonly Vector3Int AnchorOrigin = new(100, 0, 100);

        // GUIDはkey由来で安定。表示名は変わるので同定子にしない
        // Challenge GUIDs are key-derived and stable, unlike display names, so they identify the tutorial
        private static readonly Guid[] ConnectionTutorialChallengeGuids =
        {
            new("c78e653e-c5e0-506e-afcf-75ffa9f9f3c7"),
            new("401afc60-25c8-5418-8617-f026335b887a")
        };

        private static readonly int FuelBurnTicks = (int)GameUpdater.SecondsToTicks(1);

        private string _extractionRoot;

        [TearDown]
        public void DeleteExtractedMaster()
        {
            // 展開先は呼び出しごとに固有。消さないと積み上がる
            // The destination is unique per call and piles up unless deleted
            if (Directory.Exists(_extractionRoot)) Directory.Delete(_extractionRoot, true);
        }

        [Test]
        public void 接続チュートリアルの相対座標に置いたブロックが風車の動力で回る()
        {
            _extractionRoot = PinnedMasterRepository.ExtractPinnedDirectories(MapDirectoryPath, MasterDirectoryPath);
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(Path.Combine(_extractionRoot, ServerDirectoryName)));

            var previews = ConnectionTutorialChallengeGuids.Select(CollectSingleRelativePlacePreview).ToList();

            // 同じアンカー基準でないと配置検証が成り立たない
            // The layout check only holds if both previews share one anchor block
            var anchorGuid = previews[0].AnchorBlockGuid;
            Assert.IsTrue(previews.All(preview => preview.AnchorBlockGuid == anchorGuid), "the two placement previews use different anchor blocks");

            var world = ServerContext.WorldBlockDatastore;
            var anchorBlockId = MasterHolder.BlockMaster.GetBlockId(anchorGuid);
            Assert.IsTrue(world.TryAddBlock(anchorBlockId, AnchorOrigin, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var anchorBlock), "failed to place the anchor block");

            var placedBlocks = new List<IBlock>();
            foreach (var preview in previews)
            {
                var blockId = MasterHolder.BlockMaster.GetBlockId(preview.BlockGuid);
                var direction = Enum.Parse<BlockDirection>(preview.BlockDirection);
                var placed = world.TryAddBlock(blockId, AnchorOrigin + preview.Offset, direction, Array.Empty<BlockCreateParam>(), out var block);
                Assert.IsTrue(placed, $"failed to place the tutorial block at offset {preview.Offset}");
                placedBlocks.Add(block);
            }

            // 風車は燃料を燃やして初めて回る
            // The windmill only turns once it burns fuel
            InsertFirstFuelItem(anchorBlock);
            for (var tick = 0; tick < FuelBurnTicks; tick++) GameUpdater.UpdateOneTick();

            Assert.Greater(anchorBlock.GetComponent<IGearEnergyTransformer>().CurrentRpm.AsPrimitive(), 0f, "the anchor windmill did not start turning");
            foreach (var placed in placedBlocks)
            {
                var name = MasterHolder.BlockMaster.GetBlockMaster(placed.BlockId).Name;
                Assert.Greater(placed.GetComponent<IGearEnergyTransformer>().CurrentRpm.AsPrimitive(), 0f, $"the tutorial layout does not carry gear power to {name}");
            }

            #region Internal

            RelativeBlockPlacePreviewTutorialParam CollectSingleRelativePlacePreview(Guid challengeGuid)
            {
                var challenge = MasterHolder.ChallengeMaster.ChallengeCategoryMasterElements
                    .SelectMany(category => category.Challenges)
                    .FirstOrDefault(element => element.ChallengeGuid == challengeGuid);
                Assert.IsNotNull(challenge, $"the connection tutorial challenge {challengeGuid} is gone from the master");

                var relativePreviews = challenge.Tutorials
                    .Select(tutorial => tutorial.TutorialParam)
                    .OfType<RelativeBlockPlacePreviewTutorialParam>()
                    .ToList();
                Assert.AreEqual(1, relativePreviews.Count, $"challenge {challengeGuid} no longer has exactly one relative placement preview");

                return relativePreviews[0];
            }

            void InsertFirstFuelItem(IBlock windmill)
            {
                var blockParam = (FuelGearGeneratorBlockParam)MasterHolder.BlockMaster.GetBlockMaster(windmill.BlockId).BlockParam;
                Assert.Greater(blockParam.GearFuelItems.Length, 0, "the anchor block accepts no item fuel");

                var fuelItemId = MasterHolder.ItemMaster.GetItemId(blockParam.GearFuelItems[0].ItemGuid);
                windmill.GetComponent<IBlockInventory>().SetItem(0, ServerContext.ItemStackFactory.Create(fuelItemId, 1));
            }

            #endregion
        }
    }
}

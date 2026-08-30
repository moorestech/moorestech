using System;
using System.IO;
using System.Linq;
using Client.Game.InGame.Tutorial.PlacementGuide;
using Client.Tests.Support;
using Core.Master;
using Core.Update;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.World.Interface.DataStore;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.ChallengesModule;
using NUnit.Framework;
using Server.Boot;
using UnityEngine;

namespace Client.Tests.EarlyGame
{
    /// <summary>
    ///     接続チュートリアルの相対座標が風車の全4方位で動力を伝えるか、ピン済みv8マスタで確かめる
    ///     Proves the connection tutorial's relative layout carries gear power for all four windmill directions on the pinned v8 master
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
        private static readonly Guid ShaftCrusherChallengeGuid = new("3b1991bf-eaed-597c-a0f7-2b45c58c78da");
        private static readonly Guid BeltShaftChallengeGuid = new("57372e58-2fff-5296-8947-c3f4c1b02325");

        private static readonly int FuelBurnTicks = (int)GameUpdater.SecondsToTicks(1);

        private string _extractionRoot;

        [TearDown]
        public void DeleteExtractedMaster()
        {
            // 展開先は呼び出しごとに固有。消さないと積み上がる
            // The destination is unique per call and piles up unless deleted
            if (Directory.Exists(_extractionRoot)) Directory.Delete(_extractionRoot, true);
        }

        [TestCase(BlockDirection.North)]
        [TestCase(BlockDirection.East)]
        [TestCase(BlockDirection.South)]
        [TestCase(BlockDirection.West)]
        public void 接続チュートリアルの相対座標に置いたブロックが風車の全方位で回る(BlockDirection windmillDirection)
        {
            CreateServerFromPinnedMaster();

            var challenge = FindChallenge(ShaftCrusherChallengeGuid);
            var previews = challenge.Tutorials
                .Select(tutorial => tutorial.TutorialParam)
                .OfType<RelativeBlockPlacePreviewTutorialParam>()
                .ToList();
            Assert.AreEqual(2, previews.Count, "the merged challenge no longer has exactly two relative placement previews");

            var anchorGuid = previews[0].AnchorBlockGuid;
            Assert.IsTrue(previews.All(preview => preview.AnchorBlockGuid == anchorGuid), "the two placement previews use different anchor blocks");

            var world = ServerContext.WorldBlockDatastore;
            var anchorBlockId = MasterHolder.BlockMaster.GetBlockId(anchorGuid);
            Assert.IsTrue(world.TryAddBlock(anchorBlockId, AnchorOrigin, windmillDirection, Array.Empty<BlockCreateParam>(), out var anchorBlock), "failed to place the anchor block");

            // 本番と同じ換算（アンカー回転込み）で目標セルと向きを解決して設置する
            // Place at cells and directions resolved with the same anchor-rotated conversion production uses
            foreach (var preview in previews)
            {
                var blockId = MasterHolder.BlockMaster.GetBlockId(preview.BlockGuid);
                var localDirection = Enum.Parse<BlockDirection>(preview.BlockDirection);
                var blockSize = MasterHolder.BlockMaster.GetBlockMaster(blockId).BlockSize;
                var cell = AnchorRelativeOriginUtil.ResolveWorldOrigin(anchorBlock.BlockPositionInfo, preview.Offset, localDirection, blockSize);
                var direction = AnchorRelativeDirectionUtil.RotateByAnchor(localDirection, windmillDirection);
                Assert.IsTrue(world.TryAddBlock(blockId, cell, direction, Array.Empty<BlockCreateParam>(), out _), $"failed to place the tutorial block at {cell} ({direction})");
            }

            // 風車は燃料を燃やして初めて回る
            // The windmill only turns once it burns fuel
            InsertFirstFuelItem(anchorBlock);
            for (var tick = 0; tick < FuelBurnTicks; tick++) GameUpdater.UpdateOneTick();

            Assert.Greater(anchorBlock.GetComponent<IGearEnergyTransformer>().CurrentRpm.AsPrimitive(), 0f, "the anchor windmill did not start turning");
            foreach (var preview in previews)
            {
                var blockId = MasterHolder.BlockMaster.GetBlockId(preview.BlockGuid);
                var blockSize = MasterHolder.BlockMaster.GetBlockMaster(blockId).BlockSize;
                var cell = AnchorRelativeOriginUtil.ResolveWorldOrigin(anchorBlock.BlockPositionInfo, preview.Offset, Enum.Parse<BlockDirection>(preview.BlockDirection), blockSize);
                Assert.IsTrue(world.TryGetBlock(cell, out var placed), $"no block found at {cell}");
                Assert.AreEqual(blockId, placed.BlockId, "an unexpected block occupies the tutorial cell");
                var name = MasterHolder.BlockMaster.GetBlockMaster(placed.BlockId).Name;
                Assert.Greater(placed.GetComponent<IGearEnergyTransformer>().CurrentRpm.AsPrimitive(), 0f, $"the tutorial layout does not carry gear power to {name}");
            }
        }

        [Test]
        public void ベルト段の相対座標に置いたシャフトはベルトへ歯車接続する()
        {
            CreateServerFromPinnedMaster();

            var challenge = FindChallenge(BeltShaftChallengeGuid);
            var preview = challenge.Tutorials
                .Select(tutorial => tutorial.TutorialParam)
                .OfType<RelativeBlockPlacePreviewTutorialParam>()
                .Single();

            var world = ServerContext.WorldBlockDatastore;
            var beltBlockId = MasterHolder.BlockMaster.GetBlockId(preview.AnchorBlockGuid);
            Assert.IsTrue(world.TryAddBlock(beltBlockId, AnchorOrigin, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var belt), "failed to place the belt");

            var shaftBlockId = MasterHolder.BlockMaster.GetBlockId(preview.BlockGuid);
            var direction = Enum.Parse<BlockDirection>(preview.BlockDirection);
            var shaftSize = MasterHolder.BlockMaster.GetBlockMaster(shaftBlockId).BlockSize;
            var cell = AnchorRelativeOriginUtil.ResolveWorldOrigin(belt.BlockPositionInfo, preview.Offset, direction, shaftSize);
            Assert.IsTrue(world.TryAddBlock(shaftBlockId, cell, direction, Array.Empty<BlockCreateParam>(), out var shaft), "failed to place the shaft");

            // 回転は不要。接続の成立だけを見る（gearConnectToBlock判定と同じ規則）
            // No rotation needed; only the connection matters, same rule as the gearConnectToBlock judge
            Assert.IsTrue(shaft.TryGetComponent<IGearEnergyTransformer>(out var transformer), "the shaft has no gear transformer");
            var connectedIds = transformer.GetGearConnects().Select(connect => connect.Transformer.BlockInstanceId).ToList();
            Assert.Contains(belt.BlockInstanceId, connectedIds, "the shaft next to the belt is not gear-connected to it");
        }

        private void CreateServerFromPinnedMaster()
        {
            _extractionRoot = PinnedMasterRepository.ExtractPinnedDirectories(MapDirectoryPath, MasterDirectoryPath);
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(Path.Combine(_extractionRoot, ServerDirectoryName)));
        }

        private static ChallengeMasterElement FindChallenge(Guid challengeGuid)
        {
            var challenge = MasterHolder.ChallengeMaster.ChallengeCategoryMasterElements
                .SelectMany(category => category.Challenges)
                .FirstOrDefault(element => element.ChallengeGuid == challengeGuid);
            Assert.IsNotNull(challenge, $"the tutorial challenge {challengeGuid} is gone from the master");
            return challenge;
        }

        private static void InsertFirstFuelItem(IBlock windmill)
        {
            var blockParam = (FuelGearGeneratorBlockParam)MasterHolder.BlockMaster.GetBlockMaster(windmill.BlockId).BlockParam;
            Assert.Greater(blockParam.GearFuelItems.Length, 0, "the anchor block accepts no item fuel");

            var fuelItemId = MasterHolder.ItemMaster.GetItemId(blockParam.GearFuelItems[0].ItemGuid);
            windmill.GetComponent<IBlockInventory>().SetItem(0, ServerContext.ItemStackFactory.Create(fuelItemId, 1));
        }

    }
}

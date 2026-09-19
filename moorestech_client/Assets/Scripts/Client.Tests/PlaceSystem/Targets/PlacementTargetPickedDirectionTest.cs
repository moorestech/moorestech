using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Game.Block.Interface;
using NUnit.Framework;

namespace Client.Tests.PlaceSystem.Targets
{
    /// <summary>
    ///     スポイトで選び直した直後だけピックした向きが設置向きになることを検証（通常設置とベルトの共通入口）
    ///     Verifies the picked direction becomes the placement direction only right after an eyedropper selection (shared by normal and belt placement)
    /// </summary>
    public class PlacementTargetPickedDirectionTest
    {
        private static readonly Guid AnyBlockGuid = Guid.Parse("00000000-0000-0000-0000-000000000001");

        [Test]
        public void スポイト選択直後はピックした向きを採用する()
        {
            var target = new BlockPlacementTarget(AnyBlockGuid, BlockDirection.West);

            Assert.AreEqual(BlockDirection.West, target.ResolveDirectionOnSelection(BlockDirection.North, true));
        }

        [Test]
        public void 選択が変わらないフレームは回転済みの向きを保つ()
        {
            var target = new BlockPlacementTarget(AnyBlockGuid, BlockDirection.West);

            // 毎フレーム上書きするとスポイト後にRで回せなくなる
            // Overwriting every frame would make R-rotation impossible after an eyedropper pick
            Assert.AreEqual(BlockDirection.South, target.ResolveDirectionOnSelection(BlockDirection.South, false));
        }

        [Test]
        public void メニュー選択はピック向きを持たず現在の向きを保つ()
        {
            var target = new BlockPlacementTarget(AnyBlockGuid, null);

            Assert.AreEqual(BlockDirection.East, target.ResolveDirectionOnSelection(BlockDirection.East, true));
        }
    }
}

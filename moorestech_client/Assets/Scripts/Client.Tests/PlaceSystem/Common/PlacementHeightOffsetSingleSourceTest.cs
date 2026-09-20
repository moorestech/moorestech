using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.Run;
using Client.Game.InGame.BlockSystem.PlaceSystem.Empty;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Game.Topics;
using Core.Master;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.PlaceSystem.Common
{
    /// <summary>
    ///     実値とHUD表示が同一ソースであることを検証
    ///     Verifies the actual height and the HUD share one source
    /// </summary>
    public class PlacementHeightOffsetSingleSourceTest
    {
        [Test]
        public void ドラッグ中の高さ変更は解放で開始値へ戻り表示も同じ値になる()
        {
            var heightOffset = new PlacementHeightOffset();
            var dragState = new CommonBlockPlaceDragState(heightOffset);
            var hub = new WebSocketHub();
            var topic = new PlacementModeTopic(hub, CreateController(heightOffset), heightOffset);
            try
            {
                dragState.BeginDrag(Vector3Int.zero, PlacementHitSurfaceKind.Ground);

                var revisionBeforeAdjust = hub.GetTopicRevision(PlacementModeTopic.TopicName);
                dragState.AdjustHeightOffset(2);

                // ドラッグ中の値がsnapshotへ現れ、購読がpushしたことをrevision増分で固定する
                // The in-drag value reaches the snapshot, pinning that the subscription pushed it via the revision bump
                Assert.AreEqual(2, ReadHeight(topic), "the in-drag height did not reach the shared snapshot value");
                Assert.AreEqual(dragState.HeightOffset, ReadHeight(topic));
                Assert.Greater(hub.GetTopicRevision(PlacementModeTopic.TopicName), revisionBeforeAdjust, "PlacementModeTopic did not push on height change");

                dragState.EndDrag();

                Assert.AreEqual(0, dragState.HeightOffset);
                Assert.AreEqual(0, ReadHeight(topic), "the HUD kept the in-drag height after the drag restored it");
            }
            finally
            {
                topic.Dispose();
            }
        }

        [Test]
        public void 持ち替えで高さが地表基準へ戻ると表示も戻る()
        {
            var heightOffset = new PlacementHeightOffset();
            var dragState = new CommonBlockPlaceDragState(heightOffset);
            dragState.SyncSelectedBlock(new BlockId(1));
            dragState.AdjustHeightOffset(3);

            dragState.SyncSelectedBlock(new BlockId(2));

            Assert.AreEqual(0, heightOffset.Value, "the HUD kept the old height after a hotbar swap reset the actual one");
        }

        [Test]
        public void ドラッグ中に設置系が畳まれても高さは開始値へ戻る()
        {
            var heightOffset = new PlacementHeightOffset();
            var dragState = new CommonBlockPlaceDragState(heightOffset);
            dragState.SyncSelectedBlock(new BlockId(1));
            dragState.BeginDrag(Vector3Int.zero, PlacementHitSurfaceKind.Ground);
            dragState.AdjustHeightOffset(2);

            // ドラッグ途中の離脱（Tab等）でも高さはドラッグ開始値へ戻す。解放時と同じ規則
            // Leaving mid-drag (Tab and friends) returns the height to the drag's starting value, as a release does
            dragState.ClearDrag();

            Assert.AreEqual(0, heightOffset.Value, "the temporary in-drag height survived a teardown");
        }

        [Test]
        public void 共有の正を渡した2つのドラッグ状態は同じ高さを読み書きする()
        {
            var heightOffset = new PlacementHeightOffset();
            var firstDragState = new CommonBlockPlaceDragState(heightOffset);
            var secondDragState = new CommonBlockPlaceDragState(heightOffset);

            firstDragState.SyncSelectedBlock(new BlockId(1));
            firstDragState.AdjustHeightOffset(1);
            Assert.AreEqual(1, secondDragState.HeightOffset);

            secondDragState.AdjustHeightOffset(2);
            Assert.AreEqual(3, firstDragState.HeightOffset);

            // 持ち替え判定も1本。片側で持ち替えたらもう片側から見た高さも地表基準へ戻る
            // The block-switch check is shared too, so a swap on one side returns the other side's height to ground
            secondDragState.SyncSelectedBlock(new BlockId(2));
            Assert.AreEqual(0, firstDragState.HeightOffset);
        }

        private static PlaceSystemStateController CreateController(PlacementHeightOffset heightOffset)
        {
            return new PlaceSystemStateController(new EmptySelector(), new NullPresenter(), heightOffset);
        }

        private static int ReadHeight(PlacementModeTopic topic)
        {
            var json = JObject.Parse(topic.GetSnapshotJsonAsync().GetAwaiter().GetResult());
            return json["height"]!.Value<int>();
        }

        // ManualUpdateを一切駆動しないため、EmptyPlaceSystemが読まれるだけの空セレクタ
        // ManualUpdate is never driven here, so this only backs the EmptyPlaceSystem read
        private class EmptySelector : IPlaceSystemSelector
        {
            public IPlaceSystem EmptyPlaceSystem { get; } = new EmptyPlaceSystem();
            public IPlaceSystem GetCurrentPlaceSystem(PlaceSystemUpdateContext context) => EmptyPlaceSystem;
        }

        private class NullPresenter : IPlacementFeedbackPresenter
        {
            public void Present(PlacementFeedback feedback) { }
            public void Hide() { }
        }
    }
}

using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.Run;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Game.Topics;
using Core.Master;
using Mooresmaster.Model.BlocksModule;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using UnityEngine;
using Object = UnityEngine.Object;

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
            var topic = new PlacementModeTopic(hub, CreateController(), heightOffset);
            try
            {
                dragState.BeginDrag(Vector3Int.zero, PlacementHitSurfaceKind.Ground);

                var revisionBeforeAdjust = hub.GetTopicRevision(PlacementModeTopic.TopicName);
                dragState.AdjustHeightOffset(2);

                // ドラッグ中の値がsnapshotへ現れ、購読(:31)がpushしたことをrevision増分で固定する
                // The in-drag value reaches the snapshot, pinning that the subscription (:31) pushed it via the revision bump
                Assert.AreEqual(2, ReadHeight(topic), "the in-drag height did not reach the shared snapshot value");
                Assert.AreEqual(dragState.HeightOffset, ReadHeight(topic));
                Assert.Greater(hub.GetTopicRevision(PlacementModeTopic.TopicName), revisionBeforeAdjust, "PlacementModeTopic did not push on height change (subscription at :31 missing?)");

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

        // productionのDisable()を通す。dragState.ClearDragAndHeight()を直接呼ぶと
        // ベルトctorが独自instanceを作る退行等をすり抜ける
        // Goes through the production Disable(); calling dragState.ClearDragAndHeight() directly
        // would let regressions like the belt ctor creating its own instance slip through
        [Test]
        public void 建築モード離脱で高さを畳み再入場は地表基準から始まる()
        {
            var heightOffset = new PlacementHeightOffset();
            var dragState = new CommonBlockPlaceDragState(heightOffset);
            var (beltSystem, dataStoreObject) = CreateBeltSystem(heightOffset);
            try
            {
                dragState.SyncSelectedBlock(new BlockId(1));
                dragState.AdjustHeightOffset(2);

                beltSystem.Disable();
                Assert.AreEqual(0, heightOffset.Value);

                // 同じブロックで入り直しても選択変化とみなされないため、離脱時に畳んでおく必要がある
                // Re-entering with the same block is not a selection change, so the height must fold on exit
                dragState.SyncSelectedBlock(new BlockId(1));
                Assert.AreEqual(0, dragState.HeightOffset);

                // 系切替でも高さは0のまま
                // The height stays 0 across a system switch
                var (otherBeltSystem, otherDataStoreObject) = CreateBeltSystem(heightOffset);
                try
                {
                    otherBeltSystem.Enable();
                    Assert.AreEqual(0, heightOffset.Value);
                }
                finally
                {
                    Object.DestroyImmediate(otherDataStoreObject);
                }
            }
            finally
            {
                Object.DestroyImmediate(dataStoreObject);
            }
        }

        [Test]
        public void 通常設置とベルトの高さは同じ1本を共有する()
        {
            var heightOffset = new PlacementHeightOffset();
            var commonDragState = new CommonBlockPlaceDragState(heightOffset);
            var (beltSystem, dataStoreObject) = CreateBeltSystem(heightOffset);
            try
            {
                commonDragState.SyncSelectedBlock(new BlockId(1));
                commonDragState.AdjustHeightOffset(1);
                Assert.AreEqual(1, heightOffset.Value);

                // ベルト側のDisableが同じheightOffsetを畳めば、注入されたのが独自instanceでは
                // なくこの共有singletonだと分かる
                // If the belt's Disable folds this same heightOffset, it was wired to this shared
                // singleton rather than an instance of its own
                beltSystem.Disable();

                Assert.AreEqual(0, heightOffset.Value, "BeltConveyorPlaceSystem must share the injected PlacementHeightOffset, not create its own");
                Assert.AreEqual(0, commonDragState.HeightOffset);
            }
            finally
            {
                Object.DestroyImmediate(dataStoreObject);
            }
        }

        private static (BeltConveyorPlaceSystem system, GameObject dataStoreObject) CreateBeltSystem(PlacementHeightOffset heightOffset)
        {
            var dataStoreObject = new GameObject("BlockGameObjectDataStore");
            var dataStore = dataStoreObject.AddComponent<BlockGameObjectDataStore>();
            var system = new BeltConveyorPlaceSystem(null, new NullPreviewController(), dataStore, null, null, heightOffset);
            return (system, dataStoreObject);
        }

        private static PlaceSystemStateController CreateController()
        {
            return new PlaceSystemStateController(new NullSelector(), new NullPresenter());
        }

        private static int ReadHeight(PlacementModeTopic topic)
        {
            var json = JObject.Parse(topic.GetSnapshotJsonAsync().GetAwaiter().GetResult());
            return json["height"]!.Value<int>();
        }

        // Disable()内のSetActive(false)以外は呼ばれない前提のno-opフェイク
        // A no-op fake; only SetActive(false) inside Disable() is expected to run
        private class NullPreviewController : IPlacementPreviewBlockGameObjectController
        {
            public bool IsActive { get; private set; }
            public void SetPreview(List<PlaceInfo> currentPlaceInfos, BlockMasterElement holdingBlockMaster) { }
            public IReadOnlyList<bool> DetectGroundOverlaps() => new List<bool>();
            public void UpdatePlaceableColors(List<PlaceInfo> placeInfos) { }
            public void SetActive(bool active) => IsActive = active;

            public bool TryGetPreviewBlock(int index, out BlockPreviewObject previewBlock)
            {
                previewBlock = null;
                return false;
            }
        }

        // ManualUpdateを一切駆動しないため、EmptyPlaceSystemが読まれるだけの空フェイク
        // ManualUpdate is never driven here, so this only backs the EmptyPlaceSystem read
        private class NullSelector : IPlaceSystemSelector
        {
            public IPlaceSystem EmptyPlaceSystem { get; } = new NullPlaceSystem();
            public IPlaceSystem GetCurrentPlaceSystem(PlaceSystemUpdateContext context) => EmptyPlaceSystem;
        }

        private class NullPlaceSystem : IPlaceSystem
        {
            public bool OwnsWheelInput => false;
            public void Enable() { }
            public void ManualUpdate(PlaceSystemUpdateContext context) { }
            public void Disable() { }
            public bool TryCancelInProgressOperation() => false;
        }

        private class NullPresenter : IPlacementFeedbackPresenter
        {
            public void Present(PlacementFeedback feedback) { }
            public void Hide() { }
        }
    }
}

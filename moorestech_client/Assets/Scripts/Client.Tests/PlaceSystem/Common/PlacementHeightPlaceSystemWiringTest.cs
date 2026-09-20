using System;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor;
using Client.Game.InGame.BlockSystem.PlaceSystem.ChainPreview;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.BlockSystem.PlaceSystem.Empty;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using System.Collections.Generic;
using Tests.Module.TestMod;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Client.Tests.PlaceSystem.Common
{
    /// <summary>
    ///     設置系が共有の設置高さへ実際に配線されていることを、本番のManualUpdate・Disable・系切替を通して検証
    ///     Verifies through the production ManualUpdate, Disable and system switch that place systems are wired to the shared height
    /// </summary>
    public class PlacementHeightPlaceSystemWiringTest
    {
        private static readonly Guid FirstBeltGuid = Guid.Parse("00000000-0000-0000-0000-000000000003");
        private static readonly Guid SecondBeltGuid = Guid.Parse("00000000-0000-0000-0000-000000000030");

        private GameObject _sceneObject;
        private Camera _camera;

        [SetUp]
        public void SetUp()
        {
            // MasterHolderを読むのは本番のManualUpdate。ForUnitTest modのマスタで通す
            // The production ManualUpdate reads MasterHolder, so it runs on the ForUnitTest mod master
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            _sceneObject = new GameObject("PlacementHeightWiringTest");
            _camera = _sceneObject.AddComponent<Camera>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_sceneObject);
        }

        [Test]
        public void ベルト設置系の持ち替えは注入された共有高さを地表へ戻す()
        {
            var heightOffset = new PlacementHeightOffset();
            var beltSystem = CreateBeltSystem(heightOffset);

            UpdateWithTarget(beltSystem, FirstBeltGuid);
            heightOffset.Adjust(2);

            // 本番のManualUpdateが持ち替えを見て共有インスタンスを畳む。自前instanceを握る退行ならここが2のまま残る
            // The production ManualUpdate folds the shared instance on a block switch; a system holding its own instance leaves 2 here
            UpdateWithTarget(beltSystem, SecondBeltGuid);

            Assert.AreEqual(0, heightOffset.Value, "BeltConveyorPlaceSystem must write the injected PlacementHeightOffset, not one of its own");
        }

        [Test]
        public void 高さを使わない系へ切り替えると共有高さは地表へ戻る()
        {
            var heightOffset = new PlacementHeightOffset();
            var beltSystem = CreateBeltSystem(heightOffset);
            var selector = new SwitchableSelector(beltSystem);
            var controller = new PlaceSystemStateController(selector, new NullPresenter(), heightOffset);

            controller.SetTarget(new BlockPlacementTarget(FirstBeltGuid, null), PlacementOrigin.FromHotbarSlot(0));
            controller.ManualUpdate();
            heightOffset.Adjust(2);

            // レール等の高さを持たない系へ移ると、HUDが読む共有高さも地表へ戻る
            // Moving to a height-less system (rails and friends) returns the shared height the HUD reads to ground
            selector.SwitchToEmpty();
            controller.ManualUpdate();

            Assert.AreEqual(0, heightOffset.Value, "the height stayed while a system that never applies it was active");
        }

        [Test]
        public void 設置系のDisableは共有高さを畳まない()
        {
            var heightOffset = new PlacementHeightOffset();
            var beltSystem = CreateBeltSystem(heightOffset);
            var (commonSystem, commonDataStoreObject) = CreateCommonSystem(heightOffset);
            try
            {
                UpdateWithTarget(beltSystem, FirstBeltGuid);
                heightOffset.Adjust(2);

                // 離脱・再入場で高さを保つのが現仕様。畳む責務を各系へ戻す退行をここで捕まえる
                // Keeping the height across leave and re-entry is the current rule; a regression folding it per system fails here
                beltSystem.Disable();
                commonSystem.Disable();

                Assert.AreEqual(2, heightOffset.Value, "a place system folded the shared height on Disable");
            }
            finally
            {
                Object.DestroyImmediate(commonDataStoreObject);
            }
        }

        private void UpdateWithTarget(IPlaceSystem placeSystem, Guid blockGuid)
        {
            var context = new PlaceSystemUpdateContext(new BlockPlacementTarget(blockGuid, null), false, new PlacementFeedback());
            placeSystem.ManualUpdate(context);
        }

        private BeltConveyorPlaceSystem CreateBeltSystem(PlacementHeightOffset heightOffset)
        {
            var dataStore = _sceneObject.AddComponent<BlockGameObjectDataStore>();
            return new BeltConveyorPlaceSystem(_camera, new NullPreviewController(), dataStore, null, null, heightOffset);
        }

        private static (CommonBlockPlaceSystem system, GameObject dataStoreObject) CreateCommonSystem(PlacementHeightOffset heightOffset)
        {
            var dataStoreObject = new GameObject("BlockGameObjectDataStore");
            var dataStore = dataStoreObject.AddComponent<BlockGameObjectDataStore>();
            var system = new CommonBlockPlaceSystem(null, new NullPreviewController(), dataStore, null, null, null, null, null, null, new ChainPlacePreviewState(), null, heightOffset);
            return (system, dataStoreObject);
        }

        // 設置系を1度だけ高さ非対応の本番EmptyPlaceSystemへ切り替えるセレクタ
        // A selector that switches once from the given system to the production height-less EmptyPlaceSystem
        private class SwitchableSelector : IPlaceSystemSelector
        {
            public IPlaceSystem EmptyPlaceSystem { get; } = new EmptyPlaceSystem();

            private IPlaceSystem _current;

            public SwitchableSelector(IPlaceSystem current)
            {
                _current = current;
            }

            public void SwitchToEmpty()
            {
                _current = EmptyPlaceSystem;
            }

            public IPlaceSystem GetCurrentPlaceSystem(PlaceSystemUpdateContext context) => _current;
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

        private class NullPresenter : IPlacementFeedbackPresenter
        {
            public void Present(PlacementFeedback feedback) { }
            public void Hide() { }
        }
    }
}

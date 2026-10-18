using System.Collections.Generic;
using System.Reflection;
using Client.Game.InGame.Block;
using Client.Game.InGame.Block.Preview;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject;
using Client.Game.InGame.UI.UIState.State.PlaceBlock;
using Core.Master;
using NUnit.Framework;
using UniRx;
using UnityEngine;

namespace Client.Tests
{
    public class PlaceBlockPreviewDisplayTest
    {
        private GameObject _storeObject;
        private GameObject _blockObject;
        private PlaceBlockPreviewDisplay _display;

        [TearDown]
        public void TearDown()
        {
            _display.OnExit();
            Object.DestroyImmediate(_blockObject);
            Object.DestroyImmediate(_storeObject);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void EnterDuringPlaceAnimation_HidesUntilCompletion(bool reenterBeforeCompletion)
        {
            _storeObject = new GameObject("preview-display-test-store");
            var store = _storeObject.AddComponent<BlockGameObjectDataStore>();
            _blockObject = new GameObject("preview-display-test-block");
            var block = _blockObject.AddComponent<BlockGameObject>();
            var preview = new FakePreviewOnlyObject();
            var view = new BlockPreviewOnlyObjectsView(new List<IPreviewOnlyObject> { preview });

            // 実アニメーターを起動せず、設置途中の状態と完了通知だけを再現する
            // Reproduce the in-progress state and completion event without running the real animator
            typeof(BlockGameObject).GetField("_previewOnlyObjectsView", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(block, view);
            typeof(BlockGameObject).GetProperty(nameof(BlockGameObject.IsPlayingPlaceAnimation)).SetValue(block, true);
            var blocks = (Dictionary<Vector3Int, BlockGameObject>)store.BlockGameObjectDictionary;
            blocks.Add(Vector3Int.zero, block);
            _display = new PlaceBlockPreviewDisplay(store);
            _display.OnEnter();
            Assert.That(preview.Active, Is.True);
            Assert.That(preview.Rendered, Is.False, "入場時もアニメーション中は描画しない");

            // 完了前に退出して戻るケースでも、完了購読を張り直す
            // Re-establish completion subscriptions after leaving and returning before completion
            if (reenterBeforeCompletion)
            {
                _display.OnExit();
                Assert.That(preview.Active, Is.False);
                _display.OnEnter();
                Assert.That(preview.Active, Is.True);
                Assert.That(preview.Rendered, Is.False, "再入場でも完了前は描画しない");
            }

            typeof(BlockGameObject).GetProperty(nameof(BlockGameObject.IsPlayingPlaceAnimation)).SetValue(block, false);
            var completed = (Subject<BlockGameObject>)typeof(BlockGameObject).GetField("_onFinishedPlaceAnimation", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(block);
            completed.OnNext(block);
            Assert.That(preview.Rendered, Is.True, "完了後に半径内描画へ切り替える");

            // 完了後も半径状態に従い、退出で必ず非表示になる
            // Continue following the radius after completion and always hide on exit
            view.SetWithinPlayerRadius(false);
            Assert.That(preview.Active, Is.True);
            Assert.That(preview.Rendered, Is.False);
            _display.OnExit();
            Assert.That(preview.Active, Is.False);
            Assert.That(preview.Rendered, Is.False);
        }

        private sealed class FakePreviewOnlyObject : IPreviewOnlyObject
        {
            public bool Active;
            public bool Rendered;
            public void Initialize(BlockId blockId) { }
            public void SetActive(bool active) { Active = active; }
            public void SetEnableRenderers(bool enable) { Rendered = enable; }
        }
    }
}

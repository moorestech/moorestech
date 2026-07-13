using Client.Game.InGame.Player;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.ViewMode
{
    public class PlayerObjectModelVisibilityTest
    {
        private GameObject _player;
        private PlayerObjectController _controller;

        [SetUp]
        public void SetUp()
        {
            _player = new GameObject("Player");
            _controller = _player.AddComponent<PlayerObjectController>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_player);
        }

        [Test]
        public void ShowingModelRestoresEachRenderersPreviousEnabledState()
        {
            var visibleRenderer = CreateRenderer("VisibleRenderer", true);
            var disabledRenderer = CreateRenderer("DisabledRenderer", false);

            _controller.SetModelVisible(false);
            _controller.SetModelVisible(true);

            Assert.IsTrue(visibleRenderer.enabled);
            Assert.IsFalse(disabledRenderer.enabled);
        }

        [Test]
        public void RefreshWhileHiddenPreservesNewRendererState()
        {
            _controller.SetModelVisible(false);
            var addedVisibleRenderer = CreateRenderer("AddedVisibleRenderer", true);
            var addedDisabledRenderer = CreateRenderer("AddedDisabledRenderer", false);

            _controller.RefreshModelVisible();
            _controller.SetModelVisible(true);

            Assert.IsTrue(addedVisibleRenderer.enabled);
            Assert.IsFalse(addedDisabledRenderer.enabled);
        }

        [Test]
        public void ShowingModelSkipsRendererDestroyedWhileHidden()
        {
            var renderer = CreateRenderer("DestroyedRenderer", true);
            _controller.SetModelVisible(false);
            Object.DestroyImmediate(renderer.gameObject);

            Assert.DoesNotThrow(() => _controller.SetModelVisible(true));
        }

        private MeshRenderer CreateRenderer(string objectName, bool enabled)
        {
            var child = new GameObject(objectName);
            child.transform.SetParent(_player.transform);
            var renderer = child.AddComponent<MeshRenderer>();
            renderer.enabled = enabled;
            return renderer;
        }
    }
}

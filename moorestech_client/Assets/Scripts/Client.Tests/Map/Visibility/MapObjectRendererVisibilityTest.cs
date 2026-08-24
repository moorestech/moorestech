using System.Collections.Generic;
using Client.Game.InGame.Map.MapObject;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Map.Visibility
{
    public class MapObjectRendererVisibilityTest
    {
        private readonly List<GameObject> _roots = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var root in _roots) Object.DestroyImmediate(root);
            _roots.Clear();
        }

        [Test]
        public void 描画だけを隠してrootとColliderを維持する()
        {
            var mapObject = CreateMapObject(out var enabledRenderer, out var disabledRenderer, out var collider);
            var visibility = new MapObjectRendererVisibility(mapObject);

            visibility.SetVisible(false);

            Assert.IsTrue(mapObject.gameObject.activeSelf);
            Assert.IsTrue(collider.enabled);
            Assert.IsFalse(enabledRenderer.enabled);
            Assert.IsFalse(disabledRenderer.enabled);
        }

        [Test]
        public void 再表示時にRendererのauthoring状態を復元する()
        {
            var mapObject = CreateMapObject(out var enabledRenderer, out var disabledRenderer, out _);
            var visibility = new MapObjectRendererVisibility(mapObject);
            visibility.SetVisible(false);

            visibility.SetVisible(true);

            Assert.IsTrue(enabledRenderer.enabled);
            Assert.IsFalse(disabledRenderer.enabled);
        }

        [Test]
        public void 破壊後は近距離へ戻ってもRendererを復元しない()
        {
            var mapObject = CreateMapObject(out var enabledRenderer, out var disabledRenderer, out _);
            var visibility = new MapObjectRendererVisibility(mapObject);
            visibility.SetVisible(false);
            mapObject.DestroyMapObject();

            visibility.SetVisible(true);

            Assert.IsFalse(enabledRenderer.enabled);
            Assert.IsFalse(disabledRenderer.enabled);
        }

        private MapObjectGameObject CreateMapObject(
            out MeshRenderer enabledRenderer,
            out MeshRenderer disabledRenderer,
            out BoxCollider collider)
        {
            var root = new GameObject("MapObjectRendererVisibilityTest");
            _roots.Add(root);
            var mapObject = root.AddComponent<MapObjectGameObject>();
            collider = root.AddComponent<BoxCollider>();

            enabledRenderer = CreateRenderer(root.transform, "Enabled", true);
            disabledRenderer = CreateRenderer(root.transform, "Disabled", false);
            return mapObject;
        }

        private static MeshRenderer CreateRenderer(Transform parent, string name, bool enabled)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent);
            var renderer = child.AddComponent<MeshRenderer>();
            renderer.enabled = enabled;
            return renderer;
        }
    }
}

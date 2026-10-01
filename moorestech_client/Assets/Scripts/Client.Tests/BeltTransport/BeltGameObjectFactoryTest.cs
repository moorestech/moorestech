using System.Collections;
using Client.Game.InGame.Entity.Factory;
using Client.Game.InGame.Entity.Object;
using Core.Master;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
namespace Client.Tests.BeltTransport
{
    public sealed class BeltGameObjectFactoryTest
    {
        [TestCase(false)]
        [TestCase(true)]
        public void TextureAndCustomModelUseCpuPositionUnderHiddenParentTest(bool custom)
        {
            var parent = new GameObject("belt-hidden-parent");
            parent.SetActive(false);
            var prefab = GameObject.CreatePrimitive(PrimitiveType.Quad);
            var material = new Material(Shader.Find("UI/Default"));
            var texture = new Texture2D(1, 1);
            if (!custom)
            {
                var component = prefab.AddComponent<BeltConveyorItemEntityObject>();
                // Editorの正式なシリアライズ経路で既存viewの参照を構成する。
                // Configure existing view references through the Editor serialization API.
                var serialized = new SerializedObject(component);
                serialized.FindProperty("meshRenderer").objectReferenceValue = prefab.GetComponent<MeshRenderer>();
                serialized.FindProperty("itemMaterial").objectReferenceValue = material;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            var factory = new BeltConveyorItemEntityObjectFactory(new Prefabs(new BeltItemPrefab(prefab, texture, custom)));
            var position = new Vector3(-1.5f, 3.85f, 4f);
            var view = factory.CreateItem(parent.transform, 17, new ItemId(custom ? 991 : 992), position).GetAwaiter().GetResult();
            var instance = ((Component)view).gameObject;
            Assert.AreEqual(17, view.EntityId);
            Assert.AreEqual(position, instance.transform.position);
            Assert.IsFalse(instance.activeInHierarchy);
            Assert.AreEqual(custom, view is CustomModelBeltConveyorItemEntityObject);
            if (!custom) Assert.AreSame(texture, instance.GetComponent<MeshRenderer>().sharedMaterial.mainTexture);
            parent.SetActive(true);
            view.SetDirectPosition(position + Vector3.forward);
            Assert.AreEqual(position + Vector3.forward, instance.transform.position);
            Assert.IsTrue(instance.activeInHierarchy);
            Object.DestroyImmediate(parent);
            Object.DestroyImmediate(prefab);
            Object.DestroyImmediate(material);
            Object.DestroyImmediate(texture);
        }
        private sealed class Prefabs : IBeltItemPrefabLoader
        {
            private readonly BeltItemPrefab _prefab;
            internal Prefabs(BeltItemPrefab prefab) { _prefab = prefab; }
            public UniTask<BeltItemPrefab> LoadAsync(ItemId itemId) => UniTask.FromResult(_prefab);
        }
    }
}

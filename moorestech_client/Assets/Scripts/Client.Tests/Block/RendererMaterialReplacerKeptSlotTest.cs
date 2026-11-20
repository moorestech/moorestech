using System.Collections.Generic;
using System.Reflection;
using Client.Game.InGame.Block;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Block
{
    /// <summary>
    /// IgnoreRendererMaterialReplacer の枠指定で、同じ Renderer の一部の材質だけを置き換えから外すテスト
    /// Tests that slot-specific IgnoreRendererMaterialReplacer keeps only some materials of the same renderer out of replacement
    /// </summary>
    public class RendererMaterialReplacerKeptSlotTest
    {
        private readonly List<Object> _createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _createdObjects) Object.DestroyImmediate(obj);
            _createdObjects.Clear();
        }

        [Test]
        public void KeptSlotKeepsTheMaterialAttachedAtReplacementThroughResetAndDestroy()
        {
            // 窓と水を1つの Renderer にまとめたパイプと同じ形: 枠0は置き換え、枠1は水位表示が後から複製した材質を残す
            // Same shape as the pipe that shares one renderer for window and water: slot 0 is replaced, slot 1 keeps the material the water-level view cloned later
            var window = NewMaterial(); var sharedWater = NewMaterial();
            var go = Own(new GameObject("window and water"));
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { window, sharedWater };
            SetMaterialSlots(go.AddComponent<IgnoreRendererMaterialReplacer>(), new[] { 1 });
            var controller = new RendererMaterialReplacerController(go);

            var clonedWater = NewMaterial();
            clonedWater.color = Color.blue;
            renderer.sharedMaterials = new[] { window, clonedWater };

            var place = NewMaterial();
            controller.CopyAndSetMaterial(place);
            controller.SetColor("_BaseColor", Color.red);
            Assert.AreNotSame(window, renderer.sharedMaterials[0]);
            Assert.AreSame(clonedWater, renderer.sharedMaterials[1]);
            Assert.AreEqual(Color.blue, clonedWater.color);

            controller.ResetMaterial();
            Assert.AreSame(window, renderer.sharedMaterials[0]);
            Assert.AreSame(clonedWater, renderer.sharedMaterials[1]);

            controller.DestroyMaterial();
            Assert.IsTrue(clonedWater != null);
        }

        [Test]
        public void EmptySlotsStillIgnoreEveryRendererUnderTheComponent()
        {
            // 枠を指定しない従来の使い方では、子の Renderer は丸ごと置き換えない
            // Without slots, as before, renderers under the component are not replaced at all
            var root = Own(new GameObject("root"));
            var ignored = new GameObject("ignored");
            ignored.transform.SetParent(root.transform);
            ignored.AddComponent<IgnoreRendererMaterialReplacer>();
            var ignoredRenderer = ignored.AddComponent<MeshRenderer>();
            var original = NewMaterial();
            ignoredRenderer.sharedMaterial = original;

            new RendererMaterialReplacerController(root).CopyAndSetMaterial(NewMaterial());
            Assert.AreSame(original, ignoredRenderer.sharedMaterial);
        }

        #region Internal

        private Material NewMaterial()
        {
            return Own(new Material(Shader.Find("Universal Render Pipeline/Lit")));
        }

        private T Own<T>(T obj) where T : Object
        {
            _createdObjects.Add(obj);
            return obj;
        }

        private static void SetMaterialSlots(IgnoreRendererMaterialReplacer component, int[] slots)
        {
            typeof(IgnoreRendererMaterialReplacer).GetField("materialSlots", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(component, slots);
        }

        #endregion
    }
}

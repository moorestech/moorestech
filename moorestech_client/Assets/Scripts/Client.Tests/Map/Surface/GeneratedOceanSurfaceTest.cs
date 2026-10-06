using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Client.Game.InGame.Environment.Terrain;
using Game.MapGeneration.Facade.Surface;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Map.Surface
{
    public class GeneratedOceanSurfaceTest
    {
        private readonly List<UnityEngine.Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            for (var index = _created.Count - 1; index >= 0; index--)
                UnityEngine.Object.DestroyImmediate(_created[index]);
            _created.Clear();
        }

        [TestCase(1)]
        [TestCase(3)]
        [Category("IgnoreCI")]
        public void 配線された全水面を包絡へ揃え共有assetを保護する(int count)
        {
            var shader = Shader.Find("BK/Water");
            Assert.That(shader, Is.Not.Null, "Production water shader required");
            var shared = new Material(shader);
            shared.SetFloat("_WavesHeight", 7f);
            _created.Add(shared);
            var renderers = new Renderer[count];
            for (var index = 0; index < count; index++)
            {
                var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
                _created.Add(plane);
                plane.transform.position = new Vector3(index * 10f, 30f, -10f);
                renderers[index] = plane.GetComponent<Renderer>();
                renderers[index].sharedMaterials = new[] { shared, shared };
            }

            // 元assetの波高を変更せず各インスタンスへ同じ包絡を適用する
            // Apply the same envelope to every instance without changing the source asset wave height
            var surface = CreateSurface(renderers);
            surface.Initialize(SurfaceEnvelope.GeneratedV5);
            var firstMaterials = renderers[0].sharedMaterials;
            surface.Initialize(SurfaceEnvelope.GeneratedV5);
            CollectionAssert.AreEqual(firstMaterials, renderers[0].sharedMaterials);
            foreach (var renderer in renderers)
            {
                Assert.That(renderer.transform.position.y, Is.EqualTo(SurfaceEnvelope.GeneratedV5.SeaY));
                Assert.That(renderer.sharedMaterials.Length, Is.EqualTo(2));
                foreach (var material in renderer.sharedMaterials)
                {
                    _created.Add(material);
                    Assert.That(material, Is.Not.SameAs(shared));
                    Assert.That(material.GetFloat("_WavesHeight"), Is.EqualTo(SurfaceEnvelope.GeneratedV5.MaximumWaveRise));
                }
            }
            Assert.That(shared.GetFloat("_WavesHeight"), Is.EqualTo(7f));
        }

        [Test]
        public void 空配線は起動失敗をErrorへ記録する()
        {
            LogAssert.Expect(LogType.Error, new Regex("At least one water renderer"));
            Assert.Throws<InvalidOperationException>(() => CreateSurface(Array.Empty<Renderer>()).Initialize(SurfaceEnvelope.GeneratedV5));
        }

        [Test]
        public void MissingReferencesAreReportedBeforeDuplicateDetection()
        {
            LogAssert.Expect(LogType.Error, new Regex("A water renderer reference is missing"));
            Assert.Throws<InvalidOperationException>(() =>
                CreateSurface(new Renderer[] { null, null }).Initialize(SurfaceEnvelope.GeneratedV5));
        }

        private GeneratedOceanSurface CreateSurface(Renderer[] renderers)
        {
            var root = new GameObject("OceanSurfaceFixture");
            _created.Add(root);
            var surface = root.AddComponent<GeneratedOceanSurface>();
            typeof(GeneratedOceanSurface).GetField("waterRenderers", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(surface, renderers);
            return surface;
        }
    }
}

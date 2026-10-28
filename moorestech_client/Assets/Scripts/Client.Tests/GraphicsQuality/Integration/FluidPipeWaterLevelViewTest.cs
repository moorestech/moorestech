using System.Reflection;
using Client.Game.InGame.FluidNetwork.View;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Client.Tests.GraphicsQuality
{
    public class FluidPipeWaterLevelViewTest : GraphicsQualityTestScope
    {
        [Test]
        public void LevelBeforeAndAfterStartUpdatesTransformAndOnlyClonedMaterial()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Asset/Block/Materials/FluidPipe/FluidPipe_Water.shadergraph");
            Assert.IsNotNull(shader);
            var source = Own(new Material(shader));
            source.SetFloat("_WaterLevel", 0.13f);
            var root = Own(new GameObject("water level view"));
            var renderer = root.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = source;
            var water = new GameObject("water").transform;
            water.SetParent(root.transform);
            var view = root.AddComponent<FluidPipeWaterLevelView>();
            SetField(view, "meshRenderer", renderer);
            SetField(view, "waterLevelTransform", water);

            // Start前に届いた水位を複製後に一度だけ反映する
            // Apply a pre-Start level once the material has been cloned
            view.SetWaterLevel(0.25f);
            Assert.AreSame(source, renderer.sharedMaterial);
            typeof(FluidPipeWaterLevelView).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, null);
            Own(renderer.sharedMaterial);
            Assert.AreNotSame(source, renderer.sharedMaterial);
            AssertWater(0.25f);
            view.SetWaterLevel(0.75f);
            AssertWater(0.75f);
            Assert.AreEqual(0.13f, source.GetFloat("_WaterLevel"), 0.00001f);

            #region Internal

            void AssertWater(float level)
            {
                Assert.AreEqual(level, renderer.sharedMaterial.GetFloat("_WaterLevel"), 0.00001f);
                Assert.AreEqual((level + 0.5f) * 0.5f, water.localPosition.y, 0.00001f);
                Assert.AreEqual(Mathf.Sqrt(1 - Mathf.Pow(level * 2 - 1, 2)) * 0.5f, water.localScale.x, 0.00001f);
            }

            #endregion
        }
    }
}

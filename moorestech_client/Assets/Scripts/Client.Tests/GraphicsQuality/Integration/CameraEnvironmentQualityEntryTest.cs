using Cinemachine;
using Client.Common;
using Client.Game.InGame.Control;
using Client.Game.InGame.Environment.Terrain;
using Client.Game.InGame.GraphicsQuality;
using Client.Game.InGame.GraphicsQuality.Appliers;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Client.Tests.GraphicsQuality
{
    public class CameraEnvironmentQualityEntryTest : GraphicsQualityTestScope
    {
        [Test]
        public void CameraInitializeAndPreferenceEventsApplyAllCameraItems()
        {
            var root = Own(new GameObject("camera quality entry"));
            root.SetActive(false);
            var camera = root.AddComponent<Camera>();
            root.AddComponent<AudioListener>();
            var virtualCamera = root.AddComponent<CinemachineVirtualCamera>();
            virtualCamera.AddCinemachineComponent<CinemachineFramingTransposer>();
            var controller = root.AddComponent<InGameCameraController>();
            SetField(controller, "mainCamera", camera);
            SetField(controller, "virtualCamera", virtualCamera);
            var previous = typeof(CameraManager).GetField("_instance", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            var savedManager = previous.GetValue(null);
            CameraManager.Initialize();
            try
            {
                // helper直呼びでは初期適用と購読の削除を検出できない
                // Direct helper calls cannot detect removal of initialization or subscriptions
                controller.Initialize();
                AssertCamera(300, true, true, CameraOverrideOption.UsePipelineSettings);
                GraphicsQualityPreference.ApplyPreset(GraphicsQualityPreset.Low);
                AssertCamera(300, false, false, CameraOverrideOption.Off);
                GraphicsQualityPreference.ApplyPreset(GraphicsQualityPreset.High);
                AssertCamera(5000, true, true, CameraOverrideOption.UsePipelineSettings);
            }
            finally { previous.SetValue(null, savedManager); }

            #region Internal

            void AssertCamera(float distance, bool shadows, bool post, CameraOverrideOption color)
            {
                Assert.AreEqual(distance, camera.farClipPlane);
                Assert.AreEqual(distance, virtualCamera.m_Lens.FarClipPlane);
                var data = camera.GetUniversalAdditionalCameraData();
                Assert.AreEqual(shadows, data.renderShadows);
                Assert.AreEqual(post, data.renderPostProcessing);
                Assert.AreEqual(color, data.requiresColorOption);
            }

            #endregion
        }

        [Test]
        public void EnvironmentInitializeAndPreferenceEventsApplyTerrainAndOcean()
        {
            var root = Own(new GameObject("environment quality entry"));
            var ocean = new GameObject("ocean");
            ocean.transform.SetParent(root.transform);
            ocean.AddComponent<GeneratedOceanSurface>();
            var renderer = ocean.AddComponent<MeshRenderer>();
            var terrainData = Own(new TerrainData());
            var terrainObject = Own(Terrain.CreateTerrainGameObject(terrainData));
            var terrain = terrainObject.GetComponent<UnityEngine.Terrain>();
            GraphicsQualityPreference.ApplyPreset(GraphicsQualityPreset.Low);
            new EnvironmentQualityApplier(new[] { terrain }, root.transform).Initialize();

            // 初期値と変更通知の両方を観測する
            // Observe both the initial value and subsequent change notifications
            AssertEnvironment(10, false);
            GraphicsQualityPreference.ApplyPreset(GraphicsQualityPreset.High);
            AssertEnvironment(5, true);
            GraphicsQualityPreference.ApplyPreset(GraphicsQualityPreset.Low);
            AssertEnvironment(10, false);

            #region Internal

            void AssertEnvironment(float pixelError, bool visible)
            {
                Assert.AreEqual(pixelError, terrain.heightmapPixelError);
                Assert.AreEqual(visible, terrain.drawTreesAndFoliage);
                Assert.AreEqual(visible, renderer.enabled);
            }

            #endregion
        }
    }
}

using System.Collections.Generic;
using System.Reflection;
using Client.Game.InGame.GraphicsQuality;
using Client.Game.InGame.GraphicsQuality.Appliers;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Client.Tests.GraphicsQuality
{
    public class RenderPipelineQualityEntryTest : GraphicsQualityTestScope
    {
        [Test]
        public void StartClonesPipelineRendererAndSsaoAndEventsRestoreSourceValues()
        {
            var previous = QualitySettings.renderPipeline;
            var previousBias = QualitySettings.lodBias;
            var source = Own(ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>());
            var renderer = Own(ScriptableObject.CreateInstance<UniversalRendererData>());
            var ssao = Own(ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>());
            renderer.rendererFeatures.Add(ssao);
            var listField = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", BindingFlags.Instance | BindingFlags.NonPublic);
            listField.SetValue(source, new ScriptableRendererData[] { renderer });
            var toggles = new[] { "m_SoftShadowsSupported", "m_AdditionalLightShadowsSupported", "m_EnableLODCrossFade" };
            foreach (var field in toggles) SetPipelineField(source, field, true);
            SetPipelineField(source, "m_AdditionalLightsRenderingMode", LightRenderingMode.PerPixel);
            source.msaaSampleCount = 4;
            source.supportsHDR = true;
            QualitySettings.renderPipeline = source;
            QualitySettings.lodBias = 1.7f;
            var previousCallbacks = new HashSet<System.Delegate>(ReadPlayModeCallbacks());
            try
            {
                // 初期適用・変更通知・元資産の不変を同時に固定する
                // Fix initial application, change delivery and source immutability together
                GraphicsQualityPreference.ApplyPreset(GraphicsQualityPreset.Low);
                new RenderPipelineQualityApplier().Start();
                var runtime = Own((UniversalRenderPipelineAsset)QualitySettings.renderPipeline);
                var clonedData = (ScriptableRendererData[])listField.GetValue(runtime);
                foreach (var data in clonedData)
                {
                    Own(data);
                    foreach (var feature in data.rendererFeatures) Own(feature);
                }
                var clonedSsao = clonedData[0].rendererFeatures[0];
                Assert.AreNotSame(source, runtime);
                Assert.AreNotSame(renderer, clonedData[0]);
                Assert.AreNotSame(ssao, clonedSsao);
                Assert.AreEqual(0.5f, runtime.renderScale);
                Assert.IsFalse(clonedSsao.isActive);
                Assert.AreEqual(1, runtime.msaaSampleCount);
                Assert.IsFalse(runtime.supportsHDR);
                Assert.AreEqual(0.4f, QualitySettings.lodBias);
                foreach (var field in toggles) Assert.AreEqual(false, GetPipelineField(runtime, field));
                Assert.AreEqual(LightRenderingMode.Disabled, GetPipelineField(runtime, "m_AdditionalLightsRenderingMode"));

                GraphicsQualityPreference.ApplyPreset(GraphicsQualityPreset.High);
                Assert.AreEqual(1f, runtime.renderScale);
                Assert.IsTrue(clonedSsao.isActive);
                Assert.AreEqual(4, runtime.msaaSampleCount);
                Assert.IsTrue(runtime.supportsHDR);
                Assert.AreEqual(1.7f, QualitySettings.lodBias);
                foreach (var field in toggles)
                {
                    Assert.AreEqual(true, GetPipelineField(runtime, field));
                    Assert.AreEqual(true, GetPipelineField(source, field));
                }
                Assert.AreEqual(LightRenderingMode.PerPixel, GetPipelineField(runtime, "m_AdditionalLightsRenderingMode"));
                Assert.AreEqual(LightRenderingMode.PerPixel, GetPipelineField(source, "m_AdditionalLightsRenderingMode"));
                Assert.AreEqual(1f, source.renderScale);
                Assert.IsTrue(ssao.isActive);
                Assert.AreEqual(4, source.msaaSampleCount);
                Assert.IsTrue(source.supportsHDR);
                Assert.AreSame(renderer, ((ScriptableRendererData[])listField.GetValue(source))[0]);
            }
            finally
            {
                // Editor終了時の復元も実入口で実行し登録を解除する
                // Exercise the actual editor-exit restoration and unregister it
                var restoreCount = 0;
                foreach (var callback in ReadPlayModeCallbacks())
                {
                    if (previousCallbacks.Contains(callback) || callback.Method.DeclaringType.DeclaringType != typeof(RenderPipelineQualityApplier)) continue;
                    restoreCount++;
                    ((System.Action<UnityEditor.PlayModeStateChange>)callback)(UnityEditor.PlayModeStateChange.ExitingPlayMode);
                }
                Assert.AreEqual(1, restoreCount, "exactly one pipeline restoration subscription is required");
                Assert.AreSame(source, QualitySettings.renderPipeline);
                Assert.AreEqual(1.7f, QualitySettings.lodBias);
                QualitySettings.renderPipeline = previous;
                QualitySettings.lodBias = previousBias;
            }

            #region Internal

            List<System.Delegate> ReadPlayModeCallbacks()
            {
                // Unity 6のイベント登録簿から、このテストが足した購読だけを回収する
                // Read Unity 6's event registry to release only subscriptions added by this test
                var tracker = typeof(UnityEditor.EditorApplication).GetField("m_PlayModeStateChangedEvent", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                var enumerator = tracker.GetType().GetMethod("GetEnumerator").Invoke(tracker, null);
                var moveNext = enumerator.GetType().GetMethod("MoveNext");
                var current = enumerator.GetType().GetProperty("Current");
                var callbacks = new List<System.Delegate>();
                while ((bool)moveNext.Invoke(enumerator, null)) callbacks.Add((System.Delegate)current.GetValue(enumerator));
                return callbacks;
            }

            void SetPipelineField(UniversalRenderPipelineAsset asset, string name, object value)
            {
                typeof(UniversalRenderPipelineAsset).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(asset, value);
            }

            object GetPipelineField(UniversalRenderPipelineAsset asset, string name)
            {
                return typeof(UniversalRenderPipelineAsset).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(asset);
            }

            #endregion
        }
    }
}

using System.Collections.Generic;
using System.Reflection;
using UniRx;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using VContainer.Unity;

namespace Client.Game.InGame.GraphicsQuality.Appliers
{
    /// <summary>
    /// URPアセットとrenderer dataを実行時に複製し、描画解像度とSSAOを複製側だけに当てる（アセットを汚さない）
    /// Clones the URP asset and renderer data at runtime and applies render scale and SSAO to the clone only (assets stay untouched)
    /// </summary>
    public sealed class RenderPipelineQualityApplier : IStartable
    {
        private const string RendererDataListField = "m_RendererDataList";
        private const float LowLodBias = 0.4f;
        private UniversalRenderPipelineAsset _source;
        private UniversalRenderPipelineAsset _runtimePipeline;
        private ScriptableRendererFeature[] _ssaoFeatures;
        private float _sourceLodBias;

        public void Start()
        {
            _source = (UniversalRenderPipelineAsset)QualitySettings.renderPipeline;
            _sourceLodBias = QualitySettings.lodBias;
            _runtimePipeline = Object.Instantiate(_source);
            _ssaoFeatures = CloneRendererData(_runtimePipeline);
            QualitySettings.renderPipeline = _runtimePipeline;
#if UNITY_EDITOR
            RestoreOnExitPlayMode(_source, _sourceLodBias);
#endif

            Apply(GraphicsQualityPreference.Current);
            GraphicsQualityPreference.OnSettingsChanged.Subscribe(Apply);
        }

        private void Apply(GraphicsQualitySettings settings)
        {
            _runtimePipeline.renderScale = settings.RenderScale.ToScale();
            foreach (var feature in _ssaoFeatures) feature.SetActive(settings.SsaoEnabled);

            // 項目に無いv7の変更は各項目の最低値に含める（裁定 2026-10-20）。最低値以外は元アセットの値へ戻す
            // v7 changes without an item ride on each item's lowest value (ruling 2026-10-20); otherwise restore the source asset's value
            var isLowResolution = settings.RenderScale == GraphicsRenderScale.Percent50;
            _runtimePipeline.msaaSampleCount = isLowResolution ? 1 : _source.msaaSampleCount;
            _runtimePipeline.supportsHDR = !isLowResolution && _source.supportsHDR;
            CopyOrDisable("m_SoftShadowsSupported", settings.ShadowsEnabled);
            CopyOrDisable("m_AdditionalLightShadowsSupported", settings.ShadowsEnabled);
            CopyOrDisable("m_EnableLODCrossFade", settings.MapObjectsVisible);
            SetField("m_AdditionalLightsRenderingMode", settings.EffectsEnabled ? GetField("m_AdditionalLightsRenderingMode", _source) : LightRenderingMode.Disabled);
            QualitySettings.lodBias = settings.MapObjectsVisible ? _sourceLodBias : LowLodBias;

            #region Internal

            void CopyOrDisable(string fieldName, bool keepSource)
            {
                SetField(fieldName, keepSource && (bool)GetField(fieldName, _source));
            }

            #endregion
        }

        // 公開setterの無い直列化フィールドは複製側だけをリフレクションで書く。名前が変わったら起動時に止める
        // Serialized fields without public setters are written on the clone only via reflection; a renamed field stops startup
        private void SetField(string fieldName, object value)
        {
            FindField(fieldName).SetValue(_runtimePipeline, value);
        }

        private static object GetField(string fieldName, UniversalRenderPipelineAsset asset)
        {
            return FindField(fieldName).GetValue(asset);
        }

        private static FieldInfo FindField(string fieldName)
        {
            var field = typeof(UniversalRenderPipelineAsset).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null) throw new System.InvalidOperationException($"[RenderPipelineQualityApplier] {fieldName} not found on URP asset.");
            return field;
        }

        // renderer dataとfeatureも複製しないとSSAOの切替が元アセットへ書き込まれる
        // Without cloning renderer data and features, toggling SSAO would write into the source assets
        private static ScriptableRendererFeature[] CloneRendererData(UniversalRenderPipelineAsset pipeline)
        {
            var sourceList = (ScriptableRendererData[])FindField(RendererDataListField).GetValue(pipeline);
            var clonedList = new ScriptableRendererData[sourceList.Length];
            var ssaoFeatures = new List<ScriptableRendererFeature>();
            for (var index = 0; index < sourceList.Length; index++)
            {
                var clonedData = Object.Instantiate(sourceList[index]);
                var features = clonedData.rendererFeatures;
                for (var featureIndex = 0; featureIndex < features.Count; featureIndex++)
                {
                    features[featureIndex] = Object.Instantiate(features[featureIndex]);
                    if (features[featureIndex] is ScreenSpaceAmbientOcclusion) ssaoFeatures.Add(features[featureIndex]);
                }
                clonedData.SetDirty();
                clonedList[index] = clonedData;
            }
            FindField(RendererDataListField).SetValue(pipeline, clonedList);
            return ssaoFeatures.ToArray();
        }

#if UNITY_EDITOR
        // Editorでは実行時の差し替えがQualitySettingsへ残るため、PlayMode終了で元アセットへ戻す
        // In the Editor the runtime swap would persist in QualitySettings, so restore the source asset when PlayMode exits
        private static void RestoreOnExitPlayMode(UniversalRenderPipelineAsset source, float sourceLodBias)
        {
            UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

            void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange change)
            {
                if (change != UnityEditor.PlayModeStateChange.ExitingPlayMode) return;
                QualitySettings.renderPipeline = source;
                QualitySettings.lodBias = sourceLodBias;
                UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            }
        }
#endif
    }
}

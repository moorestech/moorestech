using UnityEngine;
using UnityEngine.VFX;

namespace Client.Game.InGame.GraphicsQuality.Appliers
{
    /// <summary>
    /// ブロック/列車1個分のVFX・パーティクル・ライトを画質設定で止め、止める直前の有効状態へ戻す
    /// Stops one block's or train's VFX, particles and lights by graphics quality and restores the state saved just before stopping
    /// </summary>
    public sealed class EffectsQualitySwitch : MonoBehaviour
    {
        private VisualEffect[] _visualEffects;
        private ParticleSystem[] _particleSystems;
        private ParticleSystemRenderer[] _particleRenderers;
        private Light[] _lights;
        private bool[] _savedVisualEffects;
        private bool[] _savedParticlePlaying;
        private bool[] _savedParticleRenderers;
        private bool[] _savedLights;
        private bool _effectsEnabled = true;

        public void Initialize(bool effectsEnabled)
        {
            _visualEffects = GetComponentsInChildren<VisualEffect>(true);
            _particleSystems = GetComponentsInChildren<ParticleSystem>(true);
            _particleRenderers = GetComponentsInChildren<ParticleSystemRenderer>(true);
            _lights = GetComponentsInChildren<Light>(true);
            _savedVisualEffects = new bool[_visualEffects.Length];
            _savedParticlePlaying = new bool[_particleSystems.Length];
            _savedParticleRenderers = new bool[_particleRenderers.Length];
            _savedLights = new bool[_lights.Length];
            SetEffectsEnabled(effectsEnabled);
        }

        public void SetEffectsEnabled(bool effectsEnabled)
        {
            if (_effectsEnabled == effectsEnabled)
            {
                // 設置アニメの再有効化や手動Playで再開した演出も、保存状態を上書きせず止め直す
                // Stop effects restarted by placement reactivation or manual Play without overwriting saved state
                if (!effectsEnabled) StopEffects();
                return;
            }
            _effectsEnabled = effectsEnabled;

            // 状態駆動の演出が消したものを点けないよう、止める瞬間の状態を保存して戻す
            // Save the state at the moment of stopping and restore it, so effects turned off by state logic stay off
            for (var i = 0; i < _visualEffects.Length; i++) _visualEffects[i].enabled = Toggle(_visualEffects[i].enabled, ref _savedVisualEffects[i]);
            for (var i = 0; i < _particleRenderers.Length; i++) _particleRenderers[i].enabled = Toggle(_particleRenderers[i].enabled, ref _savedParticleRenderers[i]);
            for (var i = 0; i < _lights.Length; i++) _lights[i].enabled = Toggle(_lights[i].enabled, ref _savedLights[i]);

            // パーティクルは計算も止め、止める直前に再生中だったものだけ再生し直す（裁定 2026-10-20）
            // Particles stop simulating too; only those playing just before stopping are replayed (ruling 2026-10-20)
            for (var i = 0; i < _particleSystems.Length; i++)
            {
                if (effectsEnabled)
                {
                    if (_savedParticlePlaying[i]) _particleSystems[i].Play(false);
                    continue;
                }
                _savedParticlePlaying[i] = _particleSystems[i].isPlaying;
                _particleSystems[i].Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            bool Toggle(bool current, ref bool saved)
            {
                if (effectsEnabled) return saved;
                saved = current;
                return false;
            }

            #region Internal

            void StopEffects()
            {
                foreach (var effect in _visualEffects) effect.enabled = false;
                foreach (var renderer in _particleRenderers) renderer.enabled = false;
                foreach (var light in _lights) light.enabled = false;
                foreach (var particle in _particleSystems) particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            #endregion
        }

    }
}

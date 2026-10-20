using Client.Game.InGame.GraphicsQuality.Appliers;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.GraphicsQuality
{
    public class EffectsQualitySwitchTest
    {
        [Test]
        public void DisablingSavesAuthoredLightStateAndEnablingRestoresIt()
        {
            var root = new GameObject("block");
            var onLight = new GameObject("on").AddComponent<Light>();
            var offLight = new GameObject("off").AddComponent<Light>();
            onLight.transform.SetParent(root.transform);
            offLight.transform.SetParent(root.transform);
            offLight.enabled = false;
            var effects = root.AddComponent<EffectsQualitySwitch>();
            effects.Initialize(true);

            // 意図的に消してあるライトを復元で点けない
            // Restoring must not turn on a light that was authored off
            effects.SetEffectsEnabled(false);
            Assert.IsFalse(onLight.enabled);
            effects.SetEffectsEnabled(false);
            effects.SetEffectsEnabled(true);
            Assert.IsTrue(onLight.enabled);
            Assert.IsFalse(offLight.enabled);
            Object.DestroyImmediate(root);
        }

        [Test]
        public void ReapplyingOffStopsManualParticleRestartAndPreservesRestoreState()
        {
            var root = new GameObject("particle-block");
            try
            {
                var particle = root.AddComponent<ParticleSystem>();
                particle.Play(false);
                var effects = root.AddComponent<EffectsQualitySwitch>();
                effects.Initialize(true);

                // 手動再開後もoffを強制し、最初の再生状態だけを復元する
                // Enforce off after a manual restart and restore only the original playing state
                effects.SetEffectsEnabled(false);
                Assert.IsFalse(particle.isPlaying);
                particle.Play(false);
                Assert.IsTrue(particle.isPlaying);
                effects.SetEffectsEnabled(false);
                Assert.IsFalse(particle.isPlaying);
                effects.SetEffectsEnabled(true);
                Assert.IsTrue(particle.isPlaying);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}

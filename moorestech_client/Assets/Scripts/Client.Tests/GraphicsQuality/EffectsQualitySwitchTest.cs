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
            effects.SetEffectsEnabled(true);
            Assert.IsTrue(onLight.enabled);
            Assert.IsFalse(offLight.enabled);
            Object.DestroyImmediate(root);
        }
    }
}

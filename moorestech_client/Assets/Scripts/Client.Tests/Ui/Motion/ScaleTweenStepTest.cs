using Client.Ui.Foundation.Motion.Steps;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Motion
{
    public class ScaleTweenStepTest
    {
        [TestCase(0f, 1f)]
        [TestCase(0.5f, 1.04f)]
        [TestCase(1f, 1.08f)]
        public void 進み具合に応じてfromからtoへ等方に拡大する(float progress, float expected)
        {
            var target = new VisualElement();
            new ScaleTweenStep(target, 1f, 1.08f).Apply(progress);
            Assert.AreEqual(expected, target.style.scale.value.value.x, 1e-4f);
            Assert.AreEqual(expected, target.style.scale.value.value.y, 1e-4f);
        }
    }
}

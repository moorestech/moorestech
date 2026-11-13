using System.Linq;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Components
{
    // 群の淡さは filter: opacity() の値を読む（要素の opacity は子ごとに掛かり Web の群と違う）
    // Group fading reads the filter: opacity() value (element opacity applies per child, unlike the Web group)
    public static class GroupOpacity
    {
        public static float Read(VisualElement element)
        {
            var functions = element.resolvedStyle.filter.ToList();
            Assert.AreEqual(1, functions.Count, "filter の関数は opacity 1 つだけ");
            Assert.AreEqual(FilterFunctionType.Opacity, functions[0].type);
            Assert.AreEqual(1f, element.resolvedStyle.opacity, 0.001f, "要素ごとの opacity は 1 のまま（群は filter が担う）");
            return functions[0].GetParameter(0).floatValue;
        }

        // 群にしない状態は filter を持たない（opacity(1) でもオフスクリーンに描かれ倍率 1.5 でぼける）
        // A non-group state has no filter (even opacity(1) draws offscreen and blurs at scale 1.5)
        public static void AssertNone(VisualElement element)
        {
            Assert.AreEqual(0, element.resolvedStyle.filter.Count(), "filter を持たない");
            Assert.AreEqual(1f, element.resolvedStyle.opacity, 0.001f);
        }
    }
}

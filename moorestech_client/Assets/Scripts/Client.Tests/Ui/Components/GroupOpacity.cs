using System.Linq;
using Client.Ui.Foundation.Filter;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Components
{
    // 群の淡さは置き場の直下の PixelFilterGroup の filter: opacity() の値を読む（要素の opacity は子ごとに掛かり Web の群と違う）
    // Group fading reads the filter: opacity() of the PixelFilterGroup directly under the host (element opacity applies per child, unlike the Web group)
    public static class GroupOpacity
    {
        public static float Read(VisualElement host)
        {
            var functions = GroupOf(host).resolvedStyle.filter.ToList();
            Assert.AreEqual(1, functions.Count, "群の filter の関数は opacity 1 つだけ");
            Assert.AreEqual(FilterFunctionType.Opacity, functions[0].type);
            AssertHostPlain(host);
            return functions[0].GetParameter(0).floatValue;
        }

        // 群にしない状態は filter を持たない（filter はオフスクリーンに描くので、要らない状態では付けない）
        // A non-group state has no filter (a filter draws offscreen, so it is left off when not needed)
        public static void AssertNone(VisualElement host)
        {
            Assert.AreEqual(0, GroupOf(host).resolvedStyle.filter.Count(), "群は filter を持たない");
            AssertHostPlain(host);
        }

        private static VisualElement GroupOf(VisualElement host)
        {
            var group = host.hierarchy.Children().OfType<PixelFilterGroup>().SingleOrDefault();
            Assert.IsNotNull(group, $"{host.name} の直下に PixelFilterGroup がある");
            return group;
        }

        private static void AssertHostPlain(VisualElement host)
        {
            Assert.AreEqual(1f, host.resolvedStyle.opacity, 0.001f, "置き場の要素の opacity は 1 のまま（群は filter が担う）");
            Assert.AreEqual(0, host.resolvedStyle.filter.Count(), "置き場の要素は filter を持たない");
        }
    }
}

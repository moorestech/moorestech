using System.Linq;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Screens.Hotbar
{
    // 群の減光は filter: opacity() で当てる（Web の opacity と同じ群の合成）。その値を読む
    // Group dimming uses filter: opacity() (same group compositing as Web opacity); this reads its value
    public static class HotbarGroupOpacity
    {
        public static float Read(VisualElement element)
        {
            var functions = element.resolvedStyle.filter.ToList();
            Assert.AreEqual(1, functions.Count, "filter の関数は opacity 1 つだけ");
            Assert.AreEqual(FilterFunctionType.Opacity, functions[0].type);
            Assert.AreEqual(1f, element.resolvedStyle.opacity, 0.001f, "要素ごとの opacity は 1 のまま（群は filter が担う）");
            return functions[0].GetParameter(0).floatValue;
        }
    }
}

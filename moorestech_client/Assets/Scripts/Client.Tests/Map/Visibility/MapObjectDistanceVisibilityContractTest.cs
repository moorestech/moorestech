using Mooresmaster.Model.MapModule;
using NUnit.Framework;

namespace Client.Tests.Map.Visibility
{
    public class MapObjectDistanceVisibilityContractTest
    {
        [Test]
        public void 距離表示区分は通常と遠景ランドマークの二値を持つ()
        {
            Assert.AreEqual("cullable", MapObjectMasterElement.DistanceVisibilityTypeConst.cullable);
            Assert.AreEqual("landmark", MapObjectMasterElement.DistanceVisibilityTypeConst.landmark);
        }
    }
}

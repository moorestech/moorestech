using Client.WebUiHost.Game.Topics.BlockDetail;
using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.GearConsumptionModule;
using NUnit.Framework;

namespace Client.Tests.WebUi.BlockDetail
{
    /// <summary>
    /// 歯車の役割導出がスキーマの IGearConsumptionParam だけで決まることを固定する
    /// Pins that the gear role is settled solely by the schema's IGearConsumptionParam
    /// </summary>
    public class GearDetailDtoBuilderTest
    {
        // 消費パラメータを持つブロックは消費側
        // A block carrying the consumption param is a consumer
        [Test]
        public void ResolveRoleReturnsConsumerForConsumptionParam()
        {
            Assert.AreEqual("consumer", GearDetailDtoBuilder.ResolveRole(new GearConsumptionParamStub()));
        }

        // 消費パラメータを持たないブロックは発電機
        // A block without the consumption param is a generator
        [Test]
        public void ResolveRoleReturnsGeneratorForParamWithoutConsumption()
        {
            Assert.AreEqual("generator", GearDetailDtoBuilder.ResolveRole(new GearGeneratorParamStub()));
        }

        // 役割判定はinterfaceの実装有無だけを見るためGearConsumptionの中身は参照されない
        // The role check looks only at interface implementation, so GearConsumption's content is never read
        private class GearConsumptionParamStub : IGearConsumptionParam
        {
            public GearConsumption GearConsumption => null;
        }

        private class GearGeneratorParamStub
        {
        }
    }
}

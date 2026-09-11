using System.Text.RegularExpressions;
using Client.WebUiHost.Game.Topics.BlockDetail;
using Core.Master;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.GearConsumptionModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.WebUi.BlockDetail
{
    /// <summary>
    /// 歯車の役割はサーバー送信のRoleを写し、基準RPMは消費側だけマスタから引くことを固定する
    /// Pins that the gear role copies the server-sent Role and base RPM comes from master only for consumers
    /// </summary>
    public class GearDetailDtoBuilderTest
    {
        // 実マスタの発電機ブロック。サーバーが発電機と送れば基準RPM無し
        // The real generator block from master: when the server says generator there is no base RPM
        [Test]
        public void BuildGearDetailOmitsBaseRpmForMasterGeneratorBlock()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var param = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.SimpleGearGenerator).BlockParam;
            var gearDto = GearDetailDtoBuilder.BuildGearDetail(new GearStateDetail(true, 20f, 5f, GearRole.Generator), param);

            Assert.AreEqual("generator", gearDto.Role);
            Assert.IsNull(gearDto.BaseRpm);
            Assert.AreEqual(20f, gearDto.CurrentRpm, 0.001f);
            Assert.AreEqual(5f, gearDto.CurrentTorque, 0.001f);
        }

        // 実マスタの歯車機械。消費側の基準RPMはマスタ値そのもの
        // The real gear machine from master: a consumer's base RPM is the master value itself
        [Test]
        public void BuildGearDetailCarriesMasterBaseRpmForMasterConsumerBlock()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var master = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.GearMachine);
            var expectedBaseRpm = (float)((IGearConsumptionParam)master.BlockParam).GearConsumption.BaseRpm;
            var gearDto = GearDetailDtoBuilder.BuildGearDetail(new GearStateDetail(true, 12.5f, 3f, GearRole.Consumer), master.BlockParam);

            Assert.AreEqual("consumer", gearDto.Role);
            Assert.AreEqual(10f, expectedBaseRpm, 0.001f);
            Assert.AreEqual(expectedBaseRpm, gearDto.BaseRpm.Value, 0.001f);
            Assert.AreEqual(12.5f, gearDto.CurrentRpm, 0.001f);
            Assert.AreEqual(3f, gearDto.CurrentTorque, 0.001f);
        }

        // 消費側の基準RPMは消費パラメータから来る
        // A consumer's base RPM comes from its consumption param
        [Test]
        public void BuildGearDetailReturnsConsumerForConsumptionParam()
        {
            var gearDto = GearDetailDtoBuilder.BuildGearDetail(new GearStateDetail(true, 1f, 2f, GearRole.Consumer), new GearConsumptionParamStub());

            Assert.AreEqual("consumer", gearDto.Role);
            Assert.AreEqual(7f, gearDto.BaseRpm.Value, 0.001f);
        }

        // 役割はマスタでなくサーバー値に従う。消費パラメータがあっても発電機なら基準RPMは落ちる
        // The role follows the server, not master: a generator drops base RPM even with a consumption param
        [Test]
        public void BuildGearDetailFollowsServerGeneratorRoleOverConsumptionParam()
        {
            var gearDto = GearDetailDtoBuilder.BuildGearDetail(new GearStateDetail(true, 1f, 2f, GearRole.Generator), new GearConsumptionParamStub());

            Assert.AreEqual("generator", gearDto.Role);
            Assert.IsNull(gearDto.BaseRpm);
        }

        // サーバーが消費側と言うのに消費パラメータが無い不整合は歯車行を出さずエラーログする
        // A server-declared consumer without a consumption param omits the gear rows and logs an error
        [Test]
        public void BuildGearDetailOmitsGearForConsumerWithoutConsumptionParam()
        {
            LogAssert.Expect(LogType.Error, new Regex(@"\[GearDetailDtoBuilder\] Server reports GearRole\.Consumer"));
            var gearDto = GearDetailDtoBuilder.BuildGearDetail(new GearStateDetail(true, 1f, 2f, GearRole.Consumer), new GearGeneratorParamStub());

            Assert.IsNull(gearDto);
        }

        private class GearConsumptionParamStub : IGearConsumptionParam
        {
            // 引数順は baseRpm, minimumRpm, baseTorque, idlePowerRate, torqueExponentUnder, torqueExponentOver
            // Argument order: baseRpm, minimumRpm, baseTorque, idlePowerRate, torqueExponentUnder, torqueExponentOver
            public GearConsumption GearConsumption { get; } = new(7f, 0f, 0.1f, 0.2f, 2f, 1.585f);
        }

        private class GearGeneratorParamStub
        {
        }
    }
}

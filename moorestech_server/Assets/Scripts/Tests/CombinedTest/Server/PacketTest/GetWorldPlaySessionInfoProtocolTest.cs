using System;
using Game.Map.Interface.Json;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class GetWorldPlaySessionInfoProtocolTest
    {
        [Test]
        public void ワールド作成日時と累計プレイ時間が返る()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            serviceProvider.GetService<IWorldSettingsDatastore>().Initialize(serviceProvider.GetService<MapInfoJson>());

            var request = MessagePackSerializer.Serialize(new GetWorldPlaySessionInfoProtocol.RequestWorldPlaySessionInfoMessagePack());
            var response = packet.GetPacketResponse(request, new PacketResponseContext(null))[0];
            var info = MessagePackSerializer.Deserialize<GetWorldPlaySessionInfoProtocol.ResponseWorldPlaySessionInfoMessagePack>(response);

            Assert.IsTrue(DateTime.TryParse(info.WorldCreatedAt, out _), $"ISO日時ではない: {info.WorldCreatedAt}");
            Assert.GreaterOrEqual(info.TotalPlaySeconds, 0);
        }
    }
}

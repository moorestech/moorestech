using System;
using System.Globalization;
using Game.Map.Interface.Json;
using Game.Paths;
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
        public void ワールド作成日時と累計プレイ時間がDataStoreの値と一致する()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldSettings = serviceProvider.GetService<IWorldSettingsDatastore>();
            worldSettings.Initialize(serviceProvider.GetService<MapInfoJson>());

            var info = GetResponse(packet);

            Assert.IsTrue(worldSettings.TryGetWorldCreationDateTimeUtc(out var worldCreationDateTimeUtc));
            Assert.IsNull(info.WorldCreatedAtMissingReason);
            Assert.IsNull(info.TotalPlaySecondsMissingReason);
            Assert.AreEqual(worldCreationDateTimeUtc.ToUniversalTime().ToString(BugReportBundleLayout.Utc8601Format, CultureInfo.InvariantCulture), info.WorldCreatedAt);
            Assert.AreEqual(worldSettings.GetCurrentPlayTime().TotalSeconds, info.TotalPlaySeconds, 5d);
        }

        // セーブから読み直した作成日時がそのまま返ること。ここが現在時刻に化けると「今日作られた世界」が毎回並ぶ
        // The creation time restored from a save comes back as is; drifting to now would list every world as created today
        [Test]
        public void セーブから読んだ作成日時がそのまま返る()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldSettings = serviceProvider.GetService<IWorldSettingsDatastore>();
            var createdAt = new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc);
            worldSettings.LoadSettingData(new WorldSettingJsonObject(UnityEngine.Vector3.zero, createdAt, TimeSpan.FromSeconds(600), DateTime.UtcNow));

            var info = GetResponse(packet);

            Assert.IsNull(info.WorldCreatedAtMissingReason);
            Assert.AreEqual("2026-03-04T05:06:07Z", info.WorldCreatedAt);
            Assert.GreaterOrEqual(info.TotalPlaySeconds, 600d);
        }

        // 作成日時が欠けたセーブは警告付きで通る（WorldSettingsDatastore）。もっともらしい実日時を名乗らず欠損として返す
        // A save without a creation time passes with a warning (WorldSettingsDatastore); it is returned as a gap instead of a plausible timestamp
        [Test]
        public void 作成日時が無いセーブは作成日時だけが欠損理由つきで返る()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldSettings = serviceProvider.GetService<IWorldSettingsDatastore>();
            var json = new WorldSettingJsonObject(UnityEngine.Vector3.zero, default, TimeSpan.FromSeconds(600), DateTime.UtcNow);
            json.WorldCreationDateTime = null;
            worldSettings.LoadSettingData(json);

            var info = GetResponse(packet);

            Assert.IsFalse(worldSettings.TryGetWorldCreationDateTimeUtc(out _));
            Assert.IsNull(info.WorldCreatedAt);
            Assert.IsNotNull(info.WorldCreatedAtMissingReason);

            // 作成日時の欠損に累計プレイ時間を巻き込まない。1本の理由で両方を捨てると実値の累計まで失われる
            // The creation-time gap never drags the total play time along; one shared reason would throw away a real total too
            Assert.IsNull(info.TotalPlaySecondsMissingReason);
            Assert.GreaterOrEqual(info.TotalPlaySeconds, 600d);
        }

        // 復号がキーの順で束ねられると tag が worldCreatedAt に入り、進行記録には毎回「読めない日時」が載る
        // Binding the decode by position would put the tag into worldCreatedAt and fill every progress record with an unreadable date
        [Test]
        public void 応答は復号してもフィールドがずれない()
        {
            var encoded = MessagePackSerializer.Serialize(new GetWorldPlaySessionInfoProtocol.ResponseWorldPlaySessionInfoMessagePack("2026-03-04T05:06:07Z", "作成日時の理由", 12.5, "累計の理由"));
            var decoded = MessagePackSerializer.Deserialize<GetWorldPlaySessionInfoProtocol.ResponseWorldPlaySessionInfoMessagePack>(encoded);

            Assert.AreEqual("2026-03-04T05:06:07Z", decoded.WorldCreatedAt);
            Assert.AreEqual("作成日時の理由", decoded.WorldCreatedAtMissingReason);
            Assert.AreEqual(12.5, decoded.TotalPlaySeconds);
            Assert.AreEqual("累計の理由", decoded.TotalPlaySecondsMissingReason);
            Assert.AreEqual(GetWorldPlaySessionInfoProtocol.ProtocolTag, decoded.Tag);
        }

        private static GetWorldPlaySessionInfoProtocol.ResponseWorldPlaySessionInfoMessagePack GetResponse(PacketResponseCreator packet)
        {
            var request = MessagePackSerializer.Serialize(new GetWorldPlaySessionInfoProtocol.RequestWorldPlaySessionInfoMessagePack());
            var response = packet.GetPacketResponse(request, new PacketResponseContext(null))[0];
            return MessagePackSerializer.Deserialize<GetWorldPlaySessionInfoProtocol.ResponseWorldPlaySessionInfoMessagePack>(response);
        }
    }
}

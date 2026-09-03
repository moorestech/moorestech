using System;
using Game.MapGeneration.Transfer;
using MessagePack;
using NUnit.Framework;
using Server.Protocol.PacketResponse.MapData;
using UnityEngine;

namespace Tests.UnitTest.Game.MapGeneration
{
    // モード文字列の解釈はワイヤ→ドメイン復元(TerrainTransferMeta.FromWire)の1箇所だけが行う。消費側は判別子だけを見る
    // Mode-string interpretation happens only in the wire-to-domain restore (TerrainTransferMeta.FromWire); consumers look at the discriminator alone
    public class TerrainTransferMetaModeTest
    {
        [Test]
        public void ワイヤメタからのモード解釈は単一入口で完結する()
        {
            var template = new TerrainTransferMetaMessagePack(TerrainTransferMeta.CreateTemplate("world-a", 42), string.Empty);
            var templateMeta = template.ToTerrainTransferMeta();
            Assert.IsTrue(templateMeta.IsTemplate);
            Assert.IsNull(templateMeta.GeneratedPayload);

            var generated = new TerrainTransferMetaMessagePack(CreateGeneratedMeta(), "hash");
            Assert.IsFalse(generated.ToTerrainTransferMeta().IsTemplate);
        }

        [Test]
        public void 未知モードは変換入口で例外になる()
        {
            var unknown = new TerrainTransferMetaMessagePack(TerrainTransferMeta.CreateTemplate("world-c", 1), string.Empty);
            unknown.MapMode = "unknown-mode";
            Assert.Throws<InvalidOperationException>(() => unknown.ToTerrainTransferMeta());
        }

        // generated専用値はpayload単位でワイヤ往復し、別ビルドや別ノイズ窓の検出材料を落とさない
        // Generated-only values round-trip as one payload so no evidence of another build or noise window is lost
        [Test]
        public void generatedPayloadはワイヤを往復する()
        {
            var wire = new TerrainTransferMetaMessagePack(CreateGeneratedMeta(), "hash");
            var bytes = MessagePackSerializer.Serialize(wire);
            var restoredWire = MessagePackSerializer.Deserialize<TerrainTransferMetaMessagePack>(bytes);
            var payload = restoredWire.ToTerrainTransferMeta().GeneratedPayload;

            Assert.AreEqual(new Vector2(10f, 20f), payload.Origins.NoiseOrigin);
            Assert.AreEqual(new Vector2(30f, 40f), payload.Origins.SceneOrigin);
            Assert.AreEqual("fingerprint", payload.GenerationMasterFingerprint);
            Assert.AreEqual(WorldGeneratorVersion.Current, payload.GeneratorVersion);
            Assert.AreEqual("ledger-digest", payload.PlacementLedgerDigest);
        }

        [Test]
        public void templateのワイヤは従来の空値を書くがドメインにpayloadを作らない()
        {
            var wire = new TerrainTransferMetaMessagePack(TerrainTransferMeta.CreateTemplate("world-a", 42), string.Empty);

            Assert.AreEqual(Vector2.zero, (Vector2)wire.NoiseOrigin);
            Assert.AreEqual(Vector2.zero, (Vector2)wire.SceneOrigin);
            Assert.AreEqual(string.Empty, wire.GenerationMasterFingerprint);
            Assert.AreEqual(string.Empty, wire.GeneratorVersion);
            Assert.AreEqual(string.Empty, wire.PlacementLedgerDigest);
            Assert.IsNull(wire.ToTerrainTransferMeta().GeneratedPayload);
        }

        [Test]
        public void generatedPayloadは必須文字列の空値を拒否する()
        {
            var origins = new TerrainOrigins(Vector2.zero, Vector2.zero);

            Assert.Throws<ArgumentException>(() => new GeneratedTerrainTransferPayload(origins, null, "version", "digest"));
            Assert.Throws<ArgumentException>(() => new GeneratedTerrainTransferPayload(origins, string.Empty, "version", "digest"));
            Assert.Throws<ArgumentException>(() => new GeneratedTerrainTransferPayload(origins, "fingerprint", null, "digest"));
            Assert.Throws<ArgumentException>(() => new GeneratedTerrainTransferPayload(origins, "fingerprint", string.Empty, "digest"));
            Assert.Throws<ArgumentException>(() => new GeneratedTerrainTransferPayload(origins, "fingerprint", "version", null));
            Assert.Throws<ArgumentException>(() => new GeneratedTerrainTransferPayload(origins, "fingerprint", "version", string.Empty));
        }

        [Test]
        public void generatedメタはpayloadなしで構築できない()
        {
            Assert.Throws<ArgumentNullException>(() =>
                TerrainTransferMeta.CreateGenerated("world-b", 513, 4, 3, 42, null));
        }

        // 旧ビルドのワイヤ値は必須項目が欠けたまま届く。payloadを先に組むと空文字の例外が先に出て版不一致の診断へ到達しない
        // Another build's wire values arrive with required fields missing; building the payload first would raise an empty-string error before the version diagnosis
        [Test]
        public void 旧版のワイヤメタは必須項目が空でも版不一致で落ちる()
        {
            var exception = Assert.Throws<InvalidOperationException>(() => TerrainTransferMeta.FromWire(
                WorldMapMode.Generated, "world-old", 513, 4, 3, 42,
                new TerrainOrigins(Vector2.zero, Vector2.zero), "fingerprint", "3.0.0", string.Empty));

            Assert.That(exception.Message, Does.Contain("connect to a server on the same build"));
        }

        private static TerrainTransferMeta CreateGeneratedMeta()
        {
            return TerrainTransferMeta.CreateGenerated(
                "world-b", 513, 4, 3, 42,
                new GeneratedTerrainTransferPayload(
                    new TerrainOrigins(new Vector2(10f, 20f), new Vector2(30f, 40f)), "fingerprint", WorldGeneratorVersion.Current, "ledger-digest"));
        }
    }
}

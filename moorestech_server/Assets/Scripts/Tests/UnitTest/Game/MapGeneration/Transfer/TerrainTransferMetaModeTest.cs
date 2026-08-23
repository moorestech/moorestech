using System;
using Game.MapGeneration.Transfer;
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
            Assert.IsTrue(template.ToTerrainTransferMeta().IsTemplate);

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

        // 生成器の版はワイヤを往復する。落とすと別ビルドのサーバーに繋いだクライアントが違いを検出できない
        // The generator version round-trips over the wire; dropping it would blind a client connected to a server on another build
        [Test]
        public void 生成器の版はワイヤを往復する()
        {
            var generated = new TerrainTransferMetaMessagePack(CreateGeneratedMeta(), "hash");
            Assert.AreEqual("9.9.9", generated.GeneratorVersion);
            Assert.AreEqual("9.9.9", generated.ToTerrainTransferMeta().GeneratorVersion);
        }

        private static TerrainTransferMeta CreateGeneratedMeta()
        {
            return TerrainTransferMeta.CreateGenerated(
                "world-b", 513, 4, 3, 42, new TerrainOrigins(Vector2.zero, Vector2.zero), "fingerprint", "9.9.9", "ledger-digest");
        }
    }
}

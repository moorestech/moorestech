using System;
using Game.MapGeneration.Transfer;
using NUnit.Framework;
using Server.Protocol.PacketResponse.MapData;
using UnityEngine;

namespace Tests.UnitTest.Game.MapGeneration
{
    // モード文字列の解釈はワイヤ→ドメイン変換の1箇所だけが行う。消費側は判別子だけを見る
    // Mode-string interpretation happens only in the wire-to-domain conversion; consumers look at the discriminator alone
    public class TerrainTransferMetaModeTest
    {
        [Test]
        public void ワイヤメタからのモード解釈は単一入口で完結する()
        {
            var template = new TerrainTransferMetaMessagePack(TerrainTransferMeta.CreateTemplate("world-a", 42), string.Empty);
            Assert.IsTrue(template.ToTerrainTransferMeta().IsTemplate);

            var generated = new TerrainTransferMetaMessagePack(
                TerrainTransferMeta.CreateGenerated("world-b", 513, 4, 3, 42, new TerrainOrigins(Vector2.zero, Vector2.zero), "fingerprint"), "hash");
            Assert.IsFalse(generated.ToTerrainTransferMeta().IsTemplate);
        }

        [Test]
        public void 未知モードは変換入口で例外になる()
        {
            var unknown = new TerrainTransferMetaMessagePack(TerrainTransferMeta.CreateTemplate("world-c", 1), string.Empty);
            unknown.MapMode = "unknown-mode";
            Assert.Throws<InvalidOperationException>(() => unknown.ToTerrainTransferMeta());
        }
    }
}

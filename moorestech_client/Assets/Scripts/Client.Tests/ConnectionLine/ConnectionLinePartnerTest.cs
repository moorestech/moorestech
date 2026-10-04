using System;
using Client.Game.InGame.BlockSystem.StateProcessor.ConnectionLine;
using Game.Block.Blocks.ConnectionLine;
using NUnit.Framework;

namespace Client.Tests.ConnectionLine
{
    /// <summary>
    ///     状態詳細の Partners をクライアント表現へ写す変換を検証する
    ///     Verifies conversion of the state-detail Partners into the client representation
    /// </summary>
    public class ConnectionLinePartnerTest
    {
        [Test]
        public void FromMessagePacksKeepsIdAndToolGuid()
        {
            // IDと種類の組を順序どおり写す
            // Copy id/tool pairs in order
            var guid = Guid.NewGuid();
            var secondGuid = Guid.NewGuid();
            var packs = new[] { new ConnectionLinePartnerMessagePack(7, guid), new ConnectionLinePartnerMessagePack(-3, secondGuid) };

            var partners = ConnectionLinePartner.FromMessagePacks(packs);

            Assert.AreEqual(2, partners.Length);
            Assert.AreEqual(7, partners[0].PartnerId.AsPrimitive());
            Assert.AreEqual(guid, partners[0].ConnectToolGuid);
            Assert.AreEqual(-3, partners[1].PartnerId.AsPrimitive());
            Assert.AreEqual(secondGuid, partners[1].ConnectToolGuid);
        }

        [Test]
        public void FromMessagePacksTreatsNullAsEmpty()
        {
            // 接続ゼロのブロックはnull配列で届き得るため空として扱う
            // A block with no connections may arrive as a null array, treated as empty
            Assert.AreEqual(0, ConnectionLinePartner.FromMessagePacks(null).Length);
        }
    }
}

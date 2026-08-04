using Client.Game.InGame.Map.Outcrop;
using NUnit.Framework;
using Server.Protocol.PacketResponse.MapData;
using UnityEngine;

namespace Client.Tests.Mining
{
    /// <summary>
    ///     地形外の鉱脈でもAABB中心高さへ露頭を生成できることを検証する
    ///     Verifies outcrops outside loaded terrain fall back to the vein AABB center height
    /// </summary>
    public class OutcropPositionResolverTest
    {
        [Test]
        public void 地表未解決時はAABB中心高さへフォールバックする()
        {
            var layout = new VeinLayoutMessagePack("vein", 10, 20, 30, 14, 24, 34);
            var inclusiveCenter = new Vector3(12.5f, 22.5f, 32.5f);

            // 地形コライダが無い遠方でもXZを維持し、裁定済みAABB中心Yを採用する
            // Preserve XZ and use the ruled AABB-center Y even when distant terrain has no collider
            var position = OutcropGameObjectDatastore.SelectOutcropPosition(
                layout,
                inclusiveCenter,
                false,
                default);

            Assert.AreEqual(new Vector3(12.5f, 22.5f, 32.5f), position);
        }

        [Test]
        public void 地表解決時はRaycast結果をそのまま使う()
        {
            var layout = new VeinLayoutMessagePack("vein", 10, 20, 30, 14, 24, 34);
            var resolvedGround = new Vector3(12.5f, 101.25f, 32.5f);

            var position = OutcropGameObjectDatastore.SelectOutcropPosition(
                layout,
                new Vector3(12.5f, 22.5f, 32.5f),
                true,
                resolvedGround);

            Assert.AreEqual(resolvedGround, position);
        }
    }
}

using Client.Game.InGame.Map.Outcrop;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Mining
{
    /// <summary>
    ///     地形外の露頭位置を検証
    ///     Verify outcrop position beyond terrain
    /// </summary>
    public class OutcropPositionResolverTest
    {
        [Test]
        public void 地表未解決時はAABB中心高さへフォールバックする()
        {
            var inclusiveCenter = new Vector3(12.5f, 22.5f, 32.5f);

            // 地形コライダが無い遠方でも裁定済みAABB中心をそのまま採用する
            // Use the ruled AABB center as-is even when distant terrain has no collider
            var position = OutcropGameObjectDatastore.SelectOutcropPosition(
                inclusiveCenter,
                false,
                default);

            Assert.AreEqual(new Vector3(12.5f, 22.5f, 32.5f), position);
        }

        [Test]
        public void 地表解決時はRaycast結果をそのまま使う()
        {
            var resolvedGround = new Vector3(12.5f, 101.25f, 32.5f);

            var position = OutcropGameObjectDatastore.SelectOutcropPosition(
                new Vector3(12.5f, 22.5f, 32.5f),
                true,
                resolvedGround);

            Assert.AreEqual(resolvedGround, position);
        }
    }
}

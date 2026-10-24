using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject;
using Game.Block.Interface;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.PlaceSystem
{
    public class PreviewSurfaceTypeResolverTest
    {
        private static readonly Vector3Int[] LocalOutwardNormals =
        {
            new(0, 0, -1), new(0, 0, 1), new(-1, 0, 0), new(1, 0, 0), new(0, -1, 0), new(0, 1, 0),
        };

        // 水平4向きは、置き換える前の switch 式の対応表と同じ面を返す
        // The four horizontal directions return the same faces as the replaced switch tables
        [TestCase(BlockDirection.North, PreviewSurfaceType.YX_Origin, PreviewSurfaceType.YX_Z, PreviewSurfaceType.YZ_Origin, PreviewSurfaceType.YZ_X)]
        [TestCase(BlockDirection.East, PreviewSurfaceType.YZ_Origin, PreviewSurfaceType.YZ_X, PreviewSurfaceType.YX_Z, PreviewSurfaceType.YX_Origin)]
        [TestCase(BlockDirection.South, PreviewSurfaceType.YX_Z, PreviewSurfaceType.YX_Origin, PreviewSurfaceType.YZ_X, PreviewSurfaceType.YZ_Origin)]
        [TestCase(BlockDirection.West, PreviewSurfaceType.YZ_X, PreviewSurfaceType.YZ_Origin, PreviewSurfaceType.YX_Origin, PreviewSurfaceType.YX_Z)]
        public void 水平の向きは従来の対応表と一致する(BlockDirection direction, PreviewSurfaceType back, PreviewSurfaceType forward, PreviewSurfaceType left, PreviewSurfaceType right)
        {
            Assert.AreEqual(back, PreviewSurfaceTypeResolver.Resolve(direction, new Vector3Int(0, 0, -1)));
            Assert.AreEqual(forward, PreviewSurfaceTypeResolver.Resolve(direction, new Vector3Int(0, 0, 1)));
            Assert.AreEqual(left, PreviewSurfaceTypeResolver.Resolve(direction, new Vector3Int(-1, 0, 0)));
            Assert.AreEqual(right, PreviewSurfaceTypeResolver.Resolve(direction, new Vector3Int(1, 0, 0)));
            Assert.AreEqual(PreviewSurfaceType.XZ_Origin, PreviewSurfaceTypeResolver.Resolve(direction, new Vector3Int(0, -1, 0)));
            Assert.AreEqual(PreviewSurfaceType.XZ_Y, PreviewSurfaceTypeResolver.Resolve(direction, new Vector3Int(0, 1, 0)));
        }

        [Test]
        public void 全12向きで6面が6種類の面へ1対1に対応する([Values] BlockDirection direction)
        {
            var resolved = new HashSet<PreviewSurfaceType>();
            foreach (var normal in LocalOutwardNormals) resolved.Add(PreviewSurfaceTypeResolver.Resolve(direction, normal));
            Assert.AreEqual(6, resolved.Count, $"two faces resolved to the same world face for {direction}");
        }

        [Test]
        public void 上向き北に置いたブロックのローカル上面はワールドの南面になる()
        {
            // UpNorth は x 軸まわりに -90 度回すので、ローカルの上はワールドの -z を向く
            // UpNorth rotates -90 degrees about x, so local up faces world -z
            Assert.AreEqual(PreviewSurfaceType.YX_Origin, PreviewSurfaceTypeResolver.Resolve(BlockDirection.UpNorth, new Vector3Int(0, 1, 0)));
        }
    }
}

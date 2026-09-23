using System;
using Client.Game.InGame.BeltSegment.Rendering;
using Game.BeltSegment;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Client.Tests.BeltSegment.Rendering
{
    public sealed class BeltDrawResourceTest
    {
        [Test]
        public void EmptyReplacementClearsCountsAndDisposesOldBuffers()
        {
            for(int i=0;i<3;i++)
            {
                var old=new DrawFixture(Array.Empty<BeltRoute>(),Array.Empty<BeltReplaySegmentState>());
                CollectionAssert.AreEqual(new uint[]{0,0},old.Counts());
                var buffer=old.Buffers.Positions;old.Dispose();Assert.IsFalse(buffer.IsValid());
                var cell=BeltDrawPositionTest.Cell(0,0,0,0);
                using var filled=new DrawFixture(new[]{new BeltRoute(new[]{cell},BeltDrawPositionTest.Entries(cell))},
                    new[]{BeltReplaySegmentState.Normal(1,16,new[]{BeltDrawPositionTest.Item(7,BeltDirection.Back,0)})});
                CollectionAssert.AreEqual(new uint[]{1,0},filled.Counts());
            }
        }
        [Test]
        public void MissingExternalImageProducesVisibleMagentaMaterial()
        {
            var shader=Resources.Load<Shader>("BeltSegment/Rendering/BeltItemInstanced");
            Assert.IsTrue(shader.isSupported);
            LogAssert.Expect(LogType.Warning,"[BeltItemMaterials] Missing image for item kind 7; using magenta cube.");
            var missing=BeltItemMaterials.Create(7,null,shader,true);
            Assert.AreEqual(Color.magenta,missing.GetColor("_BaseColor"));
            var textured=BeltItemMaterials.Create(900000,Texture2D.whiteTexture,shader,true);
            Assert.AreSame(Texture2D.whiteTexture,textured.GetTexture("_BaseMap"));
            BeltItemMaterials.Destroy(missing);BeltItemMaterials.Destroy(textured);
        }
    }
}

using Client.Ui.Components.Recipe;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Tests.Ui.Components.Recipe
{
    /// <summary>
    /// レシピ行の光彩と括弧の影のメッシュ（Web の box-shadow 0 0 2px と drop-shadow 0 0 1px）
    /// Meshes for the recipe row glow and bracket shadow (the Web box-shadow 0 0 2px and drop-shadow 0 0 1px)
    /// </summary>
    public class RecipeRowFrameShadowMeshTest
    {
        // Vertex.tint は Color32 なので α を 255 で割って比べ、8 bit の量子化を許す（前例 ProgressArrowGlyphShadowMeshTest）
        // Vertex.tint is Color32, so divide alpha by 255 and allow the 8-bit quantum (precedent ProgressArrowGlyphShadowMeshTest)
        private const float Tolerance = 0.002f + 1f / 255f;

        [Test]
        public void 光彩は線の箱の縁で被覆率半分から外へ2σで消える()
        {
            // 線の箱 (10,10)-(110,30)、σ 1px（ぼかし 2px の半分）、Web の α 0.25
            // Border box (10,10)-(110,30), σ 1px (half the 2px blur), Web alpha 0.25
            var vertices = new Vertex[RecipeRowFrameShadowMesh.GlowVertexCount];
            var indices = new ushort[RecipeRowFrameShadowMesh.GlowIndexCount];
            RecipeRowFrameShadowMesh.BuildGlow(new Rect(10f, 10f, 100f, 20f), 1f, new Color(0f, 0.804f, 1f, 0.25f), vertices, indices);
            var outer = RecipeRowFrameShadowMesh.RingCount * 4;
            Assert.AreEqual(new Vector3(10f, 10f, Vertex.nearZ), vertices[0].position);
            Assert.AreEqual(new Vector3(8f, 8f, Vertex.nearZ), vertices[outer].position);
            Assert.AreEqual(new Vector3(112f, 32f, Vertex.nearZ), vertices[outer + 2].position);

            // 縁は Web の α 0.25 × 被覆率 0.5 をそのまま掛けた値、2σ の外端は 0.25 × Φ(-2)（約 0.0228）で 8 bit の 1 段ほど
            // The edge is Web alpha 0.25 times coverage 0.5 as-is; the 2σ rim is 0.25 × Φ(-2) (about 0.0228), about one 8-bit step
            Assert.AreEqual(0.25f * 0.5f, vertices[0].tint.a / 255f, Tolerance);
            Assert.Greater(vertices[0].tint.a, 0, "glow core survives the 8-bit quantum");
            Assert.AreEqual(0.25f * 0.02275f, vertices[outer].tint.a / 255f, Tolerance);
            AssertIndicesInRange(indices, RecipeRowFrameShadowMesh.GlowVertexCount);
        }

        [Test]
        public void 括弧の影は棒の両縁の被覆率の差で塗り外へ2σで消え1pxの棒の内側は裏返らない()
        {
            // 横の棒 9×1、σ 1px、Web の α 0.3
            // A 9 by 1 horizontal bar, σ 1px, Web alpha 0.3
            var vertices = new Vertex[RecipeRowFrameShadowMesh.BarShadowVertexCount];
            var indices = new ushort[RecipeRowFrameShadowMesh.BarShadowIndexCount];
            RecipeRowFrameShadowMesh.BuildBarShadow(new Rect(0f, 0f, 9f, 1f), 1f, new Color(0.184f, 0.737f, 1f, 0.3f), vertices, indices);
            var outer = RecipeRowFrameShadowMesh.RingCount * 4;

            // 内端は太さの半分の手前（0.49px 内側）で止まり、上下が入れ替わらない
            // The inner end stops 0.49px inside (short of half the thickness), so top and bottom never swap
            Assert.AreEqual(0.49f, vertices[0].position.y, 0.001f);
            Assert.Less(vertices[0].position.y, vertices[3].position.y);
            Assert.AreEqual(new Vector3(-2f, -2f, Vertex.nearZ), vertices[outer].position);

            // 太さ 1px・σ 1px の棒は両縁の差 Φ(-d/σ)−Φ(-(d+1)/σ): 芯（0.49px 内側）は約 0.383、縁は約 0.341、外端は Φ(-2)−Φ(-3)（約 0.0214）
            // A 1px bar with σ 1px uses the two-edge difference Φ(-d/σ)−Φ(-(d+1)/σ): core (0.49px inside) about 0.383, edge about 0.341, outer rim Φ(-2)−Φ(-3) (about 0.0214)
            Assert.AreEqual(0.3f * 0.38291f, vertices[0].tint.a / 255f, Tolerance);
            Assert.Greater(vertices[0].tint.a, 0, "bracket shadow core survives the 8-bit quantum");
            Assert.AreEqual(0.3f * 0.34134f, vertices[RecipeRowFrameShadowMesh.RingCount / 2 * 4].tint.a / 255f, Tolerance);
            Assert.AreEqual(0.3f * 0.0214f, vertices[outer].tint.a / 255f, Tolerance);
            CollectionAssert.AreEquivalent(new ushort[] { 0, 1, 2, 0, 2, 3 }, new[] { indices[0], indices[1], indices[2], indices[3], indices[4], indices[5] });
            AssertIndicesInRange(indices, RecipeRowFrameShadowMesh.BarShadowVertexCount);
        }

        private static void AssertIndicesInRange(ushort[] indices, int vertexCount)
        {
            foreach (var index in indices) Assert.Less(index, vertexCount);
        }
    }
}

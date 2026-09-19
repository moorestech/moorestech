using Client.Tests.UnitTest.CiShard.SourceScan;
using NUnit.Framework;

namespace Client.Tests.UnitTest.CiShard
{
    // マスクがコードを残し、コメント・リテラルだけを位置と改行を保って塗ることを固定する
    // Pins that masking keeps code and paints only comments and literals, preserving offsets and newlines
    public class CSharpSourceMaskerTest
    {
        [TestCase("a // x { \nb", "a        \nb")]
        [TestCase("a /* {\n} */ b", "a     \n     b")]
        [TestCase("a \"x\\\" {\" b", "a         b")]
        [TestCase("a @\"x\"\" {\" b", "a          b")]
        [TestCase("a $\"{(c ? \"}\" : \"{\")}\" b", "a                      b")]
        [TestCase("a '\"' '\\'' b", "a          b")]
        [TestCase("@class { }", "@class { }")]
        public void コメントとリテラルだけを空白で塗る(string source, string expected)
        {
            Assert.That(CSharpSourceMasker.Mask(source), Is.EqualTo(expected));
        }
    }
}

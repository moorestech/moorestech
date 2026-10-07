using Game.PlayerIdentity;
using NUnit.Framework;

namespace Tests.UnitTest.Game.PlayerIdentity
{
    public class PlayerIdentityTextTest
    {
        [TestCase("steam:76561198319362448")]
        [TestCase("steam:1")]
        [TestCase("steam:12345678901234567890")]
        [TestCase("device:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef")]
        public void 正しい書式の身元は受け入れるTest(string identity)
        {
            Assert.IsTrue(PlayerIdentityText.IsValid(identity, out var reason), reason);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("76561198319362448")]
        [TestCase("steam:")]
        [TestCase("steam:12a")]
        [TestCase("steam:１２３")]
        [TestCase("steam:1\n")]
        [TestCase("steam\0:1")]
        [TestCase("Steam:1")]
        [TestCase("steam:123456789012345678901")]
        [TestCase("device:0123")]
        [TestCase("device:0123456789ABCDEF0123456789abcdef0123456789abcdef0123456789abcdef")]
        [TestCase("mac:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef")]
        public void 不正な書式の身元は理由付きで拒否するTest(string identity)
        {
            Assert.IsFalse(PlayerIdentityText.IsValid(identity, out var reason));
            Assert.IsFalse(string.IsNullOrEmpty(reason));
        }
    }
}

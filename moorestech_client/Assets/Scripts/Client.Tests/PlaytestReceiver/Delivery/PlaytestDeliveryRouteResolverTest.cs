using Client.PlaytestReceiver.Credentials;
using Client.PlaytestReceiver.Delivery;
using NUnit.Framework;

namespace Client.Tests.PlaytestReceiver
{
    public class PlaytestDeliveryRouteResolverTest
    {
        // 配布ビルド側の分岐はTask 10の配布ビルドsmokeで確認する
        // Distribution branches are checked by the shipped-build smoke in Task 10
        [TestCase(false)]
        [TestCase(true)]
        public void Editorでは起動形態に関わらず開発者認証を選ぶ(bool isDistribution)
        {
            var route = (PlaytestDeveloperTokenRoute)PlaytestDeliveryRouteResolver.Resolve(isDistribution, new PlaytestDeveloperTokenPresent("token"));
            Assert.AreEqual("token", route.DeveloperToken);
            Assert.AreEqual(PlaytestOutboxScope.ReportsOnly, route.Scope);
        }

        [Test]
        public void 無いトークンは報告だけローカルに残す()
        {
            var route = (PlaytestLocalOnlyRoute)PlaytestDeliveryRouteResolver.Resolve(false, new PlaytestDeveloperTokenAbsent());
            Assert.AreEqual(PlaytestLocalOnlyReason.DeveloperTokenMissing, route.Reason);
            Assert.AreEqual(PlaytestOutboxScope.ReportsOnly, route.Scope);
        }

        [Test]
        public void 読めないトークンは理由を保持する()
        {
            var route = (PlaytestLocalOnlyRoute)PlaytestDeliveryRouteResolver.Resolve(false, new PlaytestDeveloperTokenUnreadable("denied"));
            Assert.AreEqual(PlaytestLocalOnlyReason.DeveloperTokenUnreadable, route.Reason);
            Assert.AreEqual("denied", route.Detail);
        }
    }
}

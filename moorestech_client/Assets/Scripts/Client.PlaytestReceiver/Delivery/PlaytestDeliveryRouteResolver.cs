using Client.PlaytestReceiver.Credentials;
using Client.PlaytestReceiver.Launch;
using Game.Paths;

namespace Client.PlaytestReceiver.Delivery
{
    public static class PlaytestDeliveryRouteResolver
    {
        internal static PlaytestDeliveryRoute ResolveCurrent()
        {
            // 配布版は開発者トークンのファイルを読まない
            // Distribution builds never read the developer token file
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var tokenRead = PlaytestDeveloperTokenFile.Read(GameSystemPaths.PlaytestDeveloperTokenFilePath);
#else
            var tokenRead = new PlaytestDeveloperTokenAbsent();
#endif
            return Resolve(PlaytestLaunchProfile.Resolve() == PlaytestLaunchKind.Distribution, tokenRead);
        }

        public static PlaytestDeliveryRoute Resolve(bool isDistribution, PlaytestDeveloperTokenRead tokenRead)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Steam起動中の開発ビルドも進行記録をテスター集計へ混ぜない
            // Even developer builds launched with Steam must not mix progress into tester statistics
            switch (tokenRead)
            {
                case PlaytestDeveloperTokenPresent present: return new PlaytestDeveloperTokenRoute(present.Token);
                case PlaytestDeveloperTokenUnreadable unreadable:
                    return new PlaytestLocalOnlyRoute(PlaytestLocalOnlyReason.DeveloperTokenUnreadable, PlaytestOutboxScope.ReportsOnly, unreadable.Reason);
                default:
                    return new PlaytestLocalOnlyRoute(PlaytestLocalOnlyReason.DeveloperTokenMissing, PlaytestOutboxScope.ReportsOnly, "developer token is absent");
            }
#else
            if (isDistribution) return new PlaytestSteamRoute();
            return new PlaytestLocalOnlyRoute(PlaytestLocalOnlyReason.DistributionUnavailable, PlaytestOutboxScope.ReportsAndProgress, "Distribution delivery unavailable; check build-info and Steam");
#endif
        }
    }
}

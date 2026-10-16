using System;
using Client.PlaytestReceiver.Credentials;
using Client.PlaytestReceiver.Delivery;

namespace Game.Paths
{
    public static class GameSystemPaths { public static string PlaytestDeveloperTokenFilePath => "unreadable"; }
}
namespace Client.PlaytestReceiver.Launch
{
    public enum PlaytestLaunchKind { Distribution, DeveloperMode }
    public static class PlaytestLaunchProfile
    {
        public static PlaytestLaunchKind Current;
        public static PlaytestLaunchKind Resolve() => Current;
    }
}
namespace Client.PlaytestReceiver.Credentials
{
    public static class PlaytestDeveloperTokenFile
    {
        public static int Reads;
        public static PlaytestDeveloperTokenRead Read(string path)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Reads++;
            return new PlaytestDeveloperTokenPresent("token");
#else
            throw new Exception("Distribution read developer credentials");
#endif
        }
    }
}
internal static class DeliveryRouteProbe
{
    private static void Require(bool condition) { if (!condition) throw new Exception("Wrong delivery route"); }
    public static void Main()
    {
        // 実resolverを三つのdefine形態で実行し、配布版の秘密読取を失敗させる
        // Execute the actual resolver under three define sets, rejecting distribution credential reads
        foreach (var distribution in new[] { false, true })
        {
            Client.PlaytestReceiver.Launch.PlaytestLaunchProfile.Current = distribution
                ? Client.PlaytestReceiver.Launch.PlaytestLaunchKind.Distribution
                : Client.PlaytestReceiver.Launch.PlaytestLaunchKind.DeveloperMode;
            var direct = PlaytestDeliveryRouteResolver.Resolve(distribution, new PlaytestDeveloperTokenPresent("token"));
            var current = PlaytestDeliveryRouteResolver.ResolveCurrent();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Require(direct is PlaytestDeveloperTokenRoute && current is PlaytestDeveloperTokenRoute);
            Require(direct.Scope == PlaytestOutboxScope.ReportsOnly);
            var absent = (PlaytestLocalOnlyRoute)PlaytestDeliveryRouteResolver.Resolve(distribution, new PlaytestDeveloperTokenAbsent());
            var unreadable = (PlaytestLocalOnlyRoute)PlaytestDeliveryRouteResolver.Resolve(distribution, new PlaytestDeveloperTokenUnreadable("denied"));
            Require(absent.Reason == PlaytestLocalOnlyReason.DeveloperTokenMissing);
            Require(unreadable.Reason == PlaytestLocalOnlyReason.DeveloperTokenUnreadable && unreadable.Detail == "denied");
#else
            Require(direct.Scope == PlaytestOutboxScope.ReportsAndProgress && current.Scope == direct.Scope);
            if (distribution) Require(direct is PlaytestSteamRoute && current is PlaytestSteamRoute);
            else Require(direct is PlaytestLocalOnlyRoute local && local.Reason == PlaytestLocalOnlyReason.DistributionUnavailable
                         && current is PlaytestLocalOnlyRoute);
#endif
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Require(PlaytestDeveloperTokenFile.Reads == 2);
#else
        Require(PlaytestDeveloperTokenFile.Reads == 0);
#endif
    }
}

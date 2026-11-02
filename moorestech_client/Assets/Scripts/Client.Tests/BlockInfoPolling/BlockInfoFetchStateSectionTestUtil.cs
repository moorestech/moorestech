using System.Reflection;
using System.Text.RegularExpressions;
using Client.WebUiHost.Common;
using Client.WebUiHost.Game.Topics.BlockDetail;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.BlockInfoPolling
{
    /// <summary>
    ///     照会欄の取得状態テストの準備・応答・検証の共通手順
    ///     Shared setup, responses and checks for the query-section fetch-state tests
    /// </summary>
    internal static class BlockInfoFetchStateSectionTestUtil
    {
        // 既存の注入前例（WireContractCleanRoomTest）に従い、実キャッシュへテスト用の通信元を持つポーラーと追跡の判定を差し込む
        // Following the existing injection precedent (WireContractCleanRoomTest), plug a poller with the test transport and the tracking decision into a real cache
        internal static BlockNetworkInfoCache CreateCache(string pollerField, object poller, string trackingProperty, object trackingValue)
        {
            var cache = new BlockNetworkInfoCache();
            typeof(BlockNetworkInfoCache).GetField(pollerField, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(cache, poller);
            typeof(BlockNetworkInfoCache).GetProperty(trackingProperty).SetValue(cache, trackingValue);
            return cache;
        }

        internal static async UniTask RespondTransportFailure<TResponse, TSnapshot>(WireResponsePollSource<TResponse, TSnapshot> source) where TResponse : class
        {
            LogAssert.Expect(LogType.Warning, new Regex(@"\[BlockInfoPoller\] .* failed for block"));
            await source.RespondTransportFailure();
        }

        internal static async UniTask RespondUnreadable<TResponse, TSnapshot>(WireResponsePollSource<TResponse, TSnapshot> source, TResponse invalid) where TResponse : class
        {
            LogAssert.Expect(LogType.Warning, "Unreadable response kind: 999");
            LogAssert.Expect(LogType.Warning, new Regex(@"\[BlockInfoPoller\] .* got an unreadable response"));
            await source.Respond(invalid);
        }

        internal static async UniTask RespondUnsupported<TResponse, TSnapshot>(WireResponsePollSource<TResponse, TSnapshot> source, TResponse unsupported) where TResponse : class
        {
            LogAssert.Expect(LogType.Warning, new Regex(@"\[BlockInfoPoller\] .* does not support block"));
            await source.Respond(unsupported);
        }

        // 取得できない欄は fetchState だけを持ち、ドメインの値を1つも持たない
        // An unavailable section carries only fetchState and not a single domain value
        internal static void AssertUnavailable(JObject section, string fetchState)
        {
            Assert.IsNotNull(section, $"section missing while {fetchState}");
            Assert.AreEqual(1, section.Count, $"unavailable section carries domain values: {section}");
            Assert.AreEqual(fetchState, section["fetchState"].Value<string>());
        }

        internal static void AssertFetched(JObject section, string key, string value)
        {
            Assert.IsNotNull(section, "section missing while fetched");
            Assert.AreEqual("fetched", section["fetchState"].Value<string>());
            Assert.AreEqual(value, section[key].Value<string>());
        }

        internal static JObject ToJson(object section)
        {
            return section == null ? null : JObject.Parse(WebUiJson.Serialize(section));
        }
    }
}

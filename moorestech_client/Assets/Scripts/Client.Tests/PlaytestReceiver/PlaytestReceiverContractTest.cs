using System;
using System.IO;
using System.Linq;
using Client.PlaytestReceiver;
using Client.PlaytestReceiver.Http;
using Client.PlaytestReceiver.Upload;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.PlaytestReceiver
{
    // 受け口（Worker）と共有する契約値の正本は tools/playtest-receiver/contract.json。Worker側は vitest で同じ一致を見る
    // The source of the values shared with the receiver is contract.json; the worker pins the same match in vitest
    public class PlaytestReceiverContractTest
    {
        private static JObject ReadContract()
        {
            var path = Path.Combine(Application.dataPath, "..", "..", "tools", "playtest-receiver", "contract.json");
            return JObject.Parse(File.ReadAllText(path));
        }

        [Test]
        public void Steamのidentityとファイル上限が受け口と一致する()
        {
            var contract = ReadContract();
            Assert.AreEqual((string)contract["steamIdentity"], PlaytestReceiverConfig.SteamIdentity);
            Assert.AreEqual((long)contract["maxFileBytes"], PlaytestReceiverConfig.MaxFileBytes);
        }

        [Test]
        public void 予約セグメントと箱の種別の語が受け口と一致する()
        {
            var contract = ReadContract();
            CollectionAssert.AreEquivalent(contract["reservedUploadSegments"].Select(token => (string)token), PlaytestOutboxScanner.ReservedUploadSegments);

            var kindSegments = Enum.GetValues(typeof(PlaytestUploadKind)).Cast<PlaytestUploadKind>().Select(PlaytestUploadPath.KindSegment);
            CollectionAssert.AreEquivalent(contract["kinds"].Select(token => (string)token), kindSegments);
        }

        [Test]
        public void 取り直しの余裕はトークン寿命より短い()
        {
            // 余裕が寿命以上だと、発行直後から毎回取り直しになる
            // A margin at or above the lifetime would renew on every call right after issue
            Assert.Less(PlaytestReceiverConfig.TokenRefreshMarginSeconds, (int)ReadContract()["tokenTtlSeconds"]);
        }

        [Test]
        public void 箱単位の上限とURL期限とアイドル期限が受け口と一致する()
        {
            var contract = ReadContract();
            Assert.AreEqual((int)contract["maxBundleFiles"], PlaytestReceiverConfig.MaxBundleFiles);
            Assert.AreEqual((long)contract["maxBundleBytes"], PlaytestReceiverConfig.MaxBundleBytes);
            Assert.AreEqual((int)contract["uploadUrlTtlSeconds"], PlaytestReceiverConfig.UploadUrlTtlSeconds);
            Assert.AreEqual((int)contract["uploadIdleTimeoutSeconds"], PlaytestReceiverConfig.UploadIdleTimeoutSeconds);
        }
    }
}

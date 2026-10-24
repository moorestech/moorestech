using System;
using System.Collections;
using Client.Game.InGame.BlockInfoPolling;
using Client.Game.InGame.CleanRoom;
using Client.WebUiHost.Game.Topics;
using Client.WebUiHost.Game.Topics.BlockDetail;
using Client.WebUiHost.Game.Topics.BlockDetail.CleanRoom;
using Client.WebUiHost.Game.Topics.BlockDetail.MachineInput;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Game.Block.Interface;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using UnityEngine.TestTools;
using static Client.Tests.BlockInfoPolling.BlockInfoFetchStateSectionTestUtil;

namespace Client.Tests.BlockInfoPolling
{
    /// <summary>
    ///     部屋・抽選の中身・止まった理由の3欄が、初回失敗・成功→失敗→成功の間ずっと fetchState を正しく出し、取得できない間にドメインの値を出さないことを固定する
    ///     Pins that the room, draw-detail and stall sections carry the right fetchState through an initial failure and success -> failure -> success, never showing domain values while unavailable
    /// </summary>
    public class BlockInfoFetchStateSectionTest
    {
        [UnityTest]
        public IEnumerator CleanRoomSectionTracksFetchStateTest() => UniTask.ToCoroutine(async () =>
        {
            var source = new WireResponsePollSource<GetCleanRoomInfoProtocol.ResponseCleanRoomInfoMessagePack, CleanRoomInfoView>(new CleanRoomInfoPollSource());
            var poller = new BlockInfoPoller<GetCleanRoomInfoProtocol.ResponseCleanRoomInfoMessagePack, CleanRoomInfoView>(source, "CleanRoom", TimeSpan.FromMilliseconds(1));
            var cache = CreateCache("_cleanRoomPoller", poller, nameof(BlockNetworkInfoCache.CleanRoomTarget), CleanRoomDetailTarget.AirFilter);
            var valid = GetCleanRoomInfoProtocol.ResponseCleanRoomInfoMessagePack.CreateNotEnclosed(CleanRoomLeakMessagePack.CreateNoEnclosure());
            var invalid = GetCleanRoomInfoProtocol.ResponseCleanRoomInfoMessagePack.CreateBlockNotFound();
            invalid.Kind = (CleanRoomInfoResponseKind)999;

            // 初回の通信失敗・成功・読めない応答・回復・通信失敗・回復を順に流す
            // Feed an initial transport failure, success, unreadable, recovery, transport failure and recovery in order
            poller.Start(new BlockInstanceId(1));
            AssertUnavailable(Section(), "notFetched");
            await RespondTransportFailure(source);
            AssertUnavailable(Section(), "transportFailed");
            await source.Respond(valid);
            AssertFetched(Section(), "kind", "noEnclosure");
            await RespondUnreadable(source, invalid);
            AssertUnavailable(Section(), "unreadable");
            await source.Respond(valid);
            AssertFetched(Section(), "kind", "noEnclosure");
            await RespondTransportFailure(source);
            AssertUnavailable(Section(), "transportFailed");
            await source.Respond(valid);
            AssertFetched(Section(), "kind", "noEnclosure");
            cache.Clear();

            #region Internal

            JObject Section()
            {
                var dto = new BlockInventoryDto();
                CleanRoomDetailDtoBuilder.Apply(dto, null, cache);
                return ToJson(dto.CleanRoom);
            }

            #endregion
        });

        [UnityTest]
        public IEnumerator CleanRoomYieldSectionTracksFetchStateTest() => UniTask.ToCoroutine(async () =>
        {
            var source = new WireResponsePollSource<GetCleanRoomYieldInfoProtocol.ResponseCleanRoomYieldInfoMessagePack, CleanRoomYieldView>(new CleanRoomYieldInfoPollSource());
            var poller = new BlockInfoPoller<GetCleanRoomYieldInfoProtocol.ResponseCleanRoomYieldInfoMessagePack, CleanRoomYieldView>(source, "CleanRoomYield", TimeSpan.FromMilliseconds(1));
            var cache = CreateCache("_cleanRoomYieldPoller", poller, nameof(BlockNetworkInfoCache.CleanRoomTarget), CleanRoomDetailTarget.Machine);
            var valid = GetCleanRoomYieldInfoProtocol.ResponseCleanRoomYieldInfoMessagePack.CreateDicing(0.25d, null);
            var invalid = GetCleanRoomYieldInfoProtocol.ResponseCleanRoomYieldInfoMessagePack.CreateNoRole();
            invalid.Kind = (CleanRoomYieldResponseKind)999;

            // 非対応の対象も読めない応答と同じく値を捨てて再試行し、回復する
            // An unsupported target drops the value and retries like an unreadable response, then recovers
            poller.Start(new BlockInstanceId(1));
            AssertUnavailable(Section(), "notFetched");
            await RespondTransportFailure(source);
            AssertUnavailable(Section(), "transportFailed");
            await source.Respond(valid);
            AssertFetched(Section(), "role", "dicing");
            await RespondUnreadable(source, invalid);
            AssertUnavailable(Section(), "unreadable");
            await source.Respond(valid);
            AssertFetched(Section(), "role", "dicing");
            await RespondUnsupported(source, GetCleanRoomYieldInfoProtocol.ResponseCleanRoomYieldInfoMessagePack.CreateUnsupportedTarget());
            AssertUnavailable(Section(), "unsupportedTarget");
            await RespondTransportFailure(source);
            AssertUnavailable(Section(), "transportFailed");
            await source.Respond(valid);
            AssertFetched(Section(), "role", "dicing");

            // 取得済みの役割なしは欄なしで、取得できない欄とは別
            // A fetched role-less recipe has no section, distinct from an unavailable one
            await source.Respond(GetCleanRoomYieldInfoProtocol.ResponseCleanRoomYieldInfoMessagePack.CreateNoRole());
            Assert.IsNull(Section());
            cache.Clear();

            #region Internal

            JObject Section()
            {
                var dto = new BlockInventoryDto();
                CleanRoomYieldDetailDtoBuilder.Apply(dto, cache);
                return ToJson(dto.CleanRoomYield);
            }

            #endregion
        });

        [UnityTest]
        public IEnumerator MachineInputStallSectionTracksFetchStateTest() => UniTask.ToCoroutine(async () =>
        {
            var source = new WireResponsePollSource<GetMachineInputStallProtocol.ResponseMachineInputStallMessagePack, MachineInputStallView>(new MachineInputStallPollSource());
            var poller = new BlockInfoPoller<GetMachineInputStallProtocol.ResponseMachineInputStallMessagePack, MachineInputStallView>(source, "MachineInputStall", TimeSpan.FromMilliseconds(1));
            var cache = CreateCache("_machineInputStallPoller", poller, nameof(BlockNetworkInfoCache.IsTrackingMachineInputStall), true);
            var valid = GetMachineInputStallProtocol.ResponseMachineInputStallMessagePack.CreateStalled(MachineInputStallReasonKind.MixedLevel, 7);
            var invalid = GetMachineInputStallProtocol.ResponseMachineInputStallMessagePack.CreateNotStalled();
            invalid.Kind = (MachineInputStallResponseKind)999;

            // 初回失敗から回復し、読めない応答・非対応・通信失敗のどれの後も正常値へ戻る
            // Recover from an initial failure and return to the valid value after unreadable, unsupported and transport failures
            poller.Start(new BlockInstanceId(1));
            AssertUnavailable(Section(), "notFetched");
            await RespondTransportFailure(source);
            AssertUnavailable(Section(), "transportFailed");
            await source.Respond(valid);
            AssertFetched(Section(), "reason", "mixedLevel");
            await RespondUnreadable(source, invalid);
            AssertUnavailable(Section(), "unreadable");
            await source.Respond(valid);
            AssertFetched(Section(), "reason", "mixedLevel");
            await RespondUnsupported(source, GetMachineInputStallProtocol.ResponseMachineInputStallMessagePack.CreateUnsupportedTarget());
            AssertUnavailable(Section(), "unsupportedTarget");
            await RespondTransportFailure(source);
            AssertUnavailable(Section(), "transportFailed");
            await source.Respond(valid);
            AssertFetched(Section(), "reason", "mixedLevel");

            // 取得済みで止まっていなければ欄なしで、取得できない欄とは別
            // A fetched not-stalled machine has no section, distinct from an unavailable one
            await source.Respond(GetMachineInputStallProtocol.ResponseMachineInputStallMessagePack.CreateNotStalled());
            Assert.IsNull(Section());
            cache.Clear();

            #region Internal

            JObject Section()
            {
                var dto = new BlockInventoryDto();
                MachineInputStallDetailDtoBuilder.Apply(dto, cache);
                return ToJson(dto.MachineInputStall);
            }

            #endregion
        });
    }
}

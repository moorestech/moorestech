using System.Collections.Generic;
using System.Threading;
using Client.Game.InGame.Train.Timetable;
using Client.WebUiHost.Game.Topics;
using Cysharp.Threading.Tasks;
using Game.Train.RailGraph;
using Game.Train.Unit;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using Server.Util.MessagePack;
using UniRx;
using UnityEngine;

namespace Client.Tests.WebUiHost.Train
{
    // 時刻表タブの取得要求の回数・重複抑止・再要求を、問い合わせ境界の差し替えで観測する
    // Observe fetch request counts, de-duplication, and re-requests by swapping the query boundary
    public class TrainTimetableFetcherTest
    {
        [Test]
        public void RequestsOnceWhileInFlightAndAfterFetched()
        {
            var query = new RecordingTrainTimetableQuery();
            var datastore = new ClientTrainTimetableDatastore();
            var fetcher = new TrainTimetableFetcher(query, datastore);
            var id = TrainUnitInstanceId.Create();

            // 応答前の重複要求は1回に畳む
            // Duplicate requests before the response fold into one
            fetcher.RequestForOpenedTab(id);
            fetcher.RequestForOpenedTab(id);
            Assert.That(query.Requests, Is.EqualTo(new[] { id }));

            // 応答はドメイン型でデータストアへ入り、取得済みの列車は再要求しない
            // The response lands in the datastore as the domain type, and a fetched train is not re-requested
            query.Respond(0, Found(id));
            fetcher.RequestForOpenedTab(id);
            Assert.That(query.Requests.Count, Is.EqualTo(1));
            Assert.That(datastore.TryGet(id, out var stored), Is.True);
            Assert.That(stored.Stops[0].StationPosition, Is.EqualTo(new Vector3Int(1, 2, 3)));
            Assert.That(fetcher.IsUnavailable(id), Is.False);
        }

        [Test]
        public void CloseLetsTheNextOpenRequestAgain()
        {
            var query = new RecordingTrainTimetableQuery();
            var fetcher = new TrainTimetableFetcher(query, new ClientTrainTimetableDatastore());
            var id = TrainUnitInstanceId.Create();

            fetcher.RequestForOpenedTab(id);
            query.Respond(0, Found(id));
            fetcher.Close();
            fetcher.RequestForOpenedTab(id);

            Assert.That(query.Requests, Is.EqualTo(new[] { id, id }));
        }

        [Test]
        public void NoResponseRetriesOnlyOnceThenBecomesUnavailable()
        {
            var query = new RecordingTrainTimetableQuery();
            var fetcher = new TrainTimetableFetcher(query, new ClientTrainTimetableDatastore());
            var id = TrainUnitInstanceId.Create();
            var failures = 0;
            using var subscription = fetcher.OnFetchFailed.Subscribe(_ => failures++);

            // 1回目の応答なしは取得不可にせず1回だけ再要求する
            // The first no-response re-requests once without turning unavailable
            fetcher.RequestForOpenedTab(id);
            query.Respond(0, null);
            Assert.That(query.Requests.Count, Is.EqualTo(2));
            Assert.That(fetcher.IsUnavailable(id), Is.False);

            // 再要求も応答なしなら、それ以上要求せず取得不可として再配信を促す
            // If the retry also gets no response, stop requesting and ask for a republish as unavailable
            query.Respond(1, null);
            Assert.That(query.Requests.Count, Is.EqualTo(2));
            Assert.That(fetcher.IsUnavailable(id), Is.True);
            Assert.That(failures, Is.EqualTo(1));
        }

        [Test]
        public void TrainMissingOnServerIsUnavailableWithoutRetry()
        {
            var query = new RecordingTrainTimetableQuery();
            var fetcher = new TrainTimetableFetcher(query, new ClientTrainTimetableDatastore());
            var id = TrainUnitInstanceId.Create();

            fetcher.RequestForOpenedTab(id);
            query.Respond(0, new GetTrainTimetableProtocol.GetTrainTimetableResponse(null));

            Assert.That(query.Requests.Count, Is.EqualTo(1));
            Assert.That(fetcher.IsUnavailable(id), Is.True);
        }

        [Test]
        public void ResponseAfterCloseDoesNotMarkTheNextOpenUnavailable()
        {
            var query = new RecordingTrainTimetableQuery();
            var fetcher = new TrainTimetableFetcher(query, new ClientTrainTimetableDatastore());
            var id = TrainUnitInstanceId.Create();

            fetcher.RequestForOpenedTab(id);
            fetcher.Close();
            query.Respond(0, new GetTrainTimetableProtocol.GetTrainTimetableResponse(null));

            Assert.That(fetcher.IsUnavailable(id), Is.False);
        }

        [Test]
        public void TrainChangeRefetchesOnlyWhileTheTabIsOpen()
        {
            var query = new RecordingTrainTimetableQuery();
            var fetcher = new TrainTimetableFetcher(query, new ClientTrainTimetableDatastore());
            var before = TrainUnitInstanceId.Create();
            var after = TrainUnitInstanceId.Create();

            // タブを開く前の編成変化では取得しない
            // A composition change before the tab opens does not fetch
            fetcher.FollowOpenTrainChange(after);
            Assert.That(query.Requests, Is.Empty);

            // タブを開いた後は新しい列車IDで取り直す
            // After the tab opens, refetch with the new train id
            fetcher.RequestForOpenedTab(before);
            fetcher.FollowOpenTrainChange(after);
            Assert.That(query.Requests, Is.EqualTo(new[] { before, after }));
        }

        private static GetTrainTimetableProtocol.GetTrainTimetableResponse Found(TrainUnitInstanceId id)
        {
            var stops = new[] { new TrainTimetableStop(new Vector3Int(1, 2, 3), StationNodeSide.Back) };
            return new GetTrainTimetableProtocol.GetTrainTimetableResponse(new TrainTimetableMessagePack(new TrainTimetableSnapshot(id, false, 0, stops)));
        }

        // 要求を記録し、応答はテストが順に返す
        // Record requests; the test returns responses in order
        private class RecordingTrainTimetableQuery : ITrainTimetableQuery
        {
            public readonly List<TrainUnitInstanceId> Requests = new();
            private readonly List<UniTaskCompletionSource<GetTrainTimetableProtocol.GetTrainTimetableResponse>> _pending = new();

            public UniTask<GetTrainTimetableProtocol.GetTrainTimetableResponse> GetTrainTimetable(TrainUnitInstanceId trainUnitInstanceId, CancellationToken ct)
            {
                Requests.Add(trainUnitInstanceId);
                var source = new UniTaskCompletionSource<GetTrainTimetableProtocol.GetTrainTimetableResponse>();
                _pending.Add(source);
                return source.Task;
            }

            public void Respond(int requestIndex, GetTrainTimetableProtocol.GetTrainTimetableResponse response)
            {
                _pending[requestIndex].TrySetResult(response);
            }
        }
    }
}

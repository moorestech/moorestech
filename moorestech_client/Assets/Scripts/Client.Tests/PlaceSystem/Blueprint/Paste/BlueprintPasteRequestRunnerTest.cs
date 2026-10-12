using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Paste;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Tests.PlaceSystem.Blueprint.Paste
{
    public class BlueprintPasteRequestRunnerTest
    {
        [Test]
        public async Task 分割要求は応答待ちして不足後のチャンクを送らない()
        {
            var transport = new GatedTransport();
            var runner = new BlueprintPasteRequestRunner(transport);
            var origins = Enumerable.Range(0, 129).Select(index => new Vector3Int(index, 0, 0)).ToList();
            var run = runner.Run(Guid.NewGuid(), 0, origins).AsTask();

            // 最初の応答前に二つ目を送らない
            // Do not send the second chunk before the first response
            Assert.AreEqual(1, transport.Requests.Count);
            Assert.AreEqual(64, transport.Requests[0].Origins.Count);
            transport.CompleteNext(false);
            await Task.Yield();
            Assert.AreEqual(2, transport.Requests.Count);
            Assert.AreEqual(64, transport.Requests[1].Origins.Count);

            // 二つ目で不足すると最後の一原点を送らない
            // A shortage in the second chunk prevents the last origin from being sent
            transport.CompleteNext(true);
            var cells = await run;
            Assert.AreEqual(2, transport.Requests.Count);
            Assert.IsEmpty(cells);
        }

        private sealed class GatedTransport : IBlueprintPasteRequestTransport
        {
            private readonly Queue<UniTaskCompletionSource<BlueprintResponse>> _pending = new();
            public readonly List<BlueprintRequest> Requests = new();

            public UniTask<BlueprintResponse> Send(BlueprintRequest request)
            {
                Requests.Add(request);
                var gate = new UniTaskCompletionSource<BlueprintResponse>();
                _pending.Enqueue(gate);
                return gate.Task;
            }

            public void CompleteNext(bool hasCostShortage)
            {
                var response = new BlueprintResponse(true, BlueprintFailureReason.None, hasCostShortage,
                    new List<BlueprintPlacedCellMessagePack>());
                _pending.Dequeue().TrySetResult(response);
            }
        }
    }
}

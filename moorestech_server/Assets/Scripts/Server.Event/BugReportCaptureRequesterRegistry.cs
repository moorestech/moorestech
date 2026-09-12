using System.Collections.Generic;
using UnityEngine;

namespace Server.Event
{
    // 即時スナップショット要求の要求元プレイヤーを覚え、完了イベントをその1人へ返すために引き当てる
    // Remembers which player asked for an immediate snapshot so the completion event goes back to that one player
    public class BugReportCaptureRequesterRegistry
    {
        // 要求はプロトコル（受信スレッド）で登録し、完了はtickスレッドで引き当てるので錠で直列化する
        // Requests are recorded on the receive thread and resolved on the tick thread, so a lock serializes them
        private readonly object _lock = new();
        private readonly Dictionary<long, int> _requesterPlayerIds = new();

        public void Remember(long requestId, int playerId)
        {
            lock (_lock)
            {
                _requesterPlayerIds[requestId] = playerId;
            }
        }

        // 完了は1要求につき1回なので、引き当てたら覚えを捨てる
        // A request completes once, so the memory is dropped as soon as it is resolved
        public bool TryTakeRequester(long requestId, out int playerId)
        {
            lock (_lock)
            {
                if (!_requesterPlayerIds.Remove(requestId, out playerId))
                {
                    Debug.LogWarning($"即時スナップショットの要求元が分からないため完了イベントを配信しません 要求ID:{requestId}");
                    return false;
                }
                return true;
            }
        }
    }
}

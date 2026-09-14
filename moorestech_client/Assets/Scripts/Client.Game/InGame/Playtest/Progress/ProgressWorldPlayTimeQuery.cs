using System;
using System.Threading;
using Client.Game.InGame.Context;
using Cysharp.Threading.Tasks;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.Playtest.Progress
{
    // ヘッダの中で唯一サーバー応答を待つ値の取得口。待ちと欠損理由付けをここへ畳み、ProgressRecorderには開始の配線だけを残す
    // The one header value that awaits the server; the wait and its missing reasons live here so ProgressRecorder keeps only the start wiring
    internal static class ProgressWorldPlayTimeQuery
    {
        public static async UniTask FillAsync(ProgressSessionWriter writer, CancellationToken cancellationToken)
        {
            var info = await ClientContext.VanillaApi.Response.GetWorldPlaySessionInfo(cancellationToken);
            writer.UpdateWorldPlayTime(ToWorldPlayTime(info));

            #region Internal

            // 応答なし（10秒のタイムアウト）も欠損の一種。空文字と0で埋めず理由を持たせる
            // A missing response (the 10 second timeout) is a gap too, carried with its reason instead of an empty string and a zero
            ProgressWorldPlayTime ToWorldPlayTime(GetWorldPlaySessionInfoProtocol.ResponseWorldPlaySessionInfoMessagePack response)
            {
                if (response == null) return ProgressWorldPlayTime.Unavailable("ワールドのプレイ時間の応答が返らなかった");
                if (response.MissingReason != null) return ProgressWorldPlayTime.Unavailable(response.MissingReason);
                return ProgressWorldPlayTime.Received(response.WorldCreatedAt, response.TotalPlaySeconds, DateTime.UtcNow);
            }

            #endregion
        }

        public static void LogFailure(Exception exception)
        {
            if (exception is OperationCanceledException)
            {
                Debug.Log("セッションが終わったためワールドのプレイ時間の取得を打ち切りました");
                return;
            }
            Debug.LogError($"進行記録のヘッダにプレイ時間を書けませんでした: {exception.GetBaseException().Message}");
        }
    }
}

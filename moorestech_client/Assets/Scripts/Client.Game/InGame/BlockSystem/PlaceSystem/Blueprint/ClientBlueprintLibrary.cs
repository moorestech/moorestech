using System;
using System.Collections.Generic;
using System.Threading;
using Client.Game.InGame.Context;
using Cysharp.Threading.Tasks;
using Server.Protocol.PacketResponse;
using UniRx;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint
{
    // DeleteBlueprintの結果種別。呼び出し側がNotFound（サーバー応答あり）と通信失敗を区別するために使う
    // Result kind for DeleteBlueprint; lets callers distinguish a server-side NotFound from a communication failure
    public enum BlueprintDeleteResult
    {
        Success,
        NotFound,
        RequestFailed,
    }

    /// <summary>
    ///     サーバーのBPライブラリのクライアント側キャッシュ
    ///     Client-side cache of the server blueprint library
    /// </summary>
    public class ClientBlueprintLibrary
    {
        // キャッシュが最新全件に置き換わったら発火する（BuildMenuTopic の再配信トリガ）
        // Fires when the cache is replaced with a fresh full list (republish trigger for BuildMenuTopic)
        public IObservable<Unit> OnChanged => _onChanged;
        private readonly Subject<Unit> _onChanged = new();

        private readonly List<BlueprintMessagePack> _blueprints = new();

        public IReadOnlyList<BlueprintMessagePack> Blueprints => _blueprints;

        public async UniTask Refresh(CancellationToken ct)
        {
            var response = await ClientContext.VanillaApi.Response.SendBlueprintRequest(BlueprintRequest.CreateGetAllRequest(), ct);
            ApplyResponse(response);
        }

        public async UniTask<(bool success, Guid blueprintGuid)> CreateBlueprint(string name, Vector3Int min, Vector3Int max, CancellationToken ct)
        {
            var request = BlueprintRequest.CreateCreateRequest(name, min, max);
            var response = await ClientContext.VanillaApi.Response.SendBlueprintRequest(request, ct);

            // タイムアウト等のnull応答は失敗扱い
            // Treat a null response (timeout etc.) as failure
            if (response == null) return (false, Guid.Empty);

            ApplyResponse(response);
            return (response.Success, response.Success ? Guid.Parse(response.RegisteredGuidStr) : Guid.Empty);
        }

        public async UniTask<BlueprintDeleteResult> DeleteBlueprint(Guid blueprintGuid, CancellationToken ct)
        {
            var response = await ClientContext.VanillaApi.Response.SendBlueprintRequest(BlueprintRequest.CreateDeleteRequest(blueprintGuid), ct);
            ApplyResponse(response);

            // null応答（タイムアウト等）は通信失敗、Success=falseはサーバー側NotFoundとして区別する
            // A null response (timeout etc.) is a communication failure; distinguish it from a server-side NotFound (Success=false)
            if (response == null) return BlueprintDeleteResult.RequestFailed;
            return response.Success ? BlueprintDeleteResult.Success : BlueprintDeleteResult.NotFound;
        }

        private void ApplyResponse(BlueprintResponse response)
        {
            // 成功レスポンスのみ最新全件を持つため、null・失敗時はキャッシュを保持する
            // Only success responses carry the full list; keep the cache on null or failure
            if (response == null || !response.Success) return;

            _blueprints.Clear();
            _blueprints.AddRange(response.Blueprints);
            _onChanged.OnNext(Unit.Default);
        }
    }
}

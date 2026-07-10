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

        public async UniTask<(bool success, string registeredName)> CreateBlueprint(string name, Vector3Int min, Vector3Int max, CancellationToken ct)
        {
            var request = BlueprintRequest.CreateCreateRequest(name, min, max);
            var response = await ClientContext.VanillaApi.Response.SendBlueprintRequest(request, ct);

            // タイムアウト等のnull応答は失敗扱い
            // Treat a null response (timeout etc.) as failure
            if (response == null) return (false, null);

            ApplyResponse(response);
            return (response.Success, response.RegisteredName);
        }

        public async UniTask DeleteBlueprint(string name, CancellationToken ct)
        {
            var response = await ClientContext.VanillaApi.Response.SendBlueprintRequest(BlueprintRequest.CreateDeleteRequest(name), ct);
            ApplyResponse(response);
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

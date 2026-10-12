using System;
using System.Collections.Generic;
using System.Threading;
using Client.Game.InGame.Context;
using Cysharp.Threading.Tasks;
using Game.Blueprint;
using Server.Protocol.PacketResponse;
using UniRx;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint
{
    // 削除結果でNotFound・未解放拒否・通信失敗・未知の拒否理由を区別
    // Distinguishes NotFound, locked-feature rejection, request failure and unknown rejection reasons
    public enum BlueprintDeleteResult
    {
        Success,
        NotFound,
        NotUnlocked,
        RequestFailed,
        Unknown,
    }

    public interface IBlueprintDeleteService
    {
        UniTask<BlueprintDeleteResult> DeleteBlueprint(Guid blueprintGuid, CancellationToken ct);
    }

    /// <summary>
    ///     サーバーのBPライブラリのクライアント側キャッシュ
    ///     Client-side cache of the server blueprint library
    /// </summary>
    public class ClientBlueprintLibrary : IBlueprintDeleteService, IBlueprintLookup
    {
        // キャッシュが最新全件に置き換わったら発火する（BuildMenuTopic の再配信トリガ）
        // Fires when the cache is replaced with a fresh full list (republish trigger for BuildMenuTopic)
        public IObservable<Unit> OnChanged => _onChanged;
        private readonly Subject<Unit> _onChanged = new();

        private readonly List<BlueprintMessagePack> _blueprints = new();

        public IReadOnlyList<BlueprintMessagePack> Blueprints => _blueprints;

        // 現行BPをGuid・名前で供給
        // Supplies current blueprints by GUID and name
        public IReadOnlyList<(Guid id, string name)> BlueprintEntries
        {
            get
            {
                var entries = new List<(Guid, string)>();
                foreach (var blueprint in _blueprints) entries.Add((blueprint.BlueprintGuid, blueprint.Name));
                return entries;
            }
        }

        public async UniTask Refresh(CancellationToken ct)
        {
            var response = await ClientContext.VanillaApi.Response.Block.SendBlueprintRequest(BlueprintRequest.CreateGetAllRequest(), ct);
            ApplyResponse(response);
        }

        public async UniTask<BlueprintCreateResult> CreateBlueprint(string name, Vector3Int min, Vector3Int max, CancellationToken ct)
        {
            var request = BlueprintRequest.CreateCreateRequest(name, min, max);
            var response = await ClientContext.VanillaApi.Response.Block.SendBlueprintRequest(request, ct);

            // タイムアウト等のnull応答は失敗扱い
            // Treat a null response (timeout etc.) as failure
            if (response == null) return BlueprintCreateResult.RequestFailed();

            ApplyResponse(response);
            return response.Success
                ? BlueprintCreateResult.Succeeded(Guid.Parse(response.RegisteredGuidStr))
                : BlueprintCreateResult.Rejected(response.FailureReason);
        }

        public bool TryGetBlueprint(Guid blueprintGuid, out BlueprintJsonObject blueprint)
        {
            foreach (var pack in _blueprints)
            {
                if (pack.BlueprintGuid != blueprintGuid) continue;
                blueprint = pack.ToJsonObject();
                return true;
            }

            Debug.Log($"[ClientBlueprintLibrary] blueprint {blueprintGuid} is not in the cache (deleted or not yet synced)");
            blueprint = null;
            return false;
        }

        public async UniTask<BlueprintDeleteResult> DeleteBlueprint(Guid blueprintGuid, CancellationToken ct)
        {
            var response = await ClientContext.VanillaApi.Response.Block.SendBlueprintRequest(BlueprintRequest.CreateDeleteRequest(blueprintGuid), ct);
            ApplyResponse(response);

            // nullは通信失敗。未知の拒否理由はCreateと同じくUnknownへ畳む
            // Null means request failure; unknown rejection reasons fold into Unknown just like Create
            if (response == null) return BlueprintDeleteResult.RequestFailed;
            if (response.Success) return BlueprintDeleteResult.Success;
            return response.FailureReason switch
            {
                BlueprintFailureReason.NotFound => BlueprintDeleteResult.NotFound,
                BlueprintFailureReason.NotUnlocked => BlueprintDeleteResult.NotUnlocked,
                _ => BlueprintDeleteResult.Unknown,
            };
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

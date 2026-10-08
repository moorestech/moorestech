using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Context;
using Cysharp.Threading.Tasks;
using Game.Blueprint;
using UniRx;
using UnityEngine;
using VContainer.Unity;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Thumbnail
{
    /// <summary>
    ///     ライブラリ更新時に未撮影BPを順に撮影する
    ///     Photographs uncached blueprints when the library changes
    /// </summary>
    public class BlueprintThumbnailRenderer : IInitializable
    {
        private readonly ClientBlueprintLibrary _library;
        private readonly BlueprintThumbnailContainer _container;
        private bool _isRendering;
        private bool _isRerunRequested;

        public BlueprintThumbnailRenderer(ClientBlueprintLibrary library, BlueprintThumbnailContainer container)
        {
            _library = library;
            _container = container;
        }

        public void Initialize()
        {
            _library.OnChanged.Subscribe(_ => Sync().Forget());
            Sync().Forget();
        }

        private async UniTaskVoid Sync()
        {
            // 撮影中のライブラリ更新は最後に一度まとめて反映する
            // Fold library updates during shooting into one final rerun
            if (_isRendering)
            {
                _isRerunRequested = true;
                return;
            }
            _isRendering = true;

            var plan = BlueprintThumbnailSyncPlanner.Plan(_library.Blueprints.Select(b => b.BlueprintGuid).ToList(), _container.Guids);
            foreach (var guid in plan.ToRemove) _container.Remove(guid);
            foreach (var guid in plan.ToRender)
            {
                if (!_library.TryGetBlueprint(guid, out var blueprint)) continue;
                var thumbnail = await Photograph(blueprint);
                if (thumbnail == null) continue;
                _container.Add(guid, thumbnail);
            }

            _isRendering = false;
            if (!_isRerunRequested) return;
            _isRerunRequested = false;
            Sync().Forget();
        }

        private static async UniTask<Texture2D> Photograph(BlueprintJsonObject blueprint)
        {
            // バッチ実行では描画器が無いためプレースホルダーを返す
            // Batch runs have no renderer, so use a placeholder
            if (Application.isBatchMode) return Texture2D.whiteTexture;

            var photographer = ClientContext.BlockIconImagePhotographer;
            if (!BlueprintThumbnailSubjectBuilder.TryBuild(blueprint, photographer.transform, out var subject)) return null;
            var textures = await photographer.TakeIconImages(new List<(GameObject prefab, string debugName)> { (subject, blueprint.Name) });
            UnityEngine.Object.Destroy(subject);
            return textures[0];
        }
    }
}

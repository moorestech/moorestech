using System;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Thumbnail
{
    /// <summary>
    ///     撮影済みBPだけをGuidで保持して変化を通知する
    ///     Holds only photographed blueprints by GUID and announces changes
    /// </summary>
    public class BlueprintThumbnailContainer : IBlueprintThumbnailLookup
    {
        private readonly Dictionary<Guid, Texture2D> _thumbnails = new();
        private readonly Subject<Guid> _onThumbnailChanged = new();

        public IObservable<Guid> OnThumbnailChanged => _onThumbnailChanged;
        public IReadOnlyCollection<Guid> Guids => _thumbnails.Keys;
        public bool Contains(Guid blueprintGuid) => _thumbnails.ContainsKey(blueprintGuid);
        public bool TryGet(Guid blueprintGuid, out Texture2D thumbnail) => _thumbnails.TryGetValue(blueprintGuid, out thumbnail);

        public void Add(Guid blueprintGuid, Texture2D thumbnail)
        {
            _thumbnails[blueprintGuid] = thumbnail;
            _onThumbnailChanged.OnNext(blueprintGuid);
        }

        public void Remove(Guid blueprintGuid)
        {
            if (!_thumbnails.Remove(blueprintGuid)) return;
            _onThumbnailChanged.OnNext(blueprintGuid);
        }
    }
}

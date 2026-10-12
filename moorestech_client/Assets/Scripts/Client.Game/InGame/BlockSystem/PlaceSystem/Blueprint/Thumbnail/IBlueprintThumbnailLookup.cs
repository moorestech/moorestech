using System;
using System.Collections.Generic;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Thumbnail
{
    /// <summary>
    ///     配信・DTO・トピックに公開するサムネイルの読み取り面
    ///     Read-only thumbnail access for delivery, DTOs and topics
    /// </summary>
    public interface IBlueprintThumbnailLookup
    {
        IObservable<Guid> OnThumbnailChanged { get; }
        IReadOnlyCollection<Guid> Guids { get; }
        bool Contains(Guid blueprintGuid);
        bool TryGet(Guid blueprintGuid, out Texture2D thumbnail);
    }
}

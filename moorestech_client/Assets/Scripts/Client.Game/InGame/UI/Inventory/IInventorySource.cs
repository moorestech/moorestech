using System;
using System.Collections.Generic;
using System.Threading;
using Core.Item.Interface;
using Cysharp.Threading.Tasks;
using Server.Util.MessagePack;

namespace Client.Game.InGame.UI.Inventory
{
    /// <summary>
    /// インベントリのタイプと識別子を提供し、どのUIビューを使用するかを決定するインターフェース
    /// Interface that provides inventory type and identifier, and determines which UI view to use
    /// </summary>
    public interface IInventorySource
    {
        /// <summary>
        /// インベントリタイプを取得
        /// Get inventory type
        /// </summary>
        InventoryType GetInventoryType();
        
        /// <summary>
        /// インベントリ識別子を取得（Block: Vector3Int, Train: Guid）
        /// Get inventory identifier (Block: Vector3Int, Train: Guid)
        /// </summary>
        InventoryIdentifierMessagePack GetIdentifier();
        
        /// <summary>
        /// 使用するビューのタイプを取得
        /// Get view type to use
        /// </summary>
        Type GetViewType();
        
        /// <summary>
        /// Addressableパスを取得
        /// Get Addressable path
        /// </summary>
        string GetAddressablePath();
        
        /// <summary>
        /// インベントリデータ取得用の非同期処理を実行
        /// Execute async processing to get inventory data
        /// </summary>
        UniTask<List<IItemStack>> FetchInventoryData(CancellationToken ct);
    }
}


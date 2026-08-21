using System;
using System.Collections.Generic;

namespace Game.Hotbar
{
    // 割当の読み取り口。配信・初期データ同梱など変更を伴わない利用者はこちらへ依存する
    // The read side of the hotbar; publishers and initial-data bundlers depend on this instead of the concrete store
    public interface IHotbarAssignmentLookup
    {
        // 変更されたプレイヤーIDを流す
        // Emits the player id whose assignments changed
        IObservable<int> OnAssignmentChanged { get; }

        IReadOnlyList<Guid> GetAssignments(int playerId);
    }
}

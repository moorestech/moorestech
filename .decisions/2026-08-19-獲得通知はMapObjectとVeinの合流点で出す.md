決定: アイテム獲得通知は `MiningProtocol.GetResponse` の `InsertItem` 直前の合流点1箇所で出し、MapObject採掘とVein手掘りの両方を対象にする。
棄却案: MapObject採掘のみを対象にする案／プレイヤーがアイテムを取得する全経路（インベントリ移動・クラフト等）を対象にする案。
理由: 前者は同じ「手掘りで取る」体験が経路で割れる。合流点が1箇所なので両方を賄うコストは同じ。後者は経路ごとに獲得の意味が違い（移動は獲得ではない）、別設計が必要になる。
リンク: MiningProtocol.cs:54 / docs/adr/0019-item-earned-notification.md

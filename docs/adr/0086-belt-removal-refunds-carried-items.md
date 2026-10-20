# 0086: ベルト撤去時に搬送アイテムを返却する

## Status
Accepted

## Context
ユーザー原文「歯車ベルコン消したらその上に乗ってるアイテムって消失しませんか」「それ直してほしい。サクッと直せる？」。
原因: 実測（証拠: `/tmp/belt-refund-repro-result.json`、Unity EditModeの実サーバーDIを初期化し、歯車ベルトの実segmentへ1個をRestoreItemsで注入してRemoveBlockProtocolを呼び、実tickを進めた結果 `removalSuccess=True; carriedBefore=1; refunded=0; segmentsAfter=0`）。UI操作ではなく状態注入＋本番プロトコル／tick経路による再現。撤去時の返却問い合わせにベルトの搬送内容が含まれず、再構築で所有マスが失われる。

## Decision
- 手動撤去で載っているアイテムを手持ちへ返し、全量が入らなければ撤去しない。出所: ユーザー裁定（質問「載っているアイテムも手持ちに返す。全部入りきらなければ撤去しない」に対する原文「進めて」）。
- 歯車・通常ベルト双方で同じ返却契約を使う。出所: agent前提（両template共通のBeltConveyorInventoryComponent、既存IGetRefundItemsInfo撤去契約）。
- 所有権は既存BeltTransportSnapshotと同じ: 走行中は先頭のあるマス、bufferは末尾マス、内部segmentは合流マス。返却照会は非破壊とし、撤去拒否時は搬送状態を変更しない。出所: agent前提（BeltTransportSnapshot、BeltTransportSaveStateBuilderの既存所有規則）。
- 手動撤去の修正に限定し、過負荷破壊・再構築の他の消失規則・セーブ形式・新規通信を変更しない。出所: agent前提（ユーザー依頼の「消したら」と既存RemoveBlockProtocol経路に限定）。

## Placement
BeltConveyorInventoryComponentがIGetRefundItemsInfoを実装し、IBeltTransportLookup経由で所有ブロックの返却アイテムを読む。運搬内容の列挙は所有者BeltTransportDatastoreに置く。Server.Protocolの既存の全量容量確認→削除→返却をそのまま使用する。前例: VanillaFilterSplitterComponent.GetRefundItems、RemoveBlockProtocol.GetRefundItems。

## Verification
通常・歯車ベルト、隣接マスの非返却、buffer／内部segment、満杯拒否と空きを作った後の再試行、連続撤去時の重複防止を検証する。コンパイル・ベルト／撤去関連テストおよびPlayMode統合検証を実行する。

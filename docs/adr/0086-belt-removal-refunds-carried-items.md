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

## 2026-10-20: ベルコン以外の搬出元の撤去

- チェスト・機械・駅などの搬出元を手動撤去するとき、接続情報から消失する内部segmentを特定し、中身を返却する。2入力→1入力では残る側も含め、3入力→2入力では撤去側だけを返す。複数出力も同じ規則で扱う。
- `IBeltTransportLookup` が現在の接続と搬送snapshotから追加返却物を読み取り、`RemoveBlockProtocol` の既存の全量容量確認へ合算する。入りきらなければ撤去せず、搬送状態も変えない。
- 撤去イベントで消える内部segmentの合流ID・入力方向を記録し、以後の返却・保存・再構築snapshotから除外する。同tickの連続撤去や再設置で二重返却・復活を防ぐ。
- 搬送通知→tick末尾の撤去→境界ハッシュの間はlive Assemblyを保つ。次tick先頭の再構築で物理状態へ反映して記録をクリアする。セーブ形式と内部segmentの合流所有規則は維持する。
- 検証: 入力数の変化、複数出力、満杯拒否と再試行、同tickの連続撤去・再設置、保存と境界ハッシュ、通常・歯車ベルト撤去、PlayMode統合検証。

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

- 出所: ユーザー裁定 2026-10-20「機械ってのは広義であって、チェストや駅blockなどようはベルコン以外のなにか」「この部分の修正をおねがいしたい」。ベルコン以外の接続元の撤去で消える内部segmentの中身も、撤去者の手持ちへ返す。ブロック種類の列挙ではなく接続情報で対象を特定する。
- 原因: 推測（根拠: `BeltTransportDatastore.GetRefundItems` は内部segmentを合流の所有物としてだけ列挙し、`BeltTransportRestorer.RestoreInternalItems` は同じ合流・入力方向が残る場合だけ復元する）。前ターンで実行再現未確認を明示した後、ユーザーが上記の修正を依頼した。UnityはLibrary再生成中であり、コード調査を先行し再現・検証を後段で実行する。
- 出所: agent前提（既存 `RemoveBlockProtocol` の全量容量確認と本ADRの返却契約）。通常インベントリ・建設費・レール返却と内部segment分を合算し、全部入らなければ撤去を拒否する。拒否は照会だけで搬送状態を変えない。
- 出所: agent前提（`BeltSegmentLayoutBuilder` の合流条件）。2入力から1入力になって合流が解消される場合は、残る入力側を含む消失する内部segmentを返す。3入力から2入力の場合は存続する方向を返さない。複数の合流へ接続する搬出元も同じ規則で扱う。
- 出所: agent前提（`WorldBlockDatastore.RemoveBlock` の同期イベント順序と既存Datastore購読）。返却算出は搬送側に置き、プロトコルには返却物の合算だけを追加する。成功した撤去の既存イベントで、消える内部segmentの実状態とpendingロード内容を消費し、保存cacheを無効化する。同tickの合流撤去で二重返却しない。
- 出所: agent前提（既存ロード契約）。pendingロード内容は同じ所有blockの実segmentより優先し、既存converterで検証する。セーブの形式と所有規則は変更しない。
- 呼び出し側の線引き: `Server.Protocol` は `IBeltTransportLookup` から追加返却物を受け取り、既存の全量容量確認に含める。合流・方向・入力数の判定やsegment操作は行わない。
- 出所: ユーザー裁定 2026-10-20「ok」（欠けている個人スキル `codex-audit` を `gpt-6.1-sol / medium` の独立サブエージェントで代替する質問への回答）。独立質問チェックの残問は0件。

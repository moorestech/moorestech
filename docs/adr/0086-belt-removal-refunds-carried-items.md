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
- 原因: 実測（証拠: `C:/Users/5080/AppData/Local/Temp/moorestech-internal-refund-20261020/repro-before.json`、再現コード: 同ディレクトリの `repro.txt`）。旧コードの実サーバーDIで、`MachinePortTestTemplate` の接続を使うチェストから合流へ入る内部segmentへ `RestoreItems` で1個を注入し、本番撤去プロトコルと次tickを通した結果は `source=Chest; success=True; carriedBefore=1; refunded=0; carriedAfter=0; internalSegmentsAfter=0`。内部segmentは合流所有物としてしか返却照会されず、搬出元撤去後の再構築で消失する。
- 出所: agent前提（既存 `RemoveBlockProtocol` の全量容量確認と本ADRの返却契約）。通常インベントリ・建設費・レール返却と内部segment分を合算し、全部入らなければ撤去を拒否する。拒否は照会だけで搬送状態を変えない。
- 出所: agent前提（`BeltSegmentLayoutBuilder` の合流条件）。2入力から1入力になって合流が解消される場合は、残る入力側を含む消失する内部segmentを返す。3入力から2入力の場合は存続する方向を返さない。複数の合流へ接続する搬出元も同じ規則で扱う。
- 出所: agent前提（`WorldBlockDatastore.RemoveBlock` の同期イベント順序と既存Datastore購読）。返却算出は搬送側に置き、プロトコルには返却物の合算だけを追加する。成功した撤去の既存イベントで、消える内部segmentの合流ID・入力方向を記録し、以後の返却照会・保存・次回再構築snapshotから除外する。pendingロード内容は対象方向を除いた新しいオブジェクトへ置き換え、保存cacheを無効化する。同tickの合流撤去で二重返却しない。
- 出所: 既存同期契約（`MasterTickUpdater.Update`、`BeltTransportTickEventPacket`）。搬送通知→tick末尾の撤去→境界ハッシュの間はlive Assemblyを保つ。内部区間の物理的な消費は次tick先頭の再構築で行い、除外記録をクリアする。これにより搬送通知だけで進むクライアント複製と境界ハッシュが一致し、同tickの全量取得にも同じ物理状態が渡る。同期違反の実測: `C:/Users/5080/AppData/Local/Temp/moorestech-internal-refund-20261020/repro-boundary-hash-blocked.json` に `postTransportHash=4069276182; boundaryHashAfterRemoval=3653395762; hashesMatch=False`。この契約を搬送tick後の撤去・返却/保存除外・次tick再構築のNUnitで検証する。
- 出所: agent前提（既存ロード契約）。pendingロード内容は同じ所有blockの実segmentより優先し、既存converterで検証する。セーブの形式と所有規則は変更しない。
- 呼び出し側の線引き: `Server.Protocol` は `IBeltTransportLookup` から追加返却物を受け取り、既存の全量容量確認に含める。合流・方向・入力数の判定やsegment操作は行わない。
- 出所: ユーザー裁定 2026-10-20「ok」（欠けている個人スキル `codex-audit` を `gpt-6.1-sol / medium` の独立サブエージェントで代替する質問への回答）。独立質問チェックの残問は0件。

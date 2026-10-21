# 0087: 網の再構築をtick末尾へ移し、撤去で消える搬送アイテムを返却する

## Status
Accepted

## Context
ユーザー原文「ベルトの手動撤去で搬送中のアイテムを全量返し、チェスト・機械・駅などの搬出元を撤去するときも、消える合流内部segmentのアイテムを返却します。建設費・通常インベントリ等との合計を既存の容量確認へ渡し、手持ちに入らない場合は InventoryFull で拒否します。」「クライアント操作の適応は従来通りtickの最後、その後にsegment再構築やgear再構築をはさみます。今のtick先頭のを末尾にするということです。そのあとセーブ。セーブはtickの最後のまま。ロードしたあとはバリデーションとして例外的にsegment gearなどの再構築を行います。」

旧ブランチ `fix/belt-removal-refund` は再構築がtick先頭のまま返却を実装したため、撤去〜次tick先頭の間に境界ハッシュ・搬送通知・セーブが挟まり、消える内部segmentを「記録して除外する」状態を跨tickで持つ必要があった。

## Decision
- 電力・歯車・流体・ベルトの再構築（RebuildIfDirty）をtick先頭から、tick末尾の操作適用（WorldMutationTickEndUpdater・ServerThreadActionQueue）の直後・境界ハッシュとセーブの前へ移す。1tickに1回だけ。出所: ユーザー裁定 2026-10-21 原文「今のtick先頭のを末尾にするということです。そのあとセーブ。」
- ロード完了時に同じ再構築を1回行う。ベルトのロード内容の預かり（次tick先頭まで保持）は廃止。出所: ユーザー裁定 2026-10-21 原文「ロードしたあとはバリデーションとして例外的にsegment gearなどの再構築を行います。」
- masterから新規ブランチで実装し直し、旧ブランチの返却計算・容量合算・テストは移植する。出所: ユーザー裁定 2026-10-21「okその案で進めたい」（新規ブランチ案への回答）
- 撤去による返却は、返却確定時にその場で旧い組から取り除く（形は再構築まで変えない）。除外記録を持たない。同tickの連続撤去・再設置で二重返却や復元による複製が起きない。出所: ユーザー裁定 2026-10-21 原文「古いアイテムがそこに戻るのがまず理解できない、その必要はないのでは？」→ 撤去時に組から取り除く案を承認「はい実装して」
- テスト用の `GameUpdater.UpdateOneTick`/`RunFrames` は各tickの前に dirty なら再構築する。tick外で設置するテストを、本番の「末尾の適用→再構築→次tick」と同じ前提で動かす。出所: ユーザー裁定 2026-10-21 原文「UpdateOneTick の定義をどうにかして変えられない？dirtyフラグつけてonなら再構築する　みたいな」
- 手動撤去でベルト所有のアイテム（走行中・buffer・内部segment）と、搬出元撤去で消える内部segmentのアイテムを返却物に含め、既存の全量容量確認で入らなければ InventoryFull で拒否し組を変えない。2入力→1入力は両側、3入力→2入力は撤去側だけ。出所: ユーザー裁定 2026-10-21（Context冒頭の原文）、分岐規則は ADR 0086(旧ブランチ) の agent前提を継承
- 再構築したtickは、再構築直後・境界ハッシュ前にベルト全量を連番付きで送る。搬送tickの報告は常に差分。出所: agent前提（クライアントは同tick内で hash より前の連番を順に適用する既存の仕組み。TrainUnitClientSimulator・TrainUnitHashVerifier）
- 機械からベルトへの押し込み受付区間は tick 先頭で開き、末尾の再構築では開かない。出所: agent前提（再構築〜境界ハッシュ間の押し込みは差分に載らず不一致になるため）
- 過負荷破壊は返却しない（従来どおり）。出所: agent前提（ADR 0086 の範囲を継承）

## Placement
- Core.Update: `IMasterTick`（Tick / RebuildTopologiesIfDirty）。GameUpdater はこれを保持する。
- Game.World.Interface: `IWorldTopologyRebuilder`。Server.Boot の `WorldTopologyRebuilder` が4網を順に再構築し、MasterTickUpdater と WorldSaveDataRestorer が使う。
- 返却: BeltConveyorInventoryComponent が IGetRefundItemsInfo を実装。RemoveBlockProtocol が搬出元撤去分を合算し、撤去成立後に組から取り除く。

## Verification
- 既存のサーバーテスト一式（tick順変更の回帰）。ベルト同期テストの連番前提を更新。
- 返却: 通常・歯車ベルト、buffer、内部segment、搬出元撤去（2→1・3→2・複数出力）、満杯拒否と再試行、同tickの連続撤去（順序両方）・撤去直後の再設置、ロード直後のセーブ一致。

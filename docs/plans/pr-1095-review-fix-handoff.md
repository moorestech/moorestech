# PR #1095 レビュー修正 申し送り（2026-08-02）

## 状況

- 対象: PR #1095「設置対象をGUID化し装備スロットとサーバー権威採掘を実装する」
- ブランチ: `feature/placement-guid-equipment-mining` / worktree `~/moorestech-worktrees/tree2` / HEAD `3a84588b6`（74ba6e8以降はmasterマージとスキル改修のみ・プロダクトコードの修正対象は不変）
- 人間レビュー 4829833297（18件・コメントanchor `74ba6e8`）＋較正済み独立レビュー再実走の統合結果に基づく**コード修正が未着手**。本書はその実施指示書
- 参照物:
  - ダイジェストHTML（全指摘のコード抜粋・直し方つき）: `/tmp/pr-review-1095/index.html`
  - レビュー時diff: `/tmp/replay-1095-74ba6.diff` / 人間コメント実文: `/tmp/human-1095-comments.json`
  - `/tmp` が消えていたらHTMLの生成元 `<scratchpad>/gen_digest.py` は失われている前提で、本書の記載だけで進めてよい

## ユーザー裁定（2026-08-02・確定済み・`.decisions/` に記録あり）

1. **D1 表示名の正 = マスタ名**（`trainCar.Name`）。`TrainCarPlacementTarget.DisplayName` 1箇所に閉じる
2. **D2 PlacementTargetCatalogはClient.Gameへ移設**。`IBlueprintCatalogSource` と `Game.PlacementTarget` アセンブリを廃止（Guid統一の語彙自体は承認）
3. **D3 IDisposableは削除**。一般原則: このゲームは「メインメニューに戻る」を想定せず、ゲーム寿命のオブジェクトはゲーム終了と同時破棄。IDisposableがあると「どこかで破棄される前提」という誤解を与えるので付けない
4. **D4 AGENTS.mdの `{ get; private set; }` 許容追記は承認済み**（文言は簡潔化済み・追加作業なし）
5. **新形N1〜N3すべて承認**（Game.PlacementTarget語彙・装備インベントリ語彙+スキーマ・Game.Map依存2本）

## 修正タスク（推奨実施順）

### 前提整備
1. **GameUpdaterに累積ティック公開を追加**（`Core.Update/GameUpdater.cs`）。現在 `TicksPerSecond`/`SecondsToTicks`/`TicksToSeconds` のみ。人間指示: 「簡易的なインクリメントだけする現在ティック数が分かるプロパティを1個作って公開」。→ 2の前提

### MapObjectMiningService / MapObjectAcquisitionProtocol
2. **C2**: `MapObjectMiningService.cs:85,88` — `Stopwatch.GetTimestamp()` のクールダウンをGameUpdater累積ティックへ。閾値は `SecondsToTicks(AttackSpeed * CooldownMarginRate)`
3. **C8**: `MapObjectAcquisitionProtocol.cs:50-53` — production本体の `MapObjectSuperMine` デバッグ分岐を除去し破壊経路を `TryAttack` 1本へ統一。バイパスはサービス側へ押し込み、`ForceDestroy` のprotocol層からの直接public呼びをやめる（人間#14/#17）
4. **人間#15（if集約）**: `MapObjectMiningService.cs:86` 周辺 — `if (分岐) return` 群をメソッド直下の一箇所に集約する書式へ

### EquipmentHeldItemModel（同一ファイル3件まとめて）
5. **C3**: `:88` — `AddressableLoader.LoadAsync<GameObject>(path, token)` へtoken貫通（80行目で取得済みのtokenを渡すだけ。渡さないと装備連打で旧ロードがフィールドを乗っ取り恒久リーク）
6. **C1**: `:99` — try-catch除去（「Addressableは外部境界」は許可3種外）。失敗はロード結果のnull判定で受ける
7. **D3適用**: `:17` — `IDisposable` 実装と `Dispose()` を削除、`MainGameStarter.cs:172` 側の破棄配線も整理。**注意**: 装備切替時のCTS Cancel/Dispose（`:55-58`）は「切替処理」でありライフサイクル破棄ではないので残す

### 受益者なき抽象の削除
8. **C4+D2**: `IBlueprintCatalogSource.cs` 削除・`PlacementTargetCatalog` ほか `Game.PlacementTarget` 一式を `Client.Game` 配下へ移設・アセンブリ廃止。`ClientBlueprintLibrary` は具体型で受ける。`MainGameStarter.cs:206` の `.As<IBlueprintCatalogSource>()` 削除。サーバ側UnitTest2本（`PlacementTargetCatalogTest`/`PlacementTargetCatalogUnlockTest`）はクライアントTestsへ移動
9. **C5**: `BuildToolPlacementTarget` 削除・`PlacementTargetKind.BuildTool` 削除・`PlaceSystemSelector.cs:76-86` のBuildTool分岐（到達不能`_`含む=C11も同時解消）を畳み、blueprintCopyはBlueprint側で直接扱う。`BlueprintCopySystem` の型引数をBlueprint側へ
10. **C6**: `EquipmentProtocol.cs` — 1値enum `EquipmentOperation` とswitchを削除し、プロトコル名を実処理どおり `SetSelectedEquipmentIndexProtocol` へリネーム（人間#16/#18）
11. **C7**: `ToolMaster.cs` 削除（items.jsonをItemMasterと二重ロードしているだけ）。`MasterHolder` 登録を外し、参照3件（`EquipmentInventoryData.cs:39`・`InventoryAreaMapper.cs:31`・`LocalPlayerEquipment.cs:44`）を `MasterHolder.ItemMaster.Items.EquipmentSlotCount` へ

### 表示名の一本化（C12+D1・最大の構造修正）
12. `IPlacementTarget` に `string DisplayName { get; }` を宣言し各具体型で計算プロパティ実装（車両は `trainCar.Name` を正とする=D1）。消費側の種別switch・ダウンキャストを全廃:
    - `PlacementModeTopic.GetSelectedName`/`GetTrainCarMasterName` → `target.DisplayName` 1行
    - `WebBuildMenuEntryCatalog.CreateEntry`/`CreateCostlessEntry`/`ToRequiredItems`/`CreateEntries` → クラスごと削除し `BuildMenuEntryDtoFactory` へ吸収
    - `WebBuildMenuEntry` 構造体削除（DTOへ素通しなだけ）
    - uGUI側 `BuildMenuEntryCatalog.CreateEntry` も削除（人間#4の芯）
    - `BuildMenuEntryDtoFactory` のKind switch（`GetKind`/`CreateIconUrl` のダウンキャスト）→ `target.Id`/`entry.Kind` 直読み
    - `BuildMenuActions` はWebカタログ全再構築をやめ `UnlockedEntries`+`PlacementTargetFactory.Create` 直呼び
    - Blockの `Guid→BlockId→Guid` 往復も解消（`BlockPlacementTarget` がGuid保持）
    - `TryGetTrainCarMaster` の戻り値捨て3箇所はこの修正で消える

### LocalPlayerEquipment
13. **C13**: `:21,37` — `SelectionConfirmationRevision` を `{ get; private set; }` へ畳む（D4承認済み）
14. **人間#11（Initialize規約）**: `:88` — 初期化メソッドは `Initialize` 固定・ctor→Initializeの記述順へ
15. **人間#9（OnChanged命名）**: `:33` — 何が変わったか分かる名前へ（スロット更新・選択変更・初期適用の3種が混流。名前を直すついでにイベントを種別で割るか判断）

### 残骸
16. **C14**: `CharacterTestDebug.cs:5` 未使用using削除 / `MapObjectMiningFocusState.cs:85` `ShowRecommendMiningTools` を `#region Internal` ローカル関数化 / `BlueprintDatastore.TryGet`（`:29`・呼び出しゼロ）をinterface宣言ごと削除
17. **C9**: `NetworkEventInventoryUpdater.cs:55,66` — テスト用publicハンドラ2本をprivateへ戻し、テストは実イベント経路（キャプチャ機構）へ
18. **人間#12（Responses.cs）**: 旧4引数コンストラクタ削除（production参照0・テスト1+デバッグ1のみ）。呼び出し側はMessagePack版コンストラクタか本来経路へ

### 対応不要
- `MiningAttackResult` の失敗4値（suppressed・ADR 0004「却下理由は返さない」で免責）
- `MapObjectMiningMiningState.cs:61` の `masterElement == null ||` は**削除する**（C10・仕様上不能。`MiningType != Mining` だけ残す）— これは対応要。対応不要なのは上のsuppressedのみ

## 検証・完了条件

- `.cs` 変更ごとに `uloop compile --project-path ./moorestech_client`
- テスト: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlacementTarget|Equipment|MapObject|Blueprint|BuildMenu"`（サーバテストも同コマンドで走る）
- 仕上げに moores-code-review を1パス（スキルは本worktree `.claude/skills/` が正典）
- コミットして終える。**同worktreeに未コミットのハーネス変更（`.claude/`・`.agents/`・`.codex/`・`AGENTS.md`・records）が居るので、プロダクトコード修正とは分けてコミットすること**

## 注意（このリポジトリの規約の要点）

- `.meta` 手動作成禁止 / Unity YAML（prefab・シーン等）のテキスト直接編集禁止（`uloop execute-dynamic-code` 経由は可）
- 1ファイル200行以下・1ディレクトリ10ファイル以下・`partial`/`Func<>` 絶対禁止
- 新設の規約（今回のレビュー由来・AGENTS.md記載済み）: 初期化は `Initialize` 固定 / サーバの時間計測はGameUpdaterティックのみ / デバッグ専用APIをプロダクションに残さない / `{ get; private set; }` 許容

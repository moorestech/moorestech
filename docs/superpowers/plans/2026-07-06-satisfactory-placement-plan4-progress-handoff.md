# プラン4実行 申し送り（Task 5完了・belt-place待ち一時停止時点）

日付: 2026-07-06 14:20
状態: **プラン4のTask 1〜5完了（全レビューApproved）。Task 6以降はbelt-placeセッションとの競合によりユーザー決定で一時停止中**
実行方式: superpowers:subagent-driven-development（implementer=**opus指定**（ユーザー指示）、task reviewer=sonnet、タスクごとにフレッシュsubagent＋レビューゲート）

## 0. 最重要サマリ（これだけ読めば再開できる）

1. 作業場所は **worktree `/Users/katsumi/moorestech-worktrees/plan4`**（ブランチ `feature/replace-place-system-plan4`、HEAD `bbc8f3468`）。メインチェックアウト`/Users/katsumi/moorestech`は他セッションと共用のため**使わない**
2. 再開条件: **belt-placeセッション（`feature/belt-conveyor-place-system`）が`feature/replace-place-system`へマージされたら**、plan4ブランチをrebase（またはmerge）してTask 6から再開（§5に競合解消の詳細）
3. プラン本文: `docs/superpowers/plans/2026-07-05-satisfactory-placement-plan4-special-systems.md`（Task 6は§5の再設計事項を反映してから実行）。プラン5も作成済み: `...plan5-destructive-cleanup.md`
4. 進行台帳: worktreeの`.superpowers/sdd/progress.md`（**gitignored** — 消えていたら本書§2と`git log`が正）
5. ユーザー判断待ち: Task 5レビューのImportant指摘（§6-1）

## 1. ブランチ・環境の全体像

```
/Users/katsumi/moorestech                     … メインチェックアウト。feature/replace-place-system @ f226731c3
                                                （他セッション共用。00:06にPlaceBlockProtocol.csの作業ツリー巻き戻り事故あり→worktree移行の契機。
                                                  作業ツリーにMの残骸が残っている可能性あり。触らない）
/Users/katsumi/moorestech-worktrees/plan4     … 本作業worktree。feature/replace-place-system-plan4 @ bbc8f3468（Task1-5）
/Users/katsumi/moorestech-worktrees/belt-place… 並行セッション。feature/belt-conveyor-place-system（アクティブ）
/Users/katsumi/moorestech_master              … 本番マスタ。branch plan2-master-migration @ f67eee8（belt側がplaceParam追加コミット済み）
```

worktree環境の構築済み事項（再構築不要）:
- Unity Library: client/serverともメインから`cp -Rc`（APFS clone）済み。**worktree専用Unityを`uloop launch /Users/katsumi/moorestech-worktrees/plan4/moorestech_client`で起動して使う**（uloopはproject-pathで対象Unityを解決。Unityが落ちていたら再launchして数分待つ）
- 私有アセット: `moorestech_client/Assets/PersonalAssets/moorestech-client-private` → メインへのシンボリックリンク
- `../moorestech_master`解決: `/Users/katsumi/moorestech-worktrees/moorestech_master`シンボリックリンク（既存）
- `.moorestech-external-revisions.json`がUnity起動でピンf67eee88へ書き換わり**未ステージのまま**（意図的。コミットに混ぜない。Task 6/12でmoorestech_masterへコミットする際に整合させる）

コマンド規約: コンパイル`uloop compile --project-path ./moorestech_client`／テスト`uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<regex>"`（worktree相対）。「Domain Reload in progress」は45秒待ちリトライ。

## 2. 完了タスクの成果物（後続タスクが依存する正確なAPI）

### Task 1（f226731c3）: ConstructionCostServiceのItemCount正準化
`Server.Protocol.PacketResponse.Util.Construction.ConstructionCostService`:
- `static (ItemId itemId, int count)[] ToItemCounts(ConstructionRequiredItemElement[])` / `ToItemCounts(TrainCarRequiredItemElement[])`（null/空→空配列）
- `static bool HasRequiredItems(IReadOnlyList<(ItemId itemId, int count)>, IReadOnlyList<IItemStack>)`
- `static void ConsumeRequiredItems(IReadOnlyList<(ItemId itemId, int count)>, IOpenableInventory)`（実体は`ElectricWireSystemUtil.ConsumeItem`）
- `static List<IItemStack> CreateRefundItems(IReadOnlyList<(ItemId itemId, int count)>)`
- 変換結果は`ElectricWireAutoConnectService.EvaluateAutoConnect`の`reservedItems`引数（同型）へそのまま渡せる。PlaceBlockProtocolのインライン`.Select`変換は削除済み
- ForUnitTest train.json 1両目へ `requiredItems: [Test3(…1234-…0003) x3, Test4(…0004) x2]` + `initialUnlocked: true` 投入済み（2両目は未解放拒否テスト用に無変更）

### Task 2（f524e2ae1）: 列車車両2プロトコルGuid化＋RemoveTrainCar全額返却
- `PlaceTrainOnRailRequestMessagePack(RailPositionSnapshotMessagePack, Guid trainCarGuid, int playerId)`（`[Key(3)] Guid TrainCarGuid`。HotBarSlot/InventorySlot削除）
- `AttachTrainCarToUnitRequestMessagePack`は`[Key(4)] Guid TrainCarGuid`
- 検証順: `TryGetTrainCarMaster(Guid)`失敗=ItemNotFound → `TrainCarUnlockStateInfos[guid].IsUnlocked`否=NotUnlocked → `HasRequiredItems`否=InsufficientItems → 生成/連結成功後に`ConsumeRequiredItems`
- enum末尾追加: `PlaceTrainCarFailureType.NotUnlocked=5, InsufficientItems=6` / `AttachTrainCarFailureType.NotUnlocked=6, InsufficientItems=7`
- `RemoveTrainCarProtocol.BuildRefundItems`: requiredItems全額返却＋コスト未定義マスタは車両アイテム1個フォールバック（「プラン5で削除予定」コメント付き）
- クライアント: `VanillaApiWithResponse.PlaceTrainOnRail/AttachTrainCarToUnit`がGuid引数化。`VanillaApiSendOnly.PlaceTrainOnRail`は未使用のため削除。`TrainCarPlaceSystem`は暫定で`TryGetTrainCarMaster(context.HoldingItemId)`→`TrainCarGuid`解決（Task 9で選択駆動化）
- EditModeInPlayingTestModのtrain.jsonは`trainCars:[]`のため変更なし（正当逸脱）

### Task 3（ecf18c401）: 電柱延長BlockId化
- `ElectricWireExtendRequest`: `[Key(6)] int PoleBlockIdInt`＋`[IgnoreMember] BlockId PoleBlockId`。`CreateExtendRequest/CreateIsolatedPlaceRequest`の引数`BlockId poleBlockId`化
- `ElectricWireExtendService.Execute(bool, Vector3Int, PlaceInfoMessagePack, int, BlockId poleBlockId, ItemId wireItemId)`。冒頭検証順: 占有→アンロック（基底BlockGuid）→縦オーバーライド＋ElectricPoleBlockParam→コスト検証
- 起点あり: 電線所持判定=`totalWire+reservedWire`（コスト中の同一アイテム合算）。消費=`ConsumeRequiredItems`+`ConsumeItem(wire, consumedWire)`
- 起点なし: `EvaluateAutoConnect`の予約に`costItemCounts`（申し送りの必須事項、対応済み）
- `ElectricWirePlacementFailureReason`末尾に`NotUnlocked, InsufficientItems`追加
- テストマスタ: 電柱コストは**既存のTest5 x1へ整合**（briefのTest3 x2から正当逸脱。PlaceBlockProtocolTestが既にTest5 x1を検証していたため）。`TestLockedElectricPole`（guid末尾…101）＋`ForUnitTestModBlockId.LockedElectricPoleId`追加

### Task 4（85d8f2f64）: 歯車チェーンポール延長BlockId化
- `GearChainPoleExtendRequest`: `[Key(6)] int PoleBlockIdInt`＋IgnoreMember変換。Create系はBlockId引数
- `GearChainPlacementEvaluator.EvaluatePlacement`最終引数を`IReadOnlyList<(ItemId itemId, int count)> reservedItemCounts`へ一般化。定数`NotUnlockedError="NotUnlocked"` / `InsufficientItemsError="InsufficientItems"`追加
- プロトコル検証順はTask 3と同型（`IGameUnlockStateDataController`コンストラクタDI）。ポール消費=`ConsumeRequiredItems`
- クライアント: `GearChainPoleExtendSendCommand.PoleSlot`→`PoleBlockId`、`GearChainPoleFrameInputCollector`が`GetBlockId(poleBlockMaster.BlockGuid)`で暫定解決、`GearChainPoleExtendPreviewCalculator.CalculateExtend`もreservedItemCounts化（シグネチャ変更の正当波及）
- テストマスタ: GearChainPoleへ`Test3 x2`+`initialUnlocked`投入、`TestLockedGearChainPole`（…102）追加

### Task 5（bbc8f3468）: 橋脚付きレール接続BlockId化・事前検証化
- `RailConnectWithPlacePierRequest`: `[Key(6)] int PierBlockIdInt`＋IgnoreMember変換。`Create(..., BlockId pierBlockId, ...)`
- 処理順: アンロック→橋脚コスト事前検証→`TrainRailBlockParam`ガード（非TrainRail BlockIdのNRE防止、自己レビューで追加）→設置（TryAddBlock戻り値チェック追加）→レールコスト解決→**不足なら`RemoveBlock`ロールバック＋失敗応答**（旧実装のブロック残置バグ解消）→接続→消費（橋脚=`ConsumeRequiredItems`、レール=`ElectricWireSystemUtil.ConsumeItem`でコピペ走査ループ解消）
- クライアント: `PlaceRailWithPier`（WithResponse/SendOnly両方）BlockId引数化。`TrainRailConnectSystem`は暫定でインベントリ検索由来のpierBlockIdを送る
- テスト新設: `RailConnectWithPlacePierProtocolTest.cs` 4件（成功消費／橋脚不足／レール不足ロールバック／未解放拒否）
- テストマスタ: TestTrainRailへ`Test3 x2`+`initialUnlocked`、`TestLockedTrainRail`（…103）追加

### テスト実績
各タスクでTDD（RED→GREEN）実施、回帰込み全PASS。最後の広域回帰: `RailConnect|GearChainPoleExtend|ElectricWireExtend|PlaceTrainCarOnRail` 25/25 ほか。

## 3. sdd実行の運用資産（worktree内）

- ブリーフ生成: `/Users/katsumi/moorestech/.claude/skills/subagent-driven-development/scripts/task-brief <plan.md> <N>` → `.superpowers/sdd/task-N-brief.md`
- レビューパッケージ: 同`scripts/review-package <BASE> <HEAD>` → `.superpowers/sdd/review-BASE..HEAD.diff`（BASEは**タスク開始前に記録したコミット**。HEAD~1禁止）
- レポート: implementerが`.superpowers/sdd/task-N-report.md`へ全文、返信は15行以内のステータスのみ
- 台帳: `.superpowers/sdd/progress.md`に1タスク1行＋Minor所見を追記（最終レビューでトリアージするため）
- レビューアはbrief+report+diffファイルの3点セットで起動し、Spec ComplianceとTask Qualityの2判定を返す。Critical/Importantはfixer→再レビュー。plan-mandated指摘はユーザー判断へ

## 4. 一時停止の理由（belt-place競合、2026-07-06 10:47観測時点）

belt-placeセッション（`feature/belt-conveyor-place-system`、**観測時点でアクティブ・未コミット変更あり**）:
- コミット済み: `c120bf7b2` va:placeBlockを**セル毎BlockId方式**に変更＋**ファミリーunlock判定**導入 / `efa756439` PlaceBlockProtocolTestを**3ファイルに分割** / `4b125160b` BeltConveyorファミリー解決ユーティリティ / `1b4cc38a5` moorestech_masterピンをf67eee88へ / `b16d873ca` ベルト経路デコンポーザ
- 未コミット（観測時点）: `PlaceSystemSelector.cs` / `ConstructionCostPreviewCalculator.cs` / `BuildMenuView.cs` / `MainGameStarter.cs` / 新規`BeltConveyor/*`一式
- スキーマ: `VanillaSchema/placeSystem.yml`へ**+47行（placeParam等）**。moorestech_master `f67eee8`で本番placeSystem.json全4エントリに`"placeParam": {}`追加済み

プラン4のTask 6〜10はまさに同じファイル群（placeSystem.yml/json・PlaceSystemSelector・BuildMenuView・MainGameStarter・PlaceBlockProtocol系）を全面改修するため、ユーザー決定で「**ベルト完了待ち→rebase→Task 6再開**」を選択。

## 5. 再開手順（belt側がfeature/replace-place-systemへマージされた後）

1. `pwd`確認 → worktreeへ。`git -C /Users/katsumi/moorestech fetch`不要（ローカル共有）。plan4ブランチを更新:
   `git rebase feature/replace-place-system`（競合が酷ければ`git merge feature/replace-place-system`でも可 — 履歴の綺麗さよりも確実さ優先）
2. **予想される競合と解消方針**:
   - `PlaceBlockProtocol.cs`: Task 1の`costItemCounts`化 vs belt側セル毎BlockId+ファミリーunlock。**belt側の新構造を土台に、コスト計算をToItemCounts正準形で組み込む**（セル毎にBlockIdが変わるならセル毎に`ToItemCounts`。EvaluateAutoConnectへの予約渡しを絶対に落とさない）
   - `PlaceBlockProtocolTest`: belt側で3分割済み。Task 1で入れたテスト修正を分割後ファイルへ再適用
   - `ConstructionCostServiceTest` / ForUnitTestマスタJSON（blocks.json/train.json/items.json）: 双方の追加ブロック・アイテムを両立させる（guid衝突に注意: 本セッションはguid末尾…101/102/103を使用）
   - `VanillaApiWithResponse/SendOnly`: 双方の変更をマージ
3. rebase後に全回帰: `uloop compile` → `Client.Tests` / `Tests.CombinedTest` / `Tests.UnitTest`（分割実行）
4. **Task 6を再設計してから実行**（プラン本文のTask 6を以下で上書き解釈）:
   - belt側がマージした`placeSystem.yml`の現物を読む（placeParam構造、placeModeのenum optionsにBeltConveyor等が追加されているか）
   - `usePlaceItems`/`priority`の削除と`name`/`iconItemGuid`/`placeBlockGuid`(optional)/`sortPriority`の追加は維持しつつ、**belt側の`placeParam`（switch/cases構造の可能性）を温存**
   - 本番/ForUnitTestのplaceSystem.jsonは「接続ツール3エントリ＋belt側が必要とするエントリ」で再構成（belt側エントリの扱いはbelt実装の選択経路を読んで判断。TrainRail/TrainCarエントリの削除は維持 — 役割はブロック/車両エントリへ移行済み）
   - `PlaceSystemMasterUtil`のバリデーション更新（iconItemGuid/placeBlockGuidのforeignKey検証）
   - `PlaceSystemSelector`からUsePlaceItemsマッチングを削除する際、**belt側がSelectorに追加した分岐を壊さない**
5. Task 7〜13はプラン本文どおり。ただし:
   - Task 8のSelector新分岐に**BeltConveyor経路を統合**（belt側の選択方法を現物確認）
   - Task 12（本番マスタ追補スクリプト）実行時はmoorestech_masterの最新状態（f67eee8以降）を前提に、mooreseditor停止確認・ブランチ確認（plan2-master-migration）・ピン更新まで実施
   - Task 13の実機検証はworktree Unityで実施（moorestech_masterシンボリックリンク解決済み）
6. プラン4完了後: `feature/replace-place-system-plan4`を`feature/replace-place-system`へマージ（メインチェックアウトのブランチ操作は他セッションと調整すること）。worktreeの後片付けはremove-git-worktreeスキル

## 6. ユーザー判断待ち・未解決事項

1. **Task 5レビューのImportant（plan-mandated・既存挙動）**: `RailConnectWithPlacePierProtocol`で`TryConnect`失敗時に`Success=true`＋橋脚コスト消費＋孤立橋脚残置（クライアントは返ってきたToNodeIdの解決を試みる）。GearChain前例（RemoveBlockロールバック＋失敗応答）に合わせる修正は小規模。**最終レビュー時にユーザーへ提示して指示を仰ぐ**（プラン本文が既存挙動維持を明記しているため勝手に直さない）
2. Minorトリアージ（最終whole-branchレビューへ渡すリスト）:
   - `PlaceTrainCarOnRailProtocol.cs`(286行)/`AttachTrainCarToUnitProtocol.cs`(342行)の200行超（既存違反、Task 2で微増）
   - `ElectricWirePlacementFailureReason.NoPoleItem`がService内で未使用化
   - `GearChainPlacementEvaluator.cs:54`の防御的nullチェック（規約上不要）
   - 車両にnameフィールドが無い（ビルドメニュー表示はアイテム名代用。プラン5でアイテム削除時に再燃）
3. メインチェックアウトの`PlaceBlockProtocol.cs`作業ツリー巻き戻り残骸（00:06事故。コミットは無事。メイン側で作業する際に`git status`確認）

## 7. 既知の罠（本実行で実際に踏んだもの）

- **テストマスタの休眠キー**: briefのコスト指定（Test3 x2）と既存キーが食い違うことがある。必ず対象ブロックの既存requiredItems/initialUnlockedをgrepしてから投入（Task 3で電柱は既存Test5 x1に整合させた）
- **サブエージェントのセッション上限死**: implementerがAPI上限で無通知終了することがある。worktreeの`git status`で変更有無を確認→変更なしなら同一agentIdへSendMessageで再開（コンテキスト温存。Task 5で実績）
- **worktreeのUnityとメインのUnityは別**: uloopはproject-pathの一致で対象を選ぶ。最初のuloopコマンドが接続エラーならworktree用Unityの起動を確認
- **`.moorestech-external-revisions.json`はUnityが書き換える**: コミットに混ぜない（意図的なピン更新時のみステージ）
- 新規サーバー.csがUnityに認識されない場合がある（Unity再起動が必要なケース）。Task 5では発生せず
- レビューパッケージのBASEはタスク開始前に記録したSHA。`HEAD~1`はマルチコミットタスクを黙って切り捨てる

## 8. プラン5（次フェーズ、着手前）

`docs/superpowers/plans/2026-07-05-satisfactory-placement-plan5-destructive-cleanup.md`作成済み（7タスク）。要点: 旧ホットバー設置プロトコル削除／itemGuidスキーマ削除／items.jsonから69アイテム削除／**木のシャフトは素材として存続（機械レシピ材料置換を回避する推奨案）**／アイコンはimagePath整備ではなく**スクショコンテナのキーをItemId→BlockId/車両Guidへ変更**（車両スクショ生成のみ新設）。プラン4完了後、belt側の変更も踏まえて着手前に整合を再確認すること。

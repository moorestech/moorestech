# PR #1127 独立レビュー裁定への対応

作業ブランチ: `feature/vein-hand-mining`（worktree: `~/hermes-agent/data/repos/moorestech-worktrees/vein-hand-mining`）
レビュー記録: `../moorestech_logs/harness/pr-independent-review/records/pr-1127.md`
ダイジェスト: `../moorestech_logs/harness/pr-independent-review/runs/pr-1127/digest.html`

## 状況

PR #1127 を独立レビューし verdict は **Critical差し戻し**（Critical 6件・設計判断11件）。
2026-08-14 にユーザー裁定が全件出揃った。裁定は `.decisions/2026-08-14-*.md`（8件）が正。
grill（moores-grill-with-docs）で「手掘り不可の提示」の具体形まで確定済み。**まだ1行も実装していない。**

**ブロッカー**: PR は現在 master と `CONFLICTING`（ブランチが71コミット遅れ）。rebase/merge の要否と順序はユーザー判断待ち。

## 確定した裁定

### 必読4件（Criticalの直し方）

1. **掘れない露頭のレイ遮蔽** — 遮蔽は仕様として維持（rayは吸う）。そのうえで「現時点では掘れない」ことを**プレイヤーに見せる**。
   ＝ 非minable露頭にも `OutcropRayTarget` を付け、照準時に専用ツールチップを出す。
   （棄却: レイヤーを付けない案／RaycastAllで貫通する案／コード上の表現だけの案）
2. **スキット抑止の固着** — 深さカウンタ方式（`BeginSkitSuppress`/`EndSkitSuppress`）へ変更し `WorldPinActivationSnapshot` を廃止。
3. **veinPin の毎フレームLogError** — チュートリアル適用を `InitialEventApplyWaiter.WaitAllAsync` 完了後へ移す。
4. **ドロップの maxStack 分割漏れ** — `VanillaStaticMapObject.GenerateEarnItems` と同じ分割ロジックを移植。

### grillで確定した「提示」の具体形

- 文言は**一律「手掘りできません」**。鉱脈種別ごとの理由（掘削機/ポンプが必要）の出し分けは**やらない** → GitHub issue へ後続タスクとして起票する。
- 「手掘り可否」は **`IMiningTargetObject` のメンバー**として持たせ、`MapObjectMiningFocusState` がそれを見てツールチップを出す。`IsAvailable` は「対象として生きているか」の意味へ戻す。
- 「手掘りできません」と既存の「必要アイテム: ○○」（道具不足）は**別のローカライズキー**にする。

### 残り7件（一括で推奨案採用）

1. DI注入メソッド名は AGENTS.md の `Initialize` を正とし、本PRの2件はそのまま
2. `VanillaApiSendOnly` を `AttackMapObject(int)` / `MineVein(Vector3Int)` の2メソッドへ戻す
3. ツール照合を `VeinHandMiningService` の `public static` へ集約しクライアントから委譲
4. 露頭ロード失敗は兄弟実装に揃え LogError+skip。`outcropAddressablePath` の非空検証を `MapVeinMasterUtil` へ追加
5. vein採掘リクエストに veinGuid を載せ、座標∧guid で判定
6. `FrameYieldObjectInterval` を 50 へ差し戻し、根拠コメント4件を復元
7. ugui廃止計画とレビュー機構変更を別PRへ分離。private revision バンプを PR 本文へ明記

## やること

### A. Critical の修正

- **A1** `IMiningTargetObject` に手掘り可否のメンバーを追加。`MapObjectGameObject` は常に可。`OutcropGameObject` は minable のときだけ可。
- **A2** `OutcropGameObjectDatastore.InstantiateOutcrop` — 非minable露頭にも `OutcropRayTarget` を付ける。現在は `Initialize` の中でのみ付与され、非minableは `Initialize` を呼ばずに return しているので、**RayTarget付与と可否設定は常に走る形へ分割**する。`_minableParam == null` の半初期化状態を残さないこと。
- **A3** `MapObjectMiningFocusState` — 手掘り不可なら専用ツールチップを出して `this` を維持する分岐を追加。新規ローカライズキーを `LocalizationKeys.Ui.Tooltip` 系へ追加（既存 `RequiredItems` / `HoldToGet` / `PickUpLeftClick` と同列）。
- **A4** `MapObjectMiningFocusState` — 削除されたマスタ欠損ガードの回復。`IsAvailable => !IsDestroyed && MapObjectMasterElement != null` に畳む（`MapObjectGameObject`）。
- **A5** スキット抑止をカウンタ化。`IMapObjectPin`/`IVeinPin` から `IsSkitSuppressed()` を落とし、`WorldPinActivationSnapshot.cs` を削除。`SkitManager` は直接 Begin/End を呼ぶ。テスト（`SkitFailureCleanupTest`・`VeinPinTutorialTest`）の追従が要る。
- **A6** チュートリアル適用順序。`ChallengeManager.Construct` での即時 `ApplyTutorial` をやめ、`MainGameInitializationFinalizer` の `WaitAllAsync` 後へ移す。
- **A7** `VeinHandMiningService.CreateEarnedItems` に maxStack 分割を実装。
- **A8** `tutorial-pebble-challenge.cs:122` — B2（API復元）を先にやれば**このファイルは1行も変えずに直る**。B2の後に注入が通ることを確認する。

### B. 残り7件の反映

- **B1** `[Inject]` メソッド名は変更しない（本PRの `Initialize` 2件が正）。既存 `Construct` 13件の改名は本PRではやらない。
- **B2** `VanillaApiSendOnly` — `SendMiningRequest(MessagePack)` を廃し `AttackMapObject(int instanceId)` / `MineVein(Vector3Int position)` の2メソッドへ。内部で `_playerId` を使い `CreateMapObjectRequest` / `CreateVeinRequest` を呼ぶ。呼び出し側 `MapObjectGameObject.SendAttack` / `OutcropGameObject.SendAttack` を各1行へ縮小し、`ClientContext.PlayerConnectionSetting` 参照を削除。
- **B3** `VeinHandMiningService.TryResolveUsableTool` を `public static` へ引き上げ、`EmptyItemId` ガードを関数内へ集約。`OutcropGameObject` から委譲。
- **B4** `OutcropGameObjectDatastore` の prefab ロード失敗を throw → LogError+skip へ。`MapVeinMasterUtil` に `outcropAddressablePath` 非空検証を追加。
- **B5** `MiningProtocolMessagePack` に veinGuid を追加し `CreateVeinRequest(playerId, veinGuid, position)` へ。`VeinHandMiningService.TryMine` は「座標に乗っている ∧ guid一致」を要求。`OutcropGameObject` は保持済みの `MapVeinMasterElement` から guid を渡す。
- **B6** `FrameYieldObjectInterval` を 100 → 50 に戻し、根拠コメント4件を復元（フレーム分散間隔の選定理由／二重開始ガード／開始前待機ガード（姉妹クラスから移植可）／「ダメージ算出はサーバ権威のため打撃対象だけを送る」）。
- **B7** `docs/webui/ugui-retirement-plan.md` と `.agents/skills/moores-code-review/` の3ファイルを本PRから外す。`.moorestech-external-revisions.json` の `moorestech_client_private` バンプ（`d50802c → 40cdf1ad`）を PR 本文へ明記。

### C. 機械的修正（裁定不要）

- `OutcropGameObject` の `_destroySoundType` 二重保持を `{ get; private set; }` へ畳む
- `OutcropGameObjectDatastore.OutcropObjectNamePrefix` を `public const` → `internal const`
- `SelectOutcropPosition` の `fallbackHeight` は `CalculateInclusiveCenter` の `center.y` と同一式。`groundResolved ? groundPosition : center` に畳み `layout` 引数を落とす
- region-internal 規約（テスト5ファイル6メソッド）: `AttackTrackingMiningTarget.cs:79` / `MapObjectMiningAimTestForOutcrop.cs:89,103` / `OutcropGuidIndexTest.cs:41` / `VeinPinTutorialTest.cs:115` / `VeinMiningProtocolTest.cs:134`
- コメント短縮6件（`OutcropGameObjectDatastore.cs:70,81,94,166` / `MapObjectDatastore.cs:82` / `SkitManager.cs:137`）と自明コメント削除7件（`VeinHandMiningService.cs:44,48,52` / `OutcropGameObject.cs:32,38,56` / `MiningProtocol.cs:42`）
- 誤字「手採採掘」→「手掘り」（`MiningProtocol.cs:16` / `MiningCooldownService.cs:7`）

### D. 起票（コード変更なし）

- GitHub issue: 「手掘り不可の鉱脈に、理由（掘削機／ポンプが必要）を出し分ける」

## 検証

1. `uloop compile --project-path ./moorestech_client` で Error 0
2. `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Mining|Outcrop|VeinPin|Skit|MapVein"` 
3. `uloop get-logs --project-path ./moorestech_client --log-type Error` で起動時エラー0（A6の検証）
4. プレイテストシナリオ `tutorial-pebble-challenge.cs` の注入が通ること（A8）

## 未了・注意

- **PR が CONFLICTING**。rebase/merge の要否はユーザー判断待ち。71コミット差分なので、解消時に手掘り関連の実装がぶつかる可能性がある
- レビュー成果物が一度 logs repo から消失し復旧済み（改善キュー Q15 の再発2例目）。Codex監査プロンプト3本と実行結果3本は復旧できていない
- bd はこのリポジトリで未初期化（`issue_prefix` 未設定）のためタスク登録はスキップした

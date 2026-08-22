# 財布システムを「指示を返すサービス」へカプセル化する Implementation Plan

> **For agentic workers:** このファイルは中断・コンパクト後の再開点である。まず「## 現在の状態」を読み、`git log` と `.superpowers/sdd/progress.md` で実態を確認してから着手すること。

**Goal:** 残り設置数（財布）システムを、呼び出し側が「財布管理側の指示に従うだけ」で済むサービスへカプセル化する。設置は「これを消費せよ」、撤去は「これを返却せよ」という同じ形の指示を返し、財布の判断ロジック（`placementsPerCost`・残数・財布キー正規化・N到達判定・`remaining + sets×N` の算術）を呼び出し側から完全に排除する。

**裁定:** `.decisions/2026-08-22-財布システムは指示を返すサービスとしてカプセル化する.md`（線引きの正本）。あわせて `.decisions/2026-08-22-ベルト財布ブランチのレビュー裁定3件.md`（D2/D4/D5）。

**Branch:** `feature/belt-remaining-placement-count`
**Worktree:** `/Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/belt-remaining-placement-count`
**bd:** `moorestech-uh5a`（claim済み）

---

## 🚨 最初に決めること（未裁定・ブロッカー）

**適用範囲がまだ決まっていない。** 2026-08-22 に2回質問したが未回答のまま。着手前にユーザーへ確認すること。

- **案1（agent推奨）**: 本ブランチでやりきる。PRは大きくなるが、財布は今まさに導入している新機構なので、漏れたまま出すと後から6か所以上の呼び出し側を追いかけ直すことになる
- **案2**: 本ブランチは D2（凝縮判定の集約）＋ D3案A（Lookup入口で正規化）の最小限に留め、カプセル化の刷新は次ブランチ

案2を選んだ場合、本計画の Task 2〜4 は次ブランチへ送る（Task 1 と Task 5 のみ実施）。

---

## 現在の状態（2026-08-22 14:40 時点）

### ブランチの中身

ADR 0026 の実装（Task 1〜8）は**完了しコミット済み**。21コミット。base は `e62d86f6e`、head は `d0106be2d`。

| Task | 内容 | コミット | テスト |
|---|---|---|---|
| 1 | マスタに必須 `placementsPerCost` 追加・ファミリー一致検証 | `412d37a21` | 14/14 |
| 2 | 残り設置数DataStore（`Game.Construction`）・セーブ配線 | `5449e37ea` + `d426d8262` | 7/7 |
| 3 | イベントパケット・handshake同梱 | `a33fcf926` | 9/9 |
| 4 | 設置時の財布課金 | `a308f73c1` | 16/16 |
| 5 | 撤去時の凝縮返却・往復テスト | `845726b95` + `545c56ce2` | 13/13 |
| 6 | クライアント同期 | `e2375dc0b` | compile 0 err |
| 7 | プレビュー計算・200行分割 | `6211adbdd` + `c740da093` | 111/111 |
| 8 | webui 表示 | `8396bca24` | vitest 722/722 |

### 未コミットの変更（レビュー自動適用分・約25ファイル）

moores-code-review の Apply フェーズが Critical 10件（C1,C2,C4,C6,C8,C10,C11,C12,C13,C14）を**ワーキングツリーに適用済み・未コミット**。compile ok。主な内容:

- **C1**: webui e2e フィクスチャ未更新で `pnpm test:e2e` が tsc で落ち Playwright が1本も起動していなかった（TS2322 × 11件）→ 修正。**検証済み: `npx tsc -p e2e/tsconfig.json --noEmit` 0エラー / vitest 722/722 PASS**
- **C2**: `placementsPerCost > 1` かつ `requiredItems` 空という二義的マスタを `BlockMasterUtil` が拒否するようにし、`RemainingPlacementChargeService.ResolveCostToConsume` の `|| fullCost.Length==0` を削除して4述語すべてを `PlacementsPerCost <= 1` に統一
- **C4/C6**: `ConstructionCostPreviewCalculator` に `PlacementsPerCost <= 1` 早期ガードと `MarkUnaffordableCellsAsNotPlaceable` を新設し、`CommonBlockPlaceSystem` と `BeltConveyorPlaceSystem` の両方が委譲（`BeltConveyorCostPreviewMarker.cs` 削除）
- **C8**: `BuildMenuDetailSidebar.tsx` の残り設置数 span を `requiredItems.length > 0` ガードの外へ
- **C10**: `ClientRemainingPlacementCountDatastore.Apply/ApplyAll` を型付き `BlockId` 受けにし、生int→BlockId 変換を wire 境界へ移動
- **C12/C13/C14**: 新規publicの過剰公開6件を internal へ、比較演算子の向き6件、イベント名を `OnRemainingPlacementCountChanged` へ

**この未コミット変更をどう扱うかを最初に決めること。** 本計画の変更と混ざる前にコミットしておくのが安全（例: `fix: 全ブランチレビューのCritical 10件を適用する`）。

### レビュー結果（run 2026-08-22-0254）

- **48/48 系統回収・欠員0・fallback 0**。Codex 3本とも `.final.md` 実在（exit 0）
- Critical 14 / Warning 17 / Info 20 / suppressed 0 / 設計判断 9
- 実入力一式: `../moorestech_logs/harness/moores-code-review/runs/2026-08-22-0254/`（`integrated.md` / `design.md` / `patch.diff` / `context.md` / `checks.json` / `agents/` / `codex-*.final.md` / `final.diff`）
- 決定論チェックの confirmed 5件はすべて**本ブランチ以前からの既存負債**（`CommonBlockPlaceSystem.cs` 245行 / `MainGameStarter.cs` 395行 / `BlockMasterUtil.cs` 436行 / `MoorestechServerDIContainerGenerator.cs` 324行 / `Server.Event/EventReceive` 24ファイル）
- **注記**: `context.md` の「許容するトレードオフ」11行はすべて `[agent前提]` ラベル（計画の判断台帳で「出所: agent前提」の項目は `[ADR:]` を名乗れないため降格）。その結果 suppressed で返った12件がすべて通常の Critical/Warning へ復帰している

### 裁定済み（本計画に反映済み）

- **D2**: 凝縮判定を1本に集約 → Task 1
- **D4**: webui DTO を block variant へ移す → Task 4
- **D5**: 無料設置デバッグの非対称は現状維持 → 対応不要
- **D1/D3**: 「財布のカプセル化」裁定に吸収 → Task 2・Task 3

### 未取得の検証（環境要因・ブロック中）

- **Unity 全EditModeスイート**: 未取得。Editor 再起動後もドメインリロード連続 → 180秒タイムアウトが再現。`EditModeInPlaying` 除外フィルタでも同様
- **unityプレイ録画テスト（計画Task 9 Step 2）**: 未実施
- 各タスクの対象別テストはすべて緑（上表）。webui は e2e tsc 0エラー・vitest 722/722
- 既知の落とし穴: uloop が「not installed」を返すのは**テスト実行中のサイン**（設定ファイルが `.bak` へ退避される）。これを失敗と誤読してリクエストを重ねると走行中のスイートを潰す。`.bak` の無条件復元は古いポートを書き戻し `Could not verify project identity` を誘発するのでやらない

### その他の状態

- **masterピン**: `.moorestech-external-revisions.json` は `990298f`（`placementsPerCost` を含む・push済み）。**レビュー中に別要因で `85434efa`（我々の変更を含まない）へ書き換わる事象があり復元済み**。master 本体は `85434ef` まで進んでいるため、PR作成時にピンの扱い（我々のマスタ変更を最新masterへ載せ直すか）を判断すること
- **図解**: `~/.agents/skills/create-infographic-light/outputs/infographic-wallet-key-normalization/index.html`（財布キー正規化の問題と案A/B/Cの図解・コメント機能付き）。cloudflared クイックトンネルで公開中（URLはセッション限り）
- **SDD台帳**: `.superpowers/sdd/progress.md`

---

## Requirements

- **R1.** 呼び出し側が財布の判断ロジックを一切持たない。具体的に、プロダクションコードで次が呼び出し側に現れないこと: `placementsPerCost` の参照、残り設置数の参照、`ConstructionWalletUtil.ResolveWalletBlockId` の呼び出し、「N到達」の判定、`remaining + sets×N` の算術、`RequiredItems` の空判定。受け入れ: grep で確認
- **R2.** 設置と撤去が**同じ形の指示**を返す。設置=「消費すべきアイテム列（空なら消費なし）」、撤去=「返却すべきアイテム列（空なら返却なし）」。受け入れ: `RemoveBlockProtocol` が `bool` を受け取って返却物を組み立てるコードが消えていること
- **R3.** 呼び出し側は Lookup と Mutation の2ハンドルを持たない。財布サービス1つを注入し、段取り（引いてから書く順序）はサービス内部に閉じる
- **R4.** `placementsPerCost == 1` のブロックは従来どおり毎セル全額・全額返却で、**現行挙動と完全一致**する。呼び出し側に財布有無の分岐を作らない
- **R5.** 「TryAddBlock失敗時は財布も素材も変えない」「凝縮返却が入り切らないなら撤去失敗（財布も変えない）」を維持する。事前判定→確定後更新の順序を崩さない（打ち消し方式にしない＝残り設置数の変更イベントが2回飛ばない）
- **R6.** クライアント側も同じ原則に従う。プレビュー計算とビルドメニューDTOが財布の算術を持たず、問い合わせ口1つに従う。「残り設置数を画面に表示する」こと自体は可
- **R7.** 素材保存（設置と撤去が完全な逆操作）を壊さない。既存の往復テスト2本が緑のままであること
- やらないこと: 無料設置デバッグの対称化（D5で現状維持裁定済み）／既存肥大化ファイル（`CommonBlockPlaceSystem.cs` 等）の分割／ベルト以外への `placementsPerCost > 1` 設定

## Global Constraints

- `pwd` で作業worktreeを確認してから着手（メインworktreeでのUnity起動禁止）
- 1ファイル200行以下・1ディレクトリ10ファイルまで・`partial` 禁止・`Func<>` 禁止・try-catch原則禁止（外部境界の隔離のみ例外・根拠をコメント明記）・デフォルト引数禁止・単純getter/setter禁止（`{ get; private set; }` 可）
- 主要処理に日本語→英語の2行セットコメント（各1行に収める）
- `#region Internal` は「メソッド内のローカル関数をまとめる用途」限定
- `.cs` 変更後は必ず `uloop compile --project-path ./moorestech_client`
- テストは `--filter-type regex` で絞る。全EditModeスイートは環境要因で流せない（上記参照）
- `.meta` 手動作成禁止。Prefab/シーンのテキスト編集禁止
- イベント通知は UniRx。C# `event Action` 禁止
- 永続化は Newtonsoft JSON、キーはGuid（揮発BlockId保存禁止）。wire は BlockId(int) でよい
- 語彙: 「建設コスト／設置数/1セット／残り設置数／財布」。`Credit`/`Payment`/「クレジット」「支払い」はコード・コメント・UI・i18n・**コミットメッセージ**とも禁止
- 各タスク終了時に必ずコミット

---

## Task 0: 未コミット変更の確定（着手前・必須）

- [ ] レビュー自動適用分（約25ファイル）を確認しコミットする。混ざる前に切る
- [ ] `git status` がクリーンな状態から Task 1 を始める

参考コミットメッセージ: `fix: 全ブランチレビューのCritical 10件を適用する`

---

## Task 1: 凝縮判定を1本に集約する（D2・裁定済み）

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.Construction/ConstructionWalletUtil.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Construction/RemainingPlacementCountDataStore.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Construction/IRemainingPlacementCountMutation.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/Construction/RemainingPlacementChargeService.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/RemainingPlacementCountDataStoreTest.cs`

- [ ] `ConstructionWalletUtil.WouldCondense(int remaining, int placementsPerCost) => placementsPerCost <= remaining + 1` を追加
- [ ] `RemainingPlacementChargeService.WouldCondenseOnReturn` と `RemainingPlacementCountDataStore.ReturnOne` の双方がこれを呼ぶ
- [ ] `ReturnOne` の未使用 `bool` 戻り値を `void` へ落とす（`IRemainingPlacementCountMutation` も更新）
- [ ] コンパイル → `RemainingPlacementCountDataStoreTest|RemoveBlockRemainingPlacementTest|PlaceBlockRemainingPlacementTest` で緑
- [ ] コミット

**注意:** 現状の2式は `placementsPerCost <= remaining + 1`（サービス側）と `placementsPerCost <= returned`（DataStore側・`returned = remaining + 1`）で**値としては等価**。挙動を変えずに式の出所を1本にするのが目的。ユーザー裁定で確定した意味論（N到達で凝縮）を動かさないこと。

---

## Task 2: サーバー側の財布サービスを「指示を返す」形へ刷新する

**Files:**
- Modify/Create: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/Construction/` 配下
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/PlaceBlockProtocol.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/RemoveBlockProtocol.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Construction/` の Lookup/Mutation 境界
- Test: `PlaceBlockRemainingPlacementTest` / `RemoveBlockRemainingPlacementTest` / `PlaceBlockProtocolTest` / `RemoveBlockProtocolTest`

**現状の違反（実コード・2026-08-22時点）:**

```csharp
// RemoveBlockProtocol.cs:109-112 — 呼び出し側が bool を聞いて返却物を自分で組み立てている
if (blockMaster.RequiredItems != null && blockMaster.RequiredItems.Length != 0
    && RemainingPlacementChargeService.WouldCondenseOnReturn(blockMaster, data.PlayerId, _remainingPlacementCountLookup))
{
    result.AddRange(ConstructionCostService.CreateRefundItems(ConstructionCostService.ToItemCounts(blockMaster.RequiredItems)));
}
```

```csharp
// PlaceBlockProtocol.cs:99,118 — こちらは既に近い形（指示を受け取って従う）
var costItemCounts = RemainingPlacementChargeService.ResolveCostToConsume(blockMaster, data.PlayerId, _remainingPlacementCountLookup);
if (!ConstructionCostService.HasRequiredItems(costItemCounts, inventory.InventoryItems)) { costShortageCount++; return; }
...
RemainingPlacementChargeService.Charge(blockMaster, data.PlayerId, _remainingPlacementCountMutation, costItemCounts, inventory);
```

- [ ] **撤去側を設置側と対称にする。** `bool` ではなく**返却アイテム列**を返す問い合わせにする（返さないなら空）。`RequiredItems` の空判定もサービス内部へ
- [ ] **`ResolveCostToConsume` の空配列の二義性を解消する**（「財布が肩代わり」と「コスト未定義」が区別できない）。指示を型で明示する（例: 消費物＋財布使用有無を持つ計画型）。※ C2 適用で `requiredItems` 空 + `placementsPerCost>1` はマスタ検証で弾かれるようになったが、意味の二義性自体は残っている
- [ ] **呼び出し側から Lookup/Mutation の2ハンドルを外す。** サービス1つを注入し、段取りはサービス内部へ
- [ ] `ResolveWalletBlockId` の呼び出しをサービス内部（または datastore 入口）へ寄せ、サーバー4か所の重複を消す
- [ ] R5（失敗時に財布も素材も変えない・事前判定→確定後更新）を崩していないことをテストで確認
- [ ] コンパイル → 関連テスト緑 → コミット

**設計の注意:** 撤去は「インベントリに入り切るか」を**撤去確定前に**知る必要があるため、判定と確定の2フェーズは構造的に必要。打ち消し方式（先に `ReturnOne` して失敗時に戻す）は禁止（残り設置数の変更イベントが2回飛びクライアントが誤った値を見る）。

---

## Task 3: クライアント側を同じ原則へ揃える

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Construction/ClientRemainingPlacementCountDatastore.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Util/ConstructionCostPreviewCalculator.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BuildMenu/BuildMenuEntryDtoFactory.cs`
- Test: `ConstructionCostPreviewCalculatorTest` / `BuildMenuEntryDtoFactoryTest`

**現状の違反:**

```csharp
// ConstructionCostPreviewCalculator.cs:71 — 正規化も、残数とNの算術も呼び出し側にある
var remaining = remainingPlacementCountDatastore.GetRemainingCount(ConstructionWalletUtil.ResolveWalletBlockId(representativeBlockId));
var affordableCount = CalculateAffordablePlacementCount(blockMaster.RequiredItems, blockMaster.PlacementsPerCost, remaining, inventoryItems);

// BuildMenuEntryDtoFactory.cs:101 — 同じ2段書き
return remainingPlacementCountDatastore.GetRemainingCount(ConstructionWalletUtil.ResolveWalletBlockId(block.BlockId));
```

- [ ] クライアント datastore の `GetRemainingCount` が生の `BlockId` を受け、内部で正規化する（`ResolveWalletBlockId` は冪等なので二重解決は無害）。引数名も `walletBlockId` → `blockId` へ
- [ ] 「置けるセル数」の算術（`remaining + sets×N`）を呼び出し側から財布側へ移す。プレビューは「何セル置けるか」を問い合わせて従うだけにする
- [ ] ビルドメニューDTOも同様に、残数の取得を問い合わせ1回に畳む（表示すること自体は可）
- [ ] コンパイル → 関連テスト緑 → コミット

---

## Task 4: webui DTO を block variant へ移す（D4・裁定済み）

**Files:**
- Modify: `moorestech_web/webui/src/bridge/contract/schemas/buildMenu.ts`
- Modify: `moorestech_web/webui/src/features/buildMenu/BuildMenuDetailSidebar.tsx`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BuildMenu/BuildMenuDtos.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BuildMenu/BuildMenuEntryDtoFactory.cs`
- Test: `buildMenu.test.ts` / `buildMenuGrouping.test.ts` / e2e `buildMenuFixtures.ts` / `WireContractTest.cs` / `build_menu_snapshot.json`

- [ ] `placementsPerCost` / `remainingPlacementCount` を `BuildMenuEntryCommonFields` から **block variant** へ移す
- [ ] C# 側は `int?` ＋ `NullValueHandling.Ignore`（キー省略）。前例は同一DTOの `Label`/`IconUrl`
- [ ] 利用側は `entry.kind === "block" && entry.placementsPerCost > 1`
- [ ] `{ kind:"trainCar", placementsPerCost:3 }` が `.strict()` で弾かれることをテストで縛る
- [ ] 検証: `npx tsc -p e2e/tsconfig.json --noEmit`（0エラー）→ `npx vitest run`（全緑）→ `npm run build` → Unity compile → `WireContractTest|BuildMenuEntryDtoFactoryTest`
- [ ] コミット

---

## Task 5: 検証・レビュー・PR

- [ ] **R1 の grep 検証**: プロダクションコードの呼び出し側に `placementsPerCost` / 残数 / `ResolveWalletBlockId` / N到達判定 / `RequiredItems` 空判定 が残っていないことを確認し、結果を報告に残す
- [ ] 関連テスト一式（サーバー財布系・クライアントプレビュー・webui）を緑にする
- [ ] **Unity 検証の再挑戦**: 環境が復旧していれば全EditModeスイートと unityプレイ録画テストを実施。ダメなら未取得であることを明示してPR本文に書く（隠さない）
- [ ] `moores-code-review` でブランチ全体を再レビュー（**省略不可**）。前回 run は `2026-08-22-0254`
- [ ] **masterピンの扱いを決める**: 現在 `990298f`。master 本体は `85434ef` まで進んでいる。我々のマスタ変更を最新masterへ載せ直すか、現ピンのままか
- [ ] `bd close moorestech-uh5a --reason="..."` → pr-create でPR作成

---

## 判断記録（ADR）

- **設計原則（財布のカプセル化）**: ユーザー裁定 2026-08-22。線引きの正本は `.decisions/2026-08-22-財布システムは指示を返すサービスとしてカプセル化する.md`
- **D2 凝縮判定の集約 / D4 webui DTO の block variant 移動 / D5 無料設置デバッグは現状維持**: ユーザー裁定 2026-08-22。`.decisions/2026-08-22-ベルト財布ブランチのレビュー裁定3件.md`
- **`ReturnOne` の凝縮閾値は N 到達（`<=`）**: ユーザー裁定 2026-08-21。計画のサンプルテストが N+1 到達（`<`）を期待していたのは誤り。設置と撤去が完全な逆操作になり素材が保存されるのはこちらだけ（元計画 `2026-08-21-belt-construction-cost-remaining-placement-count.md` の判断記録にも記載）
- **適用範囲（本ブランチでやりきるか次ブランチか）**: **未裁定**。着手前に確認すること

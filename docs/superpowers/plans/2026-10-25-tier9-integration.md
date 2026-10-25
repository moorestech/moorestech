# Tier9 統合（S1・S2・S3 とマスタの中身を 1 本の PR にして一気通貫で遊べるようにする）Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。セッションの分担と合流点は `docs/superpowers/plans/2026-10-25-tier9-integration-orchestration.md` に従う。

**Goal:** 本体 PR #1554（S1）・#1551（S2）・#1552（S3）とマスタ PR #88・#87 を 1 本の統合 PR（＋対のマスタ統合 PR）にまとめ、マスタの中身を入れ、Tier8 終盤のセーブから最終研究まで裏口なしで遊べることを、自動ゲート・録画・評価役・ユーザーの通しプレイで保証する。

**Architecture:** S1 先端から切った `feat/tier9-integrated` に S3（S2 を含む）をマージして衝突を解き、セーブ移行を V7→V8 の 1 本へ畳む。マスタは #88 と #87 をマージした統合ブランチに S1→S2→S3 の順で中身を積む。中身を書く前に、到達性チェッカー（G2）と本番マスタのロードゲート（G1）を作り、マスタ投入を「ゲートが赤から緑へ進む」形で駆動する。

**Tech Stack:** Unity / C#（moorestech_server・moorestech_client）、VanillaSchema YAML ＋ Mooresmaster SourceGenerator、マスタ JSON（moorestech_master）、Python（到達性・バランス）、React/TypeScript（moorestech_web/webui）、プレイテスト DSL（unity-playmode-recorded-playtest）

## Requirements

- R1 統合: `feat/tier9-integrated` が S1・S2・S3 の全機能を含み、origin/master に対して衝突なし。受け入れ: `git merge-tree --write-tree origin/master HEAD` が衝突0、compile エラー0、S1/S2/S3 の既存テストが全 pass、webui の typecheck と vitest が pass
- R2 セーブ移行: CurrentVersion=8、master の V6→V7 の後ろに V7→V8 が 1 本だけ。中身は部品（継承レベル・清浄室機械の液体スロット・初回打ち上げ履歴・ケーブルプール・Tier9 コスト0研究のリセット）。S1 の UnimplementedBlockUnlockRevoker は無い。受け入れ: 部品ごとの旧版 JSON→変換結果テストと SaveMigrationChainTest が pass（裁定 `2026-10-25-Tier9統合PRのセーブ移行はV7からV8の1本に部品として畳む`・`…コスト0で完了済みのTier9研究をリセットする`）
- R3 研究リセット: 旧マスタでコスト0のまま完了できた 6 ノード（フィルター分岐器・機械オーバークロック・金属超精密加工・核融合炉・反物質爆弾・軌道エレベーターの建設）の完了が旧セーブから外れ、それらが解放していたものも解放されていない。受け入れ: G5 ゴールデンセーブテストで assert
- R4 マスタの中身: S1 handoff の Part D、S2 plan と S3 plan の「統合タスクへ引き継ぐマスタの中身」の全項目が、統合マスタに入っている。受け入れ: 各スライスの完了判定、G2 緑
- R5 拠点の解放: 宇宙エレベーター拠点は「軌道エレベーターの建設」で解放され、最終研究の必須経路には入らない（裁定 `2026-10-25-宇宙エレベーター拠点は…`）。受け入れ: G2 の「どこからも解放されないブロック」が0
- R6 アップグレード研究: 半導体のアップグレード研究は Lv2〜Lv5 の 4 ノードで、各ノードが `unlockLevelInheritanceCraftLevel` で製作上限を 1 段上げ、計算データを消費する（裁定 `2026-10-25-半導体のアップグレード研究は…`）。受け入れ: G3 で 4 ノードが順に完了し、G4 で上位レベルが作れる
- R7 G1 ロードゲート: ピン先の本番マスタを `MasterHolder.Load` し、失敗なら非0で終わる Editor batch メソッドがあり、統合 PR で走る。受け入れ: 統合マスタで成功、わざと壊したマスタで失敗
- R8 G2 到達性: マスタ repo の到達性チェッカーが、初期状態→最終研究の到達、作り方の無いアイテム・どこからも解放されないブロック・機械が解放されないレシピ・使い道の無いアイテムを、許可リスト外が1件でもあれば exit 1 で判定し、通った経路と許可した詰みを印字する。軌道到着・レベル継承・クリーンルーム抽選を計算に入れる。受け入れ: 統合マスタで exit 0、#88+#87 のマージ結果で exit 1、壊した fixture で exit 1
- R9 G3 研究ウォーク: 本番マスタで研究を前提順に全ノード `CompleteResearch` でき、直後に次ノードが研究可能になる。受け入れ: batch の出力で全ノード true
- R10 G4 結合テスト: テスト mod で ICチップ→GPU→GPU衛星→打ち上げ→計算データ→研究で消費→拠点接続→ケーブル到着が 1 本のテストで通る
- R11 G5 ゴールデンセーブ: master 版 7 形式のセーブを V8 へ変換してワールドとしてロードでき、R2・R3 の部品の効果が assert される
- R12 G6 バランス: シミュレーションの値とマスタ JSON の値の照合でずれ0、目標時間（S1 区間 6〜8 時間＝S1 handoff の裁定、全体・計算データ・S3 区間は S2/S3 plan の目標）に収まる。ずれれば exit 1
- R13 録画: 出荷マスタで 7 段（チップライン／モジュール・GPU／抽選 UI／打ち上げ→計算データ／計算データでの研究／核融合・エレベーター／反物質→最終研究）を撮り、result.json 全 pass・Error ログ0。各段の最後の工程は本物で動かし、少なくとも 1 本は飛行を本当に待つ。A11 を撮り直す
- R14 最終検証: 目的だけを渡した別の評価役 2 体が実物で合格を出す。1 体でも不合格なら直し、基準は緩めない。そのうえでユーザーに通しプレイのチェックリストと Tier8 終盤セーブを渡す（裁定 `2026-10-25-Tier9統合の最終検証の通しプレイはユーザー本人が行う`）
- R15 PR: 本体の統合 PR とマスタの統合 PR が作成され、本体のピンはマスタ統合 PR の push 済みコミットを指す。#1551・#1552・#1554・#87・#88 は統合 PR へのリンクを付けて閉じる

### やらないこと
- 新しいゲームプレイ機能の追加（S1〜S3 の範囲を超える機構）。録画 DSL への時間加速・段の境目のセーブの追加
- ストーリー上のクリアの定義（最終研究で終端。既存裁定 `2026-10-23-ストーリークリアは定義せず反物質爆弾を規定量作る仮の研究で終端にする`）
- 統合で衝突しない既存コードのリファクタ
- Steam の playtest 公開ブランチへの反映（ユーザーの通しプレイ用に staging へ上げるところまで）

## Global Constraints

- セーブ: `WorldSaveAllInfo.CurrentVersion = 8`。移行ステップは `Game.SaveLoad/Migration/Steps/` 配下、`ISaveMigrationStep` 実装、変換できない形は `SaveMigrationStepResult.Failed(reason)`（moorestech-save-migration スキルが正本）
- マスタの必須キー: S1 で必須化した `allowQualityModule`（ElectricMachine・GearMachine・CleanRoomMachine 等の機械ブロック）と `levelInheritance`（全機械レシピ。None/Inherit/InheritUpToUnlocked）は、全新規行に入れる。optional 化・既定値での補完はしない
- マスタの共有クローン `~/hermes-agent/data/repos/moorestech_master` を checkout し直さない。マスタの作業は `~/hermes-agent/data/worktrees/moorestech_master/<name>` の worktree で行い、Editor・録画には引数でそのパスを渡す
- テストから本番マスタを読まない（`.github/scripts/check-test-external-dependencies.py`）。本番マスタを読む検査は Editor batch メソッド（G1・G3）で行う
- AGENTS.md の規約（1 ファイル 200 行以下・1 ディレクトリ 10 ファイルまで・partial/Func<> 禁止・try-catch は外部境界のみ・デフォルト引数禁止・.meta 手作成禁止＝.cs と .meta は `git mv` で一緒に動かす・Prefab 等の手編集禁止・日英 2 行コメント・fail-closed はログ必須）
- 別 repo（moorestech_master）の変更も push して PR を作る。本体のピンはその PR の push 済みコミット
- マージは通常のマージコミット（squash しない）
- 表記: レベル付きアイテムの名前は空白なし（ICチップLv2・GPU衛星Lv1・GPULv2。判断記録 A12）
- 検証の語: 「unityプレイ録画テスト」と呼ぶ（e2e と呼ばない）

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反2件・修正2件（再検査で違反0件） / G1・G3 を Server.MasterGate.Editor へ移し G6 を tools/tier9_balance へ一本化。nodeGraph の --check を CI に載せない件は A27 に記録
- Phase 2.6（型閉包・重複・ADR矛盾）: 実施済み / 強1・弱9・第3バケツ18（修正後の再検査: 新規 強0・弱3・第3バケツ1） / 残る強は G6 Check の None と kind の二重表現（5-A）。裁定は判断記録に記す

---
## タスク一覧（実行順）

本文の節はフェーズ（P0 ゲート・P1 コード統合・P2 マスタ投入・P4 動的検証・P5 録画・P6 最終検証）ごとに並ぶが、実行は下表の番号順・wave 順に行う。セッション A/B の分担と合流点 J1〜J3 は `docs/superpowers/plans/2026-10-25-tier9-integration-orchestration.md` が正。

| wave | セッション | Task | 内容 | フェーズ |
|---|---|---|---|---|
| W1 | B | 1 | マスタ統合ブランチ作成（#88→#87 マージ）と起点のゲート出力の控え | P2 |
| W1 | B | 2 | G2 到達性チェッカー（`tools/progression/`）と CI | P0 |
| W1 | B | 3 | G6 バランスゲートの枠（`tools/tier9_balance/`）と S1 の sim（`design/tier9-balance/sim/`） | P0 |
| W1 | A | 4 | S3 をマージして衝突23件を解く | P1 |
| W1 | A | 5 | 同名テストクラスの解消と ADR 0091〜0093 への振り直し | P1 |
| W1 | A | 6 | セーブ移行を V7→V8 の 1 本に畳む（4 部品） | P1 |
| W1 | A | 7 | Tier9 コスト0研究のリセット部品 | P1 |
| W1 | A | 8 | 研究ロードの無音スキップにログ | P1 |
| W1 | A | 9 | マージ後の健全性ゲート | P1 |
| W1 | A | 10 | G1 出荷マスタのロードゲート | P0 |
| W1 | A | 11 | G3 研究ウォーク | P0 |
| — | — | J1 | 合流点 | — |
| W2 | A | 12 | 適用スクリプト・GUID 台帳・G6 の S1 区間の照合・nodeGraph 同期ツール | P2 |
| W2 | A | 13〜17 | マスタ S1 スライス（上流・ウェハ/ダイ/チップ・専用機・モジュール/GPU・S1 研究） | P2 |
| W2 | A | 18〜19 | マスタ S2 スライス（計算データ・GPU衛星・発射台・ロケット技術・アップグレード研究 Lv2〜Lv5）と G6 の S2 区間 | P2 |
| W2 | A | 20〜22 | マスタ S3 スライス（核融合・反物質・グラフェン・拠点・S3 研究・最終研究）と G6 の S3 区間 | P2 |
| W2 | A | 23 | バランスを G6 で確定 | P2 |
| W2 | A | 24 | マスタ PR・本体ピン更新・e2e fixture | P2 |
| W2 | B | 25 | G4 結合テスト | P4 |
| W2 | B | 26 | G5 ゴールデンセーブテスト | P4 |
| — | — | J2 | 合流点 | — |
| W3 | A | 27 | W13 を仕様か不具合か確かめる（録画の期待値の前提） | P5 |
| W3 | A | 28 | 録画 7 段（8 本） | P5 |
| W3 | A | 29 | A11 撮り直しと「State is null」の再観測 | P5 |
| — | — | J3 | 合流点 | — |
| W4 | A | 30 | Windows 配布ビルド・Tier8 終盤セーブ・通しプレイのチェックリスト | P6 |
| W4 | A | 31 | 目的だけを渡す別の評価役 2 体 | P6 |
| W4 | A | 32 | 全ブランチレビュー（moores-code-review） | 閉じ |
| W4 | A | 33 | PR 作成（本体・マスタ）とセッション終了可能状態 | 閉じ |

旧番号との対応（起草時の番号。本文の参照はすべて新番号に置換済み）: P2-1→1, P0-1→2, P0-4→3, P1-1〜6→4〜9, P0-2→10, P0-3→11, P2-2〜14→12〜24, P4-1→25, P4-2→26, P5-3→27, P5-1→28, P5-2→29, P6-2→30, P6-1→31

---
## P0 判定の仕組み（自動ゲート）

**目的:** マスタの中身を書く前に、「Tier9 を一気通貫で遊べるか」を機械で判定する仕組みを先に作る。P2 は、ここで作るゲートが赤から緑へ進むことを完了判定に使う。この節では次の 4 つを作る。G4（結合テスト）と G5（ゴールデンセーブ）は別の節が扱う。

| ゲート | 何を判定するか | どこで走るか | 作るタスク |
|---|---|---|---|
| G2 到達性 | 初期状態から最終研究まで進めるか。作り方の無いアイテム、どこからも解放されないブロック、機械が解放されないレシピ、使い道の無いアイテム、コスト0の研究、暗黙の前提が無いか | マスタ repo の CI（push / PR）と手元 | Task 2 |
| G1 ロード | 出荷マスタが本番と同じ経路（`ModsResource` → `MasterHolder.Load`）で読めて、起動時の検証を通るか | 本体 repo の新しいワークフロー（PR ごと）と手元（uloop） | Task 10 |
| G3 研究ウォーク | G2 が出した研究順に、各ノードのコストだけを渡して `CompleteResearch` が通るか。G2 が当てにした解放が実際に付くか | G1 と同じ job、手元（uloop） | Task 11 |
| G6 バランス | sim の値と JSON の値が合っているか。区間ごとの目標時間に収まるか（この節で作るのは枠だけ。数値は P2） | マスタ repo の CI と手元 | Task 3 |

**共通の変数**（全タスクで使う。P2 節と同じ値）:
```bash
W=/Users/sakastudio/hermes-agent/data/worktrees/moorestech/tier9-integrated        # 本体の統合 worktree
MW=/Users/sakastudio/hermes-agent/data/worktrees/moorestech_master/tier9-integrated # マスタの統合 worktree（Task 2 Step 1 で無ければ作る）
MC=/Users/sakastudio/hermes-agent/data/repos/moorestech_master                      # マスタの共有クローン（fetch と archive にだけ使う。checkout しない）
GL=$W/moorestech_client/PlaytestResults/tier9-gates                                 # ゲート出力の控え。git 管理外（P2 節と同じ置き場）
MASTER=$MW/server_v8/mods/moorestechAlphaMod_8/master
```

### 確かめた事実（この節の前提。根拠つき）

- **本番マスタを読む CI は今ひとつも無い。**
  - `wf/VERIFY.md:6` には「`build.yml` の `WorldSnapshotBundler.cs:32` が本番マスタを読む」とあるが、これは誤り。
  - CI 入口の `BuildFromGithubAction` は `PlayerBuildRequest.ForCi` を使う（`moorestech_client/Assets/Scripts/Editor/Build/BuildPipeline.cs:150-156`）。`BuildPurpose.Ci` は `bundlesLocalGameData: false` である（`moorestech_client/Assets/Scripts/Client.Build.Policy/BuildPurpose.cs:59-61`）。
  - `GameDataBundler` と `WorldSnapshotBundler` は `if (policy.BundlesLocalGameData)` の内側でしか呼ばれない（`BuildPipeline.cs:91-95`）。
  - したがって G1 は新しい経路として作る必要がある。
- **テストから本番マスタは読めない。**
  - `.github/scripts/check-test-external-dependencies.py:10-22` が禁じている。対象は `Client.Tests/`・`Tests/`・`Tests.Module/`・`webui/e2e/` の 4 か所で、`PinnedMasterRepository`・`ServerDirectory.GetDirectory(`・`../moorestech_master` などが禁止パターン。
  - `moorestech_server/Assets/Scripts/Editor/MasterGate/` はこの 4 か所の外にある。そのため G1/G3 は、テストではなく Editor の `-executeMethod` として作る。
- **CI はピン先のマスタをすでに本体の隣へ checkout している**（`run_test.yml:119-144`。置き場は `moorestech_master/`）。
  - G1・G3 はピンを自分で解かない。マスタの場所は CI でも手元でも常に `-masterDataRoot`（dynamic code では引数）で明示して渡す（判断記録 A23）。CI では checkout したピン先の絶対パス、手元では `$GL/pin-master` や `$MW` を渡す（手元の worktree には `$W/../moorestech_master` が無い）。
  - そのため `Client.Game` の `MasterDataRootLocator` には依存しない。G1・G3 はサーバーのアセンブリだけで書ける。
- **カスタムメソッドを unity-builder で回す前例がある。** `platform-compile.yml:52-69` と `cache-warm.yml:186-202` が `game-ci/unity-builder@4423ee7…`（v4.8.2）に `buildMethod: Server.Editor.PlatformCompileCheck.RunFromGithubAction` を渡し、`EditorApplication.Exit(0|1)` で合否を返している（`moorestech_server/Assets/Scripts/Editor/PlatformCompileCheck.cs:22-59`）。
  - 置き場は `Server.Editor` の asmdef（`moorestech_server/Assets/Scripts/Editor/`）、ワークフローは `projectPath: moorestech_server/`、Library は `Library_compile_server_<ターゲット>-` のキャッシュを読むだけ（保存は `cache-warm.yml`）。
  - Editor 配下のサブディレクトリに専用 asmdef を置く前例は `moorestech_server/Assets/Scripts/Editor/SchemaWatch/Server.SchemaWatch.Editor.asmdef`。
  - サーバーのスクリプトは client project にパッケージ `tech.moores.server`（`moorestech_client/Packages/manifest.json:46` の `file:../../moorestech_server/Assets/Scripts`）として入る。サーバー側の Editor asmdef も client の Editor でコンパイルされるので、手元の uloop（`--project-path ./moorestech_client`）から同じクラスを呼べる。
- **本番と同じロード経路と、失敗のしかた。**
  - `MoorestechServerDIContainerGenerator.Create` は `ModsResource` → `ModJsonStringLoader.GetMasterString` → `MasterHolder.Load` の順に読む（`moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs:39-52`）。Editor から同じ 2 行で読む前例は `WorldSnapshotBundler.cs:31-32`。
  - 検証に落ちると `InvalidOperationException("Master data validation failed…")` を投げる（`moorestech_server/Assets/Scripts/Core.Master/MasterHolder.cs:108-114`）。
- **研究の規則。**
  - `ResearchDataStore.CompleteResearch` はコストを主インベントリからだけ払う（`moorestech_server/Assets/Scripts/Game.Research/ResearchDataStore.cs:48`）。状態の判定も主インベントリだけを見る（同 `:79`）。
  - 状態は `Completed / Researchable / UnresearchableAllReasons / UnresearchableNotEnoughItem / UnresearchableNotEnoughPreNode` の 5 つ（`Game.Research/IResearchDataStore.cs:25-33`）。
  - プレイヤーの登録は `PlayerIdentityRegistry.PreviewAssignment` → `Commit`（`Game.PlayerIdentity/PlayerIdentityRegistry.cs:35,51`）。
  - DI への登録は `Server.Boot/Composition/GameplayServiceRegistration.cs:94,135,143,149`。
- **既存の `progression_check.py`（マスタ repo `tools/research_sync/`）は壊れている。**
  - 拾える mapObject のドロップを、トップレベルの `earnItems` から読んでいる。スキーマ上は `miningParam.earnItems` にある（`VanillaSchema/map.yml:48,67`。実データの mapObject のキーは `miningParam` の下に `earnItems`）。そのため初期資源が空になり、`wf/VERIFY.md:87` の「149 件の偽陽性」が出る。
  - さらに、クラフトの解放を `unlockItemRecipeView` で判定している。実際のクラフト解放は `unlockCraftRecipe` の `unlockRecipeGuids` である（`VanillaSchema/ref/gameAction.yml:35-39`）。意味が違う。
  - 判定も「before/after で新しく増えた詰みだけ」の相対判定で、絶対判定ではない（`sync_machine_recipe_unlocks.py:185-193`）。
- **Tier9 の規則**（G2 のモデルが真似るもの）:
  - レベル族は `levelItemGuids` の index 0 が基底 = Lv1（`Core.Master/Validator/ItemMasterUtil.cs:223-239`）。初期の製作上限は 1（`Game.Block/Blocks/Machine/Upgrade/LevelInheritanceCraftCapTable.cs:14`）。上限は族の基底 GUID で引く（`Game.Block.Interface/MachineUpgrade/MachineRecipeLevelInheritanceUtil.cs`）。
  - ウェハの等級の上限は「装着した品質モジュールの最高 tier + 1」。昇格は `min(maxPromotionChance, 品質合計 × classQualityMultiplier)`（`Game.Block/Blocks/CleanRoom/Machine/Yield/ChipYieldFormula.cs`）。
  - ダイシングは `gradeDieTables[g-1].levelRates` で抽選し、出力は [ダイ Lv1..N, 失敗ダイ]（`…/OutputRewrite/DicingOutputRewriter.cs`）。
  - 発射台は `orbitalPayloads` の全行を受け付ける（S2 `Game.Block.Interface/Component/RocketLaunchPad/RocketLaunchPadSlotLayout.cs:26`）。エレベーターは `carriedPayloadItemGuids` と `cableItemGuid` を使う。到着の成果は `Game.Orbit/OrbitalArrivalService.cs`。
- **#88+#87 のマージ結果を G2 で測った値**（Task 1 の tree `f9370e7` と同じ `server_v8` を scratch に組んで実測。S2 #87 の server_v8 の変更は `items.json` だけ）:
  - 研究は 44/44 ノードに届き、21 段で済む（`wf/CONTENT.md:30` の「44 ノードすべて完了」と一致）。
  - 許可リスト外の FAIL は 69 件（許可済み 19 件）。内訳は deadRecipes 1、goal 2、neverUnlockedBlocks 10、unobtainableItems 29、unusedItems 21、zeroCostResearch 6。
  - unobtainableItems は 29 件に許可済みの 4 件を足して 33 件。`wf/CONTENT.md:30` の 33 件と一致する。
  - `origin/master` では 65 件、`0d129c8` では 51 件。どちらも stale（陳腐化した許可）は 0 件。
  - 暗黙の前提は 10 件。旧チェッカーの基準線「10 unreachable costs」と一致する。

### 置き場とブランチ

- **G2・G6（Python）:** マスタ repo の `tools/progression/` と `tools/tier9_balance/` に置く。コミット先はマスタ統合ブランチ `feat/tier9-integrated`（`$MW`）。
  - マスタ repo の main（`origin/master`）にも Tier9 の仮ブロックがすでにあるので、G2 は main でも 65 件の FAIL で赤い（上の実測）。
  - そのため、ゲートの CI は統合ブランチに載せる。P2 が緑にしてから統合 PR ごと main へ入る。main に単独で入れると、全員の push が赤くなる。
- **G1・G3（C#）:** 本体 `moorestech_server/Assets/Scripts/Editor/MasterGate/` に置く。Editor 専用の asmdef `Server.MasterGate.Editor` を作る（判断記録 A23。CI ゲートの前例 `PlatformCompileCheck.cs` と同じ `Editor/` 配下、サブディレクトリの asmdef は前例 `Editor/SchemaWatch/Server.SchemaWatch.Editor.asmdef`）。CI は新しいワークフロー `.github/workflows/shipped-master-gate.yml` に置く（前例 `platform-compile.yml`）。
- 1 ファイル 200 行以下、1 ディレクトリ 10 ファイルまで（AGENTS.md）。G2 は `tools/progression/` の 21 ファイル・計 1096 行と、共通部品 `tools/master_common/` の 2 ファイル・計 25 行で、最大は `model/machine_sources.py` の 127 行。

### G2 が見ないもの（他のゲートが受け持つ）

G2 は「入手できるか」だけを見る、量のない静的モデルである。次のものは見ないので、合格しても遊べる保証にはならない。
- 電力・燃料の収支、スループット・所要時間 → G6
- クリーンルームのクラスを達成できるか（汚染と清浄機）→ G6 の S1 sim、G4、録画
- 主インベントリに入り切るか → G3
- ブロックの接続口の配置、列車 → 録画と通しプレイ
- 確率は「0 より大きければ手に入る」と扱う。抽選の重さは G6 が見る

### 後段への渡し方（Produces の早見。P2 節の「ゲートの呼び方」はこれに合わせて読み替える）

| P2 節の書き方 | この節の実体 |
|---|---|
| `G1 $MW/server_v8` | Task 10 の Produces にあるシェル関数 `G1`。G1 の唯一の入口 `ShippedMasterGateEntry.RunLoadGate` を uloop で呼ぶ（static マスタを書き換えるのは `ShippedMasterOwner` だけ。判断記録 A32） |
| `G2` | `cd $MW && python3 tools/progression/check.py`。P2 の各タスクもこの形で書く。`tools/research_sync/progression_check.py` は `sync_machine_recipe_unlocks.py` が import する `ProgressionModel` だけを持ち、単体で実行すると check.py を案内して exit 2 で終わる（Task 2 Step 8） |
| G2 の許可リストのパス | `tools/progression/allowlist.json` |
| `G6` | `cd $MW && python3 tools/tier9_balance/run.py`（入口はこれだけ。判断記録 A22）。S1 の sim（`design/tier9-balance/sim/`、Task 3 Step 7 が置く）は区間 `s1_semiconductor` が import し、照合 8 項目は Task 12 が `sections/s1/` に書く |

---

### Task 2: G2 到達性チェッカーを作り直し、絶対判定でマスタ repo の CI に載せる

**Files:**
- Create（git worktree・無いときだけ）: `$MW`（moorestech_master、ブランチ `feat/tier9-integrated`）
- Create: `moorestech_master:tools/progression/check.py`、`…/allowlist.py`、`…/report.py`、`…/goals.json`、`…/allowlist.json`
- Create: `moorestech_master:tools/progression/model/{__init__,master_data,unlock_index,reach_state,basic_sources,machine_sources,orbital_sources,fixpoint,research_walk}.py`
- Create: `moorestech_master:tools/progression/audits/{__init__,progress,catalog,item_consumers}.py`
- Create: `moorestech_master:tools/master_common/{__init__,research_ancestors}.py`（研究の祖先集合。G2 と G6 の共通部品。Task 12 が同じパッケージに `json_text.py` を足す。判断記録 A32）
- Test: `moorestech_master:tools/progression/tests/{fixture_master,test_gate_basics,test_gate_tier9}.py`
- Modify: `moorestech_master:tools/research_sync/progression_check.py`（import 用の `ProgressionModel` だけを残し、中身を新モデルへの委譲に差し替える。G2 の入口は `tools/progression/check.py` の 1 つ）
- Create: `moorestech_master:.github/workflows/master-gates.yml`

**Interfaces:**
- Consumes: マスタ JSON の `items.json`・`blocks.json`・`fluids.json`・`machineRecipes.json`・`craftRecipes.json`・`research.json`・`map.json`・`train.json`・`buildMenu.json`・`cleanRoom.json`（`model/master_data.py` が読む）。`sync_machine_recipe_unlocks.py` が `from progression_check import ProgressionModel` で使う `ProgressionModel(v8, research, ancestors, items, blocks, recipes).unreachable_costs(recipe_nodes) -> list[(nodeGuid, itemGuid)]`（形を保つ）。
- Produces:
  - `python3 tools/progression/check.py [--master-dir DIR] [--allowlist FILE] [--goals FILE] [--emit-order FILE]`。終了コード 0 = 全部通過、1 = 許可リスト外の問題・陳腐化した許可・理由のない許可・目標未達のどれか。
  - 出力の書式（G3・P2 が読む）:
    - `== ROUTE: n/N research nodes in W waves` と、その下に各ノードの `  [wave k] 名前: アイテム<入手経路>, …`
    - `== ALLOWED (m)` と `  ALLOW <カテゴリ> <名前>: <内容> -- <理由>`
    - 1 件ずつの `FAIL <カテゴリ> <キー> <内容>`、`FAIL stale-allowlist …`、`FAIL reasonless-allowlist …`、`FAIL goal …`
    - 最後に `== RESULT: PASS|FAIL (k failures, m allowed)`
  - カテゴリ: `unreachableResearch zeroCostResearch implicitPrerequisites unobtainableItems neverUnlockedBlocks deadRecipes fluidPorts unusedItems`（`allowlist.CATEGORIES`）。goal は許可リストに載せられない。
  - `--emit-order` の JSON: `{"order":[{"researchNodeGuid","name","wave","unlockBlockGuids":[…],"unlockMachineRecipeGuids":[…]}]}`（段の順、同じ段の中はマスタの並び順）。
  - 許可リスト `tools/progression/allowlist.json`: `{カテゴリ: {キー: 理由}}`。implicitPrerequisites のキーは `ノードGUID|アイテムGUID`。
  - 共通部品 `tools/master_common/research_ancestors.py` の `ancestors_including_self(research_rows) -> dict[研究GUID, set[研究GUID]]`（自分を含む。自分を除く集合は呼び側が `- {guid}` で作る。Task 19 の G6 が使う）。

- [ ] **Step 1: マスタ統合 worktree を用意する**（Task 1 Step 1〜2 と同じ作り方。P0 が先に走ったときだけ作る。すでにあれば何もしない）
```bash
git -C $MC fetch origin
test -d $MW || {
  git -C $MC worktree add -b feat/tier9-integrated $MW origin/master &&
  git -C $MW merge --no-ff origin/feat/tier9-cleanroom-playable -m "Merge #88 (Tier9 S1 クリーンルーム) into feat/tier9-integrated" &&
  git -C $MW merge --no-ff origin/feat/tier9-compute-data-loop -m "Merge #87 (Tier9 S2 計算データ) into feat/tier9-integrated" &&
  test "$(git -C $MW rev-parse HEAD^{tree})" = f9370e7e7ce313e1a6a04b1c3776cd9f620aab66 && echo TREE-OK; }
git -C $MW log --oneline -3
```
Expected: 新しく作ったときは `TREE-OK` が出る。どちらの場合も、最新 3 件に「Merge #87 …」と「Merge #88 …」が見える。

- [ ] **Step 2: `model/` パッケージ（マスタの読み込みと到達の不動点）を置く**
  - `mkdir -p $MW/tools/progression/model && touch $MW/tools/progression/model/__init__.py` を実行する（`__init__.py` は空）。
  - 次の 8 ファイルを置く。
  - 役割の分け方:
    - `master_data.py`: JSON を読むだけ。
    - `unlock_index.py`: どの研究が何を解放するか。`unlockCraftRecipe` は `unlockRecipeGuids`、上限研究は `unlockLevelInheritanceCraftLevel`。
    - `reach_state.py`: 入手済みの集合と入手経路のラベル。
    - `*_sources.py`: 入手経路の種類ごと。
    - `fixpoint.py`: 増えなくなるまで回す。
    - `research_walk.py`: 研究を段ごとに進める。前提不足・コスト不足の判定（`missing_prerequisites`・`missing_costs`）もここに置き、audits はこれを呼ぶ（同じ述語を 2 か所に書かない。判断記録 A32）。
    - `unlock_index.py` の解放済みの述語は 1 本（`_unlocked`）。「どこかの研究が解放するか」は全ノード完了の集合 `all_nodes` を渡して同じ述語で判定し、研究順の書き出しも索引から引く（判断記録 A32）。

`tools/progression/model/master_data.py`:
```python
#!/usr/bin/env python3
# v8 マスタの JSON を到達性検査用の索引へ読み込む（読むだけで書き換えない）
# Load the v8 master JSON into indexes for the reachability check (read-only)
import json
import os


def _load(master_dir, name):
    with open(os.path.join(master_dir, name), encoding='utf-8') as f:
        return json.load(f)


def _collect(value, items, out):
    if isinstance(value, dict):
        for child in value.values():
            _collect(child, items, out)
    elif isinstance(value, list):
        for child in value:
            _collect(child, items, out)
    elif isinstance(value, str) and value in items:
        out.add(value)


class MasterData:
    def __init__(self, master_dir):
        items_doc = _load(master_dir, 'items.json')
        self.items = {i['itemGuid']: i for i in items_doc['data']}
        self.blocks = {b['blockGuid']: b for b in _load(master_dir, 'blocks.json')['data']}
        self.fluids = {f['fluidGuid']: f for f in _load(master_dir, 'fluids.json')['data']}
        self.machine_recipes = _load(master_dir, 'machineRecipes.json')['data']
        self.crafts = _load(master_dir, 'craftRecipes.json')['data']
        self.research = _load(master_dir, 'research.json')['data']
        self.research_by_guid = {r['researchNodeGuid']: r for r in self.research}
        map_doc = _load(master_dir, 'map.json')
        self.map_objects = map_doc['mapObjects']
        self.item_veins = [v for v in map_doc['mapVeins'] if v['veinType'] == 'item']
        self.initial_items = [e['itemGuid'] for e in items_doc.get('initialEquipmentItems') or []]
        self.orbital_payloads = {p['payloadItemGuid']: p for p in items_doc.get('orbitalPayloads') or []}
        # レベル変種列は levelItemGuids（先頭が基準＝Lv1）。ItemMasterUtil と同じ並び
        # Level variants are levelItemGuids (index 0 is the base = Lv1), matching ItemMasterUtil
        self.level_variants = {f['baseItemGuid']: f['levelItemGuids'] for f in items_doc.get('levelFamilies') or []}
        self.quality_module_tiers = {m['itemGuid']: m['tier'] for m in items_doc.get('modules') or []
                                     if m['effectAxis'] == 'Quality' and m.get('effectValue', 0) > 0}
        # 列車の燃料・建設コストと接続ツールのコストもアイテムの使い道に数える
        # Train fuel/build costs and connect-tool costs also count as item uses
        self.extra_consumer_guids = set()
        _collect(_load(master_dir, 'train.json'), self.items, self.extra_consumer_guids)
        _collect(_load(master_dir, 'buildMenu.json').get('connectTools') or [], self.items, self.extra_consumer_guids)
        clean_path = os.path.join(master_dir, 'cleanRoom.json')
        self.clean_room = _load(master_dir, 'cleanRoom.json')['data'][0] if os.path.exists(clean_path) else None

    def item_name(self, guid):
        if guid in self.items:
            return self.items[guid]['name']
        if guid in self.fluids:
            return self.fluids[guid]['name']
        if guid in self.blocks:
            return self.blocks[guid]['name']
        return '?' + guid

    def research_name(self, guid):
        return self.research_by_guid[guid]['researchNodeName'] if guid in self.research_by_guid else '?' + guid

    def variants_of(self, base_guid):
        return self.level_variants.get(base_guid, [base_guid])

    def is_family_base(self, guid):
        return guid in self.level_variants

    def clean_room_rows(self, key):
        if self.clean_room is None:
            return {}
        return {row['machineRecipeGuid']: row for row in self.clean_room.get(key) or []}

    def max_class_value(self, key):
        if self.clean_room is None:
            return 0
        return max((t[key] for t in self.clean_room.get('thresholds') or []), default=0)
```

`tools/progression/model/unlock_index.py`:
```python
#!/usr/bin/env python3
# 研究の clearedActions から「何をどのノードが解放するか」の索引を作る（GameActionExecutor と同じ種類だけ読む）
# Index which research node unlocks what from clearedActions (only the kinds GameActionExecutor applies)


class UnlockIndex:
    def __init__(self, research, recipe_nodes_override):
        self.block_nodes, self.recipe_nodes, self.craft_nodes = {}, {}, {}
        self.give_items, self.craft_caps = {}, {}
        # 全ノードを完了した集合。「どこかの研究が解放するか」は解放済みの述語にこの集合を渡して判定する
        # The set with every node completed; "unlocked by some research" is the unlocked predicate under this set
        self.all_nodes = frozenset(node['researchNodeGuid'] for node in research)
        for node in research:
            guid = node['researchNodeGuid']
            for action in node['clearedActions']:
                self._add(guid, action['gameActionType'], action['gameActionParam'])
        # 解放同期則の再配分を試す sync 側は、機械レシピの解放先だけ差し替えて検査する
        # The sync script swaps only the machine-recipe unlock nodes to test a redistribution
        if recipe_nodes_override is not None:
            self.recipe_nodes = {g: set(nodes) for g, nodes in recipe_nodes_override.items()}

    def _add(self, node, kind, param):
        if kind == 'unlockBlock':
            for g in param['unlockBlockGuids']:
                self.block_nodes.setdefault(g, set()).add(node)
        elif kind == 'unlockMachineRecipe':
            for g in param['unlockMachineRecipeGuids']:
                self.recipe_nodes.setdefault(g, set()).add(node)
        elif kind == 'unlockCraftRecipe':
            for g in param['unlockRecipeGuids']:
                self.craft_nodes.setdefault(g, set()).add(node)
        elif kind == 'giveItem':
            self.give_items.setdefault(node, []).extend(r['itemGuid'] for r in param['rewardItems'])
        elif kind == 'unlockLevelInheritanceCraftLevel':
            for g in param['targetItemGuids']:
                self.craft_caps.setdefault(node, []).append((g, param['level']))

    def craft_cap(self, base_guid, completed):
        # 製作上限の初期値は LevelInheritanceCraftCapTable.InitialCraftCap = 1
        # The initial craft cap is LevelInheritanceCraftCapTable.InitialCraftCap = 1
        cap = 1
        for node in completed:
            for g, level in self.craft_caps.get(node, []):
                if g == base_guid:
                    cap = max(cap, level)
        return cap

    def is_block_unlocked(self, block, completed):
        return _unlocked(block, self.block_nodes.get(block['blockGuid'], set()), completed)

    def is_recipe_unlocked(self, recipe, completed):
        return _unlocked(recipe, self.recipe_nodes.get(recipe['machineRecipeGuid'], set()), completed)

    def is_craft_unlocked(self, craft, completed):
        return _unlocked(craft, self.craft_nodes.get(craft['craftRecipeGuid'], set()), completed)

    def can_ever_unlock_block(self, block):
        return self.is_block_unlocked(block, self.all_nodes)

    def can_ever_unlock_recipe(self, recipe):
        return self.is_recipe_unlocked(recipe, self.all_nodes)

    def blocks_unlocked_by(self, node):
        # G3 の研究順に書く「この研究の完了で解放されるもの」。解放の索引から引き、clearedActions を読み直さない
        # What completing this node unlocks, for the G3 research order; read from the index, not clearedActions again
        return [g for g, nodes in self.block_nodes.items() if node in nodes]

    def recipes_unlocked_by(self, node):
        return [g for g, nodes in self.recipe_nodes.items() if node in nodes]


def _unlocked(entry, unlocking_nodes, completed):
    return bool(entry.get('initialUnlocked')) or bool(unlocking_nodes & completed)
```

`tools/progression/model/reach_state.py`:
```python
#!/usr/bin/env python3
# 完了ノード集合の下で「今入手できるもの」を持つ状態。各アイテムに最初に入手できた経路を1つ残す
# State of what is obtainable under a completed-node set; keeps the first route that made each item obtainable


class ReachState:
    def __init__(self, data, index, completed):
        self.data = data
        self.index = index
        self.completed = frozenset(completed)
        self.have = {}
        self.fluids = {}

    def add_item(self, guid, route):
        if guid in self.have:
            return False
        self.have[guid] = route
        return True

    def add_fluid(self, guid, route):
        if guid in self.fluids:
            return False
        self.fluids[guid] = route
        return True

    def has_all(self, guids):
        return all(g in self.have for g in guids)

    def block_usable(self, block_guid):
        # 解放済みで建設コストが揃うブロックだけ置ける（電力・設置場所は見ない）
        # Only unlocked blocks whose construction cost is obtainable can be placed (power and placement are not modelled)
        block = self.data.blocks.get(block_guid)
        if block is None or not self.index.is_block_unlocked(block, self.completed):
            return False
        return self.has_all(c['itemGuid'] for c in block.get('requiredItems') or [])

    def usable_blocks_of_type(self, block_type):
        return [b for b in self.data.blocks.values() if b['blockType'] == block_type and self.block_usable(b['blockGuid'])]

    def highest_quality_tier(self):
        return max((tier for g, tier in self.data.quality_module_tiers.items() if g in self.have), default=0)
```

`tools/progression/model/basic_sources.py`:
```python
#!/usr/bin/env python3
# 採掘・拾得・鉱脈・ポンプ・手作業クラフト・研究報酬から入手できるものを足す
# Add what mining, pick-up, veins, pumps, hand crafting and research rewards make obtainable


def initial_sources(state):
    data = state.data
    for guid in data.initial_items:
        state.add_item(guid, '初期装備')
    # 拾えるmapObjectは道具なしで手に入る。earnItems は miningParam の下にある（map.yml）
    # PickUp map objects need no tool; earnItems live under miningParam (map.yml)
    for obj in data.map_objects:
        if obj.get('miningType') == 'PickUp':
            for earn in (obj.get('miningParam') or {}).get('earnItems') or []:
                state.add_item(earn['itemGuid'], f"拾得 {obj['mapObjectName']}")
    for node in state.completed:
        for guid in state.index.give_items.get(node, []):
            state.add_item(guid, f"研究報酬 {data.research_name(node)}")


def gather_step(state):
    data, changed = state.data, False
    for obj in data.map_objects:
        param = obj.get('miningParam') or {}
        if obj.get('miningType') == 'Mining' and any(t['toolItemGuid'] in state.have for t in param.get('miningTools') or []):
            for earn in param.get('earnItems') or []:
                changed |= state.add_item(earn['itemGuid'], f"採取 {obj['mapObjectName']}")
    for vein in data.item_veins:
        item = vein['veinParam']['itemGuid']
        if vein.get('handMiningType') == 'minable':
            tools = vein['handMiningParam']['handMiningTools']
            if any(t['toolItemGuid'] in state.have for t in tools):
                changed |= state.add_item(item, f"手掘り {vein['veinName']}")
        for block in data.blocks.values():
            settings = (block.get('blockParam') or {}).get('mineSettings') or []
            if any(s['itemGuid'] == item for s in settings) and state.block_usable(block['blockGuid']):
                changed |= state.add_item(item, f"採掘機 {block['name']} @ {vein['veinName']}")
    for block in data.blocks.values():
        generated = (block.get('blockParam') or {}).get('generateFluid') or []
        if generated and state.block_usable(block['blockGuid']):
            for fluid in generated:
                changed |= state.add_fluid(fluid['fluidGuid'], f"汲み上げ {block['name']}")
    for craft in data.crafts:
        if not state.index.is_craft_unlocked(craft, state.completed):
            continue
        if state.has_all(i['itemGuid'] for i in craft['requiredItems'] if i.get('itemGuid')):
            changed |= state.add_item(craft['craftResultItemGuid'], '手作業クラフト')
    return changed
```

`tools/progression/model/machine_sources.py`（機械レシピ。`levelInheritance` の Inherit / InheritUpToUnlocked、ウェハ等級の抽選、ダイシングの表を扱う）:
```python
#!/usr/bin/env python3
# 機械レシピの出力を足す。レベル継承（levelInheritance）とクリーンルームの抽選役割（成膜・ダイシング）を実装と同じ規則で展開する
# Add machine-recipe outputs, expanding level inheritance and the clean-room draw roles (deposition, dicing) by the runtime rules


def machine_step(state):
    data, changed = state.data, False
    draws = data.clean_room_rows('waferGradeDraws')
    dicings = data.clean_room_rows('dicings')
    for recipe in data.machine_recipes:
        if not _runnable(state, recipe):
            continue
        label = f"機械 {data.item_name(recipe['blockGuid'])}"
        if recipe['machineRecipeGuid'] in dicings:
            changed |= _dicing(state, recipe, dicings[recipe['machineRecipeGuid']], label)
        elif recipe.get('levelInheritance', 'None') != 'None':
            changed |= _inherit(state, recipe, label)
        else:
            changed |= _plain(state, recipe, label)
        if recipe['machineRecipeGuid'] in draws:
            changed |= _wafer_grades(state, recipe, draws[recipe['machineRecipeGuid']], label)
    return changed


def _runnable(state, recipe):
    if not state.index.is_recipe_unlocked(recipe, state.completed) or not state.block_usable(recipe['blockGuid']):
        return False
    return all(f['fluidGuid'] in state.fluids for f in recipe.get('inputFluids') or [])


def _emit(state, recipe, label, override):
    changed = False
    for i, out in enumerate(recipe['outputItems']):
        if out.get('percent', 1) <= 0:
            continue
        changed |= state.add_item(override.get(i, out['itemGuid']), label)
    for out in recipe.get('outputFluids') or []:
        changed |= state.add_fluid(out['fluidGuid'], label)
    return changed


def _plain(state, recipe, label):
    if not state.has_all(i['itemGuid'] for i in recipe['inputItems']):
        return False
    return _emit(state, recipe, label, {})


def _family_index(data, entries):
    found = [i for i, e in enumerate(entries) if data.is_family_base(e['itemGuid'])]
    return found[0] if len(found) == 1 else None


def _expand_family_input(state, recipe, label, expand):
    # 族基準の入力が1つに決まらなければ普通のレシピとして扱い、他の入力が揃うときだけ expand(族基準の位置) を回す
    # Treat the recipe as plain unless exactly one family-base input exists; run expand(base index) only when the other inputs are met
    src = _family_index(state.data, recipe['inputItems'])
    if src is None:
        return _plain(state, recipe, label)
    others = [x['itemGuid'] for i, x in enumerate(recipe['inputItems']) if i != src]
    if not state.has_all(others):
        return False
    return expand(src)


def _inherit(state, recipe, label):
    # 継承元レベルLの変種が手元にあり（上限つきなら製作上限以下）、他の入力が揃えば継承先のLv Lが出る
    # With source variant L in hand (at or below the craft cap when capped) and other inputs met, target Lv L comes out
    dst = _family_index(state.data, recipe['outputItems'])
    if dst is None:
        return _plain(state, recipe, label)
    return _expand_family_input(state, recipe, label, lambda src: _emit_inherited(state, recipe, label, src, dst))


def _emit_inherited(state, recipe, label, src, dst):
    data = state.data
    src_variants = data.variants_of(recipe['inputItems'][src]['itemGuid'])
    dst_base = recipe['outputItems'][dst]['itemGuid']
    dst_variants = data.variants_of(dst_base)
    cap = len(src_variants)
    if recipe['levelInheritance'] == 'InheritUpToUnlocked':
        cap = min(cap, state.index.craft_cap(dst_base, state.completed))
    changed = False
    for level in range(1, min(cap, len(dst_variants)) + 1):
        if src_variants[level - 1] in state.have:
            changed |= _emit(state, recipe, f"{label} (Lv{level})", {dst: dst_variants[level - 1]})
    return changed


def _dicing(state, recipe, row, label):
    return _expand_family_input(state, recipe, label, lambda src: _emit_dies(state, recipe, row, label, src))


def _emit_dies(state, recipe, row, label, src):
    # ダイシングはグレードgのウェハをダイ表 gradeDieTables[g-1] の率>0 のレベルへ振り分ける（DicingOutputRewriter）
    # Dicing splits a grade-g wafer into the die levels whose gradeDieTables[g-1] rate is above zero (DicingOutputRewriter)
    data = state.data
    wafers = data.variants_of(recipe['inputItems'][src]['itemGuid'])
    dies = data.variants_of(row['dieItemGuid'])
    changed = False
    for grade, wafer in enumerate(wafers, 1):
        if wafer not in state.have or grade > len(row['gradeDieTables']):
            continue
        for level, rate in enumerate(row['gradeDieTables'][grade - 1]['levelRates'], 1):
            if rate > 0 and level <= len(dies):
                changed |= state.add_item(dies[level - 1], f"{label} (G{grade}→Lv{level})")
        if data.max_class_value('dieLossRate') > 0:
            changed |= state.add_item(row['failedDieItemGuid'], f"{label} (損失)")
    return changed


def _wafer_grades(state, recipe, row, label):
    # 成膜の上限グレード＝装着できる品質モジュールの最高tier+1。昇格確率が0なら G1 だけ（ChipYieldFormula）
    # Deposition grade cap = highest equippable quality tier + 1; zero promotion chance means G1 only (ChipYieldFormula)
    data = state.data
    param = data.blocks[recipe['blockGuid']].get('blockParam') or {}
    if not (param.get('allowQualityModule') and param.get('moduleSlotCount', 0) > 0):
        return False
    if row['maxPromotionChance'] <= 0 or data.max_class_value('qualityMultiplier') <= 0:
        return False
    if not state.has_all(i['itemGuid'] for i in recipe['inputItems']):
        return False
    grades = data.variants_of(row['gradedWaferItemGuid'])
    top = min(len(grades), state.highest_quality_tier() + 1)
    changed = False
    for grade in range(2, top + 1):
        changed |= state.add_item(grades[grade - 1], f"{label} (昇格G{grade})")
    return changed
```

`tools/progression/model/orbital_sources.py`（発射台の `orbitalPayloads` の全行と、エレベーター拠点の積荷）:
```python
#!/usr/bin/env python3
# 軌道到着の成果（items.json の orbitalPayloads）を足す。打ち口は発射台（資材が揃う）と宇宙エレベーター拠点（ケーブルが揃う）
# Add orbital arrival outputs (items.json orbitalPayloads); launch sites are the launch pad (materials met) and the elevator base (cable met)


def orbital_step(state):
    data, changed = state.data, False
    for pad in state.usable_blocks_of_type('RocketLaunchPad'):
        materials = [m['itemGuid'] for m in (pad.get('blockParam') or {}).get('launchMaterials') or []]
        if not state.has_all(materials):
            continue
        # 発射台は積荷表に載る積荷を全部扱う（RocketLaunchPadSlotLayout.Payloads）
        # The launch pad handles every payload in the table (RocketLaunchPadSlotLayout.Payloads)
        for payload in data.orbital_payloads.values():
            changed |= _arrive(state, payload, f"打ち上げ {pad['name']}")
    for base in state.usable_blocks_of_type('SpaceElevatorBase'):
        param = base.get('blockParam') or {}
        if param.get('cableItemGuid') not in state.have:
            continue
        for guid in param.get('carriedPayloadItemGuids') or []:
            if guid in data.orbital_payloads:
                changed |= _arrive(state, data.orbital_payloads[guid], f"エレベーター {base['name']}")
    return changed


def _arrive(state, payload, label):
    if payload['payloadItemGuid'] not in state.have:
        return False
    changed = False
    for out in payload['arrivalOutputs']:
        changed |= state.add_item(out['itemGuid'], f"{label}: {state.data.item_name(payload['payloadItemGuid'])}")
    return changed
```

`tools/progression/model/fixpoint.py`:
```python
#!/usr/bin/env python3
# 完了ノード集合を固定して、入手できるアイテム・液体を不動点まで広げる
# With the completed-node set fixed, grow obtainable items and fluids to a fixpoint
from model.basic_sources import gather_step, initial_sources
from model.machine_sources import machine_step
from model.orbital_sources import orbital_step
from model.reach_state import ReachState

STEPS = (gather_step, machine_step, orbital_step)


def obtainable(data, index, completed):
    state = ReachState(data, index, completed)
    initial_sources(state)
    changed = True
    while changed:
        changed = False
        for step in STEPS:
            changed |= step(state)
    return state
```

`tools/progression/model/research_walk.py`:
```python
#!/usr/bin/env python3
# 初期状態から研究を完了できる順に進める（前提が完了し、コストが入手できるノードを波ごとに全部完了する）
# Walk research from the initial state: each wave completes every node whose prerequisites are done and costs are obtainable
from model.fixpoint import obtainable


class WalkStep:
    def __init__(self, wave, guid, cost_routes):
        self.wave = wave
        self.guid = guid
        self.cost_routes = cost_routes


class WalkResult:
    def __init__(self, steps, final_state):
        self.steps = steps
        self.final_state = final_state
        self.completed = {s.guid for s in steps}


def cost_guids(node):
    return [c['itemGuid'] for c in node.get('consumeItems') or []]


def missing_prerequisites(node, completed):
    # 自分自身を前提に書いたものは飛ばす（ResearchDataStore.ArePrerequisitesCompleted と同じ）
    # A node listing itself as a prerequisite is skipped (same as ResearchDataStore.ArePrerequisitesCompleted)
    guid = node['researchNodeGuid']
    return [p for p in node.get('prevResearchNodeGuids') or [] if p != guid and p not in completed]


def missing_costs(node, have):
    return [g for g in cost_guids(node) if g not in have]


def walk(data, index):
    completed, steps, wave = set(), [], 0
    while True:
        state = obtainable(data, index, completed)
        ready = [n for n in data.research
                 if n['researchNodeGuid'] not in completed and not missing_prerequisites(n, completed)
                 and not missing_costs(n, state.have)]
        if not ready:
            return WalkResult(steps, state)
        wave += 1
        for node in ready:
            routes = {g: state.have[g] for g in cost_guids(node)}
            steps.append(WalkStep(wave, node['researchNodeGuid'], routes))
            completed.add(node['researchNodeGuid'])
```

研究の祖先集合は G6 の上流の需要（Task 19 `recipe_rates`）も使うので、`tools/` 直下の共通パッケージに置く（判断記録 A32）。`mkdir -p $MW/tools/master_common && touch $MW/tools/master_common/__init__.py` を実行する（`__init__.py` は空）。

`tools/master_common/research_ancestors.py`:
```python
#!/usr/bin/env python3
# 研究の祖先集合（自分を含む）。G2 の暗黙の前提と G6 の上流の需要が同じ導出を使う。自分を除く集合は呼び側が {guid} を引く
# Research ancestor sets including self, shared by G2 implicit prerequisites and G6 upstream demand; callers subtract {guid} to exclude self
# 未定義の前提は祖先に入れない（G2 は到達不能として、G6 は lookup_failed として別に拾う）。循環は辿り直さずに打ち切る
# Unknown prerequisites are left out (G2 reports them as unreachable, G6 as lookup_failed); cycles are cut instead of revisited


def ancestors_including_self(research_rows):
    by_guid = {r['researchNodeGuid']: r for r in research_rows}
    result = {}

    def visit(guid, stack):
        if guid in result:
            return result[guid]
        if guid in stack or guid not in by_guid:
            return set()
        found = {guid}
        for prev in by_guid[guid].get('prevResearchNodeGuids') or []:
            found |= visit(prev, stack | {guid})
        result[guid] = found
        return found

    for row in research_rows:
        visit(row['researchNodeGuid'], frozenset())
    return result
```

- [ ] **Step 3: `audits/` パッケージ（問題の検出）を置く**
  - `mkdir -p $MW/tools/progression/audits && touch $MW/tools/progression/audits/__init__.py` を実行する（`__init__.py` は空）。
  - `progress.py` は研究の詰み・コスト0・暗黙の前提・目標を見る。`unreachable_costs` は旧 API で、sync スクリプトが使う。
  - `catalog.py` は定義の孤児を見る。
  - `item_consumers.py` は「使い道がある」の定義を持つ。

`tools/progression/audits/progress.py`:
```python
#!/usr/bin/env python3
# 研究の進行に関する検査: 到達できない研究・目標・祖先だけでは払えないコスト（ADR 0033 の暗黙の前提）
# Progress audits: unreachable research, goals, and costs not payable with ancestors alone (ADR 0033 implicit prerequisites)
from master_common.research_ancestors import ancestors_including_self
from model.fixpoint import obtainable
from model.research_walk import missing_costs, missing_prerequisites


class Problem:
    def __init__(self, category, key, message):
        self.category = category
        self.key = key
        self.message = message


def unreachable_research(data, result):
    problems = []
    for node in data.research:
        guid = node['researchNodeGuid']
        if guid in result.completed:
            continue
        missing_prev = [data.research_name(p) for p in missing_prerequisites(node, result.completed)]
        missing_cost = [data.item_name(g) for g in missing_costs(node, result.final_state.have)]
        problems.append(Problem('unreachableResearch', guid,
                                f"{node['researchNodeName']}: 前提未完了={missing_prev} 入手できないコスト={missing_cost}"))
    return problems


def zero_cost_research(data):
    # コスト0の研究は押すだけで完了し、旧セーブで素通りの元になる（裁定 2026-10-25 コスト0研究のリセット）
    # A zero-cost node completes on a click and lets old saves skip content (ruling 2026-10-25 on resetting zero-cost research)
    return [Problem('zeroCostResearch', n['researchNodeGuid'], f"{n['researchNodeName']}: consumeItems が空")
            for n in data.research if not n.get('consumeItems')]


def goal_failures(data, result, goals):
    # 目標は許可リストで黙らせられない（届かなければ必ず失敗）
    # Goals can never be allowlisted; an unmet goal always fails
    failures = []
    for guid in goals['researchGuids']:
        if guid not in data.research_by_guid:
            failures.append(f"目標の研究 {guid} がマスタに無い")
        elif guid not in result.completed:
            failures.append(f"目標の研究 {data.research_name(guid)} に届かない")
    for guid in goals['itemGuids']:
        if guid not in result.final_state.have:
            failures.append(f"目標のアイテム {data.item_name(guid)} を入手できない")
    return failures


def implicit_prerequisites(data, index):
    # 祖先だけを完了した状態でコストが揃わないノード。別枝の研究を暗黙に要求している
    # Nodes whose cost is not met with only their ancestors completed; they implicitly require another branch
    problems = []
    for guid, ancestors in ancestors_including_self(data.research).items():
        state = obtainable(data, index, ancestors - {guid})
        node = data.research_by_guid[guid]
        for item_guid in missing_costs(node, state.have):
            problems.append(Problem('implicitPrerequisites', f"{guid}|{item_guid}",
                                    f"{node['researchNodeName']}: コスト {data.item_name(item_guid)} が祖先だけでは入手できない"))
    return problems


def unreachable_costs(data, index):
    # sync_machine_recipe_unlocks.py が使う旧 API の形（ノード名, アイテム名）
    # The legacy (node name, item name) shape used by sync_machine_recipe_unlocks.py
    result = []
    for p in implicit_prerequisites(data, index):
        node_guid, item_guid = p.key.split('|')
        result.append((data.research_name(node_guid), data.item_name(item_guid)))
    return result
```

`tools/progression/audits/catalog.py`:
```python
#!/usr/bin/env python3
# 定義の棚卸し: 入手できないアイテム・解放されないブロック・動かせないレシピ・使い道の無いアイテム・液体の口が無い機械
# Catalog audits: unobtainable items, never-unlocked blocks, unrunnable recipes, unused items, machines lacking fluid ports
from audits.progress import Problem
from audits.item_consumers import consumed_item_guids


def unobtainable_items(data, result):
    have = result.final_state.have
    return [Problem('unobtainableItems', g, f"{i['name']}: どの経路でも入手できない")
            for g, i in data.items.items() if g not in have]


def never_unlocked_blocks(data, index, result):
    problems = []
    for guid, block in data.blocks.items():
        if not index.can_ever_unlock_block(block):
            problems.append(Problem('neverUnlockedBlocks', guid, f"{block['name']}: どの研究も unlockBlock しない"))
        elif not result.final_state.block_usable(guid):
            problems.append(Problem('neverUnlockedBlocks', guid, f"{block['name']}: 解放はされるが建設コストが入手できない"))
    return problems


def dead_recipes(data, index):
    problems = []
    for recipe in data.machine_recipes:
        guid, block = recipe['machineRecipeGuid'], data.blocks.get(recipe['blockGuid'])
        if not index.can_ever_unlock_recipe(recipe):
            problems.append(Problem('deadRecipes', guid, f"{_describe(data, recipe)}: どの研究も解放しない"))
        elif block is not None and not index.can_ever_unlock_block(block):
            problems.append(Problem('deadRecipes', guid, f"{_describe(data, recipe)}: 機械 {block['name']} が解放されない"))
    return problems


def fluid_ports(data):
    # 液体は手で入れられないので、液体を使うレシピの機械には入口/出口の液体コネクタが要る
    # Fluids cannot be hand-inserted, so a machine running a fluid recipe needs inflow/outflow fluid connectors
    problems = []
    for recipe in data.machine_recipes:
        connectors = (data.blocks[recipe['blockGuid']].get('blockParam') or {}).get('fluidInventoryConnectors') or {}
        if recipe.get('inputFluids') and not connectors.get('inflowConnects'):
            problems.append(Problem('fluidPorts', recipe['machineRecipeGuid'], f"{_describe(data, recipe)}: 液体の入口が無い"))
        if recipe.get('outputFluids') and not connectors.get('outflowConnects'):
            problems.append(Problem('fluidPorts', recipe['machineRecipeGuid'], f"{_describe(data, recipe)}: 液体の出口が無い"))
    return problems


def unused_items(data):
    used = consumed_item_guids(data)
    return [Problem('unusedItems', g, f"{i['name']}: どこにも消費・設置・装備されない")
            for g, i in data.items.items() if g not in used]


def _describe(data, recipe):
    ins = '+'.join(data.item_name(i['itemGuid']) for i in recipe['inputItems'])
    outs = '+'.join(data.item_name(o['itemGuid']) for o in recipe['outputItems'])
    return f"{data.item_name(recipe['blockGuid'])}: {ins} -> {outs}"
```

`tools/progression/audits/item_consumers.py`:
```python
#!/usr/bin/env python3
# アイテムの「使い道」の集合: レシピ入力・研究コスト・建設コスト・設置・燃料・道具・積荷・モジュール・継承の変種
# The set of items with a use: recipe inputs, research costs, build costs, placement, fuel, tools, payloads, modules and inherited variants

# blockParam 下でアイテムを「出す」側のキー。使い道には数えない
# blockParam keys that produce items; they do not count as a use
OUTPUT_KEYS = {'mineSettings'}


def consumed_item_guids(data):
    used = set(data.initial_items)
    for recipe in data.machine_recipes:
        used.update(i['itemGuid'] for i in recipe['inputItems'])
    for craft in data.crafts:
        used.update(i['itemGuid'] for i in craft['requiredItems'] if i.get('itemGuid'))
    for node in data.research:
        used.update(c['itemGuid'] for c in node.get('consumeItems') or [])
    for block in data.blocks.values():
        used.update(c['itemGuid'] for c in block.get('requiredItems') or [])
        if block.get('itemGuid'):
            used.add(block['itemGuid'])
        _collect_guids(block.get('blockParam') or {}, data.items, used)
    for obj in data.map_objects:
        used.update(t['toolItemGuid'] for t in (obj.get('miningParam') or {}).get('miningTools') or [])
    for vein in data.item_veins:
        used.update(t['toolItemGuid'] for t in (vein.get('handMiningParam') or {}).get('handMiningTools') or [])
    used.update(data.orbital_payloads)
    used.update(data.quality_module_tiers)
    used.update(data.extra_consumer_guids)
    # 継承元の基準が使われていれば、その変種も同じ入口で使われる
    # When a family base is consumed, its variants are consumed through the same input
    for base, variants in data.level_variants.items():
        if base in used or any(v in used for v in variants):
            used.update(variants)
    return used


def _collect_guids(value, items, used):
    if isinstance(value, dict):
        for key, child in value.items():
            if key not in OUTPUT_KEYS:
                _collect_guids(child, items, used)
    elif isinstance(value, list):
        for child in value:
            _collect_guids(child, items, used)
    elif isinstance(value, str) and value in items:
        used.add(value)
```

- [ ] **Step 4: 判定・印字・入口と、目標・許可リストを置く**
  - 許可リストには Tier9 の中身（半導体・打ち上げ・核融合・エレベーター・最終研究）を載せない。載せてよいのは Tier9 の範囲外にもとからある問題だけで、計 19 件。
    - 暗黙の前提 10 件。ADR 0033 の改番時点から残るもの。
    - タングステン系 3 アイテムと、砂→ガラス→真空管の旧系統のうちガラス・真空管。
  - 目標は「最終研究」`01a167e9-21f6-7d26-a990-e7d6d78750d0`（S3 plan「共通の GUID・参照表」）と、アイテム「反物質爆弾」`01a162ab-07ba-73c1-9dd6-38e1e867663b`。

`tools/progression/allowlist.py`（未知のカテゴリは `SystemExit` で止める。検出されなくなった許可は stale として、理由が空の許可は reasonless として、どちらも FAIL にする）:
```python
#!/usr/bin/env python3
# 許可リスト（理由つき）と検出結果を突き合わせる。載っていない問題・陳腐化した登録・理由の無い登録はどれも失敗
# Match detected problems against the reasoned allowlist; unlisted problems, stale entries and reasonless entries all fail
import json

CATEGORIES = ('unreachableResearch', 'zeroCostResearch', 'implicitPrerequisites', 'unobtainableItems', 'neverUnlockedBlocks',
              'deadRecipes', 'fluidPorts', 'unusedItems')


class Verdict:
    def __init__(self, unexpected, allowed, stale, invalid):
        self.unexpected = unexpected
        self.allowed = allowed
        self.stale = stale
        self.invalid = invalid

    @property
    def failed(self):
        return bool(self.unexpected or self.stale or self.invalid)


def load_allowlist(path):
    with open(path, encoding='utf-8') as f:
        doc = json.load(f)
    unknown = [k for k in doc if not k.startswith('_') and k not in CATEGORIES]
    if unknown:
        raise SystemExit(f'ERROR allowlist has unknown categories {unknown}')
    return {c: doc.get(c) or {} for c in CATEGORIES}


def judge(problems, allowlist):
    unexpected, allowed = [], []
    seen = set()
    for p in problems:
        seen.add((p.category, p.key))
        if p.key in allowlist[p.category]:
            allowed.append((p, allowlist[p.category][p.key]))
        else:
            unexpected.append(p)
    stale = [(c, k) for c in CATEGORIES for k in allowlist[c] if (c, k) not in seen]
    invalid = [(c, k) for c in CATEGORIES for k, reason in allowlist[c].items() if not str(reason).strip()]
    return Verdict(unexpected, allowed, stale, invalid)
```

`tools/progression/report.py`:
```python
#!/usr/bin/env python3
# 検査結果の印字と研究順の書き出し。黙って exit 0 しないよう、通った経路と許可した詰みを必ず出す
# Print results and write the research order; always print the route taken and the allowed dead ends so a pass is never silent
import json


def print_route(data, result):
    print(f'== ROUTE: {len(result.steps)}/{len(data.research)} research nodes in {max((s.wave for s in result.steps), default=0)} waves')
    for step in result.steps:
        costs = ', '.join(f"{data.item_name(g)}<{route}>" for g, route in step.cost_routes.items())
        print(f'  [wave {step.wave}] {data.research_name(step.guid)}: {costs}')


def print_verdict(verdict, goal_failures):
    print(f'== ALLOWED ({len(verdict.allowed)})')
    for problem, reason in verdict.allowed:
        print(f'  ALLOW {problem.category} {problem.message} -- {reason}')
    for problem in verdict.unexpected:
        print(f'FAIL {problem.category} {problem.key} {problem.message}')
    for category, key in verdict.stale:
        print(f'FAIL stale-allowlist {category} {key}: 検出されなくなったので許可リストから消す')
    for category, key in verdict.invalid:
        print(f'FAIL reasonless-allowlist {category} {key}: 理由が空')
    for message in goal_failures:
        print(f'FAIL goal {message}')
    total = len(verdict.unexpected) + len(verdict.stale) + len(verdict.invalid) + len(goal_failures)
    print(f'== RESULT: {"FAIL" if total else "PASS"} ({total} failures, {len(verdict.allowed)} allowed)')


def write_order(path, data, index, result):
    # G3（Unity の研究ウォーク）が読む。波の順に並べた研究GUIDと、各段で解放されるブロック・機械レシピ（解放の索引から引く）
    # Read by G3 (the Unity research walk): research GUIDs in wave order and the blocks/recipes each step unlocks (from the unlock index)
    rows = [{'researchNodeGuid': step.guid, 'name': data.research_name(step.guid), 'wave': step.wave,
             'unlockBlockGuids': index.blocks_unlocked_by(step.guid),
             'unlockMachineRecipeGuids': index.recipes_unlocked_by(step.guid)}
            for step in result.steps]
    with open(path, 'w', encoding='utf-8') as f:
        json.dump({'order': rows}, f, ensure_ascii=False, indent=2)
        f.write('\n')
```

`tools/progression/check.py`:
```python
#!/usr/bin/env python3
# 出荷マスタの到達性ゲート（G2）。初期状態から目標の研究まで進めるか、定義に詰み・孤児が無いかを絶対判定する
# Shipped-master reachability gate (G2): absolute check that the goal research is reachable from the start and nothing is dead or orphaned
# 終了コード: 0=全部通過 / 1=許可リスト外の問題・陳腐化した許可・目標未達のどれか
# Exit code: 0 = all pass / 1 = an unlisted problem, a stale allowlist entry or an unmet goal
import argparse
import json
import os
import sys

# tools/progression と、共通部品 master_common を置く tools/ の両方を import できるようにする
# Make both tools/progression and tools/ (home of the shared master_common package) importable
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
from audits import catalog as audits_catalog  # noqa: E402
from audits import progress as audits_progress  # noqa: E402
from allowlist import judge, load_allowlist  # noqa: E402
from model.master_data import MasterData  # noqa: E402
from report import print_route, print_verdict, write_order  # noqa: E402
from model.research_walk import walk  # noqa: E402
from model.unlock_index import UnlockIndex  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
DEFAULT_MASTER = os.path.join(HERE, '..', '..', 'server_v8', 'mods', 'moorestechAlphaMod_8', 'master')


def parse_args(argv):
    parser = argparse.ArgumentParser(description='moorestech master reachability gate (G2)')
    parser.add_argument('--master-dir', default=DEFAULT_MASTER)
    parser.add_argument('--allowlist', default=os.path.join(HERE, 'allowlist.json'))
    parser.add_argument('--goals', default=os.path.join(HERE, 'goals.json'))
    parser.add_argument('--emit-order', default=None, help='G3 が読む研究順 JSON の書き出し先')
    return parser.parse_args(argv)


def main(argv):
    args = parse_args(argv)
    data = MasterData(args.master_dir)
    index = UnlockIndex(data.research, None)
    with open(args.goals, encoding='utf-8') as f:
        goals = json.load(f)
    result = walk(data, index)
    problems = (audits_progress.unreachable_research(data, result)
                + audits_progress.zero_cost_research(data)
                + audits_progress.implicit_prerequisites(data, index)
                + audits_catalog.unobtainable_items(data, result)
                + audits_catalog.never_unlocked_blocks(data, index, result)
                + audits_catalog.dead_recipes(data, index)
                + audits_catalog.fluid_ports(data)
                + audits_catalog.unused_items(data))
    verdict = judge(problems, load_allowlist(args.allowlist))
    goal_failures = audits_progress.goal_failures(data, result, goals)
    print_route(data, result)
    print_verdict(verdict, goal_failures)
    if args.emit_order:
        write_order(args.emit_order, data, index, result)
    return 1 if verdict.failed or goal_failures else 0


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
```

`tools/progression/goals.json`:
```json
{
  "_comment": "到達必須の目標。許可リストでは黙らせられない。最終研究は S3 裁定の仮の終点（反物質爆弾10個消費）/ Mandatory goals; never allowlistable",
  "researchGuids": ["01a167e9-21f6-7d26-a990-e7d6d78750d0"],
  "itemGuids": ["01a162ab-07ba-73c1-9dd6-38e1e867663b"]
}
```

`tools/progression/allowlist.json`:
```json
{
  "_comment": "許可した問題と理由。Tier9 の中身（半導体・打ち上げ・核融合・エレベーター・最終研究）に属するものは載せない。検出されなくなった行は stale として失敗するので消す / Allowed problems with reasons; never list Tier9 content",
  "implicitPrerequisites": {
    "019e0643-381b-7078-b7cc-8a58adcfd674|019e06db-bdad-745a-af33-fc1a063a2c5c": "ADR 0033 の改番時点から残る暗黙の前提（別枝の研究で解放される素材をコストに使う）。Tier9 範囲外。直すときは前提を足すかコストを替えて、この行を消す",
    "019e07b1-9c66-7779-8c1c-4d354812265d|019e0a0a-68ba-777e-81d6-40c8b2cbe2d4": "ADR 0033 の改番時点から残る暗黙の前提（別枝の研究で解放される素材をコストに使う）。Tier9 範囲外。直すときは前提を足すかコストを替えて、この行を消す",
    "019e07b1-9c66-7779-8c1c-4d354812265d|019e0a92-1907-707a-8625-630d86a076ee": "ADR 0033 の改番時点から残る暗黙の前提（別枝の研究で解放される素材をコストに使う）。Tier9 範囲外。直すときは前提を足すかコストを替えて、この行を消す",
    "019e07b2-4ccd-77bd-a14e-d230704645e4|019e3f98-1e4e-709d-b30d-eef9ee0ce419": "ADR 0033 の改番時点から残る暗黙の前提（別枝の研究で解放される素材をコストに使う）。Tier9 範囲外。直すときは前提を足すかコストを替えて、この行を消す",
    "019e07b2-4ccd-77bd-a14e-d230704645e4|019e0a82-9a7b-70fc-bd01-d397c326d6ac": "ADR 0033 の改番時点から残る暗黙の前提（別枝の研究で解放される素材をコストに使う）。Tier9 範囲外。直すときは前提を足すかコストを替えて、この行を消す",
    "019e07b8-c4ce-7033-b291-10792dd7949e|019e3f98-1e4e-709d-b30d-eef9ee0ce419": "ADR 0033 の改番時点から残る暗黙の前提（別枝の研究で解放される素材をコストに使う）。Tier9 範囲外。直すときは前提を足すかコストを替えて、この行を消す",
    "019e07b8-c4ce-7033-b291-10792dd7949e|019e0a82-9a7b-70fc-bd01-d397c326d6ac": "ADR 0033 の改番時点から残る暗黙の前提（別枝の研究で解放される素材をコストに使う）。Tier9 範囲外。直すときは前提を足すかコストを替えて、この行を消す",
    "019ea6ef-bf58-729a-bbfc-869b4f5032ca|019e3f98-1e4e-709d-b30d-eef9ee0ce419": "ADR 0033 の改番時点から残る暗黙の前提（別枝の研究で解放される素材をコストに使う）。Tier9 範囲外。直すときは前提を足すかコストを替えて、この行を消す",
    "019ea6ef-bf58-729a-bbfc-869b4f5032ca|019e0a82-9a7b-70fc-bd01-d397c326d6ac": "ADR 0033 の改番時点から残る暗黙の前提（別枝の研究で解放される素材をコストに使う）。Tier9 範囲外。直すときは前提を足すかコストを替えて、この行を消す",
    "019ea6d1-edc4-7694-8e3c-3f366d6437ed|019e3f98-1e4e-709d-b30d-eef9ee0ce419": "ADR 0033 の改番時点から残る暗黙の前提（別枝の研究で解放される素材をコストに使う）。Tier9 範囲外。直すときは前提を足すかコストを替えて、この行を消す"
  },
  "unobtainableItems": {
    "019e07bb-1786-71d4-ad2f-a632bc476e59": "タングステン系はどこからも入手・消費されない旧定義。Tier9 範囲外の既存。消すか使い道を足すときにこの行を消す",
    "019e0a07-52d1-7135-9c80-ed155da042b5": "タングステン系はどこからも入手・消費されない旧定義。Tier9 範囲外の既存。消すか使い道を足すときにこの行を消す",
    "019e07bc-8159-706a-8293-b184859af645": "砂→ガラス→真空管の旧系統はレシピが無い既存定義。Tier9 範囲外。Tier9 で砂の作り方が入っても、ガラス・真空管は残る",
    "019e07bc-4255-70da-a88f-fc726f483e2b": "砂→ガラス→真空管の旧系統はレシピが無い既存定義。Tier9 範囲外。Tier9 で砂の作り方が入っても、ガラス・真空管は残る"
  },
  "unusedItems": {
    "019e0a66-bc06-73e7-b9d3-ab225c468646": "タングステン系はどこからも入手・消費されない旧定義。Tier9 範囲外の既存。消すか使い道を足すときにこの行を消す",
    "019e07bb-1786-71d4-ad2f-a632bc476e59": "タングステン系はどこからも入手・消費されない旧定義。Tier9 範囲外の既存。消すか使い道を足すときにこの行を消す",
    "019e0a07-52d1-7135-9c80-ed155da042b5": "タングステン系はどこからも入手・消費されない旧定義。Tier9 範囲外の既存。消すか使い道を足すときにこの行を消す",
    "019e07bc-8159-706a-8293-b184859af645": "砂→ガラス→真空管の旧系統はレシピが無い既存定義。Tier9 範囲外。Tier9 で砂の作り方が入っても、ガラス・真空管は残る",
    "019e07bc-4255-70da-a88f-fc726f483e2b": "砂→ガラス→真空管の旧系統はレシピが無い既存定義。Tier9 範囲外。Tier9 で砂の作り方が入っても、ガラス・真空管は残る"
  }
}
```

- [ ] **Step 5: 壊した fixture で落ちることを確かめる自己テストを置く**
  - マスタ repo には既存のテストが無い。そのため、追加の依存なしで動く標準ライブラリの `unittest` を使う。
  - `fixture_master.py` は最小の鎖を組む。拾える鉱石 → 研究 r-start が機械 b-machine を解放 → 板 → 部品 → 目標アイテム。研究 r-goal が部品と目標アイテムを消費する。
  - 各テストは、この鎖の 1 か所だけを壊して exit 1 になることを見る。

`tools/progression/tests/fixture_master.py`:
```python
#!/usr/bin/env python3
# 到達性ゲートのテスト用の最小マスタ。拾得→研究→機械→目標の研究が1本でつながる形を作り、テストが1か所ずつ壊す
# Minimal master for the reachability gate tests: pick-up -> research -> machine -> goal research in one chain; each test breaks one spot
import json
import os

ORE, PLATE, PART, GOAL_ITEM = 'i-ore', 'i-plate', 'i-part', 'i-goal'
MACHINE, MACHINE_ITEM = 'b-machine', 'i-machine'
R_START, R_GOAL = 'r-start', 'r-goal'


def item(guid, name):
    return {'itemGuid': guid, 'name': name}


def recipe(guid, inputs, outputs, inherit='None'):
    return {'machineRecipeGuid': guid, 'blockGuid': MACHINE, 'time': 1, 'initialUnlocked': True,
            'levelInheritance': inherit, 'inputFluids': [], 'outputFluids': [],
            'inputItems': [{'itemGuid': g, 'count': 1} for g in inputs],
            'outputItems': [{'itemGuid': g, 'count': 1, 'percent': 1} for g in outputs]}


def node(guid, prev, costs, actions):
    return {'researchNodeGuid': guid, 'researchNodeName': guid, 'prevResearchNodeGuids': prev,
            'consumeItems': [{'itemGuid': g, 'itemCount': 1} for g in costs], 'clearedActions': actions}


def unlock_block(*guids):
    return {'gameActionType': 'unlockBlock', 'gameActionParam': {'unlockBlockGuids': list(guids)}}


def base_master():
    # 小石を拾う→研究で機械を解放→機械で板→部品→目標の研究が部品と目標アイテムを消費する
    # Pick up ore -> research unlocks the machine -> plate -> part -> the goal research consumes the part and the goal item
    return {
        'items': {'data': [item(ORE, '鉱石'), item(PLATE, '板'), item(PART, '部品'), item(GOAL_ITEM, '目標'),
                           item(MACHINE_ITEM, '機械')], 'itemStackLevelTables': []},
        'blocks': {'data': [{'blockGuid': MACHINE, 'name': '機械', 'blockType': 'ElectricMachine', 'itemGuid': MACHINE_ITEM,
                             'initialUnlocked': False, 'requiredItems': [{'itemGuid': ORE, 'count': 1}],
                             'blockParam': {'allowQualityModule': True, 'moduleSlotCount': 1}}]},
        'fluids': {'data': []},
        'machineRecipes': {'data': [recipe('m-plate', [ORE], [PLATE]), recipe('m-part', [PLATE], [PART]),
                                    recipe('m-goal', [PART], [GOAL_ITEM])]},
        'craftRecipes': {'data': [{'craftRecipeGuid': 'c-machine', 'craftResultItemGuid': MACHINE_ITEM,
                                   'initialUnlocked': True, 'requiredItems': [{'itemGuid': ORE, 'count': 1}]}]},
        'research': {'data': [node(R_START, [], [ORE], [unlock_block(MACHINE)]),
                              node(R_GOAL, [R_START], [PART, GOAL_ITEM], [])]},
        'map': {'mapObjects': [{'mapObjectName': '小石', 'miningType': 'PickUp',
                                'miningParam': {'earnItems': [{'itemGuid': ORE, 'minCount': 1, 'maxCount': 1}]}}],
                'mapVeins': []},
        'train': {'trainCars': []},
        'buildMenu': {'connectTools': []},
    }


def write_master(root, master):
    os.makedirs(root, exist_ok=True)
    for name, doc in master.items():
        with open(os.path.join(root, f'{name}.json'), 'w', encoding='utf-8') as f:
            json.dump(doc, f, ensure_ascii=False)
    return root


def write_json(path, doc):
    with open(path, 'w', encoding='utf-8') as f:
        json.dump(doc, f, ensure_ascii=False)
    return path


def goals():
    return {'researchGuids': [R_GOAL], 'itemGuids': [GOAL_ITEM]}
```

`tools/progression/tests/test_gate_basics.py`（健全なら通る／レシピ欠落・解放されないブロック・使い道なし・コスト0・暗黙の前提はそれぞれ落ちる／許可すれば通って印字される／stale・理由なしの許可は落ちる／goal は許可できない）:
```python
#!/usr/bin/env python3
# 到達性ゲートの基本: 正しい最小マスタは通り、1か所壊すと exit 1、許可リストの陳腐化・理由なしも exit 1
# Gate basics: a sound minimal master passes, breaking one spot exits 1, and stale or reasonless allowlist entries exit 1
import contextlib
import io
import os
import sys
import tempfile
import unittest

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
import check  # noqa: E402
import fixture_master as fx  # noqa: E402


def run_gate(master, allowlist=None):
    with tempfile.TemporaryDirectory() as tmp:
        master_dir = fx.write_master(os.path.join(tmp, 'master'), master)
        allow = fx.write_json(os.path.join(tmp, 'allow.json'), allowlist or {})
        goals = fx.write_json(os.path.join(tmp, 'goals.json'), fx.goals())
        out = io.StringIO()
        with contextlib.redirect_stdout(out):
            code = check.main(['--master-dir', master_dir, '--allowlist', allow, '--goals', goals])
        return code, out.getvalue()


class GateBasicsTest(unittest.TestCase):
    def test_sound_master_passes_and_prints_route(self):
        code, out = run_gate(fx.base_master())
        self.assertEqual(0, code, out)
        self.assertIn('== ROUTE: 2/2', out)
        self.assertIn('== RESULT: PASS', out)

    def test_missing_recipe_fails(self):
        master = fx.base_master()
        master['machineRecipes']['data'] = [r for r in master['machineRecipes']['data'] if r['machineRecipeGuid'] != 'm-part']
        code, out = run_gate(master)
        self.assertEqual(1, code)
        self.assertIn('FAIL goal 目標の研究 r-goal に届かない', out)
        self.assertIn('FAIL unobtainableItems i-part', out)

    def test_block_never_unlocked_fails(self):
        master = fx.base_master()
        master['research']['data'][0]['clearedActions'] = []
        code, out = run_gate(master)
        self.assertEqual(1, code)
        self.assertIn('FAIL neverUnlockedBlocks b-machine', out)

    def test_unused_item_fails(self):
        master = fx.base_master()
        master['items']['data'].append(fx.item('i-orphan', '孤児'))
        master['machineRecipes']['data'].append(fx.recipe('m-orphan', [fx.ORE], ['i-orphan']))
        code, out = run_gate(master)
        self.assertEqual(1, code)
        self.assertIn('FAIL unusedItems i-orphan', out)

    def test_allowlisted_problem_passes_and_is_printed(self):
        master = fx.base_master()
        master['items']['data'].append(fx.item('i-orphan', '孤児'))
        master['machineRecipes']['data'].append(fx.recipe('m-orphan', [fx.ORE], ['i-orphan']))
        code, out = run_gate(master, {'unusedItems': {'i-orphan': '次の段で消費先を足す'}})
        self.assertEqual(0, code, out)
        self.assertIn('ALLOW unusedItems', out)

    def test_stale_allowlist_fails(self):
        code, out = run_gate(fx.base_master(), {'unusedItems': {'i-plate': '古い登録'}})
        self.assertEqual(1, code)
        self.assertIn('FAIL stale-allowlist unusedItems i-plate', out)

    def test_reasonless_allowlist_fails(self):
        master = fx.base_master()
        master['items']['data'].append(fx.item('i-orphan', '孤児'))
        master['machineRecipes']['data'].append(fx.recipe('m-orphan', [fx.ORE], ['i-orphan']))
        code, out = run_gate(master, {'unusedItems': {'i-orphan': ' '}})
        self.assertEqual(1, code)
        self.assertIn('FAIL reasonless-allowlist', out)

    def test_goal_cannot_be_allowlisted(self):
        master = fx.base_master()
        master['research']['data'] = master['research']['data'][:1]
        code, out = run_gate(master, {'unreachableResearch': {fx.R_GOAL: '許可しても目標は消えない'}})
        self.assertEqual(1, code)
        self.assertIn('FAIL goal 目標の研究 r-goal がマスタに無い', out)

    def test_zero_cost_research_fails(self):
        master = fx.base_master()
        master['research']['data'][0]['consumeItems'] = []
        code, out = run_gate(master)
        self.assertEqual(1, code)
        self.assertIn('FAIL zeroCostResearch r-start', out)

    def test_implicit_prerequisite_fails(self):
        # 目標の研究が祖先でない枝の研究で解放されるブロックに頼ると、祖先だけでは払えない
        # When the goal relies on a block unlocked by a non-ancestor branch, its ancestors alone cannot pay
        master = fx.base_master()
        master['research']['data'][1]['prevResearchNodeGuids'] = []
        master['research']['data'][1]['consumeItems'] = [{'itemGuid': fx.PART, 'itemCount': 1}]
        code, out = run_gate(master)
        self.assertEqual(1, code)
        self.assertIn('FAIL implicitPrerequisites r-goal|i-part', out)


if __name__ == '__main__':
    unittest.main()
```

`tools/progression/tests/test_gate_tier9.py`（軌道到着で届く／積荷の行が無ければ落ちる／上限研究で Lv2 が作れる／上限が 1 のままなら落ちる／ダイ表の Lv2 率が正なら届く／0 なら落ちる／流入の無い液体レシピは落ちる）:
```python
#!/usr/bin/env python3
# Tier9 の仕組み（軌道到着・レベル継承の製作上限・成膜の昇格・ダイ表・液体の口）をモデルが追い、1か所壊すと exit 1 になることを確かめる
# The model follows the Tier9 mechanics (orbital arrival, inheritance craft cap, deposition promotion, die tables, fluid ports); breaking one spot exits 1
import os
import sys
import unittest

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
import fixture_master as fx  # noqa: E402
from test_gate_basics import run_gate  # noqa: E402

PAD, PAYLOAD = 'b-pad', 'i-payload'


def with_orbital(master):
    # 目標アイテムは発射台で積荷を打ち上げたときだけ届く
    # The goal item only arrives by launching the payload from the pad
    master['machineRecipes']['data'][2] = fx.recipe('m-payload', [fx.PART], [PAYLOAD])
    master['items']['data'].append(fx.item(PAYLOAD, '積荷'))
    master['items']['orbitalPayloads'] = [{'payloadItemGuid': PAYLOAD, 'countPerLaunch': 1,
                                           'arrivalOutputs': [{'itemGuid': fx.GOAL_ITEM, 'count': 1}]}]
    master['blocks']['data'].append({'blockGuid': PAD, 'name': '発射台', 'blockType': 'RocketLaunchPad', 'initialUnlocked': True,
                                     'requiredItems': [], 'blockParam': {'launchMaterials': [{'itemGuid': fx.PLATE, 'count': 1}]}})
    return master


def with_levels(master, cap_level):
    # 部品は Lv1/Lv2 の変種。目標は Lv2 を消費し、Lv2 は上限つき継承レシピで研究の製作上限が2のときだけ作れる
    # The part has Lv1/Lv2 variants; the goal consumes Lv2, which the capped inheritance recipe makes only at craft cap 2
    items = master['items']
    items['data'] += [fx.item('i-plate2', '板Lv2'), fx.item('i-part2', '部品Lv2')]
    items['levelFamilies'] = [{'baseItemGuid': fx.PLATE, 'levelItemGuids': [fx.PLATE, 'i-plate2']},
                              {'baseItemGuid': fx.PART, 'levelItemGuids': [fx.PART, 'i-part2']}]
    recipes = master['machineRecipes']['data']
    recipes[1] = fx.recipe('m-part', [fx.PLATE], [fx.PART], 'InheritUpToUnlocked')
    recipes.append(fx.recipe('m-plate2', [fx.ORE, fx.ORE], ['i-plate2']))
    master['research']['data'][0]['clearedActions'].append(
        {'gameActionType': 'unlockLevelInheritanceCraftLevel', 'gameActionParam': {'targetItemGuids': [fx.PART], 'level': cap_level}})
    master['research']['data'][1]['consumeItems'].append({'itemGuid': 'i-part2', 'itemCount': 1})
    return master


def with_dicing(master, lv2_rate):
    # 成膜で板の G2 を引き（品質モジュール tier1 が要る）、ダイ表の G2 行の Lv2 の率が0より大きいときだけ部品Lv2 が出る
    # Deposition draws plate G2 (needs a tier-1 quality module); part Lv2 appears only when the die table's G2 row has Lv2 above zero
    master = with_levels(master, 1)
    master['machineRecipes']['data'] = [r for r in master['machineRecipes']['data'] if r['machineRecipeGuid'] != 'm-plate2']
    master['items']['data'].append(fx.item('i-quality', '品質モジュール'))
    master['items']['modules'] = [{'itemGuid': 'i-quality', 'effectAxis': 'Quality', 'tier': 1, 'effectValue': 0.1}]
    master['machineRecipes']['data'][1]['levelInheritance'] = 'Inherit'
    master['machineRecipes']['data'].append(fx.recipe('m-quality', [fx.ORE], ['i-quality']))
    master['cleanRoom'] = {'data': [{
        'thresholds': [{'className': 'A', 'qualityMultiplier': 1.0, 'layerFailRate': 0.1, 'dieLossRate': 0.1}],
        'waferGradeDraws': [{'machineRecipeGuid': 'm-plate', 'gradedWaferItemGuid': fx.PLATE, 'maxPromotionChance': 0.6}],
        'layerExposures': [],
        'dicings': [{'machineRecipeGuid': 'm-part', 'dieItemGuid': fx.PART, 'failedDieItemGuid': fx.ORE, 'diesPerWafer': 10,
                     'maxLossReduction': 0.6, 'gradeDieTables': [{'levelRates': [1.0]}, {'levelRates': [1.0 - lv2_rate, lv2_rate]}]}]}]}
    return master


class GateTier9Test(unittest.TestCase):
    def test_orbital_arrival_reaches_goal(self):
        code, out = run_gate(with_orbital(fx.base_master()))
        self.assertEqual(0, code, out)
        self.assertIn('打ち上げ 発射台', out)

    def test_missing_payload_row_fails(self):
        master = with_orbital(fx.base_master())
        master['items']['orbitalPayloads'] = []
        code, out = run_gate(master)
        self.assertEqual(1, code)
        self.assertIn('FAIL goal', out)

    def test_craft_cap_research_unlocks_lv2(self):
        code, out = run_gate(with_levels(fx.base_master(), 2))
        self.assertEqual(0, code, out)

    def test_craft_cap_left_at_one_fails(self):
        code, out = run_gate(with_levels(fx.base_master(), 1))
        self.assertEqual(1, code)
        self.assertIn('FAIL unobtainableItems i-part2', out)

    def test_dicing_table_yields_lv2(self):
        code, out = run_gate(with_dicing(fx.base_master(), 0.3))
        self.assertEqual(0, code, out)
        self.assertIn('G2→Lv2', out)

    def test_dicing_table_without_lv2_rate_fails(self):
        code, out = run_gate(with_dicing(fx.base_master(), 0.0))
        self.assertEqual(1, code)
        self.assertIn('FAIL unobtainableItems i-part2', out)

    def test_fluid_recipe_without_inflow_fails(self):
        master = fx.base_master()
        master['fluids']['data'] = [{'fluidGuid': 'f-water', 'name': '水'}]
        master['machineRecipes']['data'][0]['inputFluids'] = [{'fluidGuid': 'f-water', 'amount': 1}]
        code, out = run_gate(master)
        self.assertEqual(1, code)
        self.assertIn('FAIL fluidPorts m-plate', out)


if __name__ == '__main__':
    unittest.main()
```

- [ ] **Step 6: 自己テストを回す**
  Run: `cd $MW && python3 -m unittest discover -s tools/progression/tests`
  Expected: 最後の 3 行が `Ran 17 tests in …` / 空行 / `OK`。

- [ ] **Step 7: 本物のマスタを 1 か所わざと壊して、絶対判定で落ちることを確かめる**（コピーを壊す。`$MW` は触らない）
```bash
mkdir -p $GL && rm -rf $GL/g2-broken && cp -R $MASTER $GL/g2-broken
python3 - "$GL/g2-broken" <<'EOF'
import json, sys
d = sys.argv[1]
bronze = next(i['itemGuid'] for i in json.load(open(f'{d}/items.json', encoding='utf-8'))['data'] if i['name'] == '青銅インゴット')
doc = json.load(open(f'{d}/machineRecipes.json', encoding='utf-8'))
before = len(doc['data'])
doc['data'] = [r for r in doc['data'] if not any(o.get('itemGuid') == bronze for o in r['outputItems'])]
print('removed', before - len(doc['data']))
json.dump(doc, open(f'{d}/machineRecipes.json', 'w', encoding='utf-8'), ensure_ascii=False, indent=2)
EOF
python3 $MW/tools/progression/check.py --master-dir $GL/g2-broken > $GL/g2-broken.txt; echo "exit=$?"
head -1 $GL/g2-broken.txt; grep '原始研究3' $GL/g2-broken.txt
```
Expected:
- `removed 3` と `exit=1` が出る。
- 1 行目が `== ROUTE: 2/44 research nodes in 2 waves`。
- `FAIL unreachableResearch bc5e7786-6759-4271-8095-836703b54490 原始研究3: 前提未完了=[] 入手できないコスト=['青銅インゴット']` が出る。
- 実測では、壊していない状態の 44/44 が 2/44 に落ちた。

- [ ] **Step 8: 旧 `progression_check.py` を、import 用の `ProgressionModel` だけを持つ委譲に差し替える**
  - `sync_machine_recipe_unlocks.py` が使う `ProgressionModel` の引数の形は保つ。この repo で `progression_check` を import しているのは `sync_machine_recipe_unlocks.py:18` の `from progression_check import ProgressionModel` だけ（`git grep -n progression_check` で確認。#87・#88・origin/master とも同じ）なので、`__main__` の G2 への委譲は外しても import は壊れない。
  - G2 の入口は `tools/progression/check.py` の 1 つにする。単体で実行されたら、黙って exit 0 せず check.py を案内して exit 2 で終わる。

`tools/research_sync/progression_check.py`（全文を置き換える）:
```python
#!/usr/bin/env python3
# 研究コストの到達可能性検査（moorestech ADR 0033 レビュー裁定 D2）。sync_machine_recipe_unlocks.py が import する ProgressionModel だけを持ち、中身は tools/progression のモデルへ委ねる
# Research-cost reachability check (moorestech ADR 0033 ruling D2); only ProgressionModel for sync_machine_recipe_unlocks.py, backed by the tools/progression model
# G2 の入口は tools/progression/check.py だけ。このファイルを単体で実行したら案内を出して exit 2（黙って exit 0 しない）
# The G2 entry is tools/progression/check.py only; running this file directly prints a pointer and exits 2 (never a silent exit 0)
import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'progression'))
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
from audits import progress as audits_progress  # noqa: E402
from model.master_data import MasterData  # noqa: E402
from model.unlock_index import UnlockIndex  # noqa: E402


class ProgressionModel:
    # sync_machine_recipe_unlocks.py が渡す引数の形を保つ（research は再配分後のリスト、他はマスタから読み直す）
    # Keeps the argument shape sync_machine_recipe_unlocks.py passes (research is the redistributed list; the rest is reread)
    def __init__(self, v8, research, ancestors, items, blocks, recipes):
        self.data = MasterData(v8)
        self.data.research = research
        self.data.research_by_guid = {r['researchNodeGuid']: r for r in research}

    def unreachable_costs(self, recipe_nodes):
        return audits_progress.unreachable_costs(self.data, UnlockIndex(self.data.research, recipe_nodes))


if __name__ == '__main__':
    print('progression_check.py は import 用（ProgressionModel）。G2 は python3 tools/progression/check.py で回す', file=sys.stderr)
    sys.exit(2)
```

  Run: `cd $MW && python3 tools/research_sync/progression_check.py; echo "direct=$?"`
  Expected: 標準エラーに `progression_check.py は import 用（ProgressionModel）。G2 は python3 tools/progression/check.py で回す` が出て、`direct=2`。

  Run: `cd $MW && python3 tools/research_sync/sync_machine_recipe_unlocks.py --check; echo "exit=$?"`
  Expected:
  - 末尾 3 行は `0 moves, 8 kept, 1 orphans, 0 stale, 60 recipes` / `10 unreachable costs before, 10 after` / `exit=1`。
  - exit 1 になるのは、`research.json` の末尾に改行が無いため（実測で末尾のバイトは `]\n}`）。`--check` はバイト一致を見ている（`sync_machine_recipe_unlocks.py:195-197`）。
  - 改行は、P2 で sync を書き込みモードで流したときに付く（同 `:195` が `+ '\n'` で書く）。この Step で `research.json` は直さない（マスタの中身は P2 の持ち分）。
  - `DEAD-END` の行が出ないこと（委譲後も相対判定が動くこと）を確かめる。

- [ ] **Step 9: マスタ統合ブランチで回し、今の欠落で赤いことを控える**
```bash
(cd $MW && python3 tools/progression/check.py --emit-order $GL/g2-order-base.json) > $GL/g2-base.txt; echo "exit=$?"
tail -1 $GL/g2-base.txt
grep '^FAIL' $GL/g2-base.txt | awk '{print $2}' | sort | uniq -c
grep -c 'stale-allowlist\|reasonless-allowlist' $GL/g2-base.txt
```
Expected:
- `exit=1`。末尾は `== RESULT: FAIL (69 failures, 19 allowed)`。
- 内訳は `1 deadRecipes` / `2 goal` / `10 neverUnlockedBlocks` / `29 unobtainableItems` / `21 unusedItems` / `6 zeroCostResearch`。stale と reasonless は `0`。
- 1 行目は `== ROUTE: 44/44 research nodes in 21 waves`。
- 主な FAIL の中身:
  - neverUnlockedBlocks: クリーンルーム系 6 種（パネルを含む）、EUV 露光式半導体製造装置、宇宙エレベーター拠点、ロケット発射台、核融合炉。
  - deadRecipes: 旧 EUV レシピ `3a13a5a9-d254-40f9-be93-e24c9ebfdfaf`。
  - goal: 「目標の研究 01a167e9-… がマスタに無い」と「目標のアイテム 反物質爆弾 を入手できない」。
  - zeroCostResearch: 裁定 2 の 6 ノード。

- [ ] **Step 10: マスタ repo の main でも回し、許可リストが Tier9 の欠落を隠していないことを確かめる**
```bash
rm -rf $GL/origin-master && mkdir -p $GL/origin-master
git -C $MC archive origin/master server_v8 | tar -x -C $GL/origin-master
python3 $MW/tools/progression/check.py --master-dir $GL/origin-master/server_v8/mods/moorestechAlphaMod_8/master > $GL/g2-origin-master.txt; echo "exit=$?"
tail -1 $GL/g2-origin-master.txt; grep -c 'stale-allowlist' $GL/g2-origin-master.txt
```
Expected:
- `exit=1`、`== RESULT: FAIL (65 failures, 19 allowed)`、stale は `0`。
- 許可 19 件はすべて main にもとからある問題である。統合ブランチで増えた FAIL（69 − 65 と中身の入れ替わり）は、どれも許可されていない。

- [ ] **Step 11: マスタ repo の CI に G2 を載せる**
  - 既存の `check-zero-width-json.yml` は別の関心事（ゼロ幅文字と出荷データの契約）なので、そこへは足さない。新しいワークフローにする。
  - このファイルは統合ブランチにだけ入る。P2 で緑になるまで、統合ブランチの push は赤い。これは意図どおり。

`.github/workflows/master-gates.yml`:
```yaml
# 出荷マスタの自動ゲート。自己テストで「壊すと落ちる」を確かめてから、出荷マスタを絶対判定する
# Automatic gates for the shipped master; prove "breaking fails" with the self-test, then judge the shipped master absolutely
name: Master Gates

on:
  push:
  pull_request:

jobs:
  reachability:
    name: Reachability (G2)
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Reachability gate self-test
        run: python3 -m unittest discover -s tools/progression/tests

      - name: Shipped master reachability
        run: python3 tools/progression/check.py
```

  Run: `ruby -ryaml -e 'p YAML.load_file(ARGV[0])["jobs"].keys' $MW/.github/workflows/master-gates.yml`
  Expected: `["reachability"]`

- [ ] **Step 12: コミットして push する**
```bash
git -C $MW add tools/progression tools/master_common tools/research_sync/progression_check.py .github/workflows/master-gates.yml
git -C $MW commit -m "tools: 到達性ゲート G2 を絶対判定で作り直し、軌道到着・レベル継承・クリーンルーム抽選をモデルに入れて CI に載せる"
git -C $MW push -u origin feat/tier9-integrated
```

**完了判定:**
- Step 6 で `Ran 17 tests` / `OK` が出る。
- Step 7 で壊したコピーが exit 1 になり、`原始研究3` の FAIL が出る。
- Step 9 で `$MW` が exit 1、`69 failures, 19 allowed`、stale 0 になる。
- Step 10 で main が `65 failures`、stale 0 になる。
- `gh run list -R moorestech/moorestech_master -b feat/tier9-integrated -w "Master Gates" -L 1` で、Step 12 の push の run が見える。この run は G2 の中身が足りないため failure になる（`Reachability gate self-test` の step は success）。

---

### Task 10: G1 出荷マスタのロードゲート（Editor の batch メソッドと PR ごとのワークフロー）

**Files:**
- Create: `moorestech_server/Assets/Scripts/Editor/MasterGate/Server.MasterGate.Editor.asmdef`
- Create: `moorestech_server/Assets/Scripts/Editor/MasterGate/MasterGateArguments.cs`
- Create: `moorestech_server/Assets/Scripts/Editor/MasterGate/ShippedMasterOwner.cs`（ゲートの中で static のマスタを書き換える唯一のクラス。判断記録 A32）
- Create: `moorestech_server/Assets/Scripts/Editor/MasterGate/ShippedMasterGateEntry.cs`
- Create: `.github/workflows/shipped-master-gate.yml`
- `.meta` は Unity が生成したものをコミットする（手で作らない）

**Interfaces:**
- Consumes: `Mod.Config.ModsResource(string)`、`Mod.Loader.ModJsonStringLoader.GetMasterString(ModsResource)`、`Core.Master.MasterJsonFileContainer`、`MasterHolder.Load`、`MasterHolder.ResearchMaster.GetAllResearches()`（`ResearchMaster.cs:46`）。どれもサーバーのアセンブリにある（client のアセンブリは参照しない。判断記録 A23）。
- Produces:
  - `Server.MasterGate.Editor.ShippedMasterOwner.TryLoad(string serverDataDirectory, out string failure) -> bool`。ゲートの中で `MasterHolder.Load` を呼ぶのはこのクラスだけ（Task 11 の `TryCreateServer` も同じクラスに置く）。
    - 成功時のログは `[ShippedMasterOwner] LOAD PASS <dir> research=<研究ノード数>`。成功した置き場を「最後に通ったマスタ」として覚える。
    - 失敗時は `Debug.LogError` と `failure` の両方に `[ShippedMasterOwner] LOAD FAIL …`（例外の全文を含む）を出し、続けて最後に通ったマスタを読み直して `[ShippedMasterOwner] RESTORED static master from <dir> after the failed load of <壊れた dir>` を出す。戻す先が無ければ `[ShippedMasterOwner] static master is half-loaded …` を警告に出す。壊れたマスタを static に残さないことは所有者が保証し、呼び出し側に「正常なマスタで回し直す」手順を求めない。
  - `Server.MasterGate.Editor.ShippedMasterGateEntry.RunLoadGate(string serverDataDirectory, out string failure) -> bool`: G1 の唯一の入口（batch・手元の G1 関数・Task 11 の `RunAll` がここを通る）。
  - `Server.MasterGate.Editor.ShippedMasterGateEntry.RunLoadGateFromCommandLine()`: `-executeMethod` の入口で、`RunLoadGate` を呼び、終了コード 0/1 を返す。
  - `MasterGateArguments.TryResolveServerDataDirectory(out string serverDataDirectory) -> bool`: `-masterDataRoot <マスタrepoの根>` の末尾に `server_v8` を足した絶対パスを返す。引数が無ければ `[MasterGate] FAIL -masterDataRoot <マスタrepoの根> is required …` を `Debug.LogError` に出して false を返す（ピンからの自動解決はしない）。
  - 置き場の根拠: 「確かめた事実」のカスタムメソッドの前例（`PlatformCompileCheck.cs` と `platform-compile.yml`）。手元の uloop は client project のまま呼べる（`tech.moores.server` パッケージ）。
  - 手元用のシェル関数（P2 節の `G1 $MW/server_v8` の実体）:
```bash
G1() { (cd $W && uloop execute-dynamic-code --project-path ./moorestech_client --code "return Server.MasterGate.Editor.ShippedMasterGateEntry.RunLoadGate(\"$1\", out var failure) ? \"G1 PASS\" : failure;"); }
```

- [ ] **Step 1: Editor 専用 asmdef を置く**
  - 参照するのは G1 と G3 が使うサーバーのアセンブリだけ。11 個の名前は、どれも `moorestech_server/Assets/Scripts/<名前>/<名前>.asmdef` として実在することを確認済み（`git grep '"name": "<名前>"' -- 'moorestech_server/*.asmdef'`）。`Client.Game` は参照しない。
  - `Microsoft.Extensions.DependencyInjection` と `Newtonsoft.Json` は自動参照の precompiled として入る見込み。`Server.Boot.asmdef` も `overrideReferences: false` のまま DI を使っている。

`moorestech_server/Assets/Scripts/Editor/MasterGate/Server.MasterGate.Editor.asmdef`:
```json
{
    "name": "Server.MasterGate.Editor",
    "rootNamespace": "",
    "references": [
        "Core.Inventory",
        "Core.Item.Interface",
        "Core.Master",
        "Game.Context",
        "Game.PlayerIdentity",
        "Game.PlayerInventory.Interface",
        "Game.Research",
        "Game.UnlockState",
        "Mod.Config",
        "Mod.Loader",
        "Server.Boot"
    ],
    "includePlatforms": [
        "Editor"
    ],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Step 2: 引数の解決を置く**

`moorestech_server/Assets/Scripts/Editor/MasterGate/MasterGateArguments.cs`:
```csharp
using System;
using System.IO;
using UnityEngine;

namespace Server.MasterGate.Editor
{
    /// <summary>
    ///     ゲートの入力（出荷マスタの置き場・G2 の研究順ファイル）をコマンドライン引数から解く。置き場は推測せず -masterDataRoot を必須にする
    ///     Resolves gate inputs (shipped master location, G2 research-order file) from command-line args; the location is never guessed and -masterDataRoot is required
    /// </summary>
    public static class MasterGateArguments
    {
        private const string MasterDataRootArgument = "-masterDataRoot";
        private const string ResearchOrderArgument = "-researchOrder";

        // server_v8 の絶対パス。-masterDataRoot が無ければ理由をログに出して false（ピンからの自動解決はしない。判断記録 A23）
        // Absolute server_v8 path; without -masterDataRoot log the reason and return false (no pin auto-resolution, decision A23)
        public static bool TryResolveServerDataDirectory(out string serverDataDirectory)
        {
            var masterRoot = FindArgument(MasterDataRootArgument);
            if (string.IsNullOrEmpty(masterRoot))
            {
                serverDataDirectory = null;
                Debug.LogError($"[MasterGate] FAIL {MasterDataRootArgument} <マスタrepoの根> is required (the gate never guesses the master location)");
                return false;
            }
            serverDataDirectory = Path.GetFullPath(Path.Combine(masterRoot, "server_v8"));
            return true;
        }

        // G2 が書いた研究順 JSON のパス。指定が無ければ null（読み手が理由を出して失敗する）
        // Path of the research-order JSON G2 wrote; null when absent (the reader fails with a reason)
        public static string FindResearchOrderPath()
        {
            return FindArgument(ResearchOrderArgument);
        }

        private static string FindArgument(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name) return args[i + 1];
            }
            return null;
        }
    }
}
```

- [ ] **Step 3: static マスタの所有者を置く**（出荷マスタは別 repo から来る外部入力 JSON なので、try-catch の境界例外に当たる。catch した失敗はログと戻り値の両方に残す）
  - `MasterHolder.Load` は static のマスタを書き換える。ゲートの中でこれを呼ぶのは `ShippedMasterOwner` だけにし、読み込みに失敗したら所有者が最後に通ったマスタへ戻す（判断記録 A32）。G3 の DI 構築（Task 11）も中でマスタを読むので、同じクラスに置く。

`moorestech_server/Assets/Scripts/Editor/MasterGate/ShippedMasterOwner.cs`:
```csharp
using System;
using System.IO;
using Core.Master;
using Mod.Config;
using Mod.Loader;
using UnityEngine;

namespace Server.MasterGate.Editor
{
    /// <summary>
    ///     ゲートの中で static のマスタ（MasterHolder）を書き換える唯一のクラス。読み込みに失敗したら、この Editor で最後に通ったマスタへ自分で戻す
    ///     The only class in the gates that rewrites the static master (MasterHolder); after a failed load it restores the last passing master of this Editor itself
    /// </summary>
    public static class ShippedMasterOwner
    {
        // この Editor で最後に読み込みが通った置き場。失敗した読み込みの後はここへ戻す（ドメインリロードで消える）
        // The location whose load last passed in this Editor; a failed load is followed by a reload of it (cleared by a domain reload)
        private static string _lastLoadedServerDataDirectory;

        // G1: 出荷マスタを本番と同じ経路（ModsResource → MasterHolder.Load）で読み、ローダーと起動時検証が通るかを判定する
        // G1: load the shipped master through the production path (ModsResource -> MasterHolder.Load) and judge whether the loader and boot validation pass
        public static bool TryLoad(string serverDataDirectory, out string failure)
        {
            if (!TryLoadMaster(serverDataDirectory, out failure))
            {
                Debug.LogError(failure);
                RestoreLastLoaded(serverDataDirectory);
                return false;
            }

            _lastLoadedServerDataDirectory = serverDataDirectory;
            Debug.Log($"[ShippedMasterOwner] LOAD PASS {serverDataDirectory} research={MasterHolder.ResearchMaster.GetAllResearches().Count}");
            return true;
        }

        private static bool TryLoadMaster(string serverDataDirectory, out string failure)
        {
            // 置き場が無いときは推測で続けず、見た場所を理由に残す
            // When the location is missing, do not guess; record where we looked
            var modsDirectory = Path.Combine(serverDataDirectory, "mods");
            if (!Directory.Exists(modsDirectory))
            {
                failure = $"[ShippedMasterOwner] LOAD FAIL mods directory not found: {modsDirectory}";
                return false;
            }

            // 出荷マスタは別repoから来る外部入力JSON（境界）。必須キー欠落のローダー例外と検証例外をここで理由へ変え、呼び出し側がログに全文を残す
            // The shipped master is external-input JSON from another repo (a boundary); loader and validation exceptions become a reason here and the caller logs it in full
            try
            {
                MasterHolder.Load(new MasterJsonFileContainer(ModJsonStringLoader.GetMasterString(new ModsResource(modsDirectory))));
            }
            catch (Exception exception)
            {
                failure = $"[ShippedMasterOwner] LOAD FAIL MasterHolder.Load threw for {serverDataDirectory}:\n{exception}";
                return false;
            }

            failure = null;
            return true;
        }

        private static void RestoreLastLoaded(string failedServerDataDirectory)
        {
            // 壊れたマスタで読みかけた static を後続の dynamic code に残さないため、最後に通ったマスタを読み直す
            // Reload the last passing master so later dynamic code never runs on a half-loaded broken static master
            if (_lastLoadedServerDataDirectory == null)
            {
                Debug.LogWarning($"[ShippedMasterOwner] static master is half-loaded from {failedServerDataDirectory} and no earlier load passed in this Editor session; run G1 on a good master before anything reads MasterHolder");
                return;
            }

            if (TryLoadMaster(_lastLoadedServerDataDirectory, out var restoreFailure))
            {
                Debug.Log($"[ShippedMasterOwner] RESTORED static master from {_lastLoadedServerDataDirectory} after the failed load of {failedServerDataDirectory}");
                return;
            }

            // 戻す先も読めなくなった（ファイルが変わった）ので戻す先を捨て、理由を残す
            // The restore target no longer loads either (its files changed), so forget it and log why
            Debug.LogError($"[ShippedMasterOwner] FAIL could not restore the static master: {restoreFailure}");
            _lastLoadedServerDataDirectory = null;
        }
    }
}
```

- [ ] **Step 4: 入口を置く**（G1 の入口は `RunLoadGate` 1 つ。batch の入口と手元の G1 関数はここを通る。Task 11 で G3 の入口を足す）

`moorestech_server/Assets/Scripts/Editor/MasterGate/ShippedMasterGateEntry.cs`:
```csharp
using UnityEditor;

namespace Server.MasterGate.Editor
{
    /// <summary>
    ///     ゲートの入口。batch の入口は終了コード（0=PASS / 1=FAIL）で返し、理由は Unity のログに出す。static マスタの書き換えは ShippedMasterOwner だけが行う
    ///     Gate entries; batch entries report via the exit code (0 = PASS / 1 = FAIL) and the reason goes to the Unity log; only ShippedMasterOwner rewrites the static master
    /// </summary>
    public static class ShippedMasterGateEntry
    {
        // G1 だけを回す（置き場は -masterDataRoot で必須。無ければ理由をログに出して 1 で終わる）
        // Run G1 only (-masterDataRoot is required; without it the reason is logged and the exit code is 1)
        public static void RunLoadGateFromCommandLine()
        {
            var passed = MasterGateArguments.TryResolveServerDataDirectory(out var serverDataDirectory)
                         && RunLoadGate(serverDataDirectory, out _);
            EditorApplication.Exit(passed ? 0 : 1);
        }

        // G1 の唯一の入口（batch・手元の G1 関数・Task 11 の RunAll がここを通る）
        // The single G1 entry (batch, the local G1 function and Task 11's RunAll all go through it)
        public static bool RunLoadGate(string serverDataDirectory, out string failure)
        {
            return ShippedMasterOwner.TryLoad(serverDataDirectory, out failure);
        }
    }
}
```

- [ ] **Step 5: コンパイル**
  Run: `cd $W && uloop compile --project-path ./moorestech_client`
  Expected: エラー 0。
  - 参照不足で `Newtonsoft` や `Microsoft.Extensions.DependencyInjection` が解決できないエラーが出たら、asmdef に `"overrideReferences": true` と `"precompiledReferences": ["Newtonsoft.Json.dll", "Microsoft.Extensions.DependencyInjection.dll", "Microsoft.Extensions.DependencyInjection.Abstractions.dll"]` を足す。
  - 要確認（実装時にこのコンパイルで確かめる。dll 名は `Server.Boot` が実際に解決している dll を `find $W/moorestech_server/Assets -name 'Microsoft.Extensions.DependencyInjection*.dll'` で読む）。

- [ ] **Step 6: 本体のピンが指すマスタで G1 が通ることを確かめる**
  - ピン先 `630fdce` は本体の今のコードと対になっている。
  - `$MW`（#87 込み）は、P1 で S2 のスキーマが入るまでは読めない場合がある。そのため参考として回すだけにする。
```bash
PIN=$(jq -er '.repositories[] | select(.key == "moorestech_master") | .commitHash' $W/.moorestech-external-revisions.json)
rm -rf $GL/pin-master && mkdir -p $GL/pin-master && git -C $MC archive $PIN server_v8 | tar -x -C $GL/pin-master
G1 $GL/pin-master/server_v8
G1 $MW/server_v8
```
Expected:
- 1 本目は `G1 PASS`。`cd $W && uloop get-logs --project-path ./moorestech_client --log-type Log` に `[ShippedMasterOwner] LOAD PASS …/pin-master/server_v8 research=44` が出る。
- 2 本目は、P1 完了後なら `G1 PASS`。P1 前に `FAIL` が出たら、その全文を `$GL/g1-mw-before-p1.txt` に控える（P1 の受け入れで再確認する）。そのときは続けて `[ShippedMasterOwner] RESTORED static master from …/pin-master/server_v8 …` が Log に出る。

- [ ] **Step 7: マスタを 1 か所わざと壊して、G1 が落ちることを確かめる**（必須キー `allowQualityModule` を ElectricMachine の 1 行から消す）
```bash
rm -rf $GL/g1-broken && cp -R $GL/pin-master $GL/g1-broken
python3 - "$GL/g1-broken/server_v8/mods/moorestechAlphaMod_8/master/blocks.json" <<'EOF'
import json, sys
path = sys.argv[1]
doc = json.load(open(path, encoding='utf-8'))
target = next(b for b in doc['data'] if b['blockType'] == 'ElectricMachine')
del target['blockParam']['allowQualityModule']
print('broke', target['name'])
json.dump(doc, open(path, 'w', encoding='utf-8'), ensure_ascii=False, indent=2)
EOF
G1 $GL/g1-broken/server_v8
cd $W && uloop get-logs --project-path ./moorestech_client --log-type Error
cd $W && uloop get-logs --project-path ./moorestech_client --log-type Log | grep 'ShippedMasterOwner\] RESTORED'
```
Expected:
- `broke 石窯` が出る（ElectricMachine の先頭行）。
- 壊したほうは `[ShippedMasterOwner] LOAD FAIL MasterHolder.Load threw for …/g1-broken/server_v8:` で始まる文字列を返し、同じ行が Error ログに出る。例外の型と、本文に `allowQualityModule` が出るかは要確認（実装時にこの出力で確かめる。どちらでも FAIL なら合格）。
- 最後の grep は `[ShippedMasterOwner] RESTORED static master from …/pin-master/server_v8 after the failed load of …/g1-broken/server_v8` を出す。所有者が Step 6 で通ったマスタへ戻したので、正常なマスタで G1 を回し直す手順は要らない（判断記録 A32）。

- [ ] **Step 8: batch（`-executeMethod`）でも同じ判定になることを確かめる**
  - Editor を開いたままでは同じプロジェクトを batch で開けない。そのため、自分の Editor（`moores-wt status` で所有を確認する）を閉じてから回し、終わったら `uloop launch` で戻す。
  - 手元は client project で回す。手元の worktree は client の Library しか持たず（moores-wt が複製するのは client の Library）、サーバーのスクリプトは `tech.moores.server` パッケージとして同じクラスが入るため。CI は前例どおり `moorestech_server/` で回す（Step 9）。
  - 3 本目は `-masterDataRoot` を付けずに回し、置き場を推測せず落ちることを確かめる。
```bash
UNITY=/Applications/Unity/Hub/Editor/6000.3.8f1/Unity.app/Contents/MacOS/Unity
$UNITY -batchmode -nographics -projectPath $W/moorestech_client -executeMethod Server.MasterGate.Editor.ShippedMasterGateEntry.RunLoadGateFromCommandLine -masterDataRoot $GL/pin-master -logFile - | grep 'ShippedMasterOwner\|\[MasterGate\]'; echo "exit=${pipestatus[1]}"
$UNITY -batchmode -nographics -projectPath $W/moorestech_client -executeMethod Server.MasterGate.Editor.ShippedMasterGateEntry.RunLoadGateFromCommandLine -masterDataRoot $GL/g1-broken -logFile - | grep 'ShippedMasterOwner\|\[MasterGate\]'; echo "exit=${pipestatus[1]}"
$UNITY -batchmode -nographics -projectPath $W/moorestech_client -executeMethod Server.MasterGate.Editor.ShippedMasterGateEntry.RunLoadGateFromCommandLine -logFile - | grep 'ShippedMasterOwner\|\[MasterGate\]'; echo "exit=${pipestatus[1]}"
```
Expected:
- 1 本目は `[ShippedMasterOwner] LOAD PASS … research=44` と `exit=0`。
- 2 本目は `[ShippedMasterOwner] LOAD FAIL …` と、batch では先に通ったマスタが無いので `[ShippedMasterOwner] static master is half-loaded …` の警告、`exit=1`。
- 3 本目は `[MasterGate] FAIL -masterDataRoot <マスタrepoの根> is required (the gate never guesses the master location)` と `exit=1`。
- `pipestatus` は zsh の書き方。bash で回すなら `PIPESTATUS[0]` にする。

- [ ] **Step 9: PR ごとのワークフローを置く**
  - CI でも `-masterDataRoot` で、checkout したピン先の絶対パス（`/github/workspace/moorestech_master`）を渡す。ゲートはピンを自分で解かない（判断記録 A23）。
  - 前例 `platform-compile.yml` に揃える。本体は workspace の直下に checkout し、`projectPath: moorestech_server/` で回す。ピン先は `moorestech_master/` に checkout する（Unity project の外）。
  - Library は `platform-compile.yml` と同じキー `Library_compile_server_StandaloneWindows64-` を読むだけにする（保存は `cache-warm.yml` の `warm-server-compile`）。`actions/cache` は `path` が一致しないと当たらないので、`path` も同じ `moorestech_server/Library` にする。ターゲットを Windows にするのは、その Library を再インポートなしで使うため（ゲートはプラットフォームに依存しない）。

`.github/workflows/shipped-master-gate.yml`:
```yaml
# PRごとに、ピン先の出荷マスタを本番と同じ経路で読めるかを検査する（G1）。テストはピン先マスタを読めない（check-test-external-dependencies.py）ため専用ワークフローで読む
# Per PR, check the pinned shipped master loads through the production path (G1); tests may not read it (check-test-external-dependencies.py), so a dedicated workflow does
name: Shipped Master Gate

on:
  pull_request:
    paths-ignore:
      - '**/*.md'

  # 手動実行デバッグ用
  workflow_dispatch: {}

concurrency:
  group: ${{ github.workflow }}-${{ github.ref }}
  cancel-in-progress: ${{ github.event_name == 'pull_request' }}

jobs:
  gate:
    name: Shipped Master Gate (G1)
    runs-on: ubuntu-latest
    timeout-minutes: 60
    steps:
      - name: Checkout repository
        uses: actions/checkout@v4
        with:
          submodules: recursive

      # ピン先SHAの解決と非公開master repoのcheckoutは run_test.yml の unity_test_shard と同じ手順
      # Pin resolution and the private master checkout follow unity_test_shard in run_test.yml
      - name: Read pinned master repository revision.
        id: master_revision
        shell: bash
        run: |
          commit_hash=$(jq -er '.repositories[] | select(.key == "moorestech_master") | .commitHash' .moorestech-external-revisions.json)
          echo "commit_hash=$commit_hash" >> "$GITHUB_OUTPUT"

      - name: Generate github token
        id: generate_token
        uses: tibdex/github-app-token@v1
        with:
          app_id: ${{ secrets.APP_ID }}
          private_key: ${{ secrets.PRIVATE_KEY }}

      - name: Check out pinned master repository.
        uses: actions/checkout@v4
        with:
          repository: moorestech/moorestech_master
          token: ${{ steps.generate_token.outputs.token }}
          ref: ${{ steps.master_revision.outputs.commit_hash }}
          path: moorestech_master

      # 保存はmasterのcache-warm.ymlだけが行う。path・キーは platform-compile.yml の Windows 行と逐語一致させる
      # Only master's cache-warm.yml saves; path and key match the Windows row of platform-compile.yml verbatim
      - uses: actions/cache/restore@v4
        with:
          path: moorestech_server/Library
          key: Library_compile_server_StandaloneWindows64-${{ github.sha }}
          restore-keys: |
            Library_compile_server_StandaloneWindows64-

      # buildMethodはプレイヤービルドをせず EditorApplication.Exit の終了コードで合否を返す（platform-compile.yml と同じ使い方）。置き場は推測させず必ず渡す
      # The buildMethod builds no player and reports via the EditorApplication.Exit code (same usage as platform-compile.yml); the master location is always passed
      - name: Shipped master gate
        uses: game-ci/unity-builder@4423ee75828dff56037d1d1cfdac6b68e3a6ba00 # v4.8.2
        env:
          UNITY_LICENSE: ${{ secrets.UNITY_LICENSE }}
          UNITY_EMAIL: ${{ secrets.UNITY_EMAIL }}
          UNITY_PASSWORD: ${{ secrets.UNITY_PASSWORD }}
        with:
          unityVersion: 6000.3.8f1
          buildMethod: Server.MasterGate.Editor.ShippedMasterGateEntry.RunLoadGateFromCommandLine
          targetPlatform: StandaloneWindows64
          allowDirtyBuild: true
          projectPath: moorestech_server/
          versioning: None
          customParameters: '-masterDataRoot /github/workspace/moorestech_master'
```

  Run: `ruby -ryaml -e 'p YAML.load_file(ARGV[0])["jobs"].keys' $W/.github/workflows/shipped-master-gate.yml`
  Expected: `["gate"]`

- [ ] **Step 10: コミットする**
```bash
git -C $W add moorestech_server/Assets/Scripts/Editor/MasterGate .github/workflows/shipped-master-gate.yml
git -C $W status --short moorestech_server/Assets/Scripts/Editor/MasterGate   # .cs 3 本・asmdef と、Unity が作った .meta が並ぶ
git -C $W commit -m "ci: 出荷マスタを本番の経路で読むロードゲート G1 を Editor の batch メソッドと PR ごとのワークフローで足す"
git -C $W push
```

**完了判定:**
- Step 6 の 1 本目が `G1 PASS` になる。
- Step 7 の壊したマスタで FAIL の文字列と Error ログが出る。
- Step 8 の終了コードが 0/1/1 に分かれる（3 本目は `-masterDataRoot` 無しで `[MasterGate] FAIL … is required`）。
- push 後の PR で `Shipped Master Gate (G1)` の check が success になる。ピン先は本体のコードと対なので通る見込み。失敗した場合は、ログの `[ShippedMasterOwner]` 行で原因を読む。

---

### Task 11: G3 研究ウォーク（G2 の研究順で、出荷マスタのまま全ノードを完了させる）

**Files:**
- Create: `moorestech_server/Assets/Scripts/Editor/MasterGate/ResearchWalkOrderFile.cs`
- Create: `moorestech_server/Assets/Scripts/Editor/MasterGate/ResearchWalkRunner.cs`
- Modify: `moorestech_server/Assets/Scripts/Editor/MasterGate/ShippedMasterOwner.cs`（G3 のサーバー構築 `TryCreateServer` を足す。DI 構築も中でマスタを読むので、static マスタの所有者に置く。判断記録 A32）
- Modify: `moorestech_server/Assets/Scripts/Editor/MasterGate/ShippedMasterGateEntry.cs`（G3 の入口を足す）
- Modify: `.github/workflows/shipped-master-gate.yml`（G2 で研究順を書き、G1 → G3 を回す）

**Interfaces:**
- Consumes:
  - Task 2 の `--emit-order` の JSON。
  - Task 10 の `ShippedMasterGateEntry.RunLoadGate`・`ShippedMasterOwner`・`MasterGateArguments`。
  - `MoorestechServerDIContainerGenerator.Create(MoorestechServerDIContainerOptions) -> (PacketResponseCreator, ServiceProvider)`（`MoorestechServerDIContainerGenerator.cs:39`。呼ぶのは `ShippedMasterOwner.TryCreateServer` だけ）。
  - `PlayerIdentityRegistry.PreviewAssignment(string) -> PlayerIdAssignment` と `Commit(PlayerIdAssignment)`。
  - `IResearchDataStore.GetResearchNodeStates(int)`・`CompleteResearch(Guid, int)`（`IResearchDataStore.cs:9-10`）。
  - `IPlayerInventoryDataStore.GetInventoryData(int).MainOpenableInventory.InsertItem(IItemStack) -> IItemStack`。
  - `IGameUnlockStateDataController.BlockUnlockStateInfos` / `MachineRecipeUnlockStateInfos`（`Game.UnlockState/IGameUnlockStateDatastoreController.cs:21-22`）。
  - `ServerContext.ItemStackFactory`（`Game.Context/ServerContext.cs:16`）。
- Produces:
  - `ShippedMasterOwner.TryCreateServer(string serverDataDirectory, out ServiceProvider serviceProvider, out string failure) -> bool`。G1 が最後に通った置き場でだけサーバーを組み、違えば `[ShippedMasterOwner] FAIL the server is built only on …` で false。
  - `ResearchWalkRunner.Run(ServiceProvider serviceProvider, IReadOnlyList<ResearchWalkStep> steps, out string failure) -> bool`（サーバーは組まず、渡されたものを歩く）。
  - `ShippedMasterGateEntry.RunAll(string serverDataDirectory, string researchOrderPath) -> bool`。`RunLoadGate` → 研究順の読み込み → `TryCreateServer` → `ResearchWalkRunner.Run` の順。G1 が落ちたら G3 は回さない。
  - `ShippedMasterGateEntry.RunFromCommandLine()`: CI の入口。`-researchOrder <G2 が書いた JSON>` を読む。
  - ログは成功で `[ResearchWalk] PASS <N> nodes completed in G2 order`。失敗は最初の 1 件で止まり、`[ResearchWalk] FAIL <名前> (<GUID>): <理由>`。

- [ ] **Step 1: 研究順ファイルの読み手を置く**（研究順は別プロセスが書いた外部入力 JSON なので、境界の try-catch に当たる）

`moorestech_server/Assets/Scripts/Editor/MasterGate/ResearchWalkOrderFile.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Server.MasterGate.Editor
{
    /// <summary>
    ///     G2（tools/progression/check.py --emit-order）が書いた研究順の1行。研究GUIDと、その研究で付くはずの解放
    ///     One row of the research order G2 (tools/progression/check.py --emit-order) wrote: the research GUID and the unlocks it should apply
    /// </summary>
    public readonly struct ResearchWalkStep
    {
        public readonly Guid ResearchNodeGuid;
        public readonly string Name;
        public readonly IReadOnlyList<Guid> UnlockBlockGuids;
        public readonly IReadOnlyList<Guid> UnlockMachineRecipeGuids;

        public ResearchWalkStep(Guid researchNodeGuid, string name, IReadOnlyList<Guid> unlockBlockGuids, IReadOnlyList<Guid> unlockMachineRecipeGuids)
        {
            ResearchNodeGuid = researchNodeGuid;
            Name = name;
            UnlockBlockGuids = unlockBlockGuids;
            UnlockMachineRecipeGuids = unlockMachineRecipeGuids;
        }
    }

    public static class ResearchWalkOrderFile
    {
        public static bool TryRead(string path, out List<ResearchWalkStep> steps, out string failure)
        {
            steps = null;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                failure = $"[ResearchWalk] FAIL research order file not found: '{path}' (G2 を --emit-order 付きで先に回す)";
                Debug.LogError(failure);
                return false;
            }

            // 研究順は別プロセス（Python）が書いた外部入力JSON（境界）。壊れていれば理由を残して失敗にする
            // The order is external-input JSON written by another process (Python), a boundary; a broken file fails with a reason
            try
            {
                steps = JObject.Parse(File.ReadAllText(path))["order"].Select(ToStep).ToList();
            }
            catch (Exception exception)
            {
                failure = $"[ResearchWalk] FAIL research order file is unreadable: {path}\n{exception}";
                Debug.LogError(failure);
                return false;
            }

            failure = null;
            return true;

            #region Internal

            static ResearchWalkStep ToStep(JToken row)
            {
                return new ResearchWalkStep(Guid.Parse((string)row["researchNodeGuid"]), (string)row["name"],
                    row["unlockBlockGuids"].Select(g => Guid.Parse((string)g)).ToList(),
                    row["unlockMachineRecipeGuids"].Select(g => Guid.Parse((string)g)).ToList());
            }

            #endregion
        }
    }
}
```

- [ ] **Step 2: サーバーの構築を static マスタの所有者に足し、ウォーク本体を置く**
  - 本番の起動と同じ DI 構築で動かす。DI 構築（`Create`）は中で `MasterHolder.Load` を呼ぶので、ウォーク本体からは呼ばず、Task 10 の `ShippedMasterOwner` に `TryCreateServer` として足す（static マスタを書き換えるクラスを 1 つに保つ。判断記録 A32）。
  - `ShippedMasterOwner.cs` の先頭に `using Microsoft.Extensions.DependencyInjection;` と `using Server.Boot;` を足し、`_lastLoadedServerDataDirectory` の宣言の直後に次のメソッドを足す。
```csharp
        // G3: 本番の起動と同じ DI 構築でサーバーを組む。Create の中でもう一度マスタを読むので、G1 が通った置き場でだけ組む
        // G3: build the server with the production DI container; Create reloads the master, so it only builds on a location whose G1 load passed
        public static bool TryCreateServer(string serverDataDirectory, out ServiceProvider serviceProvider, out string failure)
        {
            if (serverDataDirectory != _lastLoadedServerDataDirectory)
            {
                serviceProvider = null;
                failure = $"[ShippedMasterOwner] FAIL the server is built only on the directory whose G1 load last passed: {serverDataDirectory} (last passing: {_lastLoadedServerDataDirectory})";
                Debug.LogError(failure);
                return false;
            }

            (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(serverDataDirectory));
            failure = null;
            return true;
        }
```
  - コストは主インベントリへ入れる。`CompleteResearch` は主インベントリからだけ払うため（`ResearchDataStore.cs:48`）。
  - 判定は 3 つ。払う前の状態が前提不足でないこと、払った後が `Researchable` であること、完了の直後に G2 が当てにしたブロックと機械レシピの解放が付いていること。解放の判定は「解放済み GUID の集合に入っているか」の 1 本にし、ブロックと機械レシピで同じ述語を 2 回書かない（判断記録 A32）。

`moorestech_server/Assets/Scripts/Editor/MasterGate/ResearchWalkRunner.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Context;
using Game.PlayerIdentity;
using Game.PlayerInventory.Interface;
using Game.Research;
using Game.UnlockState;
using Microsoft.Extensions.DependencyInjection;
using UnityEngine;

namespace Server.MasterGate.Editor
{
    /// <summary>
    ///     G3: 出荷マスタで組んだサーバーで、G2 の研究順に各ノードのコストだけを主インベントリへ渡して CompleteResearch が通り、G2 が使った解放が実際に付くかを確かめる
    ///     G3: on a server built from the shipped master, give each node only its cost in G2's order, and check CompleteResearch succeeds and the unlocks G2 relied on are applied
    /// </summary>
    public static class ResearchWalkRunner
    {
        private const string GateIdentity = "gate:research-walk";

        public static bool Run(ServiceProvider serviceProvider, IReadOnlyList<ResearchWalkStep> steps, out string failure)
        {
            // サーバーは ShippedMasterOwner が本番と同じ DI 構築で組んだもの。ゲート専用のプレイヤーを1人登録する
            // The server was built by ShippedMasterOwner with the production DI container; register one gate-only player
            var registry = serviceProvider.GetRequiredService<PlayerIdentityRegistry>();
            var assignment = registry.PreviewAssignment(GateIdentity);
            registry.Commit(assignment);
            var playerId = assignment.PlayerId;
            var research = serviceProvider.GetRequiredService<IResearchDataStore>();
            var unlocks = serviceProvider.GetRequiredService<IGameUnlockStateDataController>();
            var inventory = serviceProvider.GetRequiredService<IPlayerInventoryDataStore>().GetInventoryData(playerId).MainOpenableInventory;

            // 最初の失敗で止める（後続は前提が欠けて連鎖的に落ちるだけで、原因の特定を妨げるため）
            // Stop at the first failure; later nodes would only cascade on the missing prerequisite and hide the cause
            failure = null;
            foreach (var step in steps)
            {
                failure = WalkOne(step);
                if (failure != null) break;
            }
            if (failure != null) Debug.LogError(failure);
            else Debug.Log($"[ResearchWalk] PASS {steps.Count} nodes completed in G2 order");
            return failure == null;

            #region Internal

            string WalkOne(ResearchWalkStep step)
            {
                // G2 は前提が揃った段に置いたので、支払い前に前提不足の状態なら Python のモデルと実装の前提判定がずれている
                // G2 placed the node where its prerequisites are done, so a missing-prerequisite state before paying means the Python model and the runtime disagree
                var node = MasterHolder.ResearchMaster.GetResearch(step.ResearchNodeGuid);
                var before = research.GetResearchNodeStates(playerId)[step.ResearchNodeGuid];
                if (before is ResearchNodeState.Completed or ResearchNodeState.UnresearchableNotEnoughPreNode or ResearchNodeState.UnresearchableAllReasons)
                    return Fail(step, $"支払い前の状態が {before}（前提の判定が G2 とずれている）");

                // コストを最大スタックごとに分けて主インベントリへ入れる。入り切らなければ、その時点の枠ではゲームでも払えない
                // Insert each cost split by max stack; if it does not fit, the game cannot pay it at this point either
                foreach (var cost in node.ConsumeItems)
                {
                    var stacks = ServerContext.ItemStackFactory.CreateSplitStacks(MasterHolder.ItemMaster.GetItemId(cost.ItemGuid), cost.ItemCount);
                    var overflow = 0;
                    foreach (var stack in stacks) overflow += inventory.InsertItem(stack).Count;
                    if (overflow > 0) return Fail(step, $"コスト {cost.ItemGuid} x{cost.ItemCount} のうち {overflow} 個が主インベントリに入らない（最大スタックで分けた {stacks.Count} 枠）");
                }

                var after = research.GetResearchNodeStates(playerId)[step.ResearchNodeGuid];
                if (after != ResearchNodeState.Researchable) return Fail(step, $"コストを渡した後の状態が {after}");
                if (!research.CompleteResearch(step.ResearchNodeGuid, playerId)) return Fail(step, "CompleteResearch が false を返した");

                // G2 がこの段で付くと見なした解放（ブロック・機械レシピ）が実際に付いたかを、解放済み GUID の集合 1 つの判定で見る
                // Check the unlocks G2 assumed at this step (blocks, machine recipes) with one membership test against the unlocked GUID sets
                var unlockedBlocks = new HashSet<Guid>(unlocks.BlockUnlockStateInfos.Where(pair => pair.Value.IsUnlocked).Select(pair => pair.Key));
                var unlockedRecipes = new HashSet<Guid>(unlocks.MachineRecipeUnlockStateInfos.Where(pair => pair.Value.IsUnlocked).Select(pair => pair.Key));
                return FirstNotUnlocked(step, "ブロック", step.UnlockBlockGuids, unlockedBlocks)
                       ?? FirstNotUnlocked(step, "機械レシピ", step.UnlockMachineRecipeGuids, unlockedRecipes);
            }

            string FirstNotUnlocked(ResearchWalkStep step, string label, IReadOnlyList<Guid> expected, HashSet<Guid> unlocked)
            {
                foreach (var guid in expected)
                {
                    if (!unlocked.Contains(guid)) return Fail(step, $"{label} {guid} が解放されていない");
                }
                return null;
            }

            string Fail(ResearchWalkStep step, string reason)
            {
                return $"[ResearchWalk] FAIL {step.Name} ({step.ResearchNodeGuid}): {reason}";
            }

            #endregion
        }
    }
}
```

- [ ] **Step 3: 入口に G3 を足す**（ファイル全体を次の内容に置き換える）

`moorestech_server/Assets/Scripts/Editor/MasterGate/ShippedMasterGateEntry.cs`:
```csharp
using UnityEditor;

namespace Server.MasterGate.Editor
{
    /// <summary>
    ///     ゲートの入口。batch の入口は終了コード（0=PASS / 1=FAIL）で返し、理由は Unity のログに出す。static マスタの書き換えは ShippedMasterOwner だけが行う
    ///     Gate entries; batch entries report via the exit code (0 = PASS / 1 = FAIL) and the reason goes to the Unity log; only ShippedMasterOwner rewrites the static master
    /// </summary>
    public static class ShippedMasterGateEntry
    {
        // G1 だけを回す（置き場は -masterDataRoot で必須。無ければ理由をログに出して 1 で終わる）
        // Run G1 only (-masterDataRoot is required; without it the reason is logged and the exit code is 1)
        public static void RunLoadGateFromCommandLine()
        {
            var passed = MasterGateArguments.TryResolveServerDataDirectory(out var serverDataDirectory)
                         && RunLoadGate(serverDataDirectory, out _);
            EditorApplication.Exit(passed ? 0 : 1);
        }

        // CI の入口。G1 → G3 の順に回す（-masterDataRoot と、-researchOrder に G2 の出力が要る）
        // CI entry: run G1 then G3 (needs -masterDataRoot and -researchOrder pointing at G2's output)
        public static void RunFromCommandLine()
        {
            var passed = MasterGateArguments.TryResolveServerDataDirectory(out var serverDataDirectory)
                         && RunAll(serverDataDirectory, MasterGateArguments.FindResearchOrderPath());
            EditorApplication.Exit(passed ? 0 : 1);
        }

        // G1 の唯一の入口（batch・手元の G1 関数・RunAll がここを通る）
        // The single G1 entry (batch, the local G1 function and RunAll all go through it)
        public static bool RunLoadGate(string serverDataDirectory, out string failure)
        {
            return ShippedMasterOwner.TryLoad(serverDataDirectory, out failure);
        }

        // Editor を閉じずに判定する入口（uloop execute-dynamic-code から呼ぶ）。G1 が落ちたら G3 は回さない
        // Judge without quitting the Editor (called from uloop execute-dynamic-code); G3 is skipped when G1 fails
        public static bool RunAll(string serverDataDirectory, string researchOrderPath)
        {
            if (!RunLoadGate(serverDataDirectory, out _)) return false;
            if (!ResearchWalkOrderFile.TryRead(researchOrderPath, out var steps, out _)) return false;
            if (!ShippedMasterOwner.TryCreateServer(serverDataDirectory, out var serviceProvider, out _)) return false;
            return ResearchWalkRunner.Run(serviceProvider, steps, out _);
        }
    }
}
```

- [ ] **Step 4: コンパイル**
  Run: `cd $W && uloop compile --project-path ./moorestech_client`
  Expected: エラー 0。

- [ ] **Step 5: ピン先のマスタで G2 に研究順を書かせ、G3 を回す**
  - Task 10 Step 6 の `$GL/pin-master` を使う。
  - G2 の判定は赤でも、研究順は書かれる（`check.py` は判定の前に `write_order` を呼ぶ）。
```bash
python3 $MW/tools/progression/check.py --master-dir $GL/pin-master/server_v8/mods/moorestechAlphaMod_8/master --emit-order $GL/research_order.json > /dev/null; echo "g2 exit=$? (赤でよい)"
python3 -c "import json;print(len(json.load(open('$GL/research_order.json'))['order']))"
cd $W && uloop execute-dynamic-code --project-path ./moorestech_client --code "return Server.MasterGate.Editor.ShippedMasterGateEntry.RunAll(\"$GL/pin-master/server_v8\", \"$GL/research_order.json\") ? \"G3 PASS\" : \"G3 FAIL\";"
cd $W && uloop get-logs --project-path ./moorestech_client --log-type All | grep '\[ResearchWalk\]\|\[ShippedMasterOwner\]' | tee $GL/g3-base.txt
```
Expected:
- `44` が出る。
- 合格なら `G3 PASS` と `[ResearchWalk] PASS 44 nodes completed in G2 order` が出る。
- `G3 FAIL` の場合は FAIL 行の理由で切り分ける。どれも本物の不一致なので、ゲートの側を緩めない。
  - 「主インベントリに入らない」: 研究コストが主インベントリの容量を超えている。`ResearchDataStore.cs:79` の判定も主インベントリだけを見るので、ゲーム内でも払えない。P2 でコストかスタック上限を直す対象として起草者メモ 5 に従う。
  - 「支払い前の状態が UnresearchableNotEnoughPreNode」: G2 の前提モデルと実装がずれている。直す先は Task 2 の `research_walk.py`。
  - 「解放されていない」: G2 の `unlock_index.py` が実装と違う解放を当てにしている。
- 要確認（実装時にこの Step で確かめる）: 初期の研究のコストが主インベントリに収まるか。

- [ ] **Step 6: 研究順を 1 か所わざと壊して、G3 が落ちることを確かめる**（2 段目以降のノードを先頭へ動かす）
```bash
python3 - "$GL/research_order.json" "$GL/research_order_broken.json" <<'EOF'
import json, sys
doc = json.load(open(sys.argv[1], encoding='utf-8'))
order = doc['order']
moved = order.pop(next(i for i, r in enumerate(order) if r['wave'] >= 2))
order.insert(0, moved)
print('moved', moved['name'])
json.dump(doc, open(sys.argv[2], 'w', encoding='utf-8'), ensure_ascii=False, indent=2)
EOF
cd $W && uloop execute-dynamic-code --project-path ./moorestech_client --code "return Server.MasterGate.Editor.ShippedMasterGateEntry.RunAll(\"$GL/pin-master/server_v8\", \"$GL/research_order_broken.json\") ? \"G3 PASS\" : \"G3 FAIL\";"
cd $W && uloop get-logs --project-path ./moorestech_client --log-type Error | grep '\[ResearchWalk\] FAIL'
```
Expected:
- `moved 原始研究2` が出る。2 段目の先頭は原始研究2（Task 2 Step 9 の ROUTE と同じ並び）。
- 続いて `G3 FAIL` が出る。
- Error ログに `[ResearchWalk] FAIL 原始研究2 (…): 支払い前の状態が UnresearchableAllReasons（前提の判定が G2 とずれている）` が出る。前提とコストの両方が欠けているため。状態の名前が `UnresearchableNotEnoughPreNode` でも、FAIL 行が出れば合格。

- [ ] **Step 7: ワークフローを G1 → G3 に広げる**
  - `shipped-master-gate.yml` の `Shipped master gate` の step の直前に、次の step を足す。
  - G2 の合否はマスタ repo の CI が持つ。ここでは研究順が書けたかだけを見て、書けなければ落とす。G2 が赤いことは PR の warning 注記で見える。
```yaml
      # G3 の研究順はピン先マスタの G2 が書く。G2 の合否はマスタ repo の CI が持つので、ここでは研究順が書けたかだけで落とす
      # The pinned master's G2 writes G3's research order; G2's verdict belongs to the master repo CI, so this step only fails when no order was written
      - name: Emit research order from G2
        shell: bash
        run: |
          python3 moorestech_master/tools/progression/check.py --emit-order "$GITHUB_WORKSPACE/research_order.json" > g2.txt || echo "::warning::G2 is red on the pinned master ($(tail -1 g2.txt)); the master repo CI owns that verdict"
          test -f "$GITHUB_WORKSPACE/research_order.json"
```
  同じ step の `with:` を次のように変える（`buildMethod` を差し替え、`customParameters` は `-masterDataRoot` を残したまま `-researchOrder` を足す）。名前も `Shipped Master Gate (G1)` から `Shipped Master Gate (G1+G3)` に変える。
```yaml
          buildMethod: Server.MasterGate.Editor.ShippedMasterGateEntry.RunFromCommandLine
          customParameters: '-masterDataRoot /github/workspace/moorestech_master -researchOrder /github/workspace/research_order.json'
```
  - `/github/workspace` は game-ci の docker が `$GITHUB_WORKSPACE` をマウントする先。要確認（実装時、最初の run のログに `[ResearchWalk] FAIL research order file not found` が出たら、そこに出たパスとマウント先を突き合わせる）。
  - Run: `ruby -ryaml -e 'y=YAML.load_file(ARGV[0]); p y["jobs"]["gate"]["steps"].map{|s| s["name"]}' $W/.github/workflows/shipped-master-gate.yml`
  - Expected: 並びの最後の 2 つが `"Emit research order from G2", "Shipped master gate"`。

- [ ] **Step 8: コミットする**
```bash
git -C $W add moorestech_server/Assets/Scripts/Editor/MasterGate .github/workflows/shipped-master-gate.yml
git -C $W commit -m "ci: 出荷マスタで G2 の研究順に全ノードを完了させる研究ウォーク G3 を G1 の job に足す"
git -C $W push
```

**完了判定:**
- Step 5 で `[ResearchWalk] PASS 44 nodes` が出る。または FAIL 行の理由（主インベントリの容量など）が `$GL/g3-base.txt` に控えてあり、起草者メモ 5 の扱いで本体に報告されている。
- Step 6 で壊した順が `[ResearchWalk] FAIL` になる。
- CI について:
  - G3 の CI は、ピンが Task 2 のコミット（`tools/progression` を含むマスタ統合ブランチ）以降を指すまで、`Emit research order from G2` の step で落ちる（`check.py` が無いため）。これは意図どおり。
  - ピンが P2 でマスタ統合 PR の先頭へ動いたときに緑になることを、P2 の完了判定で確かめる。

---

### Task 3: G6 バランスゲートの枠（S1 sim と S3 の検査を 1 本にまとめる入れ物）

**Files:**
- Create: `moorestech_master:tools/tier9_balance/{run,checks,hour_targets,master_view}.py`
- Create: `moorestech_master:tools/tier9_balance/sections/{__init__,s1_semiconductor,s2_compute_loop,s3_endgame}.py`
- Create: `moorestech_master:design/tier9-balance/sim/` の 7 本（S1 handoff「Task D1」Files の sim と同じ名前・同じ中身。置き場だけ `design/tier9-semiconductor-balance/sim/` から変える。Step 7）
- Test: `moorestech_master:tools/tier9_balance/tests/test_balance_frame.py`
- Modify: `moorestech_master:.github/workflows/master-gates.yml`（job `balance` を足す）

**Interfaces:**
- Consumes: 出荷マスタの JSON（`items/blocks/fluids/machineRecipes/research/cleanRoom`）。S1 handoff「Task D1 Step 1」の sim のコードブロック（`$W/docs/superpowers/plans/2026-10-23-tier9-s1-master-content-handoff.md`）。
- Produces（P2 が区間の中身を書くときの約束）:
  - S1 の sim `design/tier9-balance/sim/`（値の正本。判断記録 A22）。名前は S1 handoff「Task D1」Interfaces の Produces どおり: `params` の `LINE_SI_PER_S`・`CLASS_PARAMS`・`DIES_PER_WAFER`・`PROMOTION_CAP`・`QUALITY_REDUCTION_CAP`・`DIE_TABLES`・`MODULES`・`STACK_LIMITS`・`RECIPES`・`POWER`・`POLLUTION`・`FILTER_Q`・`FILTER_CAPACITY`・`THRESHOLDS`、`timeline.RESEARCH`・`timeline.run(クラス)["done"]`（研究名 → 完了時刻[h]）・`timeline.line_rate`・`timeline.EXTRA_LINES_AT_H`（Task 22 の S3 区間が使う）、`targets.evaluate() -> [(目標, 合否, 実測)]`。sim は互いをトップレベル名で import するので、使う側（Task 12 の `sections/s1/sim_bridge.py`）が `design/tier9-balance/sim` を `sys.path` に足す。
  - 区間モジュールは `evaluate(view: MasterView) -> SectionResult(name, checks, hours: Resolved)` を持つ。`name` は `hour_targets.SECTION_HOURS` のキー（`s1_semiconductor` / `s2_compute_loop` / `s3_endgame`）。`hours` は時間[h]を `Resolved` で持つ（中身が未実装の区間は `unimplemented(理由)`）。
  - 値の状態（判断記録 A31。None を「引けない」の合図に使わない）:
    - `checks.Resolved(status, value, reason)`。`status` は `ok` / `lookup_failed` / `not_positive` / `empty` のどれか。`value` は `ok` のときだけ読め、それ以外で読むと `ValueError`。`.ok`、`.map(変換)`（引けた値だけ変換）、`.then(次の引き当て)`（Resolved を返す関数をつなぐ）を持つ。
    - 作り方: `resolved(v)`、`unresolved(理由)`（lookup_failed）、`nonpositive(v, 理由)`、`unimplemented(理由)`（empty）、`require_positive(Resolved, 理由)`（0 以下を not_positive にする）、`failures([Resolved]) -> 引けないものの list`、`carried_failure([Resolved], 何の値か)`（最初の失敗の状態を導いた値へ引き継ぐ）、`field(dict, key, どこ)`、`element(list, index, どこ)`、`single(list, 何か)`（ちょうど 1 つのときだけ引けた扱い。0 件と 2 件以上は理由を分ける）。
  - 検査の作り方（`Check(kind, status, name, detail)`。`kind` は `target` / `match` / `section` の 3 つ、`status` は上の値の状態に `mismatch` / `out_of_range` を足したもの。`passed` は `status == 'ok'`）:
    - `checks.matches(name, sim_value, json_value: Resolved, tolerance)`: JSON が引けなければ、その状態（`lookup_failed` など）の行になる。
    - `checks.target_range(name, value: Resolved, low, high)`: 引けなければその状態、範囲外は `out_of_range`。
    - `checks.sim_target(name, passed, measured)`: S1 sim の `targets.evaluate()` の 1 行を写す。
    - `checks.lookup_failed(kind, name, reason)`: 値を作れない検査を、種類つきで `lookup_failed` の行にする。
    - `checks.unreadable(kind, name, value: Resolved)`: 引けなかった値から行を作る（状態と理由を値から写す）。
  - `MasterView` が名前で引くもの: `items / blocks / fluids / recipes / research / clean_room / families / payload_rows`（`items_doc` は items.json の全体、`master_dir` は読んだ master の置き場）。引き当ては `Resolved` を返す: `block_param(block_name, key)`、`guid_of(name)`（アイテム名か液体名）、`input_guid(name)`（アイテム名・levelFamily 名〈基準アイテム〉・液体名のどれか 1 つ）、`name_of(guid)`、`payload_row(item_guid)`、`recipe_by_output(block_name, output_name)`（0 本と 2 本以上は理由を分ける）、`recipe_input_amount(recipe, input_name)`、`research_cost(node_name, item_name)`。表示用の `label_of(guid)`（引けなければ `?GUID`）。モジュール関数 `entry_guid(entry)`・`entry_amount(entry)`（レシピ・積荷の 1 行の GUID と量。アイテムと液体を 1 か所で正規化する。判断記録 A32）。
  - `python3 tools/tier9_balance/run.py [--master-dir DIR]`: 1 行ずつ `PASS|FAIL [区間] 種類 状態 名前: 詳細` を出し、最後に `== RESULT: PASS|FAIL (k failures / n checks)` を出す。FAIL が 1 件でもあれば exit 1。検査 0 件の区間（`section empty`）と、所要時間が引けない区間も FAIL にする。
  - 「照合と引き当てに FAIL が無い」の判定は、区間の FAIL 行から `target out_of_range`（目標外。値の確定は Task 23）を除いた数が 0 であること: `grep '^FAIL \[<区間>\] ' <出力> | grep -vc ' target out_of_range '`。
  - 目標時間: S1 6〜8 h（S1 handoff RD11、`docs/superpowers/plans/2026-10-23-tier9-s1-master-content-handoff.md:20`）、S2 計算データのループ 4〜6 h（S2 plan `…tier9-compute-data-loop.md:3568`）、S3 5〜11 h、全体 15〜25 h（S3 plan R23 `…tier9-fusion-elevator-endgame.md:3393`）。

- [ ] **Step 1: 判定の部品を置く**

`tools/tier9_balance/checks.py`:
```python
#!/usr/bin/env python3
# バランス検査の1行と、マスタから引いた値の状態。状態は1つの値で持ち、None を「引けない」の合図に使わない（判断記録 A31）
# One balance check row and the state of a value read from the master; one status value, never None as "unreadable" (decision A31)

# 値の状態（Resolved.status）と検査の状態（Check.status）の語彙。ok 以外はすべて FAIL
# Vocabulary of value states (Resolved.status) and check states (Check.status); everything but ok fails
OK = 'ok'
MISMATCH = 'mismatch'
OUT_OF_RANGE = 'out_of_range'
LOOKUP_FAILED = 'lookup_failed'
NOT_POSITIVE = 'not_positive'
EMPTY = 'empty'
# 検査の種類。目標範囲・sim との照合・区間そのもの（検査0件）の3つだけ
# Check kinds: a target range, a sim match, or the section itself (zero checks)
KINDS = ('target', 'match', 'section')


class Resolved:
    # マスタから引いた値か、引けなかった理由。value は status が ok のときだけ読め、それ以外で読むと ValueError
    # A value read from the master, or why it could not be; value is readable only when status is ok, otherwise ValueError
    def __init__(self, status, value, reason):
        self.status = status
        self._value = value
        self.reason = reason

    @property
    def ok(self):
        return self.status == OK

    @property
    def value(self):
        if self.status != OK:
            raise ValueError(f'status={self.status} の値は読めない: {self.reason}')
        return self._value

    def map(self, convert):
        # 引けた値だけを変換し、引けなかった状態と理由はそのまま渡す
        # Convert only a readable value; an unreadable state and its reason pass through unchanged
        return resolved(convert(self._value)) if self.ok else self

    def then(self, lookup):
        # 引けた値で次の引き当て（Resolved を返す）をつなぐ
        # Chain the next lookup (which returns a Resolved) from a readable value
        return lookup(self._value) if self.ok else self


def resolved(value):
    return Resolved(OK, value, '')


def unresolved(reason):
    return Resolved(LOOKUP_FAILED, None, reason)


def nonpositive(value, reason):
    # 0 以下の値は欠損と分けて持つ。値そのものは理由の文に残し、数としては読ませない
    # Keep a non-positive value apart from a missing one; the number stays in the reason text and is not readable
    return Resolved(NOT_POSITIVE, None, f'{value} が 0 以下（{reason}）')


def unimplemented(reason):
    return Resolved(EMPTY, None, reason)


def require_positive(value, reason):
    # 割り算の分母に使う値。引けなければそのまま、0 以下なら not_positive にする
    # A value used as a divisor; pass an unreadable one through, and turn zero or less into not_positive
    if not value.ok:
        return value
    return value if value.value > 0 else nonpositive(value.value, reason)


def failures(values):
    return [v for v in values if not v.ok]


def carried_failure(values, what):
    # 入力のうち最初に引けなかったものの状態を、そこから導く値へ引き継ぐ
    # Carry the state of the first unreadable input over to the value derived from the inputs
    bad = failures(values)
    if not bad:
        raise ValueError(f'{what}: 引き継ぐ失敗が無い（呼び出し側の誤り）')
    return Resolved(bad[0].status, None, f'{what}: {bad[0].reason}')


def field(mapping, key, where):
    return resolved(mapping[key]) if key in mapping else unresolved(f'{where} に {key} が無い')


def element(sequence, index, where):
    return resolved(sequence[index]) if 0 <= index < len(sequence) else unresolved(f'{where} に {index + 1} 番目が無い')


def single(found, what):
    # ちょうど1つに決まるときだけ引けた扱い。0 件と 2 件以上は理由を分けて引けない扱いにする
    # Readable only when exactly one is found; zero and several are unreadable with separate reasons
    if len(found) == 1:
        return resolved(found[0])
    if not found:
        return unresolved(f'{what} が無い')
    return unresolved(f'{what} が {len(found)} 件あり1つに決まらない')


class Check:
    def __init__(self, kind, status, name, detail):
        if kind not in KINDS:
            raise ValueError(f'未知の検査の種類 {kind}')
        self.kind = kind
        self.status = status
        self.name = name
        self.detail = detail

    @property
    def passed(self):
        return self.status == OK


class SectionResult:
    # hours はその区間の所要時間の見積もり[h]を Resolved で持つ。引けない・未実装は状態で表し、全体判定が FAIL にする
    # hours holds the section's playtime estimate [h] as a Resolved; unreadable or unimplemented is a state the overall judgement fails
    def __init__(self, name, checks, hours):
        self.name = name
        self.checks = checks
        self.hours = hours


def target_range(name, value, low, high):
    goal = f'（目標 {low}〜{high}）'
    if not value.ok:
        return Check('target', value.status, name, f'{value.reason}{goal}')
    status = OK if low <= value.value <= high else OUT_OF_RANGE
    return Check('target', status, name, f'{value.value}{goal}')


def matches(name, sim_value, json_value, tolerance):
    # JSON 側は Resolved で受ける。引けなければ、その状態（lookup_failed など）の行になる
    # The JSON side comes as a Resolved; when unreadable, the row takes its state (lookup_failed and so on)
    if not json_value.ok:
        return Check('match', json_value.status, name, f'sim={sim_value} json を引けない: {json_value.reason}')
    status = OK if abs(sim_value - json_value.value) <= tolerance else MISMATCH
    return Check('match', status, name, f'sim={sim_value} json={json_value.value} 許容差={tolerance}')


def sim_target(name, passed, measured):
    # S1 sim の targets.evaluate() の1行。合否は sim が出したものを写す
    # One row of the S1 sim's targets.evaluate(); the verdict is copied from the sim
    return Check('target', OK if passed else OUT_OF_RANGE, name, str(measured))


def lookup_failed(kind, name, reason):
    return Check(kind, LOOKUP_FAILED, name, reason)


def unreadable(kind, name, value):
    # 引けなかった Resolved から作る行。状態（lookup_failed・not_positive など）と理由を値から写す
    # A row built from an unreadable Resolved; the state (lookup_failed, not_positive, ...) and reason are copied from the value
    return Check(kind, value.status, name, value.reason)


def empty_section():
    return Check('section', EMPTY, '検査', '検査が0件（区間の中身が未実装）')
```

`tools/tier9_balance/hour_targets.py`（S1 の sim にある `targets.py` と名前がぶつからないように別名にする）:
```python
#!/usr/bin/env python3
# Tier9 の時間配分の目標（S1 裁定と S2/S3 の引き継ぎ）。区間ごとの範囲と、全区間の合計の範囲を判定する
# Tier9 time-budget targets (S1 ruling and the S2/S3 handoffs); judge each section's range and the total of all sections
from checks import carried_failure, failures, resolved, target_range

SECTION_HOURS = {
    's1_semiconductor': (6.0, 8.0),
    's2_compute_loop': (4.0, 6.0),
    's3_endgame': (5.0, 11.0),
}
TOTAL_HOURS = (15.0, 25.0)


def judge_hours(results):
    checks = []
    for result in results:
        low, high = SECTION_HOURS[result.name]
        checks.append(target_range(f'{result.name} の所要時間[h]', result.hours, low, high))
    # 合計は全区間の時間が引けたときだけ出す。欠けた区間があれば、その状態を合計の行へ引き継ぐ
    # The total exists only when every section has hours; a missing section passes its state on to the total row
    hours = [r.hours for r in results]
    total = carried_failure(hours, '所要時間の無い区間がある') if failures(hours) else resolved(sum(h.value for h in hours))
    checks.append(target_range('Tier9 全体の所要時間[h]', total, *TOTAL_HOURS))
    return checks
```

`tools/tier9_balance/master_view.py`:
```python
#!/usr/bin/env python3
# 出荷マスタを名前で引く読み取り専用の窓口。引けたかどうかは Resolved の状態で返し、例外も None も使わない（判断記録 A31）
# Read-only name lookups over the shipped master; whether a lookup worked is a Resolved state, never an exception or None (decision A31)
import json
import os

from checks import field, resolved, single, unresolved


def entry_guid(entry):
    # レシピ・積荷・資材の1行の GUID。アイテムは itemGuid、液体は fluidGuid を持つ
    # The GUID of one recipe/payload/material row; items carry itemGuid and fluids carry fluidGuid
    return entry['itemGuid'] if 'itemGuid' in entry else entry['fluidGuid']


def entry_amount(entry):
    return entry['count'] if 'count' in entry else entry['amount']


class MasterView:
    def __init__(self, master_dir):
        # S1 区間がローカライズ行を master の隣から読むので置き場を持つ
        # Keep the location because the S1 section reads localization rows next to the master
        self.master_dir = master_dir

        def load(name):
            with open(os.path.join(master_dir, name), encoding='utf-8') as f:
                return json.load(f)
        self.items_doc = load('items.json')
        self.items = {i['name']: i for i in self.items_doc['data']}
        self.blocks = {b['name']: b for b in load('blocks.json')['data']}
        self.fluids = {f['name']: f for f in load('fluids.json')['data']}
        self.recipes = load('machineRecipes.json')['data']
        self.research = {r['researchNodeName']: r for r in load('research.json')['data']}
        self.clean_room = load('cleanRoom.json')['data'][0]
        self.families = {f['name']: f for f in self.items_doc.get('levelFamilies', [])}
        self.payload_rows = {r['payloadItemGuid']: r for r in self.items_doc.get('orbitalPayloads', [])}
        self._names = {i['itemGuid']: n for n, i in self.items.items()}
        self._names.update({f['fluidGuid']: n for n, f in self.fluids.items()})

    def block_param(self, block_name, key):
        if block_name not in self.blocks:
            return unresolved(f'ブロック「{block_name}」が blocks.json に無い')
        param = self.blocks[block_name].get('blockParam') or {}
        return resolved(param[key]) if key in param else unresolved(f'ブロック「{block_name}」の blockParam に {key} が無い')

    def guid_of(self, name):
        # アイテム名か液体名から GUID を引く。両方に同じ名前があれば1つに決まらないので引けない扱い
        # Find a GUID by item or fluid name; a name present as both is ambiguous and counts as unreadable
        found = [m[name][k] for m, k in ((self.items, 'itemGuid'), (self.fluids, 'fluidGuid')) if name in m]
        return single(found, f'アイテム・液体「{name}」')

    def input_guid(self, name):
        # sim のレシピ入力の名前を GUID にする。アイテム名・levelFamily 名（基準アイテム）・液体名のどれか1つに当たること
        # Turn a sim recipe input name into a GUID: exactly one of an item name, a levelFamily name (its base item) or a fluid name
        found = {self.items[name]['itemGuid']} if name in self.items else set()
        found |= {self.families[name]['baseItemGuid']} if name in self.families else set()
        found |= {self.fluids[name]['fluidGuid']} if name in self.fluids else set()
        return single(sorted(found), f'レシピ入力「{name}」（アイテム・levelFamily・液体）')

    def name_of(self, guid):
        return resolved(self._names[guid]) if guid in self._names else unresolved(f'GUID {guid} が items.json・fluids.json に無い')

    def label_of(self, guid):
        # 表示と名前の突き合わせ用の名前。引けない GUID は「?GUID」にし、どの実名とも一致しないので比べた行が FAIL する
        # A name for display and name comparison; an unknown GUID becomes "?GUID", matching no real name, so the compared row fails
        return self._names[guid] if guid in self._names else f'?{guid}'

    def payload_row(self, item_guid):
        return field(self.payload_rows, item_guid, 'items.json の orbitalPayloads')

    def recipe_by_output(self, block_name, output_name):
        # 機械名と出力名（アイテムか液体）でレシピを1本引く。0本と2本以上は理由を分けて引けない扱いにする
        # Find exactly one recipe by machine and output name (item or fluid); zero and several are unreadable with separate reasons
        if block_name not in self.blocks:
            return unresolved(f'ブロック「{block_name}」が blocks.json に無い')
        block_guid = self.blocks[block_name]['blockGuid']
        return self.guid_of(output_name).then(lambda guid: single(
            [r for r in self.recipes if r['blockGuid'] == block_guid
             and any(entry_guid(o) == guid for o in r['outputItems'] + r['outputFluids'])],
            f'{block_name} の出力「{output_name}」のレシピ'))

    def recipe_input_amount(self, recipe, input_name):
        # レシピの入力のうち、名前が指す GUID の行の量。行が1つに決まらなければ引けない扱い
        # The amount of the recipe input row the name points at; unreadable unless exactly one row matches
        return self.input_guid(input_name).then(lambda guid: single(
            [entry_amount(e) for e in recipe['inputItems'] + recipe['inputFluids'] if entry_guid(e) == guid],
            f'レシピ {recipe["machineRecipeGuid"]} の入力「{input_name}」'))

    def research_cost(self, node_name, item_name):
        if node_name not in self.research:
            return unresolved(f'研究「{node_name}」が research.json に無い')
        return self.guid_of(item_name).then(lambda guid: single(
            [c['itemCount'] for c in self.research[node_name]['consumeItems'] if c['itemGuid'] == guid],
            f'研究「{node_name}」のコスト「{item_name}」'))

```

`tools/tier9_balance/run.py`:
```python
#!/usr/bin/env python3
# Tier9 バランスゲート（G6）。各区間の sim と出荷マスタを照合し、目標時間を判定する。1件でも FAIL なら exit 1
# Tier9 balance gate (G6): match each section's sim against the shipped master and judge the hour targets; exit 1 on any FAIL
# 検査0件の区間も FAIL にする（空の検査で緑にしない）。行は「PASS|FAIL [区間] 種類 状態 名前: 詳細」
# A section with zero checks also fails, so an empty check can never turn green; rows read "PASS|FAIL [section] kind status name: detail"
import argparse
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from checks import empty_section  # noqa: E402
from hour_targets import judge_hours  # noqa: E402
from master_view import MasterView  # noqa: E402
from sections import SECTIONS  # noqa: E402

HERE = os.path.dirname(os.path.abspath(__file__))
DEFAULT_MASTER = os.path.join(HERE, '..', '..', 'server_v8', 'mods', 'moorestechAlphaMod_8', 'master')


def run(view, sections):
    results = [section.evaluate(view) for section in sections]
    rows = []
    for result in results:
        if not result.checks:
            rows.append((result.name, empty_section()))
        rows.extend((result.name, c) for c in result.checks)
    rows.extend(('overall', c) for c in judge_hours(results))
    return rows


def main(argv):
    parser = argparse.ArgumentParser(description='Tier9 balance gate (G6)')
    parser.add_argument('--master-dir', default=DEFAULT_MASTER)
    args = parser.parse_args(argv)
    rows = run(MasterView(args.master_dir), SECTIONS)
    for section, check in rows:
        print(f"{'PASS' if check.passed else 'FAIL'} [{section}] {check.kind} {check.status} {check.name}: {check.detail}")
    failures = sum(1 for _, c in rows if not c.passed)
    print(f'== RESULT: {"FAIL" if failures else "PASS"} ({failures} failures / {len(rows)} checks)')
    return 1 if failures else 0


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
```

- [ ] **Step 2: 区間の並びと 3 つの空の区間を置く**（P2 で中身を書くまでは、検査 0 件・所要時間は未実装〈`empty`〉で赤い）

`tools/tier9_balance/sections/__init__.py`:
```python
# 区間の並びの正本。区間を足すときは hour_targets.SECTION_HOURS にも同じ名前で範囲を足す
# The single list of sections; adding one also needs its range under the same name in hour_targets.SECTION_HOURS
from sections import s1_semiconductor, s2_compute_loop, s3_endgame

SECTIONS = (s1_semiconductor, s2_compute_loop, s3_endgame)
```

`tools/tier9_balance/sections/s1_semiconductor.py`:
```python
#!/usr/bin/env python3
# S1 半導体（半導体製造の解放→S1 最後の研究）。Task 12 で sim の目標判定・所要時間と照合 8 項目（sections/s1/）を書く
# S1 semiconductor (unlock to last S1 research); Task 12 fills in the sim's targets and hours plus the eight matches (sections/s1/)
# P2 で中身を書くまでは検査0件・所要時間は未実装（empty）を返し、ゲートは赤のまま
# Until P2 fills it, it returns zero checks and unimplemented (empty) hours, so the gate stays red
from checks import SectionResult, unimplemented


def evaluate(view):
    return SectionResult('s1_semiconductor', [], unimplemented('区間の中身が未実装（Task 12 で書く）'))
```

`tools/tier9_balance/sections/s2_compute_loop.py`:
```python
#!/usr/bin/env python3
# S2 計算データのループ（打ち上げ→計算データ→アップグレード研究）。Task 19 で台数計算と研究コストの照合を書く
# S2 compute-data loop (launch to data to upgrade research); Task 19 writes the machine-count math and research-cost matching
# Task 19 で中身を書くまでは検査0件・所要時間は未実装（empty）を返し、ゲートは赤のまま
# Until Task 19 fills it, it returns zero checks and unimplemented (empty) hours, so the gate stays red
from checks import SectionResult, unimplemented


def evaluate(view):
    return SectionResult('s2_compute_loop', [], unimplemented('区間の中身が未実装（Task 19 で書く）'))
```

`tools/tier9_balance/sections/s3_endgame.py`:
```python
#!/usr/bin/env python3
# S3 終盤（核融合・反物質・グラフェン・エレベーター）。Task 22 で S3 plan 旧 Task 4/12 の検査を関数に直して書く
# S3 endgame (fusion, antimatter, graphene, elevator); Task 22 rewrites the S3 plan's old Task 4/12 checks as functions
# Task 22 で中身を書くまでは検査0件・所要時間は未実装（empty）を返し、ゲートは赤のまま
# Until Task 22 fills it, it returns zero checks and unimplemented (empty) hours, so the gate stays red
from checks import SectionResult, unimplemented


def evaluate(view):
    return SectionResult('s3_endgame', [], unimplemented('区間の中身が未実装（Task 22 で書く）'))
```

- [ ] **Step 3: 枠の自己テストを置く**（全部合えば通る／sim と JSON の食い違い〈mismatch〉・JSON が引けない〈lookup_failed〉・0 以下〈not_positive。値も読めない〉・区間の所要時間が範囲外〈out_of_range〉・検査 0 件〈section empty〉・所要時間なし〈状態が合計へ引き継がれる〉は、それぞれ落ちる／合計は区間の和で、境界を含む）

`tools/tier9_balance/tests/test_balance_frame.py`:
```python
#!/usr/bin/env python3
# バランスゲートの枠: 全部合えば通り、照合の食い違い・引けない値・0以下・目標外・検査0件・所要時間欠落のどれでも状態つきで失敗する
# Balance gate frame: passes when all agree, and fails with a status on a mismatch, unreadable or non-positive value, out-of-range target, zero checks or missing hours
import os
import sys
import unittest

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
import checks  # noqa: E402
import run  # noqa: E402


class FakeSection:
    def __init__(self, name, rows, hours):
        self.name, self.rows, self.hours = name, rows, hours

    def evaluate(self, view):
        return checks.SectionResult(self.name, self.rows, self.hours)


def ok_row():
    return checks.matches('値', 1.0, checks.resolved(1.0), 0.0)


def sections(s1_rows, s1_hours):
    return [FakeSection('s1_semiconductor', s1_rows, s1_hours),
            FakeSection('s2_compute_loop', [ok_row()], checks.resolved(5.0)),
            FakeSection('s3_endgame', [ok_row()], checks.resolved(7.0))]


def dies(json_value):
    return [checks.matches('ダイ数', 300, json_value, 0)]


def failed(rows):
    return [f'{s}:{c.kind}:{c.status}:{c.name}' for s, c in rows if not c.passed]


class BalanceFrameTest(unittest.TestCase):
    def test_all_agree_passes(self):
        rows = run.run(None, sections(dies(checks.resolved(300)), checks.resolved(7.0)))
        self.assertEqual([], failed(rows))

    def test_sim_json_mismatch_fails(self):
        rows = run.run(None, sections(dies(checks.resolved(250)), checks.resolved(7.0)))
        self.assertEqual(['s1_semiconductor:match:mismatch:ダイ数'], failed(rows))

    def test_unreadable_json_value_fails_as_lookup(self):
        rows = run.run(None, sections(dies(checks.unresolved('cleanRoom.json に dicings が無い')), checks.resolved(7.0)))
        self.assertEqual(['s1_semiconductor:match:lookup_failed:ダイ数'], failed(rows))

    def test_nonpositive_divisor_is_its_own_state_and_unreadable(self):
        value = checks.require_positive(checks.resolved(0.0), '1台の毎秒')
        self.assertEqual(checks.NOT_POSITIVE, value.status)
        with self.assertRaises(ValueError):
            value.value
        rows = run.run(None, sections([checks.target_range('台数', value, 0.0, 4.0)], checks.resolved(7.0)))
        self.assertEqual(['s1_semiconductor:target:not_positive:台数'], failed(rows))

    def test_section_hours_out_of_range_fails(self):
        rows = run.run(None, sections(dies(checks.resolved(300)), checks.resolved(9.0)))
        self.assertIn('overall:target:out_of_range:s1_semiconductor の所要時間[h]', failed(rows))

    def test_total_is_sum_of_sections_with_inclusive_bounds(self):
        rows = run.run(None, [FakeSection('s1_semiconductor', [ok_row()], checks.resolved(6.0)),
                              FakeSection('s2_compute_loop', [ok_row()], checks.resolved(4.0)),
                              FakeSection('s3_endgame', [ok_row()], checks.resolved(5.0))])
        self.assertEqual([], failed(rows))
        self.assertIn('15.0（目標 15.0〜25.0）', [c.detail for s, c in rows if s == 'overall'][-1])

    def test_empty_section_fails(self):
        rows = run.run(None, sections([], checks.resolved(7.0)))
        self.assertIn('s1_semiconductor:section:empty:検査', failed(rows))

    def test_missing_hours_carry_their_state_to_the_total(self):
        rows = run.run(None, sections(dies(checks.resolved(300)), checks.unimplemented('未実装')))
        self.assertEqual(['overall:target:empty:s1_semiconductor の所要時間[h]', 'overall:target:empty:Tier9 全体の所要時間[h]'],
                         failed(rows))


if __name__ == '__main__':
    unittest.main()
```

- [ ] **Step 4: 自己テストを回す**（`test_sim_json_mismatch_fails` が「値を 1 か所わざと食い違わせると落ちる」の確認にあたる）
  Run: `cd $MW && python3 -m unittest discover -s tools/tier9_balance/tests`
  Expected: `Ran 8 tests in …` / `OK`。

- [ ] **Step 5: 統合マスタで回し、中身が無いため赤いことを控える**
  Run: `cd $MW && python3 tools/tier9_balance/run.py > $GL/g6-base.txt; echo "exit=$?"; cat $GL/g6-base.txt`
  Expected: `exit=1`。7 行の FAIL が出る。
  - `FAIL [s1_semiconductor] section empty 検査: 検査が0件（区間の中身が未実装）`。`[s2_compute_loop]`・`[s3_endgame]` も同じ。
  - 区間ごとの所要時間 3 行。例: `FAIL [overall] target empty s1_semiconductor の所要時間[h]: 区間の中身が未実装（Task 12 で書く）（目標 6.0〜8.0）`。
  - 全体 1 行: `FAIL [overall] target empty Tier9 全体の所要時間[h]: 所要時間の無い区間がある: 区間の中身が未実装（Task 12 で書く）（目標 15.0〜25.0）`。
  - 末尾は `== RESULT: FAIL (7 failures / 7 checks)`。

- [ ] **Step 6: 1 か所わざと壊して照合が落ちることを、本物のマスタでも確かめる**（一時的な区間を外から差し込む。ファイルは作らない）
```bash
cd $MW/tools/tier9_balance && python3 - "$MASTER" <<'EOF'
import sys
sys.path.insert(0, '.')
import checks, run
from master_view import MasterView
view = MasterView(sys.argv[1])
HOURS = {'s1_semiconductor': 7.0, 's2_compute_loop': 5.0, 's3_endgame': 7.0}
class Probe:
    def __init__(self, name, json_value):
        self.name, self.json_value = name, json_value
    def evaluate(self, v):
        rows = [checks.matches('probe', 1.0, self.json_value, 0.0)]
        return checks.SectionResult(self.name, rows, checks.resolved(HOURS[self.name]))
one, two = checks.resolved(1.0), checks.resolved(2.0)
ok = [Probe('s1_semiconductor', one), Probe('s2_compute_loop', one), Probe('s3_endgame', one)]
bad = [Probe('s1_semiconductor', two), Probe('s2_compute_loop', one), Probe('s3_endgame', view.research_cost('存在しない研究', '銅板'))]
print('ok', [f'{s}:{c.kind}:{c.status}' for s, c in run.run(view, ok) if not c.passed])
print('bad', [f'{s}:{c.kind}:{c.status}' for s, c in run.run(view, bad) if not c.passed])
EOF
```
Expected（#88+#87 のマージ結果で実測）:
- `ok []` が出る。区間の時間 7/5/7 はどれも範囲内で、合計 19 も 15〜25 に入る。
- `bad ['s1_semiconductor:match:mismatch', 's3_endgame:match:lookup_failed']` が出る。S1 は sim 1.0 と JSON 2.0 が食い違う。S3 は存在しない名前を引いた結果が `lookup_failed` の `Resolved` で、例外を出さずにその状態の FAIL になる。

- [ ] **Step 7: S1 の sim 7 本を `design/tier9-balance/sim/` に置く**（判断記録 A22: 値の正本の sim は `design/` 側、照合と判定は `tools/tier9_balance/` 側に一本化する）
  - 置き場の差分: S1 handoff「Task D1」の `design/tier9-semiconductor-balance/sim/` を `design/tier9-balance/sim/` に変える（P2 節の横断訂正 1）。中身は 1 文字も変えない。
  - 置かないもの: D1 の README と生成物 `sim/output.md`、D2 の照合 2 本。照合は Task 12 が `tools/tier9_balance/sections/s1/` に `checks.matches` として書く。
  - import の通り方: sim の 7 本は互いを `from params import …` のようにトップレベル名で import する。G6 からは Task 12 の `sections/s1/sim_bridge.py` が `design/tier9-balance/sim` を `sys.path` の先頭に足してから `params`・`timeline`・`targets` を import する。`tools/tier9_balance/` 直下のモジュール名（`checks`・`hour_targets`・`master_view`・`run`）とは名前がぶつからない（枠の側を `hour_targets` にしたのはこのため）。
  - コードは手で写さない。handoff の見出し `` `$B/sim/<名前>.py`: `` の直後の python ブロックを、次のスクリプトでその名前のまま書き出す。
```bash
mkdir -p $MW/design/tier9-balance/sim
python3 - $W/docs/superpowers/plans/2026-10-23-tier9-s1-master-content-handoff.md $MW/design/tier9-balance/sim <<'EOF'
import re, sys
from pathlib import Path
# S1 handoff の「`$B/sim/<名前>.py`:」見出しの直後にある python ブロックを、その名前で書き出す
# Write out each python block that follows a "`$B/sim/<name>.py`:" heading in the S1 handoff under that name
lines = Path(sys.argv[1]).read_text(encoding='utf-8').splitlines()
out, count, i = Path(sys.argv[2]), 0, 0
while i < len(lines):
    m = re.fullmatch(r'`\$B/sim/(\w+\.py)`:', lines[i])
    if m:
        start = lines.index('```python', i) + 1
        end = lines.index('```', start)
        (out / m.group(1)).write_text('\n'.join(lines[start:end]) + '\n', encoding='utf-8')
        count, i = count + 1, end
    i += 1
print(count)
EOF
wc -l $MW/design/tier9-balance/sim/*.py | tail -1
cd $MW/design/tier9-balance && python3 -c "
import sys; sys.path.insert(0, 'sim')
import params, targets, timeline
names = ('LINE_SI_PER_S', 'CLASS_PARAMS', 'DIES_PER_WAFER', 'PROMOTION_CAP', 'QUALITY_REDUCTION_CAP', 'DIE_TABLES', 'MODULES', 'STACK_LIMITS', 'RECIPES', 'POWER', 'POLLUTION', 'FILTER_Q', 'FILTER_CAPACITY', 'THRESHOLDS')
print('missing', [n for n in names if not hasattr(params, n)])
print(params.DIES_PER_WAFER, len(timeline.RESEARCH), timeline.run('B')['done']['モジュールスタック拡張II'])
for name, passed, measured in targets.evaluate():
    print('PASS' if passed else 'FAIL', name, measured)"
```
Expected（本 plan 作成時に handoff の同じコードを scratch へ書き出して実測）:
- 1 行目は `7`。`wc` の合計は `614 total`（最大のファイルでも 138 行）。
- `missing []`（D1 の Produces の名前がすべてある）。続いて `300 7 6.266666666666667`（クラス B の S1 終了 6.27 h）。
- 目標の判定が 12 行出て、FAIL は `FAIL S1 6〜8時間（クラスA） 5.833333333333333h` の 1 行だけ。値は Task 23 で S1 handoff「Task D1 Step 3」の順に直す（この Task では直さない）。

- [ ] **Step 8: マスタ repo の CI に G6 を足す**（`master-gates.yml` の末尾に job を足す）
```yaml

  balance:
    name: Tier9 balance (G6)
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Balance gate self-test
        run: python3 -m unittest discover -s tools/tier9_balance/tests

      # 検査0件の区間も失敗にするので、P2 で区間の中身が入るまで赤い
      # A section with zero checks fails, so this stays red until P2 fills the sections
      - name: Tier9 balance
        run: python3 tools/tier9_balance/run.py
```
  Run: `ruby -ryaml -e 'p YAML.load_file(ARGV[0])["jobs"].keys' $MW/.github/workflows/master-gates.yml`
  Expected: `["reachability", "balance"]`

- [ ] **Step 9: コミットして push する**
```bash
git -C $MW add tools/tier9_balance design/tier9-balance/sim .github/workflows/master-gates.yml
git -C $MW commit -m "tools: Tier9 バランスゲート G6 の枠と S1 の sim を足す（sim と JSON の照合・目標時間・検査0件の区間を FAIL にする）"
git -C $MW push
```

**P2 への引き継ぎ（下書きに対する差分。区間の中身は P2 が書く）:**
- **S1（S1 handoff「Task D1」「Task D2」、RD11・RD12）:**
  - sim の 7 本はこの Task の Step 7 が `design/tier9-balance/sim/` に置いた（値の正本）。
  - `sections/s1_semiconductor.py` の中身は Task 12 が書く。`targets.evaluate()` の結果を `checks.sim_target` へ写し、D2 の照合 8 項目は `sections/s1/` に `checks.matches` として書く（判断記録 A22。D2 の照合用 CLI は作らない）。
  - `hours` には `timeline.run('B')` の S1 最後の研究の完了時刻を `Resolved` で入れる（判断記録 A8）。
  - ゲートの入口は `run.py` だけ。sim を単体で回して表を見るのは値を決めるときの参考で、P2 の完了判定には使わない。
- **S3（S3 plan「旧 Task 4」「旧 Task 12」）:**
  - `tools/balance/tier9_endgame_balance.py` は作らない。
  - 検査は Task 22 Step 6 が `sections/s3_endgame.py` と `sections/s3/` に関数として書き、`MasterView` で引く。名前が引けないときは `sys.exit` ではなく、引けない `Resolved` を `matches`・`target_range` に渡す（判断記録 A31）。
  - 旧 Task 12 の「ロケット1機の建造時間」のキーは S3 ブランチの `VanillaSchema/blocks.yml:1116` の `buildSeconds`（`_s3blocks.yml` で確認）。
  - S3 区間の `hours`（5〜11 h）の見積もりは下書きに無いので、Task 22 Step 6 の `sections/s3/hours_model.py` で式を足す（判断記録 A26）。
- **S2（S2 plan「統合タスクへ引き継ぐマスタの中身」の計算データのループ）:** Task 19 Step 5 が `sections/s2_compute_loop.py` と `sections/s2/` に、計算データを消費する研究ごとの便数、1 便を回す工場の台数、4〜6 h の所要時間を書く（判断記録 A25）。
- **S3 plan の他の指示:** `progression_check.py # 詰みが増えない` は、Task 2 以降は `python3 tools/progression/check.py`（絶対判定の G2）で回す。「増えない」ではなく「許可リスト外が 0」で判定される。

**完了判定:**
- Step 4 で `Ran 8 tests` / `OK` が出る。
- Step 5 で `exit=1`・`7 failures / 7 checks` になる。
- Step 6 で `ok []` と、`bad` に `match` の FAIL 2 件（`mismatch` と `lookup_failed`）が出る。
- Step 7 で sim 7 本が置かれ、`missing []` と `300 7 6.266666666666667` が出る。
- `gh run list -R moorestech/moorestech_master -b feat/tier9-integrated -w "Master Gates" -L 1` の run で、`Balance gate self-test` が success、`Tier9 balance` が failure になる（P2 で緑にする）。

---



## P1 コード統合（機能追加ゼロ。セーブ移行だけ裁定1・2どおりに作り直す）

**この節のゴール:** 統合ブランチ `feat/tier9-integrated` に S3（S2 を含む）を通常のマージコミットで取り込み、黙って壊れる4件（セーブ移行の版重複・同名テストクラス・ADR 番号重複・S1 の解放取り消し）を片付ける。終了時点で compile 0 エラー、関連テストと webui の型検査・vitest がすべて緑になっていること。マスタ（`.moorestech-external-revisions.json` のピン）は P2 で差し替えるので、P1 では触らない。

**前提（実測 2026-10-25）:**
- 作業場所 `WT=/Users/sakastudio/hermes-agent/data/worktrees/moorestech/tier9-integrated`、HEAD `f246e7fd5f`（S1 先端 `2ce6ae7ff2` に裁定コミット2本）。Unity Editor は未起動で、Library は配備済み（`moores-wt status`）。
- `git merge-tree --write-tree HEAD origin/feat/tier9-fusion-elevator-endgame`（S3 先端 `87944450d4`）は衝突23ファイル、結果ツリーは `339f303e`。MERGE.md の表と一致する。
- master 由来の V6→V7 は流体の nano 化（`SaveMigrationStepV6ToV7.cs`、ADR 0090）。テスターのセーブは master の版7だけ（裁定1）。
- `origin/master` 上の ADR は 0090 が最大。全リモートブランチを走査しても 0091 以降を使っているものは無い。

**タスク一覧:** Task 4 マージと衝突解消 → Task 5 同名テストクラスと ADR 番号 → Task 6 V7→V8 の1本化（既存4部品） → Task 7 コスト0研究のリセット部品 → Task 8 研究ロードの無音スキップにログ → Task 9 健全性ゲート。各タスクは前のタスクのコミットの上に積む。

---

### Task 4: S3 をマージして衝突23件を解く

**Files:**
- Modify（衝突解消）: `.moorestech-external-revisions.json` / `Localization/localization.csv` / `moorestech_client/Assets/Scripts/Client.Localization/_CompileRequester.cs` / `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BlockDetail/BlockDetailDtoBuilder.cs` / `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BlockDetail/BlockInventoryDtos.cs` / `moorestech_server/Assets/Scripts/Core.Master/Validator/BlockMasterUtil.cs` / `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs` / `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/WorldVersions/WorldSaveAllInfo.cs` / `moorestech_server/Assets/Scripts/Game.SaveLoad/Migration/Steps/SaveMigrationStepV6ToV7.cs`(+`.meta`) / `moorestech_server/Assets/Scripts/Game.SaveLoad/Migration/Steps/SaveMigrationStepV7ToV8.cs`(+`.meta`) / `moorestech_server/Assets/Scripts/Server.Boot/Composition/SaveAndEventServiceRegistration.cs` / `moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs` / `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/blocks.json` / 同 `items.json` / `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/SaveLoadPreparerTestFixture.cs` / `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/Migration.meta` / `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/SaveMigrationChainTest.cs` / `moorestech_web/webui/src/bridge/contract/schemas/index.ts` / 同 `inventory.ts` / `moorestech_web/webui/src/features/blockInventory/BlockItemGrid.tsx` / 同 `views/SectionStackView.tsx` / `moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts`

**Interfaces:**
- Consumes: `origin/feat/tier9-fusion-elevator-endgame`（`87944450d4`）
- Produces: マージコミット1本。セーブ移行は**一時的に S1 側（HEAD）を残す**（下の方針）。Task 5 が直す既知のコンパイルエラーは CS0101 `Tests.UnitTest.Game.SaveLoad.SaveMigrationStepV7ToV8Test` の重複だけ

**マージ時点のセーブ移行の一時的な扱い（決定）:** 衝突した移行まわりは全部 S1 側（HEAD）を採る。つまり `CurrentVersion=9`、連鎖は master の V6→V7（流体）＋ S1 の V7→V8・V8→V9 のまま。S2 の V6→V7（打ち上げ履歴）と S3 の V7→V8（ケーブルプール）のステップ本体はマージ結果から消える。中身は Task 6 が `git show origin/feat/tier9-compute-data-loop:…` と `git show origin/feat/tier9-fusion-elevator-endgame:…` から部品へ移す。理由: master の V6→V7 はテスターへ出荷済みなので残すしかない。S1 側を採ればコンパイルが通る（S2/S3 のステップは同名クラスで置き換わるだけ）。一方で、`WorldSaveRequiredFieldGuard`（S2、自動マージで入る）は `rocketHasLaunchedOnce` と `spaceElevatorCablePool` を必須にしている。このため Task 6 までの間は、版7以前のセーブを使う結合テスト（`RocketLaunchHistorySaveLoadTest` の版6ケース、`SpaceElevatorUpstreamMigrationSaveLoadTest`、`LevelInheritanceSaveLoadTest.Version7…`）が赤になる。これは想定どおりで、Task 6 が緑に戻す。

- [ ] **Step 1: 作業場所と取り込み元を確かめる**
  Run: `cd $WT && pwd && git status --short && git log --oneline -1 && git fetch origin && git rev-parse --short origin/feat/tier9-fusion-elevator-endgame`
  Expected: `git status` が空。HEAD は `f246e7fd5f` かその子孫。S3 先端が `87944450d4`。S3 先端が違う場合はここで止まり、本体へ「S3 が進んだ」と報告する（以下の解き方は `87944450d4` で実測したもの）

- [ ] **Step 2: 衝突を解く補助スクリプトを git dir に置く**（リポジトリには入れない）
  ```bash
  GITDIR=$(git -C $WT rev-parse --git-dir)
  cat > "$GITDIR/tier9-resolve-hunks.py" <<'EOF'
  # 使い方: python3 tier9-resolve-hunks.py <file> <mode>  mode は ours / theirs / both（ours の後に theirs）
  import sys
  path, mode = sys.argv[1], sys.argv[2]
  out, ours, theirs, state = [], [], [], 0
  for line in open(path, encoding="utf-8").read().split("\n"):
      if state == 0 and line.startswith("<<<<<<< "):
          state, ours, theirs = 1, [], []
          continue
      if state == 1 and line.startswith("||||||| "):
          state = 3
          continue
      if state in (1, 3) and line == "=======":
          state = 2
          continue
      if state == 2 and line.startswith(">>>>>>> "):
          out += {"ours": ours, "theirs": theirs, "both": ours + theirs}[mode]
          state = 0
          continue
      if state == 1: ours.append(line)
      elif state == 2: theirs.append(line)
      elif state == 0: out.append(line)
  if state != 0: sys.exit(f"unterminated conflict: {path}")
  open(path, "w", encoding="utf-8").write("\n".join(out))
  EOF
  ```

- [ ] **Step 3: マージを始め、衝突一覧が23件か確かめる**
  Run: `cd $WT && git merge --no-ff --no-commit origin/feat/tier9-fusion-elevator-endgame; git diff --name-only --diff-filter=U | wc -l`
  Expected: `23`

- [ ] **Step 4: ファイル丸ごと HEAD を採るもの（add/add と生成物）**
  ```bash
  cd $WT
  S=moorestech_server/Assets/Scripts/Game.SaveLoad/Migration/Steps
  git checkout --ours -- \
    $S/SaveMigrationStepV6ToV7.cs $S/SaveMigrationStepV6ToV7.cs.meta \
    $S/SaveMigrationStepV7ToV8.cs $S/SaveMigrationStepV7ToV8.cs.meta \
    moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/Migration.meta \
    moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts
  ```
  意味: V6→V7 は master の流体版を、V7→V8 は S1 版を残す。テストフォルダの GUID は S1 の `64f2f797a2a15479b874a1b82f7b7100` に揃える。`localizationKeys.ts` は Step 9 で生成し直す。

- [ ] **Step 5: 衝突した塊だけ HEAD を採るもの**（自動マージされた S2/S3 の追加分は残す。`git checkout --ours` を使うとそれが消えるので使わない）
  ```bash
  cd $WT; R="python3 $(git rev-parse --git-dir)/tier9-resolve-hunks.py"
  for f in \
    .moorestech-external-revisions.json \
    moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs \
    moorestech_client/Assets/Scripts/Client.Localization/_CompileRequester.cs \
    moorestech_server/Assets/Scripts/Game.SaveLoad/Json/WorldVersions/WorldSaveAllInfo.cs \
    moorestech_server/Assets/Scripts/Server.Boot/Composition/SaveAndEventServiceRegistration.cs \
    moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/SaveLoadPreparerTestFixture.cs \
    moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/SaveMigrationChainTest.cs; do $R "$f" ours; done
  ```
  意味:
  - ピンは S1 の `630fdce08eccee47feaa15721ca607f407000ab9` のままにする（P2 で統合マスタのコミットへ差し替える）。
  - `_CompileRequester` 2本は Step 10 で SchemaWatcher が書き換える。
  - `CurrentVersion=9` と、連鎖の配列（V1〜V8ToV9 の8本）は HEAD 側にする。
  - `WorldSaveAllInfo` の `SpaceElevatorCablePool`・`RocketHasLaunchedOnce` フィールド（S3 側、塊の外）は残る。

- [ ] **Step 6: 両側を残すもの**
  ```bash
  cd $WT; R="python3 $(git rev-parse --git-dir)/tier9-resolve-hunks.py"
  for f in \
    Localization/localization.csv \
    moorestech_server/Assets/Scripts/Core.Master/Validator/BlockMasterUtil.cs \
    moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs \
    moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BlockDetail/BlockDetailDtoBuilder.cs \
    moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BlockDetail/BlockInventoryDtos.cs; do $R "$f" both; done
  ```
  解いた結果（目視で確かめる）:
  - `BlockMasterUtil.cs`: `CleanRoomItemHatchThroughputValidator` → `RocketLaunchPadParamValidator` → `SpaceElevatorBaseParamValidator` の3行。
  - DI: `MachineUpgradeServiceRegistration.Register(services);` → `OrbitServiceRegistration.Register(services);` → `SpaceElevatorServiceRegistration.Register(services);` の順に並び、その後に `SaveAndEventServiceRegistration`。
  - `BlockDetailDtoBuilder`: CleanRoom・Yield・MachineInputStall の3つの Apply の後に `RocketLaunchPadDetailDtoBuilder.Apply(dto, block);`。ファイルは176行。
  - `BlockInventoryDtos`: CleanRoom・CleanRoomYield・MachineInputStall の3フィールドの後に `RocketLaunchPad`。
  - `localization.csv`: S1 の清浄室・停止理由の行の後に S3 の宇宙エレベーターの行（衝突の塊は1つだけ）。キーの重複は無い（解消後の 491 レコードで実測）。

- [ ] **Step 7: webui の4ファイルを手で合わせる**（Edit で塊を次の内容へ置き換える）
  - `schemas/index.ts`: ファイル全体を次の18行にする（`./train` は HEAD の4行目に既にある。S3 側の `./train` を足すと二重 export になるため足さない）
    ```ts
    export * from "./common";
    export * from "./inventory";
    export * from "./energyConversion";
    export * from "./train";
    export * from "./blockInfoFetch";
    export * from "./cleanRoom";
    export * from "./machineInputStall";
    export * from "./ui";
    export * from "./recipes";
    export * from "./research";
    export * from "./challenge";
    export * from "./buildMenu";
    export * from "./hotbar";
    export * from "./presentation";
    export * from "./machineUpgrade";
    export * from "./beltThroughput";
    export * from "./spaceElevator";
    export * from "./rocketLaunchPad";
    ```
  - `schemas/inventory.ts` の塊1（import）は次の6行にする:
    ```ts
    import { CleanRoomDetailDataSchema, CleanRoomYieldDataSchema } from "./cleanRoom";
    import { MachineInputStallDataSchema } from "./machineInputStall";
    import { refineDetailSectionParents } from "./detailSectionParents";
    import { BeltThroughputDataSchema } from "./beltThroughput";
    import { SpaceElevatorDataSchema } from "./spaceElevator";
    import { RocketLaunchPadDataSchema } from "./rocketLaunchPad";
    ```
    塊2（HEAD 側にある `BeltThroughputWindowDataSchema` / `BeltThroughputDataSchema` の定義18行）は**丸ごと消す**。S3 は `1a459de50d` でこれを `schemas/beltThroughput/index.ts` に分けている。中身は1文字も違わないことを実測済み。両方残すと同名 export が二重になる。
    塊3（`BlockInventoryOpenSchema` の欄）は両側を残し、`cleanRoom`・`cleanRoomYield`・`machineInputStall`・`rocketLaunchPad` の4行にする。解消後は195行になる（実測）。
  - `BlockItemGrid.tsx` の塊は次の3行にする（S1 のスロットごとの testId と、S3 の `slotIndices` による絶対添字を合わせる）:
    ```tsx
              testId={`${testId}-slot-${index}`}
              itemId={itemSlots[index].itemId}
              count={itemSlots[index].count}
    ```
  - `views/SectionStackView.tsx` の塊は次の4行にする（S1 のセクション3つを残し、グリッドは S3 の引数にする）:
    ```tsx
          {data.cleanRoom ? <CleanRoomSection cleanRoom={data.cleanRoom} /> : null}
          {data.cleanRoomYield ? <CleanRoomYieldSection cleanRoomYield={data.cleanRoomYield} /> : null}
          {data.machineInputStall ? <MachineInputStallSection stall={data.machineInputStall} /> : null}
          {showItemGrid ? <BlockItemGrid itemSlots={data.itemSlots} slotIndices={data.itemSlots.map((_, index) => index)} testId={itemGridTestId} /> : null}
    ```

- [ ] **Step 8: テスト用マスタ（forUnitTest）の blocks/items.json を和集合で作り直す**
  実測した条件:
  - 両側とも、分岐点（`70ea9f85`）の配列の末尾に追記している。
  - S3 は分岐点の要素を1つも書き換えていない（blocks は HEAD 側だけが9要素を変更）。
  - 追記どうしで GUID の衝突は無い。
  - HEAD 側のファイルは `json.dumps(indent=2|4, ensure_ascii=False)+"\n"` とバイト単位で一致する。
  そこで HEAD の JSON に S3 の追記分を足し、items には S3 の最上位キー `orbitalPayloads` を `data` の直後へ足す。
  ```bash
  cd $WT && python3 - <<'EOF'
  import json, subprocess
  D = "moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master"
  MB = subprocess.check_output(["git", "merge-base", "HEAD", "MERGE_HEAD"], text=True).strip()
  def show(rev, name):
      return json.loads(subprocess.check_output(["git", "show", f"{rev}:{D}/{name}.json"], text=True))
  for name, key, indent in (("blocks", "blockGuid", 2), ("items", "itemGuid", 4)):
      ours, theirs, base = show("HEAD", name), show("MERGE_HEAD", name), show(MB, name)
      base_guids = {e[key] for e in base["data"]}
      ours_guids = {e[key] for e in ours["data"]}
      assert theirs["data"][:len(base["data"])] == base["data"], f"{name}: S3 が既存要素を書き換えている"
      added = [e for e in theirs["data"] if e[key] not in base_guids]
      assert not [e for e in added if e[key] in ours_guids], f"{name}: GUID 衝突"
      ours["data"].extend(added)
      merged = {}
      for k, v in ours.items():
          merged[k] = v
          if name == "items" and k == "data":
              merged["orbitalPayloads"] = theirs["orbitalPayloads"]
      for k in theirs:
          assert k in merged, f"{name}: S3 だけが持つ最上位キー {k} を取りこぼした"
      open(f"{D}/{name}.json", "w", encoding="utf-8").write(json.dumps(merged, indent=indent, ensure_ascii=False) + "\n")
      print(name, "data", len(merged["data"]))
  EOF
  ```
  Expected: `blocks data 88` / `items data 105`。ID は GUID 基準（`ForUnitTestItemId` も `GetItemId(Guid.Parse(...))`）なので、末尾に追記した S3 分の番号がずれても影響しない。

- [ ] **Step 9: 衝突が残っていないことを確かめて add する**
  Run: `cd $WT && git grep -nE '^(<<<<<<<|>>>>>>>|=======)( |$)' -- . ':!*.md' ; git add -A && git diff --name-only --diff-filter=U | wc -l`
  Expected: grep は0件、未解決は `0`

- [ ] **Step 10: webui のローカライズ生成物を作り直し、型検査と vitest を通す**
  ```bash
  cd $WT/moorestech_web && bash setup.sh
  NODE_DIR=$(ls -d $WT/moorestech_web/node/*/ | head -1); export PATH="$NODE_DIR/bin:$PATH"; PNPM="$NODE_DIR/pnpm"
  "$PNPM" --dir webui install --frozen-lockfile
  "$PNPM" --dir webui run gen:i18n
  "$PNPM" --dir webui exec tsc -b --force
  "$PNPM" --dir webui exec tsc -p e2e/tsconfig.json --noEmit
  "$PNPM" --dir webui exec vitest run
  cd $WT && git status --short moorestech_web/webui/src/shared/i18n/generated/
  ```
  Expected: tsc 2本はエラー0、vitest は全件 pass。`git status` は `localizationKeys.ts` だけを変更として出す（`contentKeys.ts` は差分なし）。

- [ ] **Step 11: Editor を起動し、生成コードの印を更新させてコンパイルする**
  Run: `cd $WT && uloop launch ./moorestech_client && sleep 60 && git diff --stat -- moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs moorestech_client/Assets/Scripts/Client.Localization/_CompileRequester.cs && uloop compile --project-path ./moorestech_client`
  Expected:
  - `_CompileRequester.cs` が2本とも書き換わっている。SchemaWatcher（`Editor/SchemaWatch/SchemaWatcher.cs`）が `VanillaSchema/` と `Localization/` の中身の MD5 から印を作るので、統合後の値は S1・S3 のどちらとも違う値になる。2本とも書き換わっていなければ、`dummyText` を手で任意の新しい値へ変える（edit-schema スキル「3. Trigger SourceGenerator」）。
  - compile のエラーは、`Tests/UnitTest/Game/SaveLoad/Migration/SaveMigrationStepV7ToV8Test.cs` と `Tests/UnitTest/Game/SaveLoad/SpaceElevator/SaveMigrationStepV7ToV8Test.cs` の CS0101（および同じクラスに付随する CS0111）だけになる。
  - それ以外のエラーが出たら、S3 が master から33コミット遅れていることによる食い違いの可能性がある。この場合は機能を変えない最小の追従修正を、マージコミットとは別のコミット `fix(merge): …` に分け、エラーとその直し方を一覧にして本体へ報告する。
  - `.moorestech-external-revisions.json` を Unity が書き換えていたら `git checkout -- .moorestech-external-revisions.json` で戻す。

- [ ] **Step 12: マージをコミットする**
  `git add -A` / `git commit -m "Merge origin/feat/tier9-fusion-elevator-endgame into feat/tier9-integrated（S2・S3 を取り込む。衝突解消のみで、セーブ移行は一時的に S1 側を残す）"`

**完了判定:**
- `git log --merges -1 --format=%P` の親が `f246e7fd5f`（またはその子孫）と `87944450d4` の2つ。
- `git grep -nE '^(<<<<<<<|>>>>>>>)( |$)'` が0件。
- webui の tsc 2本と vitest が緑（Step 10 の出力）。
- `uloop compile` のエラーが CS0101/CS0111 の2ファイルだけ（Step 11 の出力を貼る）。

---

### Task 5: 同名テストクラス（CS0101）を解き、S3 の ADR を 0091〜0093 へ振り直す

**Files:**
- Move: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/SpaceElevator/SaveMigrationStepV7ToV8Test.cs`(+`.meta`) → `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/Migration/SaveMigrationStepV7ToV8CablePoolTest.cs`(+`.meta`)
- Move: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/Migration/SaveMigrationStepV6ToV7Test.cs`(+`.meta`、S2 由来) → `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/Migration/SaveMigrationStepV7ToV8RocketHistoryTest.cs`(+`.meta`)
- Delete: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/SpaceElevator.meta`（空になるフォルダ）
- Move: `docs/adr/0088-orbital-arrival-service-shared-by-launch-sites.md` → `docs/adr/0091-…` / `docs/adr/0089-space-elevator-cable-pool-and-per-base-connection.md` → `docs/adr/0092-…` / `docs/adr/0090-fusion-net-power-from-fuel-extraction-cost.md` → `docs/adr/0093-…`
- Modify（参照の書き換え。S2/S3 が新設したファイルだけ）:
  - `.decisions/2026-10-23-Tier9のマスタの中身はS1からS3の実装後に統合タスクでまとめて入れる.md`
  - `.decisions/2026-10-23-エレベーター拠点に接続できるかはサーバーのプールが判定し画面は表示するだけ.md`
  - `.decisions/2026-10-23-核融合の収支は運転中の電力だけで評価し点火手順は設けず見た目は既存モデルと汎用UI.md`
  - `.decisions/2026-10-23-発射台は成果が出力口に入りきるときだけ建造と打ち上げを進める.md`
  - `docs/adr/0092-space-elevator-cable-pool-and-per-base-connection.md`
  - `docs/superpowers/plans/2026-10-23-tier9-compute-data-loop.md`
  - `docs/superpowers/plans/2026-10-23-tier9-fusion-elevator-endgame.md`
  - `moorestech_server/Assets/Scripts/Game.SpaceElevator/SpaceElevatorCablePool.cs`

**Interfaces:**
- Consumes: Task 4 のマージコミット
- Produces: テストクラス `Tests.UnitTest.Game.SaveLoad.Migration.SaveMigrationStepV7ToV8CablePoolTest` / `…SaveMigrationStepV7ToV8RocketHistoryTest`（中身は Task 6 で書き直す）。ADR 0091（軌道到着サービス）・0092（ケーブルプール）・0093（核融合の差し引き電力）

**参照の実測（マージ後のツリー `339f303e` で、S3 が触ったファイルに限って `git grep`）:** 27行。内訳: `.decisions` 4ファイルで各1行、`docs/adr/0089-space…` 1行、compute-data-loop plan 8行、fusion-elevator-endgame plan 12行、`SpaceElevatorCablePool.cs` 2行。ほかに S2 の `SaveMigrationStepV6ToV7.cs` に「ADR 0088」が2行あったが、Task 4 で HEAD 側を採ったので消えている。master 側の `ADR 0088〜0090`（流体）への参照は `.decisions/2026-10-2[2-4]-*`・`docs/adr/0088〜0090-fluid*`・`Game.Fluid/*.cs` など多数あるので、**ファイルを限定せずに置換してはいけない**。

- [ ] **Step 1: 同名クラスを移動・改名して CS0101 を解く**（.cs と .meta をまとめて git mv する）
  ```bash
  cd $WT; T=moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad
  git mv $T/SpaceElevator/SaveMigrationStepV7ToV8Test.cs $T/Migration/SaveMigrationStepV7ToV8CablePoolTest.cs
  git mv $T/SpaceElevator/SaveMigrationStepV7ToV8Test.cs.meta $T/Migration/SaveMigrationStepV7ToV8CablePoolTest.cs.meta
  git rm $T/SpaceElevator.meta
  git mv $T/Migration/SaveMigrationStepV6ToV7Test.cs $T/Migration/SaveMigrationStepV7ToV8RocketHistoryTest.cs
  git mv $T/Migration/SaveMigrationStepV6ToV7Test.cs.meta $T/Migration/SaveMigrationStepV7ToV8RocketHistoryTest.cs.meta
  ```
  続けて Edit で次のとおり直す。
  - `SaveMigrationStepV7ToV8CablePoolTest.cs`: `namespace Tests.UnitTest.Game.SaveLoad` → `namespace Tests.UnitTest.Game.SaveLoad.Migration`、`public class SaveMigrationStepV7ToV8Test` → `public class SaveMigrationStepV7ToV8CablePoolTest`。
  - `SaveMigrationStepV7ToV8RocketHistoryTest.cs`: `public class SaveMigrationStepV6ToV7Test` → `public class SaveMigrationStepV7ToV8RocketHistoryTest`（名前空間は元から `.Migration`）。
  - 中身（テストの期待値）は Task 6 で書き直す。それまで両クラスのテストは赤になる。S2 のテストは master の流体版 V6→V7 を、S3 のテストは S1 の V7→V8 を対象に走ってしまうため。

- [ ] **Step 2: ADR を改名する**
  ```bash
  cd $WT
  git mv docs/adr/0088-orbital-arrival-service-shared-by-launch-sites.md docs/adr/0091-orbital-arrival-service-shared-by-launch-sites.md
  git mv docs/adr/0089-space-elevator-cable-pool-and-per-base-connection.md docs/adr/0092-space-elevator-cable-pool-and-per-base-connection.md
  git mv docs/adr/0090-fusion-net-power-from-fuel-extraction-cost.md docs/adr/0093-fusion-net-power-from-fuel-extraction-cost.md
  ```

- [ ] **Step 3: 参照を対象ファイルに限って書き換える**
  ```bash
  cd $WT && python3 - <<'EOF'
  import re, pathlib
  files = [
      ".decisions/2026-10-23-Tier9のマスタの中身はS1からS3の実装後に統合タスクでまとめて入れる.md",
      ".decisions/2026-10-23-エレベーター拠点に接続できるかはサーバーのプールが判定し画面は表示するだけ.md",
      ".decisions/2026-10-23-核融合の収支は運転中の電力だけで評価し点火手順は設けず見た目は既存モデルと汎用UI.md",
      ".decisions/2026-10-23-発射台は成果が出力口に入りきるときだけ建造と打ち上げを進める.md",
      "docs/adr/0092-space-elevator-cable-pool-and-per-base-connection.md",
      "docs/superpowers/plans/2026-10-23-tier9-compute-data-loop.md",
      "docs/superpowers/plans/2026-10-23-tier9-fusion-elevator-endgame.md",
      "moorestech_server/Assets/Scripts/Game.SpaceElevator/SpaceElevatorCablePool.cs",
  ]
  slugs = [("0088-orbital-arrival", "0091-orbital-arrival"), ("0089-space-elevator", "0092-space-elevator"), ("0090-fusion-net-power", "0093-fusion-net-power")]
  numbers = {"0088": "0091", "0089": "0092", "0090": "0093"}
  for f in files:
      p = pathlib.Path(f); before = p.read_text(encoding="utf-8"); text = before
      for old, new in slugs: text = text.replace(old, new)
      text = re.sub(r"ADR ?(0088|0089|0090)", lambda m: m.group(0)[:-4] + numbers[m.group(1)], text)
      p.write_text(text, encoding="utf-8")
      print(f, "changed" if text != before else "UNCHANGED")
  EOF
  ```
  Expected: 8ファイルすべて `changed`。S3 側の plan にある「ADR 0090」は全部、核融合 ADR の意味で使われている（S3 の分岐点 `70ea9f85` には流体の ADR 0088〜0090 がまだ無いことを確認済み）。

- [ ] **Step 4: 書き換え漏れと誤爆が無いことを確かめる**
  Run: `cd $WT && git grep -nE "0088-orbital|0089-space-elevator|0090-fusion-net" ; git -c core.quotePath=false grep -nE "ADR ?0?(088|089|090)" -- .decisions/2026-10-23-* docs/adr/009[1-3]-* docs/superpowers/plans/2026-10-23-tier9-* moorestech_server/Assets/Scripts/Game.SpaceElevator ; git diff --stat HEAD -- docs/adr/0088-fluid* docs/adr/0089-fluid* docs/adr/0090-fluid* moorestech_server/Assets/Scripts/Game.Fluid`
  Expected: grep 2本とも0件。流体側の diff は空。

- [ ] **Step 5: コンパイル**
  Run: `cd $WT && uloop compile --project-path ./moorestech_client`
  Expected: エラー0

- [ ] **Step 6: コミット**
  `git add -A` / `git commit -m "fix(merge): 同名の移行テストクラスを部品別の名前へ移し、S3 の ADR を 0091〜0093 へ振り直す"`

**完了判定:**
- `uloop compile` のエラーが0。
- Step 4 の grep 2本が0件で、流体 ADR の diff が空。
- `ls docs/adr | grep -E '^009[0-3]'` に `0090-fluid…`・`0091-orbital…`・`0092-space-elevator…`・`0093-fusion…` が1本ずつある。

---

### Task 6: セーブ移行を V7→V8 の1本に畳む（継承レベル・清浄室機械の液体スロット・初回打ち上げ履歴・ケーブルプール）

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/WorldVersions/WorldSaveAllInfo.cs`（`CurrentVersion = 8`）
- Modify（全面書き直し。S1 の .meta GUID を引き継ぐ）: `moorestech_server/Assets/Scripts/Game.SaveLoad/Migration/Steps/SaveMigrationStepV7ToV8.cs`
- Delete: `moorestech_server/Assets/Scripts/Game.SaveLoad/Migration/Steps/SaveMigrationStepV8ToV9.cs`(+`.meta`) / `…/Steps/V8ToV9/UnimplementedBlockUnlockRevoker.cs`(+`.meta`)
- Move: `…/Steps/V8ToV9/`（フォルダと `V8ToV9.meta`）→ `…/Steps/V7ToV8/`（`V7ToV8.meta`）
- Modify（移動後に全面書き直し）: `…/Steps/V7ToV8/CleanRoomMachineFluidSlotFiller.cs`
- Create: `…/Steps/V7ToV8/IV7ToV8MigrationPart.cs` / `…/Steps/V7ToV8/V7ToV8SaveJson.cs` / `…/Steps/V7ToV8/MachineInheritedLevelFiller.cs` / `…/Steps/V7ToV8/RocketLaunchHistoryFiller.cs` / `…/Steps/V7ToV8/SpaceElevatorCablePoolFiller.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/Composition/SaveAndEventServiceRegistration.cs` / `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/SaveLoadPreparerTestFixture.cs` / `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/SaveMigrationChainTest.cs`（連鎖の配列）
- Move＋書き直し（テスト。`T=moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/Migration`）:
  - `$T/SaveMigrationStepV7ToV8Test.cs`(S1) → `$T/SaveMigrationStepV7ToV8InheritedLevelTest.cs`
  - `$T/SaveMigrationStepV8ToV9Test.cs` → `$T/SaveMigrationStepV7ToV8CleanRoomFluidTest.cs`
- Delete: `$T/SaveMigrationStepV8ToV9UnlockRevokeTest.cs`(+`.meta`)
- Modify（全面書き直し）: `$T/SaveMigrationStepV7ToV8RocketHistoryTest.cs` / `$T/SaveMigrationStepV7ToV8CablePoolTest.cs`
- Create: `$T/SaveMigrationStepV7ToV8Test.cs`（部品を束ねた全体のテスト）
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoom/Machine/CleanRoomMachineFluidTest.cs` / `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/RocketLaunchPad/RocketLaunchHistorySaveLoadTest.cs` / `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/SpaceElevator/Recovery/SpaceElevatorUpstreamMigrationSaveLoadTest.cs`

**Interfaces:**
- Consumes: Task 5 のコミット
- Produces（Task 7 が使う）:
  - `namespace Game.SaveLoad.Migration.Steps.V7ToV8`
  - `public interface IV7ToV8MigrationPart { bool TryPrepare(JObject save, out string failureReason); string ApplyAndSummarize(); }`
  - `public static class V7ToV8SaveJson { bool IsAbsentOrNull(JToken); bool IsExplicitNull(JToken); bool Reject(string reason, out string failureReason); bool TryCollectBlockStates(JObject save, out List<JObject> states, out string failureReason); }`（部品が共有する辿り方。Task 7 が `TryGetNestedArray` を足す）
  - `public sealed class SaveMigrationStepV7ToV8 : ISaveMigrationStep`（`FromVersion => 7`。`Migrate` の中で `IV7ToV8MigrationPart[] parts` を並べる）
  - `WorldSaveAllInfo.CurrentVersion == 8`

**最終形のディレクトリ（200行・10ファイル上限の確認用。数は .cs だけを数えたもの）:**
```
Game.SaveLoad/Migration/Steps/              .cs 7本（V1ToV2〜V6ToV7 は master のまま、V7ToV8 は作り直し）
  V2ToV3/  V3ToV4/                          既存
  V7ToV8/                                   .cs 9本（Task 7 後）
    IV7ToV8MigrationPart.cs                 約20行
    V7ToV8SaveJson.cs                       約60行（部品が共有する辿り方。Task 7 で TryGetNestedArray を足して約85行）
    NestedSaveArray.cs                      約40行（Task 7 で新設。save[節][配列] の 無い／null／配列 の3状態）
    MachineInheritedLevelFiller.cs          約60行（S1 の SaveMigrationStepV7ToV8 を移植）
    CleanRoomMachineFluidSlotFiller.cs      約70行（S1 の V8ToV9 部品を移植。W17 を吸収）
    RocketLaunchHistoryFiller.cs            約45行（S2 の SaveMigrationStepV6ToV7 を移植）
    SpaceElevatorCablePoolFiller.cs         約45行（S3 の SaveMigrationStepV7ToV8 を移植）
    ZeroCostTier9ResearchResetter.cs        約110行（Task 7 で新設）
    ZeroCostTier9ResearchResetTable.cs      約70行（Task 7 で新設）
Tests/UnitTest/Game/SaveLoad/Migration/     .cs 6本（Task 7 後）
```

**部品の共通の作法:**
- `TryPrepare` は書き換える対象を集めるだけにする。全部品の `TryPrepare` が通ってから `ApplyAndSummarize` で書き換える。こうすると、後ろの部品が拒否しても原本は変わらない（S1 V8ToV9 の「先に検査、後で書き換え」を step 全体へ広げた形）。
- 拒否するときは `SaveMigrationStepResult.Failed(reason)` を返す。例外は使わない。
- どの部品も冪等にする（バックアップから戻して再実行する運用があるため。save-migration スキル Gotchas）。

- [ ] **Step 1: 旧ステップを消し、部品フォルダを移す**
  ```bash
  cd $WT; S=moorestech_server/Assets/Scripts/Game.SaveLoad/Migration/Steps
  git rm $S/SaveMigrationStepV8ToV9.cs $S/SaveMigrationStepV8ToV9.cs.meta \
         $S/V8ToV9/UnimplementedBlockUnlockRevoker.cs $S/V8ToV9/UnimplementedBlockUnlockRevoker.cs.meta
  git mv $S/V8ToV9 $S/V7ToV8
  git mv $S/V8ToV9.meta $S/V7ToV8.meta
  ```

- [ ] **Step 2: 部品のインターフェースと、部品が共有するセーブ JSON の辿り方を作る** `Steps/V7ToV8/IV7ToV8MigrationPart.cs`・`Steps/V7ToV8/V7ToV8SaveJson.cs`
  ```csharp
  using Newtonsoft.Json.Linq;

  namespace Game.SaveLoad.Migration.Steps.V7ToV8
  {
      /// <summary>
      /// 版7→8の変換部品。全部品の検査が通ってから適用するので、どれかが拒否してもセーブは書き換わらない
      /// A version 7-to-8 conversion part; parts apply only after every part passes inspection, so a refusal never leaves the save half-rewritten
      /// </summary>
      public interface IV7ToV8MigrationPart
      {
          // 書き換える対象を集める。辿れない形なら理由を返し、セーブには触れない
          // Collect what to rewrite; on an unwalkable shape return the reason without touching the save
          bool TryPrepare(JObject save, out string failureReason);

          // 検査で集めた対象だけを書き換え、ログに載せる要約を返す
          // Rewrite only what inspection collected and return a summary for the log
          string ApplyAndSummarize();
      }
  }
  ```

  `Steps/V7ToV8/V7ToV8SaveJson.cs`（欠損と null の判定・world→ブロック→state の走査・拒否理由の返し方を、部品ごとに複製せず 1 か所に置く。判断記録 A32）:
  ```csharp
  using System.Collections.Generic;
  using Newtonsoft.Json.Linq;

  namespace Game.SaveLoad.Migration.Steps.V7ToV8
  {
      /// <summary>
      /// 版7→8の部品が共有するセーブ JSON の辿り方。欠損と null の判定・world→ブロック→state の走査・拒否理由の返し方を 1 か所に置く
      /// Save-JSON walking shared by the version 7-to-8 parts; the absent/null tests, the world->block->state walk and refusal returns live in one place
      /// </summary>
      public static class V7ToV8SaveJson
      {
          // キーが無いか、値が JSON の null（どちらも「値が無い」として扱う箇所で使う）
          // The key is absent or its value is JSON null (used where both mean "no value")
          public static bool IsAbsentOrNull(JToken token)
          {
              return token == null || token.Type == JTokenType.Null;
          }

          // キーはあり、値が JSON の null（欠損と分けて数える箇所で使う）
          // The key exists and its value is JSON null (used where it is counted apart from absence)
          public static bool IsExplicitNull(JToken token)
          {
              return token != null && token.Type == JTokenType.Null;
          }

          // 拒否理由を out に入れて false を返す（TryPrepare の拒否の書き方を 1 つにする）
          // Put the refusal reason into the out parameter and return false (one way to refuse in TryPrepare)
          public static bool Reject(string reason, out string failureReason)
          {
              failureReason = reason;
              return false;
          }

          // world 配列の各ブロックの state を集める。state の無いブロックは機械ではないので飛ばし、形が違えば拒否する
          // Collect each world block's state; blocks without state are not machines and are skipped, a wrong shape is refused
          public static bool TryCollectBlockStates(JObject save, out List<JObject> states, out string failureReason)
          {
              states = new List<JObject>();
              failureReason = null;
              if (!(save["world"] is JArray world)) return Reject($"セーブのworldが配列ではありません。 type={save["world"]?.Type}", out failureReason);

              foreach (var blockToken in world)
              {
                  if (!(blockToken is JObject block)) return Reject($"world要素がオブジェクトではありません。 type={blockToken.Type}", out failureReason);
                  var stateToken = block["state"];
                  if (IsAbsentOrNull(stateToken)) continue;
                  if (!(stateToken is JObject state)) return Reject($"world要素のstateがオブジェクトではありません。 type={stateToken.Type}", out failureReason);
                  states.Add(state);
              }

              return true;
          }
      }
  }
  ```

- [ ] **Step 3: 継承レベルの部品を作る** `Steps/V7ToV8/MachineInheritedLevelFiller.cs`（S1 `SaveMigrationStepV7ToV8.cs` の移植。判定は変えず、検査と書き換えだけを分けた）
  ```csharp
  using System.Collections.Generic;
  using Newtonsoft.Json.Linq;

  namespace Game.SaveLoad.Migration.Steps.V7ToV8
  {
      /// <summary>
      /// 通常機械とクリーンルーム機械の加工状態に継承レベル null を明示で足す（冪等）。版7に継承レシピは無いため「継承しない」が正しい
      /// Add an explicit null inherited level to normal and clean-room machine processing state (idempotent); version 7 had no inheritance recipes, so "no inheritance" is exact
      /// </summary>
      public sealed class MachineInheritedLevelFiller : IV7ToV8MigrationPart
      {
          // コンポーネントのSaveKey(typeof FullName)。ステップは現行の型を参照しないので文字列で持つ
          // Component SaveKeys (typeof FullName); the step never references live types, so they are kept as strings
          private const string VanillaMachineSaveKey = "Game.Block.Blocks.Machine.VanillaMachineSaveComponent";
          private const string CleanRoomMachineSaveKey = "Game.Block.Blocks.CleanRoom.Machine.CleanRoomMachineProcessorComponent";
          private const string InheritedLevelKey = "inheritedLevel";

          private readonly List<JObject> _vanillaProcessors = new();
          private readonly List<JObject> _cleanRoomProcessors = new();
          private int _nullVanillaProcessorCount;

          public bool TryPrepare(JObject save, out string failureReason)
          {
              // world→ブロック→state の走査と拒否文は全部品で共有する
              // The world->block->state walk and its refusal texts are shared by every part
              if (!V7ToV8SaveJson.TryCollectBlockStates(save, out var states, out failureReason)) return false;

              foreach (var state in states)
              {
                  // 通常機械はprocessorサブオブジェクトへ足す
                  // A normal machine gets the key inside its processor sub-object
                  var vanillaToken = state[VanillaMachineSaveKey];
                  if (!V7ToV8SaveJson.IsAbsentOrNull(vanillaToken))
                  {
                      if (!(vanillaToken is JObject vanilla)) return V7ToV8SaveJson.Reject($"{VanillaMachineSaveKey} がオブジェクトではありません。 type={vanillaToken.Type}", out failureReason);
                      var processorToken = vanilla["processor"];
                      // processorの明示nullはロード側も警告して復元を見送るので、止めずに台数だけ残す（清浄室機械の部品と同じ扱い）
                      // An explicit null processor is skipped with a warning by the loader too, so count it instead of refusing
                      if (V7ToV8SaveJson.IsExplicitNull(processorToken)) _nullVanillaProcessorCount++;
                      else if (!(processorToken is JObject processor)) return V7ToV8SaveJson.Reject($"{VanillaMachineSaveKey} の processor がオブジェクトではありません。 type={processorToken?.Type}", out failureReason);
                      else if (!processor.ContainsKey(InheritedLevelKey)) _vanillaProcessors.Add(processor);
                  }

                  // クリーンルーム機械は直下へ足す
                  // A clean-room machine gets the key at its top level
                  var cleanRoomToken = state[CleanRoomMachineSaveKey];
                  if (V7ToV8SaveJson.IsAbsentOrNull(cleanRoomToken)) continue;
                  if (!(cleanRoomToken is JObject cleanRoom)) return V7ToV8SaveJson.Reject($"{CleanRoomMachineSaveKey} がオブジェクトではありません。 type={cleanRoomToken.Type}", out failureReason);
                  if (!cleanRoom.ContainsKey(InheritedLevelKey)) _cleanRoomProcessors.Add(cleanRoom);
              }

              return true;
          }

          public string ApplyAndSummarize()
          {
              foreach (var processor in _vanillaProcessors) processor[InheritedLevelKey] = JValue.CreateNull();
              foreach (var cleanRoom in _cleanRoomProcessors) cleanRoom[InheritedLevelKey] = JValue.CreateNull();
              return $"継承レベルを足した通常機械={_vanillaProcessors.Count}台 クリーンルーム機械={_cleanRoomProcessors.Count}台 processorが null で飛ばした通常機械={_nullVanillaProcessorCount}台";
          }
      }
  }
  ```

- [ ] **Step 4: 清浄室機械の液体スロットの部品を書き直す** `Steps/V7ToV8/CleanRoomMachineFluidSlotFiller.cs`（S1 V8ToV9 の static 版を、インスタンスの部品にする）
  W17 も吸収する。processor キーの値が JSON の null のとき、S1 は拒否していた。ここでは、ロード側の `BlockComponentStateReader.TryRead`（`Game.Block.Interface/Component/BlockComponentStateReader.cs:46-53`）と同じく「復元を見送る」扱いにして飛ばし、飛ばした台数を step のログに残す。同じ step の継承レベル部品も null を飛ばすので、step の中で扱いが揃う。この選び方は起草者メモ1。
  ```csharp
  using System.Collections.Generic;
  using Newtonsoft.Json.Linq;

  namespace Game.SaveLoad.Migration.Steps.V7ToV8
  {
      /// <summary>
      /// 清浄室機械の state へ空の液体スロット配列を足す（冪等）。版7以前の清浄室機械はタンクを持たなかった
      /// Add empty fluid slot arrays to clean-room machine states (idempotent); machines up to version 7 had no tanks
      /// </summary>
      public sealed class CleanRoomMachineFluidSlotFiller : IV7ToV8MigrationPart
      {
          // 清浄室機械ProcessorのSaveKey(typeof FullName)。ステップは現行の型を参照しないので文字列で持つ
          // The clean-room processor's SaveKey (typeof FullName); the step never references live types, so it is kept as a string
          private const string ProcessorSaveKey = "Game.Block.Blocks.CleanRoom.Machine.CleanRoomMachineProcessorComponent";
          private const string InputFluidSlotKey = "inputFluidSlot";
          private const string OutputFluidSlotKey = "outputFluidSlot";

          private readonly List<JObject> _targets = new();
          private int _nullProcessorCount;

          public bool TryPrepare(JObject save, out string failureReason)
          {
              // world→ブロック→state の走査と拒否文は全部品で共有する
              // The world->block->state walk and its refusal texts are shared by every part
              if (!V7ToV8SaveJson.TryCollectBlockStates(save, out var states, out failureReason)) return false;

              foreach (var state in states)
              {
                  // 清浄室機械以外はキーが無いので対象外
                  // Blocks other than clean-room machines lack the key and are out of scope
                  var processorToken = state[ProcessorSaveKey];
                  if (processorToken == null) continue;

                  // 明示nullはロード側も警告して復元を見送る形なので、止めずに台数だけ残す
                  // An explicit null is also skipped with a warning by the loader, so count it instead of refusing
                  if (V7ToV8SaveJson.IsExplicitNull(processorToken))
                  {
                      _nullProcessorCount++;
                      continue;
                  }

                  if (!(processorToken is JObject processorState)) return V7ToV8SaveJson.Reject($"{ProcessorSaveKey} がオブジェクトではありません。 type={processorToken.Type}", out failureReason);
                  var inputToken = processorState[InputFluidSlotKey];
                  var outputToken = processorState[OutputFluidSlotKey];
                  // 液体キーの明示nullは欠損と同じく空配列で補う（判断記録 A3）
                  // An explicit null fluid key is filled like a missing one (decision A3)
                  var inputMissing = V7ToV8SaveJson.IsAbsentOrNull(inputToken);
                  var outputMissing = V7ToV8SaveJson.IsAbsentOrNull(outputToken);
                  if (!inputMissing && !(inputToken is JArray)) return V7ToV8SaveJson.Reject($"{InputFluidSlotKey} が配列ではありません。 type={inputToken.Type}", out failureReason);
                  if (!outputMissing && !(outputToken is JArray)) return V7ToV8SaveJson.Reject($"{OutputFluidSlotKey} が配列ではありません。 type={outputToken.Type}", out failureReason);
                  if (inputMissing || outputMissing) _targets.Add(processorState);
              }

              return true;
          }

          public string ApplyAndSummarize()
          {
              // 欠けている液体キーだけ空配列で足す
              // Add empty arrays only where a fluid key is missing
              foreach (var processorState in _targets)
              {
                  if (V7ToV8SaveJson.IsAbsentOrNull(processorState[InputFluidSlotKey])) processorState[InputFluidSlotKey] = new JArray();
                  if (V7ToV8SaveJson.IsAbsentOrNull(processorState[OutputFluidSlotKey])) processorState[OutputFluidSlotKey] = new JArray();
              }

              return $"液体スロットを足した清浄室機械={_targets.Count}台 processorが null で飛ばした清浄室機械={_nullProcessorCount}台";
          }
      }
  }
  ```

- [ ] **Step 5: 初回打ち上げ履歴の部品を作る** `Steps/V7ToV8/RocketLaunchHistoryFiller.cs`（S2 `SaveMigrationStepV6ToV7.cs` の移植）
  S2 との違いは、既に真偽値があるときに拒否せず素通しすること。冪等にするためと、`SpaceElevatorUpstreamMigrationSaveLoadTest` の `[TestCase(7, true)]`（履歴を既に持つ版7）を通すため。真偽値でない値は従来どおり拒否する（起草者メモ2）。
  ```csharp
  using Newtonsoft.Json.Linq;

  namespace Game.SaveLoad.Migration.Steps.V7ToV8
  {
      /// <summary>
      /// ロケットの初回打ち上げ履歴を未打ち上げで足す（冪等）。版7まで発射台は存在しないので未打ち上げが唯一の正しい値
      /// Add the rocket first-launch history as not yet launched (idempotent); no launch pad existed up to version 7, so not-launched is the only correct value
      /// </summary>
      public sealed class RocketLaunchHistoryFiller : IV7ToV8MigrationPart
      {
          private const string HistoryKey = "rocketHasLaunchedOnce";

          private JObject _fillTarget;

          public bool TryPrepare(JObject save, out string failureReason)
          {
              failureReason = null;
              var history = save[HistoryKey];

              // 欠損と明示nullは未打ち上げで補う
              // Fill both absence and an explicit null with not-launched
              if (V7ToV8SaveJson.IsAbsentOrNull(history))
              {
                  _fillTarget = save;
                  return true;
              }

              // 真偽値は素通しする（再実行や、既に履歴を持つ形）
              // Pass a boolean through (a rerun, or a save that already has the history)
              if (history.Type == JTokenType.Boolean) return true;
              failureReason = $"{HistoryKey} が真偽値ではありません。 type={history.Type} value={history}";
              return false;
          }

          public string ApplyAndSummarize()
          {
              if (_fillTarget == null) return "初回打ち上げ履歴=既存の値を保持";
              _fillTarget[HistoryKey] = false;
              return "初回打ち上げ履歴=未打ち上げで追加";
          }
      }
  }
  ```

- [ ] **Step 6: ケーブルプールの部品を作る** `Steps/V7ToV8/SpaceElevatorCablePoolFiller.cs`（S3 `SaveMigrationStepV7ToV8.cs` の移植）
  明示 null も欠損と同じく0で補う（履歴の部品と揃える）。
  ```csharp
  using Newtonsoft.Json.Linq;

  namespace Game.SaveLoad.Migration.Steps.V7ToV8
  {
      /// <summary>
      /// 宇宙エレベーターのケーブルプールの節を0で足す（冪等）。版7までケーブルを軌道へ運ぶ積荷表の行は無く、0が実値である
      /// Add the space-elevator cable pool section at zero (idempotent); up to version 7 no payload row could carry cables to orbit, so zero is the real value
      /// </summary>
      public sealed class SpaceElevatorCablePoolFiller : IV7ToV8MigrationPart
      {
          private const string PoolKey = "spaceElevatorCablePool";
          private const string CountKey = "cableCount";

          private JObject _fillTarget;

          public bool TryPrepare(JObject save, out string failureReason)
          {
              failureReason = null;
              var existing = save[PoolKey];

              // 欠損と明示nullは0のプールで補う
              // Fill both absence and an explicit null with an empty pool
              if (V7ToV8SaveJson.IsAbsentOrNull(existing))
              {
                  _fillTarget = save;
                  return true;
              }

              // 整数のcableCountを持つ節は形だけ確かめて素通しする。値の範囲はロード時のRestoreだけが見る
              // A section with an integer cableCount passes after a shape check; only Restore at load checks the value range
              if (existing is JObject pool && pool[CountKey]?.Type == JTokenType.Integer) return true;
              failureReason = $"{PoolKey} が整数の {CountKey} を持つオブジェクトではありません。 value={existing}";
              return false;
          }

          public string ApplyAndSummarize()
          {
              if (_fillTarget == null) return "ケーブルプール=既存の節を保持";
              _fillTarget[PoolKey] = new JObject { [CountKey] = 0 };
              return "ケーブルプール=0で追加";
          }
      }
  }
  ```

- [ ] **Step 7: ステップ本体を書き直す** `Steps/SaveMigrationStepV7ToV8.cs`（ファイル全体を置き換える。.meta は S1 のまま）
  ```csharp
  using System.Collections.Generic;
  using Game.SaveLoad.Migration.Steps.V7ToV8;
  using Newtonsoft.Json.Linq;
  using UnityEngine;

  namespace Game.SaveLoad.Migration.Steps
  {
      // V7→V8移行: Tier9統合で増えたセーブの形を機能ごとの部品で足す（継承レベル・清浄室機械の液体スロット・初回打ち上げ履歴・ケーブルプール）
      // V7->V8 migration: add the save shapes introduced by the Tier9 integration through one part per feature (inherited level, clean-room fluid slots, first-launch history, cable pool)
      public sealed class SaveMigrationStepV7ToV8 : ISaveMigrationStep
      {
          public int FromVersion => 7;

          public SaveMigrationStepResult Migrate(JObject save)
          {
              var parts = new IV7ToV8MigrationPart[]
              {
                  new MachineInheritedLevelFiller(),
                  new CleanRoomMachineFluidSlotFiller(),
                  new RocketLaunchHistoryFiller(),
                  new SpaceElevatorCablePoolFiller(),
              };

              // 先に全部品を検査し、どれかが拒否したら原本を書き換えない
              // Inspect every part first so a refusal never leaves the original half-rewritten
              foreach (var part in parts)
              {
                  if (!part.TryPrepare(save, out var reason)) return Fail(reason);
              }

              // 検査を通った対象だけを書き換える
              // Rewrite only the targets that passed inspection
              var summaries = new List<string>();
              foreach (var part in parts) summaries.Add(part.ApplyAndSummarize());

              Debug.Log($"セーブを版7から版8へ変換しました。{string.Join(" / ", summaries)}");
              return SaveMigrationStepResult.Converted(save);

              #region Internal

              SaveMigrationStepResult Fail(string failureReason)
              {
                  // 直接実行した場合も拒否理由を残す
                  // Preserve the refusal reason even when the step is called directly
                  Debug.LogWarning($"セーブを版7から版8へ変換できません: {failureReason}");
                  return SaveMigrationStepResult.Failed(failureReason);
              }

              #endregion
          }
      }
  }
  ```

- [ ] **Step 8: 現在版と連鎖の配列を直す**
  - `WorldSaveAllInfo.cs:27`: `public const int CurrentVersion = 9;` → `public const int CurrentVersion = 8;`
  - `SaveAndEventServiceRegistration.cs:117`、`SaveLoadPreparerTestFixture.cs:38`、`SaveMigrationChainTest.cs:138` の3か所の配列から、末尾の `, new SaveMigrationStepV8ToV9()` を消して、次の7本にする:
    ```csharp
    new ISaveMigrationStep[] { new SaveMigrationStepV1ToV2(), new SaveMigrationStepV2ToV3(), new SaveMigrationStepV3ToV4(), new SaveMigrationStepV4ToV5(), new SaveMigrationStepV5ToV6(), new SaveMigrationStepV6ToV7(), new SaveMigrationStepV7ToV8() }
    ```
  `SaveMigrationChain.ForCurrentVersion` は構築時に、`FromVersion` が `1..CurrentVersion-1`（＝1..7）を欠番・重複なく覆っているかを検証する。

- [ ] **Step 9: テストファイルを移して空ける**
  ```bash
  cd $WT; T=moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/Migration
  git mv $T/SaveMigrationStepV7ToV8Test.cs $T/SaveMigrationStepV7ToV8InheritedLevelTest.cs
  git mv $T/SaveMigrationStepV7ToV8Test.cs.meta $T/SaveMigrationStepV7ToV8InheritedLevelTest.cs.meta
  git mv $T/SaveMigrationStepV8ToV9Test.cs $T/SaveMigrationStepV7ToV8CleanRoomFluidTest.cs
  git mv $T/SaveMigrationStepV8ToV9Test.cs.meta $T/SaveMigrationStepV7ToV8CleanRoomFluidTest.cs.meta
  git rm $T/SaveMigrationStepV8ToV9UnlockRevokeTest.cs $T/SaveMigrationStepV8ToV9UnlockRevokeTest.cs.meta
  ```

- [ ] **Step 10: 継承レベルのテストを書き直す** `$T/SaveMigrationStepV7ToV8InheritedLevelTest.cs`（S1 の V7ToV8Test を移植）
  S1 の `InvalidOuterMachineStateReturnsFailureTest` にあった「連鎖ごと止まる」確認は、全体のテスト（Step 14）の `部品が拒否すると連鎖は版を刻まず止まるTest` へ移す。
  ```csharp
  using System.Text.RegularExpressions;
  using Game.SaveLoad.Migration.Steps;
  using Newtonsoft.Json.Linq;
  using NUnit.Framework;
  using UnityEngine;
  using UnityEngine.TestTools;

  namespace Tests.UnitTest.Game.SaveLoad.Migration
  {
      public class SaveMigrationStepV7ToV8InheritedLevelTest
      {
          private const string VanillaKey = "Game.Block.Blocks.Machine.VanillaMachineSaveComponent";
          private const string CleanRoomKey = "Game.Block.Blocks.CleanRoom.Machine.CleanRoomMachineProcessorComponent";

          [Test]
          public void 通常機械とクリーンルーム機械に継承レベルnullが入り他のキーは残るTest()
          {
              var vanilla = JObject.Parse("{\"inputSlot\":[],\"processor\":{\"state\":1,\"remainingSeconds\":0.5,\"recipeGuid\":\"r\"}}");
              var cleanRoom = JObject.Parse("{\"cycleCount\":3,\"state\":0}");
              var save = Save(
                  new JObject { ["state"] = new JObject { [VanillaKey] = vanilla } },
                  new JObject { ["state"] = new JObject { [CleanRoomKey] = cleanRoom } });

              var result = new SaveMigrationStepV7ToV8().Migrate(save);

              Assert.IsTrue(result.IsConverted, result.FailureReason);
              var migratedVanilla = (JObject)result.Save["world"][0]["state"][VanillaKey];
              Assert.AreEqual(JTokenType.Null, migratedVanilla["processor"]["inheritedLevel"].Type);
              Assert.AreEqual(1, migratedVanilla["processor"]["state"].Value<int>());
              Assert.AreEqual("r", migratedVanilla["processor"]["recipeGuid"].Value<string>());
              var migratedCleanRoom = (JObject)result.Save["world"][1]["state"][CleanRoomKey];
              Assert.AreEqual(JTokenType.Null, migratedCleanRoom["inheritedLevel"].Type);
              Assert.AreEqual(3, migratedCleanRoom["cycleCount"].Value<int>());
          }

          [Test]
          public void 既に継承レベルを持つものと機械以外は素通しされるTest()
          {
              var save = Save(
                  JObject.Parse($"{{\"state\":{{\"{VanillaKey}\":{{\"processor\":{{\"inheritedLevel\":3}}}}}}}}"),
                  JObject.Parse("{\"state\":{\"ChestComponent\":{\"items\":[]}}}"),
                  JObject.Parse("{\"blockGuid\":\"z\"}"),
                  JObject.Parse("{\"state\":null}"));

              var result = new SaveMigrationStepV7ToV8().Migrate(save);

              Assert.IsTrue(result.IsConverted, result.FailureReason);
              Assert.AreEqual(3, result.Save["world"][0]["state"][VanillaKey]["processor"]["inheritedLevel"].Value<int>());
              Assert.IsNull(result.Save["world"][1]["state"]["ChestComponent"]["inheritedLevel"]);
          }

          [Test]
          public void processorがオブジェクトでなければ失敗するTest()
          {
              var save = Save(JObject.Parse($"{{\"state\":{{\"{VanillaKey}\":{{\"processor\":5}}}}}}"));

              var result = new SaveMigrationStepV7ToV8().Migrate(save);

              Assert.IsFalse(result.IsConverted);
              StringAssert.Contains("processor", result.FailureReason);
          }

          [Test]
          public void 通常機械のprocessorが明示nullなら飛ばして台数をログに残すTest()
          {
              // 清浄室機械と同じく、ロード側が復元を見送るnullで変換を止めない（判断記録 A18）
              // Like clean-room machines, a null the loader skips must not stop the conversion (decision A18)
              var save = Save(JObject.Parse($"{{\"state\":{{\"{VanillaKey}\":{{\"processor\":null}}}}}}"));
              LogAssert.Expect(LogType.Log, new Regex("processorが null で飛ばした通常機械=1台"));

              var result = new SaveMigrationStepV7ToV8().Migrate(save);

              Assert.IsTrue(result.IsConverted, result.FailureReason);
              Assert.AreEqual(JTokenType.Null, result.Save["world"][0]["state"][VanillaKey]["processor"].Type);
          }


          [TestCase("5")]
          [TestCase("\"broken\"")]
          [TestCase("[]")]
          public void 機械の状態がオブジェクトでなければ失敗するTest(string json)
          {
              var save = Save(new JObject { ["state"] = new JObject { [VanillaKey] = JToken.Parse(json) } });

              var result = new SaveMigrationStepV7ToV8().Migrate(save);

              Assert.IsFalse(result.IsConverted);
              StringAssert.Contains(VanillaKey, result.FailureReason);
          }

          #region TestHelper

          private static JObject Save(params JObject[] blocks)
          {
              return new JObject { ["worldVersion"] = 7, ["world"] = new JArray(blocks) };
          }

          #endregion
      }
  }
  ```

- [ ] **Step 11: 液体スロットのテストを書き直す** `$T/SaveMigrationStepV7ToV8CleanRoomFluidTest.cs`（S1 の V8ToV9Test を移植し、W17 のケースを足す）
  ```csharp
  using System.Text.RegularExpressions;
  using Game.SaveLoad.Migration.Steps;
  using Newtonsoft.Json.Linq;
  using NUnit.Framework;
  using UnityEngine;
  using UnityEngine.TestTools;

  namespace Tests.UnitTest.Game.SaveLoad.Migration
  {
      public class SaveMigrationStepV7ToV8CleanRoomFluidTest
      {
          private const string ProcessorKey = "Game.Block.Blocks.CleanRoom.Machine.CleanRoomMachineProcessorComponent";
          private const string WireKey = "Game.Block.Blocks.ElectricWire.ElectricWireConnectorComponent";
          private const string FailureLog = "版7から版8へ変換できません";

          [Test]
          public void 液体キーの無い清浄室機械へ空配列が足され他のキーは残るTest()
          {
              // 版7の清浄室機械の実際の形（液体キーも継承レベルも無い）
              // The actual version-7 clean-room machine shape (neither fluid keys nor an inherited level)
              var processor = JObject.Parse("{\"cycleCount\":3,\"state\":0,\"inputSlot\":[]}");
              var save = Save(new JObject { ["blockGuid"] = "m", ["state"] = new JObject { [ProcessorKey] = processor, [WireKey] = new JObject { ["wires"] = new JArray() } } });

              var result = new SaveMigrationStepV7ToV8().Migrate(save);

              Assert.IsTrue(result.IsConverted, result.FailureReason);
              var migrated = (JObject)result.Save["world"][0]["state"][ProcessorKey];
              Assert.IsTrue(JToken.DeepEquals(new JArray(), migrated["inputFluidSlot"]));
              Assert.IsTrue(JToken.DeepEquals(new JArray(), migrated["outputFluidSlot"]));
              Assert.AreEqual(3, migrated["cycleCount"].Value<int>());
              Assert.IsNotNull(result.Save["world"][0]["state"][WireKey]);
          }

          [Test]
          public void 清浄室機械でないブロックと既に液体キーを持つブロックは素通しされるTest()
          {
              var save = Save(
                  JObject.Parse("{\"blockGuid\":\"x\",\"state\":{\"ChestComponent\":{\"items\":[]}}}"),
                  JObject.Parse("{\"blockGuid\":\"y\"}"),
                  JObject.Parse("{\"blockGuid\":\"z\",\"state\":null}"),
                  new JObject { ["state"] = new JObject { [ProcessorKey] = JObject.Parse("{\"inheritedLevel\":null,\"inputFluidSlot\":[{\"fluidGuid\":\"g\",\"amount\":5}],\"outputFluidSlot\":[]}") } });
              var originalWorld = save["world"].DeepClone();

              var result = new SaveMigrationStepV7ToV8().Migrate(save);

              Assert.IsTrue(result.IsConverted, result.FailureReason);
              Assert.IsTrue(JToken.DeepEquals(originalWorld, result.Save["world"]));
          }

          [Test]
          public void processorが明示nullの清浄室機械は飛ばして台数をログに残すTest()
          {
              // ロード側もnullを復元見送りとして扱うので、変換を止めず台数だけ残す
              // The loader also treats null as skip-restore, so keep converting and only record the count
              var save = Save(new JObject { ["state"] = new JObject { [ProcessorKey] = JValue.CreateNull() } });
              LogAssert.Expect(LogType.Log, new Regex("processorが null で飛ばした清浄室機械=1台"));

              var result = new SaveMigrationStepV7ToV8().Migrate(save);

              Assert.IsTrue(result.IsConverted, result.FailureReason);
              Assert.AreEqual(JTokenType.Null, result.Save["world"][0]["state"][ProcessorKey].Type);
          }

          [Test]
          public void 清浄室機械の液体キーが明示nullなら空配列で補うTest()
          {
              // 明示nullは欠損と同じ扱い（判断記録 A18）
              // An explicit null is treated like a missing key (decision A18)
              var save = Save(JObject.Parse($"{{\"state\":{{\"{ProcessorKey}\":{{\"inputFluidSlot\":null,\"outputFluidSlot\":null}}}}}}"));

              var result = new SaveMigrationStepV7ToV8().Migrate(save);

              Assert.IsTrue(result.IsConverted, result.FailureReason);
              Assert.AreEqual(0, ((JArray)result.Save["world"][0]["state"][ProcessorKey]["inputFluidSlot"]).Count);
              Assert.AreEqual(0, ((JArray)result.Save["world"][0]["state"][ProcessorKey]["outputFluidSlot"]).Count);
          }

          [Test]
          public void 再実行しても変化せず版はステップが書き換えないTest()
          {
              var save = Save(new JObject { ["state"] = new JObject { [ProcessorKey] = new JObject() } });

              var first = new SaveMigrationStepV7ToV8().Migrate(save);
              Assert.IsTrue(first.IsConverted, first.FailureReason);
              var once = first.Save.DeepClone();
              var second = new SaveMigrationStepV7ToV8().Migrate(first.Save);

              Assert.IsTrue(second.IsConverted, second.FailureReason);
              Assert.IsTrue(JToken.DeepEquals(once, second.Save));
              Assert.AreEqual(7, second.Save["worldVersion"].Value<int>());
          }

          // 辿れない形は理由つきで拒否し、原本を変えない
          // Refuse unwalkable shapes with a reason and leave the original untouched
          [TestCase("{\"world\":{}}")]
          [TestCase("{}")]
          [TestCase("{\"world\":[1]}")]
          [TestCase("{\"world\":[{\"state\":[]}]}")]
          [TestCase("{\"world\":[{\"state\":{\"Game.Block.Blocks.CleanRoom.Machine.CleanRoomMachineProcessorComponent\":[]}}]}")]
          [TestCase("{\"world\":[{\"state\":{\"Game.Block.Blocks.CleanRoom.Machine.CleanRoomMachineProcessorComponent\":{\"inputFluidSlot\":\"broken\"}}}]}")]
          [TestCase("{\"world\":[{\"state\":{\"Game.Block.Blocks.CleanRoom.Machine.CleanRoomMachineProcessorComponent\":{\"outputFluidSlot\":1}}}]}")]
          public void 辿れない構造は理由つきで拒否するTest(string json)
          {
              var save = JObject.Parse(json);
              save["worldVersion"] = 7;
              var original = save.DeepClone();
              LogAssert.Expect(LogType.Warning, new Regex(FailureLog));

              var result = new SaveMigrationStepV7ToV8().Migrate(save);

              Assert.IsFalse(result.IsConverted);
              Assert.IsNotEmpty(result.FailureReason);
              Assert.IsTrue(JToken.DeepEquals(original, save));
          }

          [Test]
          public void 後ろのブロックが壊れていても前のブロックを書き換えないTest()
          {
              var save = Save(
                  new JObject { ["state"] = new JObject { [ProcessorKey] = new JObject() } },
                  new JObject { ["state"] = new JObject { [ProcessorKey] = new JArray() } });
              var original = save.DeepClone();
              LogAssert.Expect(LogType.Warning, new Regex(FailureLog));

              var result = new SaveMigrationStepV7ToV8().Migrate(save);

              Assert.IsFalse(result.IsConverted);
              Assert.IsTrue(JToken.DeepEquals(original, save));
          }

          #region TestHelper

          private static JObject Save(params JObject[] blocks)
          {
              return new JObject { ["worldVersion"] = 7, ["world"] = new JArray(blocks) };
          }

          #endregion
      }
  }
  ```

- [ ] **Step 12: 打ち上げ履歴のテストを書き直す** `$T/SaveMigrationStepV7ToV8RocketHistoryTest.cs`（S2 の V6ToV7Test を移植。真偽値の素通しを足す）
  ```csharp
  using System.Text.RegularExpressions;
  using Game.SaveLoad.Migration.Steps;
  using Newtonsoft.Json.Linq;
  using NUnit.Framework;
  using UnityEngine;
  using UnityEngine.TestTools;

  namespace Tests.UnitTest.Game.SaveLoad.Migration
  {
      public class SaveMigrationStepV7ToV8RocketHistoryTest
      {
          [Test]
          public void 版7のセーブに未打ち上げの履歴が足され他の節は残るTest()
          {
              // プールの節を先に持たせ、履歴以外の差分が出ないようにする
              // Give the pool section up front so the only difference is the history
              var save = JObject.Parse("{\"worldVersion\":7,\"world\":[{\"blockGuid\":\"x\"}],\"cleanRoomRooms\":[],\"spaceElevatorCablePool\":{\"cableCount\":0}}");
              var original = (JObject)save.DeepClone();

              var result = new SaveMigrationStepV7ToV8().Migrate(save);

              Assert.IsTrue(result.IsConverted, result.FailureReason);
              Assert.AreEqual(JTokenType.Boolean, result.Save["rocketHasLaunchedOnce"].Type);
              Assert.IsFalse(result.Save["rocketHasLaunchedOnce"].Value<bool>());
              var withoutHistory = (JObject)result.Save.DeepClone();
              withoutHistory.Remove("rocketHasLaunchedOnce");
              Assert.IsTrue(JToken.DeepEquals(original, withoutHistory));
          }

          [Test]
          public void nullの履歴は未打ち上げで補うTest()
          {
              var save = JObject.Parse("{\"worldVersion\":7,\"world\":[],\"rocketHasLaunchedOnce\":null}");

              var result = new SaveMigrationStepV7ToV8().Migrate(save);

              Assert.IsTrue(result.IsConverted, result.FailureReason);
              Assert.IsFalse(result.Save["rocketHasLaunchedOnce"].Value<bool>());
          }

          [TestCase(true)]
          [TestCase(false)]
          public void 真偽値の履歴は素通しするTest(bool launched)
          {
              var save = new JObject { ["worldVersion"] = 7, ["world"] = new JArray(), ["rocketHasLaunchedOnce"] = launched };

              var result = new SaveMigrationStepV7ToV8().Migrate(save);

              Assert.IsTrue(result.IsConverted, result.FailureReason);
              Assert.AreEqual(launched, result.Save["rocketHasLaunchedOnce"].Value<bool>());
          }

          [TestCase("1")]
          [TestCase("\"launched\"")]
          [TestCase("{}")]
          [TestCase("[]")]
          public void 真偽値でない履歴は上書きせず変換しないTest(string historyJson)
          {
              var save = JObject.Parse($"{{\"worldVersion\":7,\"world\":[],\"rocketHasLaunchedOnce\":{historyJson}}}");
              var original = (JObject)save.DeepClone();
              LogAssert.Expect(LogType.Warning, new Regex("版7から版8へ変換できません"));

              var result = new SaveMigrationStepV7ToV8().Migrate(save);

              Assert.IsFalse(result.IsConverted);
              StringAssert.Contains("rocketHasLaunchedOnce", result.FailureReason);
              Assert.IsTrue(JToken.DeepEquals(original, save));
          }
      }
  }
  ```

- [ ] **Step 13: ケーブルプールのテストを書き直す** `$T/SaveMigrationStepV7ToV8CablePoolTest.cs`（S3 の V7ToV8Test を移植）
  拒否ケースには `world` を足してある。S3 のままだと、先に走る継承レベル部品が world 欠損で拒否してしまい、プール部品の拒否を確かめられないため。
  ```csharp
  using System.Text.RegularExpressions;
  using Game.SaveLoad.Migration.Steps;
  using Newtonsoft.Json.Linq;
  using NUnit.Framework;
  using UnityEngine;
  using UnityEngine.TestTools;

  namespace Tests.UnitTest.Game.SaveLoad.Migration
  {
      public class SaveMigrationStepV7ToV8CablePoolTest
      {
          [Test]
          public void 節が無ければ0で足すTest()
          {
              var result = new SaveMigrationStepV7ToV8().Migrate(JObject.Parse("{\"worldVersion\":7,\"world\":[]}"));

              Assert.IsTrue(result.IsConverted, result.FailureReason);
              Assert.AreEqual(0, result.Save["spaceElevatorCablePool"]["cableCount"].Value<int>());
              Assert.IsNull(result.Save["backfilledFields"]);
          }

          [Test]
          public void 明示nullの節も0で足すTest()
          {
              var result = new SaveMigrationStepV7ToV8().Migrate(JObject.Parse("{\"worldVersion\":7,\"world\":[],\"spaceElevatorCablePool\":null}"));

              Assert.IsTrue(result.IsConverted, result.FailureReason);
              Assert.AreEqual(0, result.Save["spaceElevatorCablePool"]["cableCount"].Value<int>());
          }

          [Test]
          public void 正しい節があれば素通しするTest()
          {
              var result = new SaveMigrationStepV7ToV8().Migrate(JObject.Parse("{\"worldVersion\":7,\"world\":[],\"spaceElevatorCablePool\":{\"cableCount\":250}}"));

              Assert.IsTrue(result.IsConverted, result.FailureReason);
              Assert.AreEqual(250, result.Save["spaceElevatorCablePool"]["cableCount"].Value<int>());
          }

          [TestCase("{\"world\":[],\"spaceElevatorCablePool\":5}")]
          [TestCase("{\"world\":[],\"spaceElevatorCablePool\":{\"cableCount\":\"x\"}}")]
          [TestCase("{\"world\":[],\"spaceElevatorCablePool\":{}}")]
          public void 形の違う節は変換を拒むTest(string json)
          {
              var save = JObject.Parse(json);
              var original = save.DeepClone();
              LogAssert.Expect(LogType.Warning, new Regex("版7から版8へ変換できません"));

              var result = new SaveMigrationStepV7ToV8().Migrate(save);

              Assert.IsFalse(result.IsConverted);
              StringAssert.Contains("spaceElevatorCablePool", result.FailureReason);
              Assert.IsTrue(JToken.DeepEquals(original, save));
          }
      }
  }
  ```

- [ ] **Step 14: 全体のテストを作る** `$T/SaveMigrationStepV7ToV8Test.cs`
  master 版7の実形を JSON で書き、全部品の変換・途中で拒否したときに何も書き換えないこと・冪等性・版6からの連鎖を確かめる。
  ```csharp
  using System.Text.RegularExpressions;
  using Game.SaveLoad.Json.WorldVersions;
  using Game.SaveLoad.Migration;
  using Game.SaveLoad.Migration.Steps;
  using Newtonsoft.Json.Linq;
  using NUnit.Framework;
  using UnityEngine;
  using UnityEngine.TestTools;

  namespace Tests.UnitTest.Game.SaveLoad.Migration
  {
      public class SaveMigrationStepV7ToV8Test
      {
          private const string VanillaKey = "Game.Block.Blocks.Machine.VanillaMachineSaveComponent";
          private const string CleanRoomKey = "Game.Block.Blocks.CleanRoom.Machine.CleanRoomMachineProcessorComponent";
          private const string PipeKey = "Game.Block.Blocks.Fluid.FluidPipeSaveComponent";

          [Test]
          public void 版7のセーブに全部品の変換が入るTest()
          {
              var result = new SaveMigrationStepV7ToV8().Migrate(MasterV7Save());

              Assert.IsTrue(result.IsConverted, result.FailureReason);
              var save = result.Save;
              Assert.AreEqual(JTokenType.Null, save["world"][0]["state"][VanillaKey]["processor"]["inheritedLevel"].Type);
              var cleanRoom = save["world"][1]["state"][CleanRoomKey];
              Assert.AreEqual(JTokenType.Null, cleanRoom["inheritedLevel"].Type);
              Assert.IsTrue(JToken.DeepEquals(new JArray(), cleanRoom["inputFluidSlot"]));
              Assert.IsTrue(JToken.DeepEquals(new JArray(), cleanRoom["outputFluidSlot"]));
              Assert.IsFalse(save["rocketHasLaunchedOnce"].Value<bool>());
              Assert.AreEqual(0, save["spaceElevatorCablePool"]["cableCount"].Value<int>());
              Assert.AreEqual(7, save["worldVersion"].Value<int>());
          }

          [Test]
          public void 後ろの部品が拒否すると前の部品も書き換えないTest()
          {
              // 継承レベルを足すべき機械があっても、プールの節が壊れていれば何も変えない
              // Even with machines awaiting an inherited level, a broken pool section changes nothing
              var save = MasterV7Save();
              save["spaceElevatorCablePool"] = 5;
              var original = save.DeepClone();
              LogAssert.Expect(LogType.Warning, new Regex("版7から版8へ変換できません"));

              var result = new SaveMigrationStepV7ToV8().Migrate(save);

              Assert.IsFalse(result.IsConverted);
              StringAssert.Contains("spaceElevatorCablePool", result.FailureReason);
              Assert.IsTrue(JToken.DeepEquals(original, save));
          }

          [Test]
          public void 再実行しても変化しないTest()
          {
              var first = new SaveMigrationStepV7ToV8().Migrate(MasterV7Save());
              Assert.IsTrue(first.IsConverted, first.FailureReason);
              var once = first.Save.DeepClone();

              var second = new SaveMigrationStepV7ToV8().Migrate(first.Save);

              Assert.IsTrue(second.IsConverted, second.FailureReason);
              Assert.IsTrue(JToken.DeepEquals(once, second.Save));
          }

          [Test]
          public void 変換元の版は7で現在版は8Test()
          {
              Assert.AreEqual(7, new SaveMigrationStepV7ToV8().FromVersion);
              Assert.AreEqual(8, WorldSaveAllInfo.CurrentVersion);
          }

          [Test]
          public void 版6のセーブは流体変換を経て現在版まで変換されるTest()
          {
              var save = JObject.Parse($"{{\"worldVersion\":6,\"world\":[{{\"state\":{{\"{PipeKey}\":{{\"amount\":1.5}}}}}}]}}");

              var result = CurrentVersionChain().Migrate(save);

              Assert.IsTrue(result.CanLoad, result.BlockedReason);
              Assert.AreEqual(WorldSaveAllInfo.CurrentVersion, result.Save["worldVersion"].Value<int>());
              Assert.AreEqual(1_500_000_000L, result.Save["world"][0]["state"][PipeKey]["amountNano"].Value<long>());
              Assert.IsFalse(result.Save["rocketHasLaunchedOnce"].Value<bool>());
              Assert.AreEqual(0, result.Save["spaceElevatorCablePool"]["cableCount"].Value<int>());
          }

          [Test]
          public void 部品が拒否すると連鎖は版を刻まず止まるTest()
          {
              var save = MasterV7Save();
              save["world"][0]["state"][VanillaKey] = 5;
              LogAssert.Expect(LogType.Error, new Regex("セーブをV7からV8へ変換できませんでした"));

              var result = CurrentVersionChain().Migrate(save);

              Assert.IsFalse(result.CanLoad);
              Assert.AreEqual(7, save["worldVersion"].Value<int>());
          }

          #region TestHelper

          // master版7の実形: 継承レベルの無い通常機械・液体キーの無い清浄室機械・履歴とプールの節なし
          // The real master version-7 shape: a normal machine without an inherited level, a clean-room machine without fluid keys, no history or pool sections
          private static JObject MasterV7Save()
          {
              return new JObject
              {
                  ["worldVersion"] = 7,
                  ["world"] = new JArray(
                      new JObject { ["blockGuid"] = "a", ["state"] = new JObject { [VanillaKey] = JObject.Parse("{\"inputSlot\":[],\"processor\":{\"state\":1,\"recipeGuid\":\"r\"}}") } },
                      new JObject { ["blockGuid"] = "b", ["state"] = new JObject { [CleanRoomKey] = JObject.Parse("{\"cycleCount\":3,\"state\":0}") } }),
                  // 旧マスタでコスト0だった金属超精密加工を完了し、金属3Dプリンタのレシピ表示を解放済み（Task 7 で使う）
                  // Completed the formerly zero-cost metal precision research with the metal 3D printer recipe view unlocked (used by Task 7)
                  ["research"] = JObject.Parse("{\"CompletedResearchGuids\":[\"019e3ad9-46e2-73fd-9555-8c1d0245d26d\",\"019e3af5-f2e6-76de-a80f-5e66e79fa35d\"]}"),
                  ["gameUnlockState"] = JObject.Parse("{\"itemUnlockStateInfos\":[{\"guid\":\"019e3af5-e750-71ae-ab09-2b98f3db9913\",\"isUnlocked\":true}],\"blockUnlockStateInfos\":[]}"),
              };
          }

          // 本番と同じ配列（SaveAndEventServiceRegistration）
          // The same array as production (SaveAndEventServiceRegistration)
          private static SaveMigrationChain CurrentVersionChain()
          {
              return SaveMigrationChain.ForCurrentVersion(new ISaveMigrationStep[] { new SaveMigrationStepV1ToV2(), new SaveMigrationStepV2ToV3(), new SaveMigrationStepV3ToV4(), new SaveMigrationStepV4ToV5(), new SaveMigrationStepV5ToV6(), new SaveMigrationStepV6ToV7(), new SaveMigrationStepV7ToV8() });
          }

          #endregion
      }
  }
  ```

- [ ] **Step 15: 版を名指しする結合テストを直す**
  - `CleanRoomMachineFluidTest.cs:81-104`: メソッド名を `VersionEightStateLoadsAfterMigrationTest` → `VersionSevenStateLoadsAfterMigrationTest` に変え、中身は次の差分にする。
    ```csharp
                // 版7の形（液体キー無し）を作り、V7→V8 を通した状態から実ロードする
                // Rebuild the version-7 shape (no fluid keys), run V7->V8 and load the block from the result
    ...
                var save = new JObject { ["worldVersion"] = 7, ["world"] = new JArray(new JObject { ["state"] = JObject.FromObject(states) }) };

                var result = new SaveMigrationStepV7ToV8().Migrate(save);
    ```
    `GetSaveState()` 由来の state は `inheritedLevel` を既に持つので、継承レベル部品は素通しする。液体部品だけが効く。
  - `RocketLaunchHistorySaveLoadTest.cs` の `版6は本番ロード経路で…` の中のコメント2行:
    ```csharp
                // 移行前の原本と退避を保ち、再保存には現在版のfalseが出る
                // Preserve the original and archive, and re-save false in the current version
    ```
  - `SpaceElevatorUpstreamMigrationSaveLoadTest.cs:60-61` のコメント2行（`[TestCase(7, true)]` は部品の素通しで緑になる）:
    ```csharp
                // 版6は履歴なし、版7は履歴を既に持つ形（部品は素通しする）。どちらもプール節なしの実ファイルにする
                // Write V6 without history and V7 already carrying history (the part passes it through), both without the pool section
    ```
  `LevelInheritanceSaveLoadTest.Version7PlainMachineLoadsWithoutInheritedLevelTest` は変更しない。版7と名乗らせたセーブが新しい V7→V8 を通り、履歴とプールの節は現行セーブ由来の値がそのまま素通しされる。

- [ ] **Step 16: Unity に .meta を作らせてコンパイル**（新規の .cs が5本〔部品3本・共有の辿り方1本・テスト1本〕と interface 1本あるので再起動する。save-migration スキル Gotchas）
  Run: `cd $WT && uloop launch ./moorestech_client --restart && uloop compile --project-path ./moorestech_client && git status --short | grep '\.meta$'`
  Expected: compile エラー0。新しい `.meta` は6本（`V7ToV8/IV7ToV8MigrationPart.cs.meta`・`V7ToV8SaveJson.cs.meta`・`MachineInheritedLevelFiller.cs.meta`・`RocketLaunchHistoryFiller.cs.meta`・`SpaceElevatorCablePoolFiller.cs.meta`・`Migration/SaveMigrationStepV7ToV8Test.cs.meta`）。

- [ ] **Step 17: テスト**
  Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "SaveMigration|SaveLoadPreparer|MigrationLoad|LevelInheritanceSaveLoad|CleanRoomMachineFluid|RocketLaunchHistorySaveLoad|SpaceElevatorUpstreamMigration|SaveTickAndRandomState" --timeout-seconds 900`
  Expected: `Success: true`、失敗0。Task 4 の時点で赤だった `RocketLaunchHistorySaveLoadTest`・`SpaceElevatorUpstreamMigrationSaveLoadTest`・`LevelInheritanceSaveLoadTest` が緑に戻る。

- [ ] **Step 18: 行数と本数を確かめる**
  Run: `cd $WT && wc -l moorestech_server/Assets/Scripts/Game.SaveLoad/Migration/Steps/V7ToV8/*.cs moorestech_server/Assets/Scripts/Game.SaveLoad/Migration/Steps/SaveMigrationStepV7ToV8.cs moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/Migration/*.cs && ls moorestech_server/Assets/Scripts/Game.SaveLoad/Migration/Steps/*.cs | wc -l`
  Expected: どれも200行以下。Steps 直下の .cs は `7`

- [ ] **Step 19: コミット**
  `git add -A` / `git commit -m "fix(save): Tier9 統合のセーブ移行を V7→V8 の1本に畳み、継承レベル・液体スロット・初回打ち上げ履歴・ケーブルプールを部品に分ける（CurrentVersion=8、S1 の V8→V9 と解放取り消しを外す）"`

**完了判定:**
- `WorldSaveAllInfo.CurrentVersion == 8` で、連鎖の配列3か所がどれも7本（`git grep -c "SaveMigrationStepV8ToV9"` が0）。
- `git grep -n "UnimplementedBlockUnlockRevoker"` が0件（`.decisions` の裁定ファイル2本は除く）。
- Step 17 が緑。
- Step 18 で200行超えが0、Steps 直下の .cs が7本、`V7ToV8/` が10本以下。

---

### Task 7: 旧マスタでコスト0のまま完了した Tier9 研究をリセットする部品（裁定2）

**手段の調査結果と推奨: (a) セーブ移行の部品で外す。** (b) は採らない。

**解放状態の仕組み（実測）:**
- ロード順は次のとおり。①`LoadUnlockState`（保存済みの解放を戻す。`WorldSaveDataRestorer.cs:103`）→ ②`LoadResearchData`（完了済み研究の `ClearedActions` を `ExecuteUnlockActions` でやり直す。`:124`、`ResearchDataStore.cs:139-157`）→ ③`LoadChallenge`（完了済みチャレンジの解放もやり直す。`ChallengeDatastore.cs:203`）。
- 保存済みの解放は holder の `Load` が上書きする。`BlockUnlockStateHolder.Load`・`ItemUnlockStateHolder.Load` は記録をそのまま `_infos` に入れる。
- 記録の無い項目は、コンストラクタが入れたマスタの `InitialUnlocked` のまま残る。
- つまり、完了を外すだけでは足りない。保存済みの `isUnlocked: true` が①で戻り、解放は残る。

**(b) マスタでノードの GUID を振り直し、MissingMasterPruner に落とさせる案の検討:**
- 完了一覧からは `ResearchSectionPruner` が消してくれる。しかし解放の記録は消えない。アイテム・ブロックの GUID は変わらないので、①で解放が残る。裁定2の「そのノードが解放していたものも取り消す」を満たせない。
- さらに、振り直しで書き換える範囲が広い。research.json の `prevResearchNodeGuids`、マスタ repo の `localization.csv` にある `research.<GUID>.name/description` 行、`.mooreseditor/nodeGraph.v1.json` の `masterGuid`、S3 plan の GUID 表（3417行付近）がすべて対象になる。
- 加えて、刈り込みの件数通知がプレイヤーに「マスタから消えた研究」として出てしまい、誤解を招く。

**(a) の要点:**
- 完了一覧から対象ノードを外す。
- そのノードが旧マスタで解放したアイテム・ブロックの記録は、`false` にせず**記録ごと消す**。記録が無ければ①でマスタの初期値になり、②③のやり直しが、残った研究・チャレンジの分を解放し直す。
- 比較: S1 の Revoker 方式（`isUnlocked=false` を書く）は、初期解放のものや他の研究も解放する項目まで閉じてしまう。
- 記録を消したときに「初期値に戻る」「やり直しで再び解放される」ことは、新設の結合テスト `ResearchUnlockRecordFallbackLoadTest` で実ロードして確かめる。

**対象（moorestech_master の `server_v8/.../research.json` の全履歴81コミットを走査した和集合）:**

| 研究（GUID） | 旧マスタで解放したもの |
|---|---|
| フィルター分岐器 `019e3aab-32c6-7166-a184-d761153c2498` | なし |
| 機械オーバークロック `019e3ae2-5005-7439-b242-9b7b3191a2c3` | なし |
| 金属超精密加工 `019e3af5-f2e6-76de-a80f-5e66e79fa35d` | アイテム 金属3Dプリンタ `019e3af5-e750-71ae-ab09-2b98f3db9913`（cdc6abd 以降すべて） |
| 核融合炉 `019e3af0-297f-726e-8a02-0a8518124e6d` | ブロック 核融合炉 `01a162f1-ab04-76e2-94cf-dc713edeb63b`（b2149a0・19ef0c2） |
| 反物質爆弾 `019e3aef-a149-746b-8fad-159e0434cb51` | アイテム 反物質爆弾 `01a162ab-07ba-73c1-9dd6-38e1e867663b`（b2149a0 以降） |
| 軌道エレベーターの建設 `01a16291-4094-7369-a87f-5dfe2197d25e` | アイテム グラフェンケーブル `01a16290-f793…`・グラフェンシート `01a16297-4724…`・使用済み銅板 `01a16297-6665…`・高純度炭素 `01a16297-a7f5…`・ロケット筐体素材 `01a162a4-9334…`・ロケット燃料 `01a162a8-e435…`。ブロック 宇宙エレベーター拠点 `01a162aa-cec0…`・ロケット発射台 `01a162b0-7769…`（b2149a0 以降） |

注意:
- 出荷中のピン（`origin/master` の `0d129c8`）には「軌道エレベーターの建設」がまだ無い。代わりに、コスト0の「ロケット」`019e3af0-7b1c-77de-9197-87d276c84392`（解放なし）がある。このノードは統合マスタに無いので、`ResearchSectionPruner` が完了一覧から消す。表には入れない（起草者メモ3）。
- 表のアイテム・ブロックの `initialUnlocked` は、630fdce ではすべて `false`。

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.SaveLoad/Migration/Steps/V7ToV8/ZeroCostTier9ResearchResetTable.cs` / `moorestech_server/Assets/Scripts/Game.SaveLoad/Migration/Steps/V7ToV8/ZeroCostTier9ResearchResetter.cs` / `moorestech_server/Assets/Scripts/Game.SaveLoad/Migration/Steps/V7ToV8/NestedSaveArray.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Migration/Steps/SaveMigrationStepV7ToV8.cs` / `moorestech_server/Assets/Scripts/Game.SaveLoad/Migration/Steps/V7ToV8/V7ToV8SaveJson.cs`（`TryGetNestedArray` を足す）
- Test（Create）: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/Migration/SaveMigrationStepV7ToV8ResearchResetTest.cs` / `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/Research/ResearchUnlockRecordFallbackLoadTest.cs`
- Test（Modify）: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/Migration/SaveMigrationStepV7ToV8Test.cs`

**Interfaces:**
- Consumes: `IV7ToV8MigrationPart`・`V7ToV8SaveJson`（Task 6）、`ResearchDataStoreTest.CompleteResearchForTest(ServiceProvider, Guid)`・`ResearchDataStoreTest.Research4Guid`・`ResearchDataStoreTest.Test3ItemGuid`（既存。`Tests/CombinedTest/Game/ResearchDataStoreTest.cs:24-26,165`）
- Produces:
  - `public static class ZeroCostTier9ResearchResetTable { public static readonly IReadOnlyDictionary<Guid, ResearchUnlockGuids> UnlocksByResearch; }`
  - `public sealed class ResearchUnlockGuids { IReadOnlyList<Guid> ItemGuids; IReadOnlyList<Guid> BlockGuids; }`
  - `public sealed class ZeroCostTier9ResearchResetter : IV7ToV8MigrationPart`
  - `public enum NestedSaveArrayState { Missing, ExplicitNull, Present }`、`public readonly struct NestedSaveArray { NestedSaveArrayState State; JArray Array /* Present のときだけ。他は InvalidOperationException */; static Missing() / ExplicitNull() / Present(JArray) }`
  - `V7ToV8SaveJson.TryGetNestedArray(JObject save, string sectionKey, string arrayKey, out NestedSaveArray result, out string failureReason) -> bool`（false は形が違うときの拒否だけ）

- [ ] **Step 1: 対象表を作る** `Steps/V7ToV8/ZeroCostTier9ResearchResetTable.cs`
  ```csharp
  using System;
  using System.Collections.Generic;

  namespace Game.SaveLoad.Migration.Steps.V7ToV8
  {
      /// <summary>
      /// 旧マスタでコスト0のまま完了できたTier9研究と、旧マスタでその研究が解放していたアイテム・ブロック
      /// Tier9 research that old masters let players finish at zero cost, and the items/blocks each of them unlocked there
      /// </summary>
      /// <remarks>
      /// ステップはマスタを引かないのでGUIDを直に持つ。値は moorestech_master の server_v8 research.json の履歴（cdc6abd〜19ef0c2）の和集合
      /// Steps never read the master, so GUIDs are held directly; values are the union over the history of moorestech_master server_v8 research.json (cdc6abd to 19ef0c2)
      /// </remarks>
      public static class ZeroCostTier9ResearchResetTable
      {
          // 辞書より先に初期化する（静的初期化は記述順）
          // Initialize before the dictionary (static initializers run in textual order)
          private static readonly Guid[] Nothing = Array.Empty<Guid>();

          public static readonly IReadOnlyDictionary<Guid, ResearchUnlockGuids> UnlocksByResearch = new Dictionary<Guid, ResearchUnlockGuids>
          {
              // フィルター分岐器・機械オーバークロック: 旧マスタでは何も解放しない
              // Filter splitter and machine overclock: unlocked nothing in old masters
              [Guid.Parse("019e3aab-32c6-7166-a184-d761153c2498")] = new ResearchUnlockGuids(Nothing, Nothing),
              [Guid.Parse("019e3ae2-5005-7439-b242-9b7b3191a2c3")] = new ResearchUnlockGuids(Nothing, Nothing),
              // 金属超精密加工: 金属3Dプリンタのレシピ表示
              // Metal precision machining: the metal 3D printer recipe view
              [Guid.Parse("019e3af5-f2e6-76de-a80f-5e66e79fa35d")] = new ResearchUnlockGuids(new[] { Guid.Parse("019e3af5-e750-71ae-ab09-2b98f3db9913") }, Nothing),
              // 核融合炉: 核融合炉ブロック
              // Fusion reactor: the fusion reactor block
              [Guid.Parse("019e3af0-297f-726e-8a02-0a8518124e6d")] = new ResearchUnlockGuids(Nothing, new[] { Guid.Parse("01a162f1-ab04-76e2-94cf-dc713edeb63b") }),
              // 反物質爆弾: 反物質爆弾のレシピ表示
              // Antimatter bomb: the antimatter bomb recipe view
              [Guid.Parse("019e3aef-a149-746b-8fad-159e0434cb51")] = new ResearchUnlockGuids(new[] { Guid.Parse("01a162ab-07ba-73c1-9dd6-38e1e867663b") }, Nothing),
              // 軌道エレベーターの建設: グラフェン系とロケット素材のレシピ表示、宇宙エレベーター拠点とロケット発射台
              // Orbital elevator construction: graphene and rocket material recipe views, the space elevator base and the rocket launch pad
              [Guid.Parse("01a16291-4094-7369-a87f-5dfe2197d25e")] = new ResearchUnlockGuids(
                  new[]
                  {
                      Guid.Parse("01a16290-f793-748b-a0b0-91d44be0d4ab"),
                      Guid.Parse("01a16297-4724-73b9-82c3-2e77120be77d"),
                      Guid.Parse("01a16297-6665-765d-8e19-96d357394c3e"),
                      Guid.Parse("01a16297-a7f5-759b-91ba-be4021ca3bf2"),
                      Guid.Parse("01a162a4-9334-7286-a8ba-7daf9c0f407a"),
                      Guid.Parse("01a162a8-e435-73d0-bddb-08533819b80e"),
                  },
                  new[]
                  {
                      Guid.Parse("01a162aa-cec0-738c-93c5-f58adc87fa7f"),
                      Guid.Parse("01a162b0-7769-7419-af24-bb35528b93d2"),
                  }),
          };
      }

      // 1研究が旧マスタで解放したアイテム（レシピ表示）とブロック
      // The items (recipe views) and blocks one research unlocked in old masters
      public sealed class ResearchUnlockGuids
      {
          public IReadOnlyList<Guid> ItemGuids { get; }
          public IReadOnlyList<Guid> BlockGuids { get; }

          public ResearchUnlockGuids(IReadOnlyList<Guid> itemGuids, IReadOnlyList<Guid> blockGuids)
          {
              ItemGuids = itemGuids;
              BlockGuids = blockGuids;
          }
      }
  }
  ```

- [ ] **Step 2: 共有の辿り方に「無い／null／配列」の3状態を返す `TryGetNestedArray` を足し、リセット部品を作る** `Steps/V7ToV8/NestedSaveArray.cs`・`Steps/V7ToV8/V7ToV8SaveJson.cs`・`Steps/V7ToV8/ZeroCostTier9ResearchResetter.cs`
  結果の状態は enum 1 つで表す（bool と配列の null の組で「拒否／節なし／配列あり」を表さない。判断記録 A31）。拒否（形が違う）だけは他の `TryPrepare` と同じく bool の false と理由で返す。

  `Steps/V7ToV8/NestedSaveArray.cs`:
  ```csharp
  using System;
  using Newtonsoft.Json.Linq;

  namespace Game.SaveLoad.Migration.Steps.V7ToV8
  {
      // save[節][配列] を辿った結果の状態。Missing=節か配列のキーが無い、ExplicitNull=どちらかが JSON の null、Present=配列がある
      // State of walking save[section][array]: Missing = a key is absent, ExplicitNull = one of them is JSON null, Present = the array exists
      public enum NestedSaveArrayState
      {
          Missing,
          ExplicitNull,
          Present,
      }

      /// <summary>
      /// save[節][配列] を辿った結果。状態は State の1つで表し、配列は Present のときだけ読める
      /// The result of walking save[section][array]; the state is the single State value and the array is readable only when Present
      /// </summary>
      public readonly struct NestedSaveArray
      {
          public readonly NestedSaveArrayState State;
          private readonly JArray _array;

          private NestedSaveArray(NestedSaveArrayState state, JArray array)
          {
              State = state;
              _array = array;
          }

          public static NestedSaveArray Missing()
          {
              return new NestedSaveArray(NestedSaveArrayState.Missing, new JArray());
          }

          public static NestedSaveArray ExplicitNull()
          {
              return new NestedSaveArray(NestedSaveArrayState.ExplicitNull, new JArray());
          }

          public static NestedSaveArray Present(JArray array)
          {
              return new NestedSaveArray(NestedSaveArrayState.Present, array);
          }

          // Present 以外で読むのは呼び出し側の分岐漏れなので、空配列で黙って続けず例外で止める
          // Reading it in any other state is a missed branch in the caller, so stop with an exception instead of silently iterating nothing
          public JArray Array => State == NestedSaveArrayState.Present
              ? _array
              : throw new InvalidOperationException($"NestedSaveArray.Array は Present のときだけ読める。 state={State}");
      }
  }
  ```

  `Steps/V7ToV8/V7ToV8SaveJson.cs`（Task 6 Step 2 のクラスの `TryCollectBlockStates` の後ろに、次のメソッドを足す）:
  ```csharp
          // save[sectionKey][arrayKey] を辿り、無い／null／配列の3状態を result に入れる。形が違えば拒否する
          // Walk save[sectionKey][arrayKey] and put one of absent/null/array into result; a wrong shape is refused
          public static bool TryGetNestedArray(JObject save, string sectionKey, string arrayKey, out NestedSaveArray result, out string failureReason)
          {
              result = NestedSaveArray.Missing();
              failureReason = null;
              var sectionToken = save[sectionKey];
              if (sectionToken == null) return true;
              if (IsExplicitNull(sectionToken))
              {
                  result = NestedSaveArray.ExplicitNull();
                  return true;
              }
              if (!(sectionToken is JObject section)) return Reject($"{sectionKey} がオブジェクトではありません。 type={sectionToken.Type}", out failureReason);

              var arrayToken = section[arrayKey];
              if (arrayToken == null) return true;
              if (IsExplicitNull(arrayToken))
              {
                  result = NestedSaveArray.ExplicitNull();
                  return true;
              }
              if (!(arrayToken is JArray found)) return Reject($"{sectionKey}.{arrayKey} が配列ではありません。 type={arrayToken.Type}", out failureReason);
              result = NestedSaveArray.Present(found);
              return true;
          }
  ```

  `Steps/V7ToV8/ZeroCostTier9ResearchResetter.cs`:
  セーブのキーは実測した値。`research.CompletedResearchGuids` は、`ResearchSaveJsonObject` に JsonProperty が無いため大文字始まりになる（`ResearchSectionPruner.cs:15` と同じ）。`gameUnlockState.itemUnlockStateInfos[].guid` と `blockUnlockStateInfos[].guid` は `GameUnlockStateDatastoreController.cs` の JsonProperty と `ItemUnlockStateInfo.cs:32`・`BlockUnlockStateInfo.cs:31` から取った。
  ```csharp
  using System;
  using System.Collections.Generic;
  using Newtonsoft.Json.Linq;

  namespace Game.SaveLoad.Migration.Steps.V7ToV8
  {
      /// <summary>
      /// 旧マスタでコスト0のまま完了できたTier9研究の完了を外し、その研究が解放した記録を消す（冪等）
      /// Remove completions of Tier9 research that old masters let players finish at zero cost, and delete the unlock records they produced (idempotent)
      /// </summary>
      /// <remarks>
      /// 解放記録はfalseにせず消す。記録が無ければロード時にマスタの初期値が使われ、残った研究とチャレンジの解放やり直しが必要な分を解放し直す。
      /// Unlock records are deleted rather than set to false: without a record the master's initial value applies on load, and the unlock replays of the remaining research and challenges re-grant whatever is still owed.
      /// </remarks>
      public sealed class ZeroCostTier9ResearchResetter : IV7ToV8MigrationPart
      {
          // ResearchSaveJsonObjectはJsonProperty無しなのでプロパティ名がそのままキーになる
          // ResearchSaveJsonObject carries no JsonProperty, so the property name itself is the key
          private const string ResearchKey = "research";
          private const string CompletedResearchGuidsKey = "CompletedResearchGuids";
          private const string GameUnlockStateKey = "gameUnlockState";
          private const string ItemUnlockStateInfosKey = "itemUnlockStateInfos";
          private const string BlockUnlockStateInfosKey = "blockUnlockStateInfos";
          private const string GuidKey = "guid";

          private readonly List<JToken> _completedEntries = new();
          private readonly HashSet<Guid> _resetResearchGuids = new();
          private readonly List<JToken> _unlockRecords = new();

          public bool TryPrepare(JObject save, out string failureReason)
          {
              // 完了一覧から対象ノードを集める。研究節か完了一覧が無い・null のセーブは外すものが無い
              // Collect target nodes from the completed list; a save whose research section or list is absent or null has nothing to remove
              if (!V7ToV8SaveJson.TryGetNestedArray(save, ResearchKey, CompletedResearchGuidsKey, out var completed, out failureReason)) return false;
              if (completed.State != NestedSaveArrayState.Present) return true;
              foreach (var entry in completed.Array)
              {
                  // 読めない値は後段の刈り込みが警告して残すので、ここでは対象ノードだけを拾う
                  // The later pruner warns about unreadable values, so only target nodes are picked here
                  if (!TryReadGuid(entry, out var researchGuid)) continue;
                  if (!ZeroCostTier9ResearchResetTable.UnlocksByResearch.ContainsKey(researchGuid)) continue;
                  _completedEntries.Add(entry);
                  _resetResearchGuids.Add(researchGuid);
              }
              if (_resetResearchGuids.Count == 0) return true;

              // 外すノードが旧マスタで解放したアイテム・ブロックの記録を集める
              // Collect the item and block records that the removed nodes unlocked in old masters
              var revokedItems = new HashSet<Guid>();
              var revokedBlocks = new HashSet<Guid>();
              foreach (var researchGuid in _resetResearchGuids)
              {
                  var unlocks = ZeroCostTier9ResearchResetTable.UnlocksByResearch[researchGuid];
                  revokedItems.UnionWith(unlocks.ItemGuids);
                  revokedBlocks.UnionWith(unlocks.BlockGuids);
              }
              if (!TryCollectRecords(save, ItemUnlockStateInfosKey, revokedItems, out failureReason)) return false;
              return TryCollectRecords(save, BlockUnlockStateInfosKey, revokedBlocks, out failureReason);
          }

          public string ApplyAndSummarize()
          {
              foreach (var entry in _completedEntries) entry.Remove();
              foreach (var record in _unlockRecords) record.Remove();
              return $"コスト0研究のリセット: 完了を外した研究={_resetResearchGuids.Count}件[{string.Join(",", _resetResearchGuids)}] 消した解放記録={_unlockRecords.Count}件";
          }

          // 解放状態の配列から対象GUIDの記録を集める。解放状態の節か配列が無い・null のセーブは消すものが無い
          // Collect records of target GUIDs from an unlock-state array; a save whose section or array is absent or null has nothing to delete
          private bool TryCollectRecords(JObject save, string infosKey, HashSet<Guid> targetGuids, out string failureReason)
          {
              if (!V7ToV8SaveJson.TryGetNestedArray(save, GameUnlockStateKey, infosKey, out var infos, out failureReason)) return false;
              if (infos.State != NestedSaveArrayState.Present) return true;
              foreach (var infoToken in infos.Array)
              {
                  if (!(infoToken is JObject info)) return V7ToV8SaveJson.Reject($"{GameUnlockStateKey}.{infosKey} の要素がオブジェクトではありません。 type={infoToken.Type}", out failureReason);
                  if (TryReadGuid(info[GuidKey], out var guid) && targetGuids.Contains(guid)) _unlockRecords.Add(info);
              }

              return true;
          }

          private static bool TryReadGuid(JToken token, out Guid guid)
          {
              guid = Guid.Empty;
              return token is JValue { Type: JTokenType.String } value && Guid.TryParse((string)value, out guid);
          }
      }
  }
  ```

- [ ] **Step 3: ステップに5つ目の部品を足す** `Steps/SaveMigrationStepV7ToV8.cs`
  ```diff
  -    // V7→V8移行: Tier9統合で増えたセーブの形を機能ごとの部品で足す（継承レベル・清浄室機械の液体スロット・初回打ち上げ履歴・ケーブルプール）
  -    // V7->V8 migration: add the save shapes introduced by the Tier9 integration through one part per feature (inherited level, clean-room fluid slots, first-launch history, cable pool)
  +    // V7→V8移行: Tier9統合で変わるセーブの形を機能ごとの部品で直す（継承レベル・清浄室機械の液体スロット・初回打ち上げ履歴・ケーブルプール・コスト0研究のリセット）
  +    // V7->V8 migration: adjust the save shapes changed by the Tier9 integration through one part per feature (inherited level, clean-room fluid slots, first-launch history, cable pool, zero-cost research reset)
  ...
                   new SpaceElevatorCablePoolFiller(),
  +                new ZeroCostTier9ResearchResetter(),
               };
  ```

- [ ] **Step 4: 部品のテストを作る** `Tests/UnitTest/Game/SaveLoad/Migration/SaveMigrationStepV7ToV8ResearchResetTest.cs`
  ```csharp
  using System.Text.RegularExpressions;
  using Game.SaveLoad.Migration.Steps;
  using Newtonsoft.Json.Linq;
  using NUnit.Framework;
  using UnityEngine;
  using UnityEngine.TestTools;

  namespace Tests.UnitTest.Game.SaveLoad.Migration
  {
      public class SaveMigrationStepV7ToV8ResearchResetTest
      {
          // 旧マスタでコスト0だった6ノード
          // The six nodes that cost nothing in old masters
          private const string FilterSplitter = "019e3aab-32c6-7166-a184-d761153c2498";
          private const string MachineOverclock = "019e3ae2-5005-7439-b242-9b7b3191a2c3";
          private const string MetalPrecision = "019e3af5-f2e6-76de-a80f-5e66e79fa35d";
          private const string FusionReactor = "019e3af0-297f-726e-8a02-0a8518124e6d";
          private const string AntimatterBomb = "019e3aef-a149-746b-8fad-159e0434cb51";
          private const string OrbitalElevator = "01a16291-4094-7369-a87f-5dfe2197d25e";

          // 旧マスタで上の研究が解放していたもの
          // What the research above unlocked in old masters
          private const string MetalPrinterItem = "019e3af5-e750-71ae-ab09-2b98f3db9913";
          private const string GrapheneCableItem = "01a16290-f793-748b-a0b0-91d44be0d4ab";
          private const string ElevatorBaseBlock = "01a162aa-cec0-738c-93c5-f58adc87fa7f";
          private const string LaunchPadBlock = "01a162b0-7769-7419-af24-bb35528b93d2";
          private const string FusionReactorBlock = "01a162f1-ab04-76e2-94cf-dc713edeb63b";

          // 対象外（コストのあるTier8研究と無関係な記録）
          // Out of scope (a costed Tier8 research and unrelated records)
          private const string OtherResearch = "019e3ad9-46e2-73fd-9555-8c1d0245d26d";
          private const string OtherItem = "00000000-0000-0000-1234-000000000001";
          private const string OtherBlock = "00000000-0000-0000-0000-000000000001";

          [Test]
          public void 対象6ノードの完了だけが外れ他の完了と読めない値は残るTest()
          {
              var save = Save(Completed(OtherResearch, FilterSplitter, MachineOverclock, MetalPrecision, FusionReactor, AntimatterBomb, OrbitalElevator, "broken"), Unlocks(), Unlocks());

              var result = Migrate(save);

              Assert.IsTrue(JToken.DeepEquals(Completed(OtherResearch, "broken"), result["research"]["CompletedResearchGuids"]));
          }

          [Test]
          public void 外したノードが解放した記録だけが消え他の記録は残るTest()
          {
              // 金属超精密加工と軌道エレベーターの建設を完了。核融合炉は未完了なので炉ブロックの記録は残る
              // Metal precision and orbital elevator are completed; the fusion reactor is not, so its block record stays
              var save = Save(
                  Completed(OtherResearch, MetalPrecision, OrbitalElevator),
                  Unlocks(MetalPrinterItem, GrapheneCableItem, OtherItem),
                  Unlocks(ElevatorBaseBlock, LaunchPadBlock, FusionReactorBlock, OtherBlock));

              var result = Migrate(save);

              Assert.IsTrue(JToken.DeepEquals(Completed(OtherResearch), result["research"]["CompletedResearchGuids"]));
              Assert.IsTrue(JToken.DeepEquals(Unlocks(OtherItem), result["gameUnlockState"]["itemUnlockStateInfos"]));
              Assert.IsTrue(JToken.DeepEquals(Unlocks(FusionReactorBlock, OtherBlock), result["gameUnlockState"]["blockUnlockStateInfos"]));
          }

          [Test]
          public void 対象ノードを完了していないセーブは解放記録も触らないTest()
          {
              var save = Save(Completed(OtherResearch), Unlocks(MetalPrinterItem), Unlocks(FusionReactorBlock));
              var original = save.DeepClone();

              var result = Migrate(save);

              Assert.IsTrue(JToken.DeepEquals(original["research"], result["research"]));
              Assert.IsTrue(JToken.DeepEquals(original["gameUnlockState"], result["gameUnlockState"]));
          }

          [TestCase("{\"worldVersion\":7,\"world\":[]}")]
          [TestCase("{\"worldVersion\":7,\"world\":[],\"research\":null,\"gameUnlockState\":null}")]
          [TestCase("{\"worldVersion\":7,\"world\":[],\"research\":{\"CompletedResearchGuids\":null}}")]
          [TestCase("{\"worldVersion\":7,\"world\":[],\"research\":{\"CompletedResearchGuids\":[\"019e3af5-f2e6-76de-a80f-5e66e79fa35d\"]}}")]
          public void 研究や解放状態の節が無くても変換できるTest(string json)
          {
              var result = new SaveMigrationStepV7ToV8().Migrate(JObject.Parse(json));

              Assert.IsTrue(result.IsConverted, result.FailureReason);
          }

          [TestCase("{\"research\":[]}")]
          [TestCase("{\"research\":{\"CompletedResearchGuids\":{}}}")]
          [TestCase("{\"research\":{\"CompletedResearchGuids\":[\"019e3af5-f2e6-76de-a80f-5e66e79fa35d\"]},\"gameUnlockState\":[]}")]
          [TestCase("{\"research\":{\"CompletedResearchGuids\":[\"019e3af5-f2e6-76de-a80f-5e66e79fa35d\"]},\"gameUnlockState\":{\"itemUnlockStateInfos\":5}}")]
          [TestCase("{\"research\":{\"CompletedResearchGuids\":[\"019e3af5-f2e6-76de-a80f-5e66e79fa35d\"]},\"gameUnlockState\":{\"blockUnlockStateInfos\":[1]}}")]
          public void 形の違う節は理由つきで拒否し原本を変えないTest(string json)
          {
              var save = JObject.Parse(json);
              save["worldVersion"] = 7;
              save["world"] = new JArray();
              var original = save.DeepClone();
              LogAssert.Expect(LogType.Warning, new Regex("版7から版8へ変換できません"));

              var result = new SaveMigrationStepV7ToV8().Migrate(save);

              Assert.IsFalse(result.IsConverted);
              Assert.IsTrue(JToken.DeepEquals(original, save));
          }

          [Test]
          public void 再実行しても変化しないTest()
          {
              var once = Migrate(Save(Completed(OtherResearch, OrbitalElevator), Unlocks(GrapheneCableItem, OtherItem), Unlocks(LaunchPadBlock)));
              var snapshot = once.DeepClone();

              var twice = Migrate(once);

              Assert.IsTrue(JToken.DeepEquals(snapshot, twice));
          }

          #region TestHelper

          private static JObject Migrate(JObject save)
          {
              var result = new SaveMigrationStepV7ToV8().Migrate(save);
              Assert.IsTrue(result.IsConverted, result.FailureReason);
              return result.Save;
          }

          private static JObject Save(JArray completed, JArray itemInfos, JArray blockInfos)
          {
              return new JObject
              {
                  ["worldVersion"] = 7,
                  ["world"] = new JArray(),
                  ["research"] = new JObject { ["CompletedResearchGuids"] = completed },
                  ["gameUnlockState"] = new JObject { ["itemUnlockStateInfos"] = itemInfos, ["blockUnlockStateInfos"] = blockInfos },
              };
          }

          private static JArray Completed(params string[] guids)
          {
              var array = new JArray();
              foreach (var guid in guids) array.Add(guid);
              return array;
          }

          private static JArray Unlocks(params string[] guids)
          {
              var array = new JArray();
              foreach (var guid in guids) array.Add(new JObject { ["guid"] = guid, ["isUnlocked"] = true });
              return array;
          }

          #endregion
      }
  }
  ```

- [ ] **Step 5: 全体のテストに研究の確認を足す** `SaveMigrationStepV7ToV8Test.cs` の `版7のセーブに全部品の変換が入るTest` の末尾（`worldVersion` の assert の前）へ:
  ```csharp
              Assert.IsTrue(JToken.DeepEquals(new JArray("019e3ad9-46e2-73fd-9555-8c1d0245d26d"), save["research"]["CompletedResearchGuids"]));
              Assert.AreEqual(0, ((JArray)save["gameUnlockState"]["itemUnlockStateInfos"]).Count);
  ```

- [ ] **Step 6: 部品が頼るロード時の振る舞いを実ロードで確かめる結合テストを作る** `Tests/CombinedTest/Game/Research/ResearchUnlockRecordFallbackLoadTest.cs`
  テスト用マスタの「Test Research 4」は Test3 アイテム（`initialUnlocked: false`）のレシピ表示を解放する。解放の記録を消したセーブを読み込み、研究が完了のまま残っていれば解放され、研究の完了も外していれば解放されないことを見る。
  ```csharp
  using System;
  using System.Linq;
  using Core.Master;
  using Game.SaveLoad.Interface;
  using Game.SaveLoad.Json;
  using Game.UnlockState;
  using Microsoft.Extensions.DependencyInjection;
  using Newtonsoft.Json.Linq;
  using NUnit.Framework;
  using Server.Boot;
  using Tests.Module.TestMod;
  using static Tests.CombinedTest.Game.ResearchDataStoreTest;

  namespace Tests.CombinedTest.Game.Research
  {
      // コスト0研究のリセット部品が頼る「解放記録が無ければマスタ初期値、完了済み研究の再実行で解放し直す」を実ロードで確かめる
      // Verify by a real load what the zero-cost research reset relies on: no record means the master default, and completed research re-grants on replay
      public class ResearchUnlockRecordFallbackLoadTest
      {
          [TestCase(true, true)]
          [TestCase(false, false)]
          public void 解放記録を消したアイテムは完了済み研究の再実行でだけ解放されるTest(bool keepResearchCompleted, bool expectedUnlocked)
          {
              var (_, saveProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
              CompleteResearchForTest(saveProvider, Research4Guid);
              var save = JObject.Parse(saveProvider.GetService<AssembleSaveJsonText>().AssembleSaveJson());

              // Test3の解放記録を消し、ケースによっては研究4の完了も外す
              // Delete the Test3 unlock record and, depending on the case, also drop research 4's completion
              var itemInfos = (JArray)save["gameUnlockState"]["itemUnlockStateInfos"];
              itemInfos.First(info => Guid.Parse(info["guid"].Value<string>()) == Test3ItemGuid).Remove();
              if (!keepResearchCompleted)
              {
                  var completed = (JArray)save["research"]["CompletedResearchGuids"];
                  completed.First(entry => Guid.Parse(entry.Value<string>()) == Research4Guid).Remove();
              }

              var (_, loadProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
              (loadProvider.GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson).Load(save.ToString());

              var unlockState = loadProvider.GetService<IGameUnlockStateDataController>();
              var test3ItemId = MasterHolder.ItemMaster.GetItemId(Test3ItemGuid);
              Assert.AreEqual(expectedUnlocked, unlockState.ItemUnlockStateInfos[test3ItemId].IsUnlocked);
          }
      }
  }
  ```
  テスト用マスタで Test3 を解放するのは研究4だけ（`forUnitTest/master` を `1234-000000000003` で grep し、challenge 側に解放が無いことを確認済み）。

- [ ] **Step 7: .meta を作らせてコンパイル**（新規の .cs が5本）
  Run: `cd $WT && uloop launch ./moorestech_client --restart && uloop compile --project-path ./moorestech_client && git status --short | grep '\.meta$'`
  Expected: エラー0。新しい `.meta` は5本（`ZeroCostTier9ResearchResetTable.cs.meta`・`ZeroCostTier9ResearchResetter.cs.meta`・`NestedSaveArray.cs.meta`・`SaveMigrationStepV7ToV8ResearchResetTest.cs.meta`・`ResearchUnlockRecordFallbackLoadTest.cs.meta`）。

- [ ] **Step 8: テスト**
  Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "SaveMigrationStepV7ToV8|ResearchUnlockRecordFallbackLoad|ResearchDataStore|SaveLoadPreparer" --timeout-seconds 900`
  Expected: `Success: true`、失敗0

- [ ] **Step 9: コミット**
  `git add -A` / `git commit -m "fix(save): 旧マスタでコスト0のまま完了した Tier9 研究6ノードの完了を V7→V8 で外し、その研究が解放した記録を消す（裁定2）"`

**完了判定:**
- Step 8 が緑。
- `ResearchUnlockRecordFallbackLoadTest` の2ケースが pass（記録を消しても完了済みなら解放され、完了も外せば未解放）。
- `ZeroCostTier9ResearchResetTable.UnlocksByResearch.Count == 6`。

---

### Task 8: 研究ロードの無音スキップにログを足す

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.Research/ResearchDataStore.cs`（`LoadResearchData`、現在の 139〜157行。ファイルは162行で、変更後は約174行）
- Test（Create）: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/Research/ResearchDataStoreLoadWarningTest.cs`

**Interfaces:**
- Consumes: `IResearchDataStore.LoadResearchData(ResearchSaveJsonObject)`、`ResearchDataStoreTest.Research1Guid`
- Produces: 挙動は変えない（読めない GUID は数えない。マスタに無い GUID は完了には入れるが、解放はやり直さない）。変えるのは、それぞれの分岐で `Debug.LogWarning` を出すことだけ

なぜ必要か: 通常のロード経路（`SaveLoadPreparer`）は、先に `ResearchSectionPruner` を通す。そこで読めない GUID は警告付きで残り、マスタに無い研究は消える。しかし `WorldLoaderFromJson.Load(string)` を直接呼ぶ経路は刈り込みを通らないので、ここでの無音スキップに誰も気づけない（AGENTS.md「fail-closed で…無視する経路はログへ必ず出す」）。

- [ ] **Step 1: `LoadResearchData` の foreach を次に置き換える**（`UnityEngine.Debug` は、`ResearchConsumeItemsUtil.cs:25` と同じく完全修飾で書き、using は足さない）
  ```csharp
              foreach (var guidString in saveData.CompletedResearchGuids)
              {
                  // 読めない値は完了に数えない。刈り込みを通らない読み込み経路でも気づけるよう警告する
                  // An unreadable value is not counted as completed; warn so load paths that skip the pruner still surface it
                  if (!Guid.TryParse(guidString, out var guid))
                  {
                      UnityEngine.Debug.LogWarning($"研究の完了一覧に GUID として読めない値があるため読み飛ばします。 value={guidString}");
                      continue;
                  }

                  _completedResearchGuids.Add(guid);

                  // マスタに無い研究は解放をやり直せない
                  // A research missing from the master cannot replay its unlocks
                  var researchElement = MasterHolder.ResearchMaster?.GetResearch(guid);
                  if (researchElement == null)
                  {
                      UnityEngine.Debug.LogWarning($"完了済みの研究がマスタに無いため解放のやり直しを見送ります。 researchGuid={guid}");
                      continue;
                  }

                  // 新規追加された要素のアンロックアクションを実行
                  // Replay unlock actions so newly added elements get unlocked
                  _gameActionExecutor.ExecuteUnlockActions(researchElement.ClearedActions.items);
              }
  ```

- [ ] **Step 2: テストを作る** `Tests/CombinedTest/Game/Research/ResearchDataStoreLoadWarningTest.cs`
  ```csharp
  using System.Collections.Generic;
  using System.Text.RegularExpressions;
  using Game.Research;
  using Microsoft.Extensions.DependencyInjection;
  using NUnit.Framework;
  using Server.Boot;
  using Tests.Module.TestMod;
  using UnityEngine;
  using UnityEngine.TestTools;
  using static Tests.CombinedTest.Game.ResearchDataStoreTest;

  namespace Tests.CombinedTest.Game.Research
  {
      public class ResearchDataStoreLoadWarningTest
      {
          [Test]
          public void 読めないGUIDとマスタに無い研究は警告を残して読み飛ばすTest()
          {
              var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
              var researchDataStore = serviceProvider.GetService<IResearchDataStore>();
              LogAssert.Expect(LogType.Warning, new Regex("GUID として読めない値.*value=not-a-guid"));
              LogAssert.Expect(LogType.Warning, new Regex("マスタに無いため.*researchGuid=ffffffff-ffff-ffff-ffff-ffffffffffff"));

              researchDataStore.LoadResearchData(new ResearchSaveJsonObject { CompletedResearchGuids = new List<string> { "not-a-guid", "ffffffff-ffff-ffff-ffff-ffffffffffff", Research1Guid.ToString() } });

              // 正常な研究は従来どおり完了として読み込まれる
              // A valid research still loads as completed
              Assert.IsTrue(researchDataStore.IsResearchCompleted(Research1Guid));
          }
      }
  }
  ```

- [ ] **Step 3: コンパイルとテスト**
  Run: `cd $WT && uloop launch ./moorestech_client --restart && uloop compile --project-path ./moorestech_client && uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ResearchDataStore|CompleteResearch|MissingMasterPrune" --timeout-seconds 600`
  Expected: compile エラー0、テストは失敗0

- [ ] **Step 4: コミット**
  `git add -A` / `git commit -m "fix(research): 完了済み研究のロードで GUID が読めない・マスタに無いときに警告を残す"`

**完了判定:**
- `ResearchDataStoreLoadWarningTest` が pass（警告2本を LogAssert で観測）。
- `ResearchDataStore.cs` が200行以下。

---

### Task 9: マージ後の健全性ゲート

**Files:**
- Test のみ（コードを変えた場合は、原因のタスクのコミットへ戻らず、`fix(merge): …` の追加コミットにする）

**Interfaces:**
- Consumes: Task 4〜Task 8 のコミット
- Produces: P2 以降が前提にする「コードは緑」の証拠一式（下の出力を P6 の PR 本文に貼る）

- [ ] **Step 1: クリーンにコンパイルし、エラーログが無いことを確かめる**
  Run: `cd $WT && uloop clear-console --project-path ./moorestech_client && uloop compile --project-path ./moorestech_client && uloop get-logs --project-path ./moorestech_client --log-type Error`
  Expected: compile エラー0、Error ログ0件

- [ ] **Step 2: セーブまわりのテスト**（クラス名から組んだ正規表現。`git grep -hE "public (sealed )?class [A-Za-z0-9_]+"` の結果から、統合で触れた機能の語を拾った）
  Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "SaveMigration|SaveLoad|MigrationLoad|MissingMasterPrune|MissingMasterItemPrune|MissingMasterRailPrune|SaveTickAndRandomState" --timeout-seconds 1500`
  Expected: `Success: true`

- [ ] **Step 3: Tier9 の機能のテスト**
  Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "RocketLaunch|Orbital|SpaceElevator|CleanRoom|ElectricGenerator|PowerGenerator|GearToElectricGenerator|LevelInherit|LevelFamily|MachineUpgrade|ModuleSlot|MachineModuleSlot" --timeout-seconds 1500`
  Expected: `Success: true`

- [ ] **Step 4: 研究・解放・Web UI ホストのテスト**（Task 4 で衝突した DTO と webui 契約を含む）
  Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Research|Unlock|WireContract|BlockDetail|WebUiTopic|BlockInventory" --timeout-seconds 1500`
  Expected: `Success: true`

- [ ] **Step 5: サーバーテスト全体**（S3 が master から33コミット遅れていたことによる食い違いを拾う唯一の手段。MERGE.md 3章4）
  Run: `uloop run-tests --project-path ./moorestech_client --filter-type assembly --filter-value "Server.Tests" --timeout-seconds 1500`
  Expected: `Success: true`。タイムアウトした場合は2つに分けて流す: `--filter-type regex --filter-value "^Tests\.UnitTest\."` と `--filter-type regex --filter-value "^Tests\.CombinedTest\."`（いずれも `--timeout-seconds 1500`）

- [ ] **Step 6: webui の型検査と vitest（CI の `webui-test` ジョブと同じ3本）と、テストの外部依存の検査**
  ```bash
  NODE_DIR=$(ls -d $WT/moorestech_web/node/*/ | head -1); export PATH="$NODE_DIR/bin:$PATH"; PNPM="$NODE_DIR/pnpm"
  cd $WT/moorestech_web && "$PNPM" --dir webui exec tsc -b --force && "$PNPM" --dir webui exec tsc -p e2e/tsconfig.json --noEmit && "$PNPM" --dir webui exec vitest run
  cd $WT && python3 .github/scripts/check-test-external-dependencies.py
  ```
  Expected: 3本ともエラー0・失敗0。外部依存の検査は exit 0（テストが本番マスタを読んでいない）

- [ ] **Step 7: P1 で触ったファイルの行数**
  Run: `cd $WT && M=$(git log --merges -1 --format=%H) && { git diff --name-only --diff-filter=AMR $M..HEAD -- '*.cs' '*.ts' '*.tsx'; echo moorestech_web/webui/src/bridge/contract/schemas/inventory.ts; echo moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BlockDetail/BlockDetailDtoBuilder.cs; } | sort -u | xargs wc -l | awk '$2 != "total" && $1 > 200'`
  Expected: 出力なし

- [ ] **Step 8: push**
  `git push origin feat/tier9-integrated`

**完了判定:** Step 1〜7 の出力がすべて期待どおりで、その結果を P6 の PR 本文用に保存してある（compile 結果・各 run-tests の `TestCount`/`FailedCount`・tsc/vitest の要約）。`git log origin/feat/tier9-integrated -1` が手元の HEAD と一致する。

---



## P2 マスタの中身の投入

**目的:** マスタ PR #88（S1）と #87（S2）を通常マージした統合ブランチの上に、S1 → S2 → S3 の順でマスタの中身をスライスごとに積む。各スライスの末尾で G1（ロード）と G2（到達性）、nodeGraph の検査（判断記録 A21）を回し、赤から緑へ進むことで完了を判定する。最後にバランスを G6 で確定し、push・PR 作成・本体ピン更新・e2e fixture の更新まで行う。

**前提（着手条件）:**
- P1 が完了していること。統合ブランチ `feat/tier9-integrated`（本体 worktree `$W`）に S2・S3 のコードが入り、`uloop compile` が通り、セーブ移行が V7→V8 の 1 本に畳まれている。スキーマ `VanillaSchema/blocks.yml` が S1 の `allowQualityModule` と S2/S3 の `RocketLaunchPad`・`SpaceElevatorBase` を併せ持つ。統合前の S3 ブランチでは `_s3blocks.yml:1105`・`:1156`、S1 ブランチでは ElectricMachine の `allowQualityModule`（`blocks.yml:318`）で確認した。
- P0 の G1・G2・G6 が使えること（下の「ゲートの呼び方」）。
- 3 本の下書き plan が `$W` に揃っていること。いずれも P1 のマージで入る。
  - S1: `docs/superpowers/plans/2026-10-23-tier9-s1-master-content-handoff.md`（以下「S1 handoff」）
  - S2: `docs/superpowers/plans/2026-10-23-tier9-compute-data-loop.md` の節「統合タスクへ引き継ぐマスタの中身」（3471 行目付近。以下「S2 引き継ぎ」）
  - S3: `docs/superpowers/plans/2026-10-23-tier9-fusion-elevator-endgame.md` の節「統合タスクへ引き継ぐマスタの中身」（3371 行目付近。以下「S3 引き継ぎ」）

**共通の変数**（全タスクで使う）:
```bash
W=/Users/sakastudio/hermes-agent/data/worktrees/moorestech/tier9-integrated        # 本体の統合 worktree（P1 で作成済み）
MW=/Users/sakastudio/hermes-agent/data/worktrees/moorestech_master/tier9-integrated # マスタの統合 worktree（Task 1 で作る）
B=$MW/design/tier9-balance                                                          # spec・適用スクリプト・GUID 台帳と S1 の sim の置き場（照合は tools/tier9_balance。判断記録 A22）
MOD=$MW/server_v8/mods/moorestechAlphaMod_8
GL=$W/moorestech_client/PlaytestResults/tier9-gates                                 # ゲート出力の控え。git 管理外（.gitignore:123 で確認）
```

**ゲートの呼び方**（P0 の成果物を使う。実際のコマンド名・出力の書式は P0 節の Produces が正）:
- **G1: 出荷マスタのロードゲート（Task 10）。** 指定したディレクトリ `server_v8` の出荷マスタを `MasterHolder.Load` で読み込み、起動時のマスタ検証まで通す。この節では `G1 $MW/server_v8` と書く。
  - G1 は Task 10 の関数 `G1`（`ShippedMasterGateEntry.RunLoadGate`）だけで回す。`MoorestechServerDIContainerGenerator().Create(...)` を dynamic code で直接呼ぶ別経路は使わない。static のマスタを書き換える口を `ShippedMasterOwner` の 1 つに保ち、読み込みに失敗したときの戻しを所有者に任せるため（判断記録 A32）。P2 は J1（Task 10 の完了）の後に始まるので、関数は必ずある。
  - 合格の条件: `G1 PASS` が返り、`uloop get-logs --project-path ./moorestech_client --log-type Error` に `MooresmasterLoaderException`・`[BlockMaster]`・`[ItemMaster]`・`[MachineUpgrade]`・`[OrbitalPayload]` の行が無い。
- **G2: 到達性チェッカー。** Task 2 が `tools/progression/` に作り直したもの。絶対判定（許可リスト外の詰みが 1 件でもあれば exit 1）、ゴール到達判定、Tier9 のモデル（orbitalPayloads・levelInheritance・ダイ表の抽選）を持つ。この節では `G2` と書き、`cd $MW && python3 tools/progression/check.py` を指す。
  - 判定に使う出力: ゴール「最終研究」に届くか、入手できないアイテム、どこからも解放されないブロック、機械が解放されないレシピ、許可した詰みの一覧。
  - P2 では**許可リストに 1 行も足さない**。確認は `git -C $MW diff --stat origin/feat/tier9-integrated -- tools/progression/allowlist.json` が空であること。
- **G6: tier9_balance。** Task 3 の枠 `tools/tier9_balance/` に、S1 区間（Task 12）・S2 区間（Task 19 Step 5）・S3 区間（Task 22 Step 6。S3 引き継ぎ「旧 Task 4」「旧 Task 12」を `sections/s3_endgame.py` と `sections/s3/` に書いたもの）を入れたもの。この節では `G6` と書き、`cd $MW && python3 tools/tier9_balance/run.py` を指す。入口はこれ 1 つ（判断記録 A22）。
  - 目標時間を判定し、sim の値と JSON の値を照合する。どちらかが外れれば exit 1。
  - S1 の sim は `$B/sim/`（Task 3 Step 7 が置く。値の正本）。`params` の定数と `timeline.RESEARCH` は S1 handoff「Task D1」Interfaces の Produces と同じ名前で、Task 12 の `sections/s1/` がこれを import して照合 8 項目を `checks.matches` で出す。
  - 行は `PASS|FAIL [区間] 種類 状態 名前: 詳細`（Task 3。状態は `ok`・`mismatch`・`out_of_range`・`lookup_failed`・`not_positive`・`empty`）。S1 の照合と引き当てが全部通ったかは、目標外（`target out_of_range`。値の確定は Task 23）を除いた FAIL の数 `grep '^FAIL \[s1_semiconductor\] ' <出力> | grep -vc ' target out_of_range '` が 0 であることで見る（S2・S3 の区間が空のうちは G6 全体は exit 1 のまま。S2・S3 は区間名を `s2_compute_loop`・`s3_endgame` に替えて同じように見る）。

**JSON の書き方（全スライス共通）:** マスタ JSON は手で書かない。S1 handoff の方式で、名前で参照する spec（`$B/spec/NN_*.json`）を GUID 台帳 `$B/guids.json` から適用スクリプトで流し込む。
- 適用: `cd $B && python3 apply/apply_spec.py --mod-dir $MOD --spec-dir spec --ledger guids.json`
- 研究の解放割り当て: `cd $MW && python3 tools/research_sync/sync_machine_recipe_unlocks.py`
  - 解放同期則（ADR 0033）で `unlockMachineRecipe` を組み直し、末尾に改行を付けて書く（`sync_machine_recipe_unlocks.py:195`）。
  - 研究の解放がまだ無いスライスの途中では、孤児レシピで exit 2 になる。そのため**スライス末尾でだけ**流す。
  - spec を直したら、必ず「apply → sync」の組で流す。
- 照合: G6（`cd $MW && python3 tools/tier9_balance/run.py`）。S1 の値は `[s1_semiconductor] match` の行で見る。

**下書きからの横断的な訂正**（以下の各タスクで具体化する）:
1. 置き場を `design/tier9-semiconductor-balance/` から `design/tier9-balance/` に変える。S1・S2・S3 の sim・spec・apply・台帳を 1 か所にまとめるため（判定の G6 は `tools/tier9_balance/`。A22）。
2. S1 で必須化した `allowQualityModule`（ElectricMachine・GearMachine・CleanRoomMachine）と `levelInheritance`（全機械レシピ）を、S2・S3 の新規行すべてに入れる。
3. 拠点の `unlockBlock` を「軌道エレベーターの建設」に戻す（裁定3）。S1 のマスタ 32874fe が消したもの。
4. 仮ブロック 3 種（拠点・発射台・核融合炉）は、index ではなく GUID で、正式な blockType の全文に置き換える。
5. 旧 EUV レシピ `3a13a5a9` を消し、`KNOWN_ORPHANS` からも外す。
6. 超純水を消す。
7. GPU の名前の表記ゆれを揃える。
8. 流体量は換算しない（根拠は Task 20）。

---

### Task 1: マスタ統合ブランチを作り、#88 と #87 を通常マージして起点のゲート出力を控える

**Files:**
- Create（git worktree）: `/Users/sakastudio/hermes-agent/data/worktrees/moorestech_master/tier9-integrated`（ブランチ `feat/tier9-integrated`、moorestech_master）
- Create（控え・git 管理外）: `$GL/p2-1-base-g1.txt`、`$GL/p2-1-base-g2.txt`

**Interfaces:**
- Consumes: `origin/feat/tier9-cleanroom-playable`（#88、630fdce）と `origin/feat/tier9-compute-data-loop`（#87、19ef0c2）。どちらも b2149a0 から分かれている。`git merge-tree` の結果 f9370e7 で衝突が無いことは確認済み。
- Produces: `$MW` の HEAD。tree はちょうど `f9370e7e7ce313e1a6a04b1c3776cd9f620aab66` になる。以後の P2 のコミットはすべてこの上に積む。

- [ ] **Step 1: worktree を切る**（置き場は全プロジェクト共通の規約 `worktrees/<project>/<name>`。共有の main clone `../moorestech_master` では checkout しない）
```bash
git -C /Users/sakastudio/hermes-agent/data/repos/moorestech_master fetch origin
git -C /Users/sakastudio/hermes-agent/data/repos/moorestech_master worktree add -b feat/tier9-integrated $MW origin/master
git -C $MW log --oneline -1   # b2149a0 tire9基板
```
- [ ] **Step 2: #88 と #87 を、マージコミットを作る形でこの順に取り込む**（Squash は使わない。AGENTS.md「Beads」節）
```bash
git -C $MW merge --no-ff origin/feat/tier9-cleanroom-playable -m "Merge #88 (Tier9 S1 クリーンルーム) into feat/tier9-integrated"
git -C $MW merge --no-ff origin/feat/tier9-compute-data-loop -m "Merge #87 (Tier9 S2 計算データ) into feat/tier9-integrated"
test "$(git -C $MW rev-parse HEAD^{tree})" = f9370e7e7ce313e1a6a04b1c3776cd9f620aab66 && echo TREE-OK
```
Expected: 2 回とも衝突しない。最後に `TREE-OK` が出る。
- [ ] **Step 3: 起点のゲート出力を控える**（G2 の `tools/progression/check.py` は Task 2 が置く。セッション B は Step 1・2・4 → Task 2 → この Step の順に回す）
```bash
mkdir -p $GL
G1 $MW/server_v8 2>&1 | tee $GL/p2-1-base-g1.txt
(cd $MW && python3 tools/progression/check.py) 2>&1 | tee $GL/p2-1-base-g2.txt; echo "G2 exit=$?"
```
Expected:
- G1 は PASS する見込み（推測）。仮の 3 行は `blockType:"Block"`・`blockParam:{}` で読み込みが通る形になっている（`fc5c6f0`）。`orbitalPayloads` は空配列だが、これを要求する発射台がまだ無い。
- G2 は exit 1。末尾は Task 2 Step 9 と同じ `== RESULT: FAIL (69 failures, 19 allowed)`。ゴール「最終研究」がまだ無い。入手できないアイテムは許可済み 4 件を足して 33 件（`wf/CONTENT.md` §2 の表の「定義だけ」と「無い」の列）、解放されないブロックは拠点・発射台・核融合炉ほか。
- G1 が落ちた場合は、P1 のスキーマ統合とマスタの食い違いなので、P1 へ戻す。P2 では直さない。
- [ ] **Step 4: push する**（中間状態を共有するため。PR は Task 24 で作る）
```bash
git -C $MW push -u origin feat/tier9-integrated
```

**完了判定:** `TREE-OK` が出ている。`$GL/p2-1-base-g2.txt` に G2 の赤（ゴール未定義と入手不能の一覧）が控えてある。`git -C $MW log --merges --oneline -2` で 2 つのマージコミットが見える。
**量感:** JSON の変更は 0 行。マージコミット 2 個。

---

### Task 12: 適用スクリプト・GUID 台帳・G6 の S1 区間の照合・nodeGraph 同期ツールを置く（S1 handoff の D2。D1 の sim は Task 3 Step 7）

**Files:**
- Create: `moorestech_master:design/tier9-balance/apply/master_io.py`、`apply/resolver.py`、`apply/element_builders.py`、`apply/localization_rows.py`、`apply/apply_spec.py`、`apply/apply_extras.py`
- Create: `moorestech_master:design/tier9-balance/guids.json`、`design/tier9-balance/README.md`
- Create: `moorestech_master:tools/tier9_balance/sections/s1/{__init__,sim_bridge,lookup,structure_checks,recipe_checks,cleanroom_checks,item_checks}.py`
- Modify: `moorestech_master:tools/tier9_balance/sections/s1_semiconductor.py`（Task 3 の空の区間を、目標判定・所要時間・照合 8 項目に置き換える）
- Create: `moorestech_master:tools/nodegraph_sync/{sync_nodegraph,graph_io,graph_edits,spatial_unlock,placement,unlock_sync,audit}.py`
- Create: `moorestech_master:tools/master_common/json_text.py`（末尾改行を保つ JSON の読み書き。`apply/master_io.py` と `nodegraph_sync/graph_io.py` が共有する。判断記録 A32）
- Test: `moorestech_master:tools/nodegraph_sync/tests/test_nodegraph_sync.py`
- S1 handoff「Task D2 Step 2」の照合 2 本（D2 の Files にある照合用 CLI と検査関数のファイル）は作らない。その 8 項目は `sections/s1/` に `checks.matches` として書く（判断記録 A22）

**Interfaces:**
- Consumes:
  - Task 3 の枠（`checks.Resolved`・`resolved`・`field`・`element`・`failures`・`require_positive`・`matches`・`sim_target`・`lookup_failed`・`SectionResult`、`MasterView` の `items` `items_doc` `blocks` `fluids` `recipes` `research` `clean_room` `families` `master_dir` `block_param` `guid_of` `input_guid` `label_of` `recipe_input_amount`、`master_view.entry_guid`）。
  - Task 3 Step 7 の sim `$B/sim/`: `params` の定数、`timeline.RESEARCH`・`timeline.run`、`targets.evaluate`。名前は S1 handoff「Task D1」Interfaces の Produces どおり。
  - 前例 `moorestech_master:tools/plan6_nodegraph_migration/migrate_nodegraph.py`（nodeGraph の取り残しで、消えたアイテムを参照するノードが 69 件残った事故の後始末。消えた参照の機械検証と、エディタの空間割り当て〈2 パスの範囲法〉の移植を持つ）。
- Produces:
  - CLI `python3 apply/apply_spec.py --mod-dir <mod> --spec-dir spec --ledger guids.json`。出力は `applied N spec files; issued M new guids`。消したはずの GUID がマスタに残っていれば `ValueError` で止まる。
  - spec のキー: S1 handoff「Task D2」Interfaces の 15 キーに、次の 6 キーを足す。
    - `fluids`（`{name, en, color, guid?}`）
    - `removeItems`（GUID の配列）
    - `removeResearch`（GUID の配列）
    - `deleteBlockKeys`（`{"<ブロック名>": ["<キー>"]}`）
    - `buildMenuSubCategories`（`{category, name, en, guid}`）
    - `orbitalPayloads`（`payloadItemGuid` で upsert）
  - G6 の区間 `s1_semiconductor`（`sections/s1_semiconductor.evaluate(view) -> SectionResult`）:
    - `targets.evaluate()` の 12 行を `checks.sim_target(目標, 合否, 実測)` として出す。
    - S1 handoff「Task D2」の照合 8 項目を、次の関数が `checks.matches` の行として出す（引けない値は `lookup_failed` の `Resolved` を渡し、その状態の行になる。判断記録 A31）。`structure` → `structure_checks.check_structure`、`upstream ratio` → `recipe_checks.check_upstream_ratio`、`recipes = params` → `recipe_checks.check_recipes_match_params`、`power / filter = params` → `recipe_checks.check_power_and_filter`、`cleanRoom = params` → `cleanroom_checks.check_cleanroom_values`、`quality module permission` → `item_checks.check_quality_module_permission`、`modules = params` → `item_checks.check_modules`、`research = params` → `item_checks.check_research_costs`。
    - `hours` は `timeline.run('B')["done"]["モジュールスタック拡張II"]` を `Resolved` で持つ（sim で完了しなければ `lookup_failed`。判断記録 A8）。
    - 出力の行は `PASS|FAIL [s1_semiconductor] target|match <状態> <名前>: <詳細>`。S1 の照合と引き当てが全部通ったことは、`grep '^FAIL \[s1_semiconductor\] ' <出力> | grep -vc ' target out_of_range '` が 0 であることで判定する。
    - S1 の `sections/s1/lookup.py` の `S1Lookup` は、台帳のレシピを `recipe_guid(key)`・`recipe(key)` で `Resolved` として返す。名前の相互引きは `MasterView` に寄せる（判断記録 A32）。機械名 → ブロック名の読み替えは `recipe_checks.block_for_machine` の 1 か所。
  - CLI `python3 tools/nodegraph_sync/sync_nodegraph.py [--mod-dir DIR] [--check]`（判断記録 A21）:
    - 書き込みモード: 参照先の消えたノードと線を除き、research.json にある研究のノードを足し、研究どうしの `dependency` 線を `prevResearchNodeGuids` に揃え、解放ノード（item・block・train）を足す・動かす・どの研究にも割り当たらない位置へ退避する。1 行目に `pruned N nodes / M edges; research nodes +R; dependency edges +A -S; unlock nodes moved X added Y parked Z` を出す。置けなかったものは `UNPLACED …` 行。
    - 検査（`--check` は検査だけ）: 参照先の消えたノード・線が 0 件、research.json と研究ノードが 1 対 1、研究どうしの線が前提と一致、エディタの空間割り当てが research.json の解放（`unlockItemRecipeView`・`unlockBlock`・`unlockTrainCar`）と一致。違反は `FAIL …` で 1 件ずつ出し、末尾は `== NODEGRAPH: PASS|FAIL (k failures)`。1 件でもあれば exit 1 で、書き込みモードでもファイルを書かない。
    - 空間割り当てを検査に入れる理由: mooreseditor は保存のたびにノードの配置から研究の解放を計算し直して research.json に書く（前例 `migrate_nodegraph.py` の unlock 差分プリフライト）。配置と research.json がずれていると、次にエディタで保存した人が黙って解放を書き換えてしまう。起点 f9370e7 では 131 件の解放すべてで配置と research.json が一致していた（本 plan 作成時に実測）。

S1 handoff「Task D2」の Step 1 の 5 ファイルを使い、Step 2 の照合 2 ファイルは置かない。差分は次のとおり。

- [ ] **Step 1: S1 handoff「Task D2 Step 1」の 5 ファイルを `$B/apply/` に置き、次の差分を当てる**

(a) `master_io.py`: 対象に `buildMenu` を足し、元のファイルの末尾改行の有無を保つ。
- 理由（実測）: 起点 f9370e7 では `cleanRoom.json` だけが末尾改行あり、他は改行なし。下書きの「改行なしで書く」では、空の spec を流しても cleanRoom が 1 バイト変わり、D2 Step 4 が通らない。
- 確認方法: 7 ファイルと localization.csv を `json.dumps(indent=2, ensure_ascii=False)`（csv は `lineterminator="\n"`）で往復させると、改行を保った場合はすべて一致した。
- 末尾改行を保つ JSON の読み書きは、Step 8 の nodeGraph 同期（`graph_io.load_graph`・`save_graph`）も同じことをするので、共通部品 `tools/master_common/json_text.py` に 1 本だけ置き、両方から使う（判断記録 A32。パッケージ `tools/master_common/` は Task 2 が作る）。

`tools/master_common/json_text.py`:
```python
#!/usr/bin/env python3
# マスタ・nodeGraph の JSON を、元ファイルの末尾改行の有無を保って読み書きする（apply と nodegraph_sync の共通部品。判断記録 A32）
# Read/write master and nodeGraph JSON keeping the original trailing newline (shared by apply and nodegraph_sync; decision A32)
# 書式はインデント2・非ASCIIそのまま。起点 f9370e7 では cleanRoom.json だけ末尾改行ありで、他は改行なし
# Format is indent 2 with raw non-ASCII; at base f9370e7 only cleanRoom.json ends with a newline, the rest do not
import json
from pathlib import Path


def has_trailing_newline(path):
    return Path(path).read_text(encoding='utf-8').endswith('\n')


def read_json_keeping_newline(path):
    text = Path(path).read_text(encoding='utf-8')
    return json.loads(text), text.endswith('\n')


def write_json_keeping_newline(path, doc, trailing_newline):
    text = json.dumps(doc, indent=2, ensure_ascii=False) + ('\n' if trailing_newline else '')
    Path(path).write_text(text, encoding='utf-8')
```

`master_io.py` の差分（`MASTER_FILES` と `save_master` を置き換え、ファイル先頭の import の後に `sys` の import と検索パスの 2 行を足す）:
```python
import sys

# 末尾改行を保つ読み書きは tools/master_common/json_text.py の 1 本を使う（nodegraph_sync と共有）
# Reuse the single newline-keeping reader/writer in tools/master_common/json_text.py (shared with nodegraph_sync)
sys.path.insert(0, str(Path(__file__).resolve().parents[3] / "tools"))
from master_common.json_text import has_trailing_newline, write_json_keeping_newline  # noqa: E402

MASTER_FILES = ["items", "fluids", "blocks", "machineRecipes", "research", "cleanRoom", "buildMenu"]


def save_master(mod_dir, master):
    # インデント2・非ASCIIそのまま・末尾改行は元ファイルに合わせる（sync が改行付きで書く research.json も往復一致する）
    # Indent 2, raw non-ASCII, trailing newline as in the original (round-trips research.json that sync writes with a newline)
    for name in MASTER_FILES:
        path = Path(mod_dir) / "master" / f"{name}.json"
        write_json_keeping_newline(path, master[name], has_trailing_newline(path))
```

(b) `apply/apply_extras.py`（新規）: 統合で足す 6 キーを処理する。`apply_spec.py` は下書きで約 125 行あり、ここに足すと 200 行を超えるので別ファイルにする。
```python
"""統合で足した spec キー（液体・削除・ビルドメニュー・積荷表）を適用し、消した GUID の残りを検査する。
Apply spec keys added for the integration (fluids, removals, build menu, orbital payloads) and check removed GUIDs are gone."""
import json

import element_builders as eb


def declare_extra(spec, r):
    for fluid in spec.get("fluids", []):
        r.declare("fluid", fluid["name"], fluid.get("guid"))


def apply_extra(spec, master, r, loc, removed):
    # 液体を足す（ローカライズ行も Guid 導出キーで書く）
    # Add fluids (and their localization rows keyed by GUID)
    for fluid in spec.get("fluids", []):
        guid = r.resolve(f"@fluid:{fluid['name']}")
        eb.upsert(master["fluids"]["data"], "fluidGuid", {"name": fluid["name"], "fluidGuid": guid, "color": fluid["color"]}, [])
        loc.put(f"fluid.{guid}.name", fluid["name"], fluid["en"])
    # アイテム・研究を消す（参照の残りは全 spec 適用後に assert_removed_absent が見る）
    # Remove items and research (leftover references are checked by assert_removed_absent after all specs)
    for guid in spec.get("removeItems", []):
        master["items"]["data"] = [e for e in master["items"]["data"] if e["itemGuid"] != guid]
        loc.remove(f"item.{guid}.name")
        removed.append(guid)
    for guid in spec.get("removeResearch", []):
        master["research"]["data"] = [n for n in master["research"]["data"] if n["researchNodeGuid"] != guid]
        loc.remove(f"research.{guid}.name")
        loc.remove(f"research.{guid}.description")
        removed.append(guid)
    for block_name, keys in spec.get("deleteBlockKeys", {}).items():
        target = next(b for b in master["blocks"]["data"] if b["name"] == block_name)
        for key in keys:
            target.pop(key)
    # ビルドメニューの小分類と積荷表の行を upsert する
    # Upsert build-menu subcategories and orbital payload rows
    for entry in spec.get("buildMenuSubCategories", []):
        category = next(c for c in master["buildMenu"]["categories"] if c["name"] == entry["category"])
        eb.upsert(category["subCategories"], "subCategoryGuid", {"name": entry["name"], "entrySource": "blocks", "subCategoryGuid": entry["guid"]}, [])
        loc.put(f"buildMenuSubCategory.{entry['guid']}.name", entry["name"], entry["en"])
    for row in spec.get("orbitalPayloads", []):
        eb.upsert(master["items"]["orbitalPayloads"], "payloadItemGuid", r.resolve(row), [])


def assert_removed_absent(master, removed):
    # 消した GUID がどのマスタにも残っていないこと（前提ノード・解放・ブロックの旧キー）。残れば止める
    # Removed GUIDs must not remain in any master (prerequisites, unlocks, legacy block keys); stop if they do
    text = json.dumps(master, ensure_ascii=False)
    leftovers = [guid for guid in removed if guid in text]
    if leftovers:
        raise ValueError(f"removed guids still referenced: {leftovers}")
```
`apply_spec.py` 側の差分: `import apply_extras as ax` を足す。`main()` で次の 3 点を変える。
- 宣言ループに `ax.declare_extra(spec, resolver)` を足す。
- 適用ループの前に `removed = []` を置き、`apply_one` の直後に `ax.apply_extra(spec, master, resolver, loc, removed)` を呼ぶ。
- `save_master` の前に `ax.assert_removed_absent(master, removed)` を呼ぶ。
```python
    removed = []
    for spec in specs:
        declare_all(spec, resolver)
        ax.declare_extra(spec, resolver)
    for spec in specs:
        apply_one(spec, master, resolver, loc)
        ax.apply_extra(spec, master, resolver, loc, removed)
    ax.assert_removed_absent(master, removed)
```

- [ ] **Step 2: G6 の S1 区間の照合を置く**（S1 handoff「Task D2 Step 2」の 8 項目を、`checks.matches` の行に書き直したもの。判断記録 A22）
  - 書き直しの規則: 元の「失敗理由の文字列リスト」は、1 値ごとの `checks.matches(名前, sim の値, JSON の値, 許容差)` にする。「あってはならないもの」（超純水の残り・仮コスト・chipDraws など）は `none_of` が違反 1 件ごとに `matches(…, 0, 1, 0)` を出し、違反が無ければ `matches(…, 0, 0, 0)` を 1 行出す。台帳かマスタに無い名前は例外にせず、引けない `Resolved`（`lookup_failed`）として `matches` に渡すか、値を作れない検査を `checks.lookup_failed('match', …)` の行にする（Task 12 の時点では工程レシピが台帳に無いので、その行は `match lookup_failed` の FAIL になる）。
  - 下書きからの訂正 3 点をここに入れる。① ICチップLv1 を消費する研究は S1 の 7 ノードに限って数える（S2・S3 を積むとロケット技術・金属超精密加工・核融合炉・軌道エレベーターの建設・反物質爆弾も消費するため。下書きのままだと最後に `12 nodes` で落ちる）。② ダイは名前の前方一致ではなく levelFamily「シリコンダイ」で判別する（S1 handoff「設計検査の持ち越し」5 項目目）。③ レシピの入力も名前の前方一致で当てない。params の入力名を `MasterView.input_guid` で GUID に直し（アイテム名・levelFamily 名〈基準アイテム〉・液体名のどれか 1 つに当たること）、その GUID の入力行が 1 つに決まるかを `recipe_input_amount` で引く（判断記録 A32）。
  - 1 ディレクトリ 10 ファイルまで・1 ファイル 200 行以下に収めるため、`sections/` の下に `s1/` を切る。判断記録 A22 の「`sections/s1_semiconductor.py` の `checks.matches`」は、区間 `s1_semiconductor` の `evaluate` が返す行として満たす（`s1_semiconductor.py` が `s1/` の 8 関数を呼んで行を集める）（`sections/` は `.py` 4 本と `s1/`、`s1/` は 7 本。最大は `structure_checks.py` の 79 行）。

`tools/tier9_balance/sections/s1/__init__.py`:
```python
# S1 半導体区間の照合（sim/params.py と出荷マスタの値の突き合わせ 8 項目）。先に sim を検索パスへ足す
# S1 semiconductor matching (eight sim/params.py-versus-shipped-master checks); put the sim on the search path first
from sections.s1 import sim_bridge  # noqa: F401
```

`tools/tier9_balance/sections/s1/sim_bridge.py`:
```python
#!/usr/bin/env python3
# design/tier9-balance/sim を import できるようにし、GUID 台帳を読む（sim と台帳の置き場はマスタ repo の design 側）
# Make design/tier9-balance/sim importable and read the GUID ledger (both live under the master repo's design tree)
import json
import sys
from pathlib import Path

DESIGN_DIR = Path(__file__).resolve().parents[4] / 'design' / 'tier9-balance'
SIM_DIR = DESIGN_DIR / 'sim'
LEDGER_PATH = DESIGN_DIR / 'guids.json'

# sim の各ファイルは互いをトップレベル名で import するので、ディレクトリごと検索パスへ足す
# The sim files import each other by top-level name, so the whole directory goes on the search path
if str(SIM_DIR) not in sys.path:
    sys.path.insert(0, str(SIM_DIR))


def load_ledger():
    # 台帳が無いときは空を返す。台帳に無いレシピキーは S1Lookup が lookup_failed の Resolved にし、その行が FAIL になる
    # Return an empty ledger when absent; S1Lookup turns a missing recipe key into a lookup_failed Resolved and that row fails
    if not LEDGER_PATH.exists():
        return {}
    return json.loads(LEDGER_PATH.read_text(encoding='utf-8'))
```

`tools/tier9_balance/sections/s1/lookup.py`:
```python
#!/usr/bin/env python3
# S1 の照合が使う台帳のレシピの引き当てとローカライズ行。引けたかどうかは Resolved の状態で返す（判断記録 A31）
# Ledger recipe lookups and localization rows for the S1 checks; whether a lookup worked is a Resolved state (decision A31)
import csv
import os

import checks


class S1Lookup:
    def __init__(self, view, ledger):
        self.view = view
        self.items_doc = view.items_doc
        self._recipes = {r['machineRecipeGuid']: r for r in view.recipes}
        self.recipe_guids = {k.split(':', 1)[1]: v for k, v in ledger.items() if k.startswith('recipe:')}
        self.ledger = ledger

    def recipe_guid(self, key):
        return checks.field(self.recipe_guids, key, '台帳 guids.json の recipe キー')

    def recipe(self, key):
        # 台帳の recipe:<key> から GUID を引き、その GUID のレシピを返す
        # Resolve recipe:<key> in the ledger to a GUID and return that recipe
        return self.recipe_guid(key).then(lambda guid: checks.field(self._recipes, guid, f'machineRecipes.json（台帳の recipe:{key}）'))

    def localization_keys(self):
        path = os.path.join(self.view.master_dir, '..', 'localization', 'localization.csv')
        if not os.path.exists(path):
            return set()
        with open(path, encoding='utf-8', newline='') as source:
            return {row[0] for row in csv.reader(source) if row}


def missing_recipe(key, recipe):
    return checks.unreadable('match', f'レシピ {key}', recipe)


def none_of(name, offenders):
    # 「あってはならないもの」の検査。違反1件ごとに1行、違反が無ければ0件の1行を出す
    # A "must be absent" check: one row per offender, or a single zero row when there are none
    if not offenders:
        return [checks.matches(name, 0, checks.resolved(0), 0)]
    return [checks.matches(f'{name}: {offender}', 0, checks.resolved(1), 0) for offender in offenders]
```

`tools/tier9_balance/sections/s1/structure_checks.py`:
```python
#!/usr/bin/env python3
# S1 照合「structure」: 消すべき値が残っていないこと・族とスタックと研究消費の形・ローカライズ行（S1 handoff D2 の check_structure）
# S1 "structure": removed values are gone, family/stack/research-use shape, localization rows (S1 handoff D2 check_structure)
import glob
import os

import checks
from params import DIES_PER_WAFER
from sections.s1.lookup import none_of
from timeline import RESEARCH

ULTRA_PURE_WATER = '019eeaa7-45b6-7733-bd3b-a043be2ccc21'
OLD_TEMP_COST = {('60daab46-bce2-4d09-b895-d872bef3f9c3', 5), ('54ccd10a-a6c1-4ee5-99d5-b61fea7e92a9', 2)}
IC_CHIP_LV1 = 'f6dd7835-0845-4877-87d0-b719d037ae7d'
RENAMED_ITEMS = ['019e3c14-b270-716f-84e5-c0b06a97592b', '019e3c25-78f1-70ec-be1b-2c2cdd9297bb',
                 '019e3b02-b9fd-73c7-9538-ef13b0f755f1', '019e3c26-9808-765d-ace7-ea1845a7e072']
S1_FAMILY_COUNT = 11
DIE_FAMILY, FAILED_DIE = 'シリコンダイ', '失敗したシリコンダイ'


def check_structure(lk):
    view, items_doc = lk.view, lk.items_doc
    rows = []
    # 超純水と仮コスト（砕いた石材5＋レンガ2）が残っていない
    # Ultra-pure water and the placeholder cost (crushed stone 5 + brick 2) are gone
    files = [os.path.basename(p) for p in sorted(glob.glob(os.path.join(view.master_dir, '*.json')))
             if ULTRA_PURE_WATER in open(p, encoding='utf-8').read()]
    rows += none_of('超純水の GUID が残るファイル', files)
    rows += none_of('仮コストのままのブロック', [name for name, b in view.blocks.items()
                                         if {(c['itemGuid'], c['count']) for c in b.get('requiredItems', [])} == OLD_TEMP_COST])
    # levelFamilies は S1 の 11 族以上あり、どれも 5 段で基準が先頭
    # levelFamilies hold at least the 11 S1 families, each of 5 levels with the base first
    families = items_doc.get('levelFamilies', [])
    shortage = checks.resolved(max(0, S1_FAMILY_COUNT - len(families)))
    rows.append(checks.matches('levelFamilies の不足数（S1 の 11 族に対して）', 0, shortage, 0))
    rows += none_of('5 段で基準が先頭になっていない levelFamily', [f['name'] for f in families
                    if len(f['levelItemGuids']) != 5 or f['levelItemGuids'][0] != f['baseItemGuid']])
    rows += none_of('cleanRoom に残る旧チップ抽選 chipDraws', ['chipDraws'] if 'chipDraws' in view.clean_room else [])
    rows += _die_stacks(lk)
    rows += _chip_research_use(view)
    return rows + _localization(lk)


def _die_stacks(lk):
    # ダイと失敗ダイのスタック上限が 1 ウェハ分×2 以上。ダイは名前の前方一致ではなく levelFamily で判別する
    # Dies and failed dies stack to at least two wafers; dies are identified by levelFamily, not by name prefix
    view = lk.view
    if DIE_FAMILY not in view.families or FAILED_DIE not in view.items:
        return [checks.lookup_failed('match', 'ダイのスタック上限', f'levelFamily「{DIE_FAMILY}」かアイテム「{FAILED_DIE}」が無い')]
    tables = {t['tableGuid']: min(t['stackCounts']) for t in lk.items_doc.get('itemStackLevelTables', [])}
    targets = set(view.families[DIE_FAMILY]['levelItemGuids']) | {view.items[FAILED_DIE]['itemGuid']}
    # silent-ok: 表の無いアイテムは上限 0 として「足りない」側に数える / 足りない行は none_of の FAIL として G6 の出力に出る
    short = [i['name'] for i in lk.items_doc['data'] if i['itemGuid'] in targets
             and tables.get(i.get('stackLevelTableGuid'), 0) < DIES_PER_WAFER * 2]
    return none_of(f'スタック上限が {DIES_PER_WAFER * 2} 未満のダイ', short)


def _chip_research_use(view):
    # ICチップLv1 を消費する研究は S1 の 7 ノードに限って数え、数と合計が timeline.RESEARCH と同じ
    # Count IC chip Lv1 research use over the seven S1 nodes only; count and total equal timeline.RESEARCH
    s1_names = {node['name'] for node in RESEARCH}
    uses = [c['itemCount'] for name, n in view.research.items() if name in s1_names
            for c in n['consumeItems'] if c['itemGuid'] == IC_CHIP_LV1]
    return [checks.matches('ICチップLv1 を消費する S1 研究の数', len(RESEARCH), checks.resolved(len(uses)), 0),
            checks.matches('S1 研究の ICチップLv1 合計', sum(n['chips'] for n in RESEARCH), checks.resolved(sum(uses)), 0)]


def _localization(lk):
    # Part D が足した・改名した要素に GUID 由来のキーの行がある（台帳の item・block・research と改名 4 件）
    # Elements Part D added or renamed have GUID-keyed rows (ledger items, blocks, research and the four renames)
    keys = lk.localization_keys()
    wanted = [f'item.{g}.name' for g in RENAMED_ITEMS]
    for key, guid in lk.ledger.items():
        kind = key.split(':', 1)[0]
        if kind in ('item', 'block'):
            wanted.append(f'{kind}.{guid}.name')
        if kind == 'research':
            wanted += [f'research.{guid}.name', f'research.{guid}.description']
    return none_of('ローカライズ行の欠け', [k for k in wanted if k not in keys])
```

`tools/tier9_balance/sections/s1/recipe_checks.py`:
```python
#!/usr/bin/env python3
# S1 照合「upstream ratio」「recipes = params」「power / filter = params」（S1 handoff D2 Step 2 の照合関数のうち 3 つ）
# S1 "upstream ratio", "recipes = params", "power / filter = params" (three of the S1 handoff D2 Step 2 check functions)
import checks
from master_view import entry_guid
from params import FILTER_CAPACITY, FILTER_Q, LINE_SI_PER_S, POWER, RECIPES
from sections.s1.lookup import missing_recipe

# sim のレシピ名 → 台帳のレシピキー / sim の機械名 → ブロック名（名前が同じ機械は載せない）
# Sim recipe name -> ledger recipe key / sim machine name -> block name (machines with the same name are not listed)
PARAM_TO_RECIPE = {'シリコンウェハ': ['ウェハ切断'], 'トランジスタ設置済みウェハ': ['成膜'], 'EUV層': ['EUV1層', 'EUV2層', 'EUV3層'],
                   'ダイ': ['ダイシング'], 'ICチップ': ['パッケージング']}
MACHINE_TO_BLOCK = {'EUV露光装置': 'EUV露光式半導体製造装置', '空気清浄機': 'クリーンルーム空気清浄機'}
IPA = 'イソプロピルアルコール'
# 上流の台数比の期待値（炉だけが2倍で、粉砕と化学が同数）
# Expected upstream machine ratios (only the furnace doubles; crusher and chemical plant are equal)
RATIO_TARGETS = (('上流の台数比 電気炉/電気粉砕機', 2.0), ('上流の台数比 化学プラント/電気粉砕機', 1.0))
EPS = 1e-9


def block_for_machine(machine):
    return MACHINE_TO_BLOCK.get(machine, machine)


def check_upstream_ratio(lk):
    # 基準ライン1本の 粉砕:炉:化学 の台数を実マスタのレシピ時間から出し、炉だけが2倍（粉砕と化学が同数）かを見る
    # Count crusher:furnace:chemical machines for one line from the real recipes; only the furnace is doubled
    found = {k: lk.recipe(k) for k in ('高純度シリコン', '金属シリコン', '砂')}
    if checks.failures(found.values()):
        return [missing_recipe(k, r) for k, r in found.items() if not r.ok]
    metal, furnace, sand = (found[k].value for k in ('高純度シリコン', '金属シリコン', '砂'))
    sand_in = checks.require_positive(lk.view.recipe_input_amount(furnace, '砂'), '電気炉のレシピの入力「砂」')
    if not sand_in.ok:
        return [checks.matches(name, expected, sand_in, 1e-6) for name, expected in RATIO_TARGETS]
    chem = LINE_SI_PER_S * metal['time'] / metal['outputItems'][0]['count']
    metal_per_s = LINE_SI_PER_S * metal['inputItems'][0]['count']
    furn = metal_per_s * furnace['time'] / furnace['outputItems'][0]['count']
    sand_per_s = metal_per_s * sand_in.value / furnace['outputItems'][0]['count']
    crush = sand_per_s * sand['time'] / sand['outputItems'][0]['count']
    measured = {RATIO_TARGETS[0][0]: furn / crush, RATIO_TARGETS[1][0]: chem / crush}
    return [checks.matches(name, expected, checks.resolved(measured[name]), 1e-6) for name, expected in RATIO_TARGETS]


def check_recipes_match_params(lk):
    rows = []
    for name, spec in RECIPES.items():
        for key in PARAM_TO_RECIPE.get(name, [name]):
            recipe = lk.recipe(key)
            rows += _recipe_rows(lk, key, spec, recipe.value) if recipe.ok else [missing_recipe(key, recipe)]
    return rows


def _recipe_rows(lk, key, spec, recipe):
    # 機械・時間・入力・IPA・出力数を params の1行と突き合わせる。入力は名前の前方一致でなく GUID で引く（判断記録 A32）
    # Match machine, time, inputs, IPA and output count with one params row; inputs are found by GUID, not name prefix (decision A32)
    view, block = lk.view, block_for_machine(spec['machine'])
    machine = checks.field(view.blocks, block, 'blocks.json').map(lambda b: int(recipe['blockGuid'] == b['blockGuid']))
    rows = [checks.matches(f'レシピ {key} の機械が {block}', 1, machine, 0),
            checks.matches(f'レシピ {key} の時間', spec['time'], checks.resolved(recipe['time']), EPS)]
    for item, count in spec.get('in', {}).items():
        rows.append(checks.matches(f'レシピ {key} の入力 {item}', count, view.recipe_input_amount(recipe, item), 0))
    if 'ipa' in spec:
        ipa = view.guid_of(IPA).map(lambda g: sum(f['amount'] for f in recipe['inputFluids'] if entry_guid(f) == g))
        rows.append(checks.matches(f'レシピ {key} の IPA', spec['ipa'], ipa, EPS))
    out = recipe['outputItems'][0]['count'] if recipe['outputItems'] else recipe['outputFluids'][0]['amount']
    rows.append(checks.matches(f'レシピ {key} の出力数', spec['out'], checks.resolved(out), EPS))
    return rows


def check_power_and_filter(lk):
    view, cleaner = lk.view, block_for_machine('空気清浄機')
    rows = []
    for machine, power in POWER.items():
        block = block_for_machine(machine)
        rows.append(checks.matches(f'{block} の消費電力', power, view.block_param(block, 'requiredPower'), EPS))
    rows.append(checks.matches('空気清浄機の毎秒除去量', FILTER_Q, view.block_param(cleaner, 'removalVolumePerSecond'), EPS))
    rows.append(checks.matches('空気清浄機のフィルター容量', FILTER_CAPACITY, view.block_param(cleaner, 'filterCapacity'), EPS))
    return rows
```

`tools/tier9_balance/sections/s1/cleanroom_checks.py`:
```python
#!/usr/bin/env python3
# S1 照合「cleanRoom = params」: 汚染係数・クラス行・役割表（成膜1・EUV3・ダイシング1）と上限・ダイ表（S1 handoff D2）
# S1 "cleanRoom = params": pollution, class rows, role rows (1 deposition, 3 EUV, 1 dicing), caps and die tables (S1 handoff D2)
import checks
from params import CLASS_PARAMS, DIE_TABLES, DIES_PER_WAFER, POLLUTION, PROMOTION_CAP, QUALITY_REDUCTION_CAP
from sections.s1.lookup import missing_recipe

CLASS_VALUE_KEYS = ('qualityMultiplier', 'layerFailRate', 'dieLossRate')
EUV_LAYERS = (('EUV1層', '1層パターンウェハG1'), ('EUV2層', '2層パターンウェハG1'), ('EUV3層', '完成パターンウェハG1'))
EPS = 1e-9


def check_cleanroom_values(lk):
    room = lk.view.clean_room
    pollution = room.get('pollution', {})
    rows = [checks.matches(f'汚染係数 {k}', v, checks.field(pollution, k, 'cleanRoom.json の pollution'), EPS)
            for k, v in POLLUTION.items()]
    by_class = {row['className']: row for row in room.get('thresholds', [])}
    for class_name, expected in CLASS_PARAMS.items():
        row = checks.field(by_class, class_name, 'cleanRoom.json の thresholds')
        for key in CLASS_VALUE_KEYS:
            value = row.then(lambda r: checks.field(r, key, f'クラス{class_name} の行'))
            rows.append(checks.matches(f'クラス{class_name} の {key}', expected[key], value, EPS))
    return rows + _role_rows(lk, room)


def _role_rows(lk, room):
    # 役割表が工程レシピと G1/Lv1 品を指し、上限・ダイ数・ダイ表が params と同じ
    # Role rows point at the stage recipes and G1/Lv1 items, with caps, dies per wafer and die tables from params
    keys = ['成膜', 'ダイシング'] + [k for k, _ in EUV_LAYERS]
    found = {k: lk.recipe_guid(k) for k in keys}
    if checks.failures(found.values()):
        return [missing_recipe(k, g) for k, g in found.items() if not g.ok]
    guids, label = {k: g.value for k, g in found.items()}, lk.view.label_of
    draws, exposures, dicings = room.get('waferGradeDraws', []), room.get('layerExposures', []), room.get('dicings', [])
    draw_rows = [(d['machineRecipeGuid'], label(d['gradedWaferItemGuid'])) for d in draws]
    euv_rows = {e['machineRecipeGuid']: (label(e['layeredWaferItemGuid']), label(e['failedWaferItemGuid'])) for e in exposures}
    euv_expected = {guids[k]: (name, '失敗したパターンウェハ') for k, name in EUV_LAYERS}
    dicing_rows = [(d['machineRecipeGuid'], label(d['dieItemGuid']), label(d['failedDieItemGuid'])) for d in dicings]
    shape = (('waferGradeDraws が成膜の 1 行', draw_rows == [(guids['成膜'], 'トランジスタ設置済みウェハG1')]),
             ('layerExposures が EUV の 3 行', len(exposures) == 3 and euv_rows == euv_expected),
             ('dicings がダイシングの 1 行', dicing_rows == [(guids['ダイシング'], 'シリコンダイLv1', '失敗したシリコンダイ')]))
    rows = [checks.matches(name, 1, checks.resolved(int(same)), 0) for name, same in shape]
    rows += [checks.matches('成膜の昇格上限', PROMOTION_CAP, checks.resolved(d['maxPromotionChance']), EPS) for d in draws]
    rows += [checks.matches('EUV の失敗低減上限', QUALITY_REDUCTION_CAP, checks.resolved(e['maxFailReduction']), EPS) for e in exposures]
    for d in dicings:
        rows.append(checks.matches('ダイシングの損失低減上限', QUALITY_REDUCTION_CAP, checks.resolved(d['maxLossReduction']), EPS))
        rows.append(checks.matches('1 ウェハのダイ数', DIES_PER_WAFER, checks.resolved(d['diesPerWafer']), 0))
        rows += _die_table_rows(d)
    return rows


def _die_table_rows(dicing):
    # ウェハの等級 g ごとのダイ表（Lv1..Lvg の確率）を1値ずつ照合する。段数が足りない側は引けない（lookup_failed）として落とす
    # Match each grade's die table (Lv1..Lvg rates) value by value; a missing level fails as unreadable (lookup_failed)
    tables = [t['levelRates'] for t in dicing.get('gradeDieTables', [])]
    rows = [checks.matches('ダイ表の等級数', len(DIE_TABLES), checks.resolved(len(tables)), 0)]
    for grade in sorted(DIE_TABLES):
        actual = tables[grade - 1] if grade - 1 < len(tables) else []
        for level, rate in enumerate(DIE_TABLES[grade], start=1):
            value = checks.element(actual, level - 1, f'ダイ表 G{grade}')
            rows.append(checks.matches(f'ダイ表 G{grade} Lv{level}', rate, value, EPS))
        rows.append(checks.matches(f'ダイ表 G{grade} の段数', len(DIE_TABLES[grade]), checks.resolved(len(actual)), 0))
    return rows
```

`tools/tier9_balance/sections/s1/item_checks.py`:
```python
#!/usr/bin/env python3
# S1 照合「quality module permission」「modules = params」「research = params」（S1 handoff D2 Step 2 の照合関数のうち 3 つ）
# S1 "quality module permission", "modules = params", "research = params" (three of the S1 handoff D2 Step 2 check functions)
import checks
from params import MODULES, STACK_LIMITS
from timeline import RESEARCH

# 専用機ごとの品質モジュール可否（Part C の blockParam.allowQualityModule）
# Quality module permission per dedicated machine (Part C's blockParam.allowQualityModule)
QUALITY_ALLOWED = {'成膜装置': True, 'EUV露光式半導体製造装置': True, 'ダイシング装置': True,
                   'ウェハ切断機': False, 'パッケージング装置': False}
COST_ITEMS = (('ICチップLv1', 'chips'), ('鋼鉄フレーム', 'frames'), ('コンピュータ', 'computers'), ('制御モータ', 'motors'))
MODULE_COUNT = 20
EPS = 1e-9


def check_quality_module_permission(lk):
    return [checks.matches(f'{name} の allowQualityModule', int(allowed), lk.view.block_param(name, 'allowQualityModule').map(int), 0)
            for name, allowed in QUALITY_ALLOWED.items()]


def check_modules(lk):
    modules = lk.items_doc.get('modules', [])
    rows = [checks.matches('モジュールの数', MODULE_COUNT, checks.resolved(len(modules)), 0)]
    for m in modules:
        axis = MODULES.get(m['effectAxis'], [])
        if not 1 <= m['tier'] <= len(axis):
            rows.append(checks.lookup_failed('match', f'{m["name"]} の効果', f'params.MODULES に {m["effectAxis"]} の tier {m["tier"]} が無い'))
            continue
        effect, tradeoff = axis[m['tier'] - 1]
        rows.append(checks.matches(f'{m["name"]} の effectValue', effect, checks.resolved(m['effectValue']), EPS))
        rows.append(checks.matches(f'{m["name"]} の tradeoffValue', tradeoff, checks.resolved(m['tradeoffValue']), EPS))
    levels = [level['stackLimit'] for level in lk.items_doc.get('moduleSlotStackLimitLevels', [])]
    rows.append(checks.matches('モジュールスタック上限の段数', len(STACK_LIMITS), checks.resolved(len(levels)), 0))
    for i, limit in enumerate(STACK_LIMITS):
        rows.append(checks.matches(f'モジュールスタック上限 段{i + 1}', limit, checks.element(levels, i, 'moduleSlotStackLimitLevels'), 0))
    return rows


def check_research_costs(lk):
    rows = []
    for node in RESEARCH:
        if node['name'] not in lk.view.research:
            rows.append(checks.lookup_failed('match', f'研究 {node["name"]}', 'research.json に無い'))
            continue
        # コストは 4 品だけで、各数が timeline.RESEARCH と同じ（0 の品はコストに載らないので 0 と比べる）
        # The cost has only the four items, each equal to timeline.RESEARCH (zero items are absent, so compare with 0)
        cost = {lk.view.label_of(c['itemGuid']): c['itemCount'] for c in lk.view.research[node['name']]['consumeItems']}
        for item, count_key in COST_ITEMS:
            rows.append(checks.matches(f'研究 {node["name"]} の {item}', node[count_key], checks.resolved(cost.get(item, 0)), 0))
        extra = sorted(name for name in cost if name not in {item for item, _ in COST_ITEMS})
        rows.append(checks.matches(f'研究 {node["name"]} の余計なコスト {extra}', 0, checks.resolved(len(extra)), 0))
    return rows
```

`tools/tier9_balance/sections/s1_semiconductor.py`（Task 3 の空の区間を全文置き換える）:
```python
#!/usr/bin/env python3
# S1 半導体（半導体製造の解放→S1 最後の研究）。sim の目標判定・所要時間と、params と出荷マスタの照合 8 項目を返す
# S1 semiconductor (unlock to the last S1 research): the sim's target judgements and hours plus eight params-versus-master matches
# 所要時間はクラス B の「モジュールスタック拡張II」完了時刻（判断記録 A8）
# The hours are the class-B finish time of "モジュールスタック拡張II" (decision A8)
import checks
from sections.s1 import cleanroom_checks, item_checks, recipe_checks, sim_bridge, structure_checks
from sections.s1.lookup import S1Lookup
import targets
import timeline

LAST_S1_RESEARCH = 'モジュールスタック拡張II'
HOURS_CLASS = 'B'


def evaluate(view):
    lk = S1Lookup(view, sim_bridge.load_ledger())
    rows = [checks.sim_target(name, passed, measured) for name, passed, measured in targets.evaluate()]
    for check in (structure_checks.check_structure, recipe_checks.check_upstream_ratio, recipe_checks.check_recipes_match_params,
                  recipe_checks.check_power_and_filter, cleanroom_checks.check_cleanroom_values,
                  item_checks.check_quality_module_permission, item_checks.check_modules, item_checks.check_research_costs):
        rows += check(lk)
    hours = checks.field(timeline.run(HOURS_CLASS)['done'], LAST_S1_RESEARCH, f'sim のクラス{HOURS_CLASS}の完了時刻')
    return checks.SectionResult('s1_semiconductor', rows, hours)
```

- [ ] **Step 3: 台帳を作る。** S1 handoff「Task D2 Step 3」の `guids.json` 19 行をそのまま書く。S2・S3 の固定 GUID は spec 側の `guid` で入る（`set_fixed` が記録する）ので、ここには書かない。工程レシピ（`recipe:砂` など）は Task 13〜15 の初回適用で台帳に発行される。

- [ ] **Step 4: README を置く**（D1 の README は sim の使い方と照合 CLI を書いていたが、A22 で照合の入口が変わったので書き直す）

`design/tier9-balance/README.md`:
````markdown
# tier9-balance

Tier9（S1 半導体・S2 計算データ・S3 終盤）のマスタ値の正本と、それを v8 mod のマスタへ入れる道具。

- `sim/` S1 の値の正本（`params.py`・`timeline.py` ほか 7 本）。値を変えるときは sim を直して目標に収まることを確かめてから spec を直す
- `spec/NN_*.json` マスタへ入れる要素の宣言（名前で書き、GUID は台帳から引く）
- `apply/apply_spec.py` spec を適用する（何度流しても同じ結果。新しい GUID は初回に UUIDv7 を発行して `guids.json` へ書く）
- `guids.json` GUID 台帳（コミットする）
- `DECISIONS.md` 値を決めた記録

照合と判定はここに置かない。sim と出荷マスタの照合・目標時間の判定は `python3 tools/tier9_balance/run.py`（G6）、到達性は `python3 tools/progression/check.py`（G2）、mooreseditor のグラフは `python3 tools/nodegraph_sync/sync_nodegraph.py --check` で見る。

```bash
python3 design/tier9-balance/apply/apply_spec.py --mod-dir server_v8/mods/moorestechAlphaMod_8 --spec-dir design/tier9-balance/spec --ledger design/tier9-balance/guids.json
python3 tools/tier9_balance/run.py
```
````

- [ ] **Step 5: 空の spec で往復一致を確かめる**
```bash
cd $B && mkdir -p /tmp/p2-empty-spec && python3 apply/apply_spec.py --mod-dir $MOD --spec-dir /tmp/p2-empty-spec --ledger guids.json && git -C $MW status --short server_v8
```
Expected: `applied 0 spec files; issued 0 new guids` の後、`git status` の出力が空。

- [ ] **Step 6: G6 を回し、S1 区間の行が出ることと、中身がまだ無いため赤いことを控える**
```bash
cd $MW && python3 tools/tier9_balance/run.py > $GL/p2-2-g6-base.txt; echo "g6=$?"
tail -1 $GL/p2-2-g6-base.txt
awk '{print $1, $2, $3, $4}' $GL/p2-2-g6-base.txt | sort | uniq -c
python3 -m unittest discover -s tools/tier9_balance/tests 2>&1 | tail -1
```
Expected（本 plan 作成時に、起点 f9370e7 の tree と 19 行の台帳で同じコードを scratch で実測）:
- `g6=1`。末尾は `== RESULT: FAIL (83 failures / 122 checks)`。
- `[s1_semiconductor]` の内訳は、`target ok` 11・`target out_of_range` 1（`S1 6〜8時間（クラスA）` の 5.83 h）、`match ok` 27・`match mismatch` 38・`match lookup_failed` 39。`lookup_failed` は工程レシピが台帳に無い・専用機と研究がまだ無いもの（Task 13〜17 で消える）。照合の FAIL 77 は、A31 の前の書き方の `match` 46 と `lookup` 31 の和と同じで、行ごとの合否も変わらない（本 plan の書き換え時に同じ起点で突き合わせた）。
- `[overall]` は `PASS [overall] target ok s1_semiconductor の所要時間[h]: 6.266666666666667（目標 6.0〜8.0）`。S2・S3・全体の 3 行が `target empty` の FAIL。`[s2_compute_loop] section empty` と `[s3_endgame] section empty` が FAIL。
- 例外で止まらない（引けない名前はすべて `lookup_failed` の FAIL になる）。
- 枠の自己テストは `OK`（S1 区間が sim を import しても 8 本とも通る）。

- [ ] **Step 7: 値を 1 か所わざと食い違わせ、S1 の照合が落ちることを確かめる**（マスタのコピーで電気炉の消費電力を 1 上げる。`$MW` は書き換えない）
```bash
rm -rf $GL/g6-broken && mkdir -p $GL/g6-broken && cp -R $MOD/master $MOD/localization $GL/g6-broken/
python3 - "$GL/g6-broken/master/blocks.json" <<'EOF'
import json, sys
path = sys.argv[1]
doc = json.load(open(path, encoding='utf-8'))
target = next(b for b in doc['data'] if b['name'] == '電気炉')
target['blockParam']['requiredPower'] += 1
json.dump(doc, open(path, 'w', encoding='utf-8'), ensure_ascii=False, indent=2)
print('broke', target['name'], target['blockParam']['requiredPower'])
EOF
cd $MW && python3 tools/tier9_balance/run.py --master-dir $GL/g6-broken/master | grep '電気炉 の消費電力'
```
Expected: `broke 電気炉 31` の後に `FAIL [s1_semiconductor] match mismatch 電気炉 の消費電力: sim=30 json=31 許容差=1e-09`（壊す前は同じ行が `PASS … match ok …`。本 plan 作成時に実測）。

- [ ] **Step 8: nodeGraph 同期ツールを置く**（判断記録 A21。前例 `tools/plan6_nodegraph_migration/migrate_nodegraph.py` と同じ流儀: マスタ repo の `tools/<目的>/` に置き、マスタ JSON は読むだけ、グラフは検査が全部通ったときだけ書き、違反があれば exit 1）
  - 前例との違い: 前例は一度きりの移行で、旧 GUID の対応を git 履歴から復元した。こちらは各スライスの末尾で何度でも流す同期なので、履歴は読まず「今のマスタと research.json」だけを正にする。recipe 線の消えた参照は外し、参照が 0 になった線は消す（前例のように buildCost 線へ変えない。統合で消すのはレシピそのものなので、関係が残らない）。
  - 置き場の決め方（`placement.py`）: 新しい研究ノードは前提の研究ノードの右に候補を並べ、重ならず既存ノードの割り当てを 1 つも変えない最初の位置。解放ノードはその研究ノードの右下に候補を並べ、1 パス目でその研究に割り当たり重ならない最初の位置。どこにも割り当てたくないノード（解放から外れたもの）は、最左の研究より左・全ノードより下へ退避する（2 パスとも対象外になる）。
  - 同じ述語・導出を 2 か所に書かない（判断記録 A32）: 研究ノードの抽出 `research_nodes`、研究の解放の列挙 `unlock_targets`、参照先の消えたノード・レシピ参照の述語 `MasterIndex.is_dangling_node`・`is_dead_recipe_ref` は `graph_io.py`。1 パス目・2 パス目のゾーン述語 `in_pass1_zone`・`in_pass2_zone` と、割り当て → (研究, 種類, GUID) の導出 `unlock_triples` は `spatial_unlock.py`。同期側（`prune_dangling`・`place_unlock`・`sync_unlock_nodes`）と検査側（`audit`）が同じものを呼ぶ。
  - 1 ディレクトリ 10 ファイルまで: `tools/nodegraph_sync/` は 7 本と `tests/`。最大は `graph_io.py` の 75 行。

`tools/nodegraph_sync/graph_io.py`:
```python
#!/usr/bin/env python3
# nodeGraph.v1.json とマスタ JSON の読み書き。マスタは読むだけで、グラフは元の末尾改行の有無を保って書く
# Read/write nodeGraph.v1.json and the master JSON; the master is read-only and the graph keeps its original trailing newline
# 研究ノードの抽出・研究の解放の列挙・参照先の消えたノードの述語は、ここの 1 本を全モジュールが使う（判断記録 A32）
# Research-node extraction, research unlock listing and the dangling predicates live here once for every module (decision A32)
import json
from pathlib import Path

from master_common.json_text import read_json_keeping_newline, write_json_keeping_newline

UNLOCK_KINDS = {
    # ノードの種類 → (研究の gameActionType, gameActionParam のキー)
    # Node type -> (research gameActionType, gameActionParam key)
    'item': ('unlockItemRecipeView', 'unlockItemGuids'),
    'block': ('unlockBlock', 'unlockBlockGuids'),
    'train': ('unlockTrainCar', 'unlockTrainCarGuids'),
}
MASTER_NODE_TYPES = ('item', 'block', 'train', 'research')


def research_nodes(graph):
    return [n for n in graph['nodes'] if n.get('type') == 'research']


def unlock_targets(research):
    # 研究 1 つの clearedActions から (ノードの種類, 対象GUID) を並び順に出す
    # Yield (node type, target GUID) from one research's clearedActions in order
    for action in research.get('clearedActions', []):
        for kind, (action_type, key) in UNLOCK_KINDS.items():
            if action.get('gameActionType') == action_type:
                for guid in action['gameActionParam'].get(key, []):
                    yield kind, guid


class MasterIndex:
    def __init__(self, mod_dir):
        master = Path(mod_dir) / 'master'

        def load(name):
            return json.loads((master / name).read_text(encoding='utf-8'))
        self.research = load('research.json')['data']
        self.valid = {
            'item': {r['itemGuid'] for r in load('items.json')['data']},
            'block': {r['blockGuid'] for r in load('blocks.json')['data']},
            'train': {r['trainCarGuid'] for r in load('train.json')['trainCars']},
            'research': {r['researchNodeGuid'] for r in self.research},
        }
        recipes = load('craftRecipes.json')['data'], load('machineRecipes.json')['data']
        self.recipe_guids = {r['craftRecipeGuid'] for r in recipes[0]} | {r['machineRecipeGuid'] for r in recipes[1]}

    def is_dangling_node(self, node):
        return node.get('type') in MASTER_NODE_TYPES and node['masterGuid'] not in self.valid[node['type']]

    def is_dead_recipe_ref(self, ref):
        return ref['masterGuid'] not in self.recipe_guids

    def expected_unlocks(self):
        # research.json の解放を (研究GUID, ノードの種類, 対象GUID) の集合にする
        # Turn research.json unlocks into a set of (research GUID, node type, target GUID)
        return {(n['researchNodeGuid'], kind, guid) for n in self.research for kind, guid in unlock_targets(n)}

    def prerequisites(self):
        return {(p, n['researchNodeGuid']) for n in self.research for p in n.get('prevResearchNodeGuids', [])}


def graph_path(mod_dir):
    return Path(mod_dir) / '.mooreseditor' / 'nodeGraph.v1.json'


def load_graph(mod_dir):
    return read_json_keeping_newline(graph_path(mod_dir))


def save_graph(mod_dir, graph, trailing_newline):
    write_json_keeping_newline(graph_path(mod_dir), graph, trailing_newline)
```

`tools/nodegraph_sync/spatial_unlock.py`:
```python
#!/usr/bin/env python3
# mooreseditor が保存時に研究の解放を決める空間割り当て（2 パスの範囲法）の移植。前例 plan6 migrate_nodegraph.py と同じ規則
# Port of mooreseditor's save-time spatial unlock (two-pass zone method); same rules as the plan6 migrate_nodegraph.py precedent
from graph_io import UNLOCK_KINDS, research_nodes


def pos(node):
    return node['position']['x'], node['position']['y']


def in_pass1_zone(rx, ry, x, y):
    # 1 パス目: 研究の右下側（R.x < n.x かつ R.y <= n.y）
    # Pass 1: the research's right/lower zone (R.x < n.x and R.y <= n.y)
    return rx < x and ry <= y


def in_pass2_zone(rx, ry, x, y):
    # 2 パス目: 研究の真下側（R.x >= n.x かつ R.y < n.y）
    # Pass 2: the zone below the research (R.x >= n.x and R.y < n.y)
    return rx >= x and ry < y


def nearest(candidates, node, eligible):
    # 条件を満たす研究のうちマンハッタン距離が最小のもの（同距離はグラフの並び順で先のもの）
    # The eligible research with the smallest Manhattan distance (ties go to the earlier one in graph order)
    x, y = pos(node)
    best, best_d = None, float('inf')
    for research in candidates:
        rx, ry = pos(research)
        if eligible(rx, ry, x, y):
            d = abs(x - rx) + abs(y - ry)
            if d < best_d:
                best, best_d = research, d
    return best


def assign(graph):
    # ノード id → 割り当てられた研究 GUID（割り当て無しは載らない）
    # Node id -> assigned research GUID (unassigned nodes are absent)
    candidates = research_nodes(graph)
    unlockable = [n for n in graph['nodes'] if n.get('type') in UNLOCK_KINDS]
    result = {}
    for node in unlockable:
        best = nearest(candidates, node, in_pass1_zone)
        if best:
            result[node['id']] = best['masterGuid']
    # 2 パス目は残りだけ。最左の研究より左のノードは対象外
    # Pass 2 takes only the rest; nodes left of the leftmost research are skipped
    min_rx = min((pos(r)[0] for r in candidates), default=0)
    for node in unlockable:
        if node['id'] in result or pos(node)[0] < min_rx:
            continue
        best = nearest(candidates, node, in_pass2_zone)
        if best:
            result[node['id']] = best['masterGuid']
    return result


def unlock_triples(graph, assigned):
    # 割り当てを (研究GUID, ノードの種類, 対象GUID) の集合にする
    # Turn an assignment into a set of (research GUID, node type, target GUID)
    nodes = {n['id']: n for n in graph['nodes']}
    return {(research, nodes[i]['type'], nodes[i]['masterGuid']) for i, research in assigned.items()}


def computed_unlocks(graph):
    # エディタが保存で research.json に書く値（空間割り当ての結果）
    # What the editor writes to research.json on save (the result of the spatial assignment)
    return unlock_triples(graph, assign(graph))
```

`tools/nodegraph_sync/graph_edits.py`:
```python
#!/usr/bin/env python3
# 参照先の消えたノード・線を除き、研究どうしの dependency 線を research.json の前提にそろえる
# Drop nodes/edges whose master target is gone and align research-to-research dependency edges with research.json prerequisites
from graph_io import research_nodes


def prune_dangling(graph, index):
    # マスタに無い GUID を指すノードと、その端を持つ線を消す。recipe 線は消えたレシピ参照だけを外し、空になれば線ごと消す
    # Remove nodes pointing at GUIDs missing from the master and edges touching them; recipe edges lose dead refs and vanish when empty
    removed = [n for n in graph['nodes'] if index.is_dangling_node(n)]
    removed_ids = {n['id'] for n in removed}
    graph['nodes'] = [n for n in graph['nodes'] if n['id'] not in removed_ids]
    kept_edges = []
    for edge in graph['edges']:
        if edge['source'] in removed_ids or edge['target'] in removed_ids:
            continue
        if edge.get('edgeType') == 'recipe':
            edge['recipes'] = [r for r in edge.get('recipes', []) if not index.is_dead_recipe_ref(r)]
            if not edge['recipes']:
                continue
        kept_edges.append(edge)
    dropped_edges = len(graph['edges']) - len(kept_edges)
    graph['edges'] = kept_edges
    return removed, dropped_edges


def research_node_ids(graph):
    return {n['masterGuid']: n['id'] for n in research_nodes(graph)}


def research_dependency_pairs(graph):
    # 研究ノードどうしの dependency 線を (前提GUID, 研究GUID) で返す
    # Return research-to-research dependency edges as (prerequisite GUID, research GUID)
    guid_of = {n['id']: n['masterGuid'] for n in research_nodes(graph)}
    return {(guid_of[e['source']], guid_of[e['target']]): e for e in graph['edges']
            if e.get('edgeType') == 'dependency' and e['source'] in guid_of and e['target'] in guid_of}


def sync_dependencies(graph, index):
    # エディタは研究どうしの線から前提を書き戻すので、線を research.json の prevResearchNodeGuids と一致させる
    # The editor writes prerequisites back from research-to-research edges, so match them to prevResearchNodeGuids
    wanted = index.prerequisites()
    current = research_dependency_pairs(graph)
    stale = [edge for pair, edge in current.items() if pair not in wanted]
    graph['edges'] = [e for e in graph['edges'] if not any(e is s for s in stale)]
    ids = research_node_ids(graph)
    added = sorted(pair for pair in wanted if pair not in current)
    for prev, node in added:
        graph['edges'].append({'id': f'edge-sync-{prev}-{node}', 'source': ids[prev], 'target': ids[node], 'edgeType': 'dependency'})
    return added, len(stale)
```

`tools/nodegraph_sync/placement.py`:
```python
#!/usr/bin/env python3
# 新しい研究ノードと解放ノードの置き場を決める。既存ノードの割り当てを変えず、空間割り当てが research.json と一致する位置だけを選ぶ
# Choose positions for new research and unlock nodes: keep existing assignments and accept only spots whose spatial unlock matches research.json
from graph_io import research_nodes
from spatial_unlock import assign, in_pass1_zone, nearest, pos

OVERLAP = (180, 80)
RESEARCH_STEP = (400, 280)
UNLOCK_STEP = (180, 90)
PARK_STEP = (220, 120)


def overlaps(graph, x, y, ignore_id):
    return any(abs(n['position']['x'] - x) < OVERLAP[0] and abs(n['position']['y'] - y) < OVERLAP[1]
               for n in graph['nodes'] if n['id'] != ignore_id)


def place_research(graph, guid, anchor):
    # 前提の研究ノードの右に候補を並べ、重ならず既存の割り当てを1つも変えない最初の位置に置く
    # Lay candidates to the right of the prerequisite node and take the first that overlaps nothing and changes no existing assignment
    before = assign(graph)
    ax, ay = pos(anchor) if anchor else (max(n['position']['x'] for n in graph['nodes']), 0)
    node = {'id': f'research-{guid}', 'position': {'x': 0, 'y': 0}, 'type': 'research', 'masterGuid': guid}
    graph['nodes'].append(node)
    for i in range(1, 9):
        for j in (0, 1, -1, 2, -2, 3, -3):
            x, y = ax + RESEARCH_STEP[0] * i, ay + RESEARCH_STEP[1] * j
            node['position'] = {'x': x, 'y': y}
            if not overlaps(graph, x, y, node['id']) and assign(graph) == before:
                return node
    graph['nodes'].remove(node)
    return None


def place_unlock(graph, node, research):
    # 研究ノードの右下に候補を並べ、1 パス目でその研究に割り当たり重ならない最初の位置へ動かす
    # Lay candidates right/below the research node and move to the first that pass 1 assigns to it without overlap
    candidates = research_nodes(graph)
    rx, ry = pos(research)
    for j in range(0, 12):
        for i in range(0, 4):
            x, y = rx + 60 + UNLOCK_STEP[0] * i, ry + UNLOCK_STEP[1] * j
            node['position'] = {'x': x, 'y': y}
            best = nearest(candidates, node, in_pass1_zone)
            if best is research and not overlaps(graph, x, y, node['id']):
                return True
    return False


def park(graph, nodes):
    # どの研究にも割り当てたくないノードを、最左の研究より左・全ノードより下へ並べる（2 パスとも対象外になる）
    # Line up nodes that must stay unassigned left of the leftmost research and below every node (both passes skip them)
    research_x = [n['position']['x'] for n in research_nodes(graph)]
    left = min(research_x) - 600
    bottom = max(n['position']['y'] for n in graph['nodes']) + 300
    for k, node in enumerate(nodes):
        node['position'] = {'x': left - PARK_STEP[0] * (k % 4), 'y': bottom + PARK_STEP[1] * (k // 4)}
```

`tools/nodegraph_sync/unlock_sync.py`:
```python
#!/usr/bin/env python3
# 研究ノードの不足を足し、解放ノードの空間割り当てを research.json の解放と一致させる（足す・動かす・どこにも割り当てない位置へ退避する）
# Add missing research nodes and make the unlock nodes' spatial assignment equal research.json (add, move, or park unassigned)
from graph_edits import research_node_ids
from graph_io import UNLOCK_KINDS, research_nodes, unlock_targets
from placement import park, place_research, place_unlock
from spatial_unlock import assign, unlock_triples


def add_missing_research(graph, index):
    # research.json の並び順に、前提の研究ノードを足場にして置く。置けなければ GUID を返して呼び出し側が失敗にする
    # Place in research.json order next to a prerequisite node; unplaceable GUIDs are returned so the caller fails
    added, unplaced = [], []
    for research in index.research:
        guid = research['researchNodeGuid']
        ids = research_node_ids(graph)
        if guid in ids:
            continue
        anchor_id = next((ids[p] for p in research.get('prevResearchNodeGuids', []) if p in ids), None)
        anchor = next((n for n in graph['nodes'] if n['id'] == anchor_id), None)
        (added if place_research(graph, guid, anchor) else unplaced).append(guid)
    return added, unplaced


def sync_unlock_nodes(graph, index):
    expected = index.expected_unlocks()
    assigned = assign(graph)
    satisfied = unlock_triples(graph, assigned) & expected
    # 正しく割り当たっていないノードは動かしてよい（間違った研究に付いたもの・解放対象なのに割り当て無しのもの）
    # Nodes not correctly assigned may move: those on the wrong research and unlock targets left unassigned
    unlocked_targets = {(kind, guid) for _, kind, guid in expected}
    free = [n for n in graph['nodes'] if n.get('type') in UNLOCK_KINDS
            and (assigned.get(n['id']), n['type'], n['masterGuid']) not in expected
            and (n['id'] in assigned or (n['type'], n['masterGuid']) in unlocked_targets)]
    research_by_guid = {n['masterGuid']: n for n in research_nodes(graph)}
    moved, added, unplaced = [], [], []
    for research in index.research:
        for kind, guid in unlock_targets(research):
            pair = (research['researchNodeGuid'], kind, guid)
            if pair in satisfied:
                continue
            node = next((n for n in free if n['type'] == kind and n['masterGuid'] == guid), None)
            if node is None:
                node = {'id': f'node-sync-{research["researchNodeGuid"]}-{guid}', 'position': {'x': 0, 'y': 0}, 'type': kind, 'masterGuid': guid}
                graph['nodes'].append(node)
                added.append(pair)
            else:
                free.remove(node)
                moved.append(pair)
            if not place_unlock(graph, node, research_by_guid[research['researchNodeGuid']]):
                unplaced.append(pair)
            satisfied.add(pair)
    # 残った「間違った研究に付いたノード」は、どの研究にも割り当たらない位置へ退避する
    # Park the remaining wrongly assigned nodes where no research picks them up
    parked = [n for n in free if n['id'] in assigned]
    park(graph, parked)
    return moved, added, parked, unplaced
```

`tools/nodegraph_sync/audit.py`:
```python
#!/usr/bin/env python3
# nodeGraph が統合マスタと揃っているかの検査。違反は1件ずつ文字列で返し、空なら合格
# Check the nodeGraph matches the integrated master; return one string per violation, empty means pass
from collections import Counter

from graph_edits import research_dependency_pairs
from graph_io import research_nodes
from spatial_unlock import computed_unlocks


def audit(graph, index):
    errors = []
    # 参照先の消えたノード・線が 0 件（述語は同期側の prune_dangling と同じ MasterIndex のもの）
    # Zero nodes or edges whose master target is gone (the same MasterIndex predicates as prune_dangling)
    errors += [f'dangling node {n["id"]} ({n["type"]}): {n["masterGuid"]}' for n in graph['nodes'] if index.is_dangling_node(n)]
    errors += [f'dangling recipe ref in edge {e["id"]}: {r["masterGuid"]}' for e in graph['edges'] if e.get('edgeType') == 'recipe'
               for r in e.get('recipes', []) if index.is_dead_recipe_ref(r)]
    node_ids = {n['id'] for n in graph['nodes']}
    errors += [f'edge {e["id"]} points at a missing node' for e in graph['edges'] if e['source'] not in node_ids or e['target'] not in node_ids]
    # research.json と研究ノードが 1 対 1
    # research.json and research nodes are one-to-one
    counts = Counter(n['masterGuid'] for n in research_nodes(graph))
    errors += [f'research node missing: {g}' for g in sorted(index.valid['research'] - set(counts))]
    errors += [f'research node duplicated x{c}: {g}' for g, c in sorted(counts.items()) if c > 1]
    # 研究どうしの線が前提と一致し、空間割り当てが解放と一致する（エディタが保存しても research.json が変わらない）
    # Research edges equal prerequisites and the spatial unlock equals research.json (an editor save leaves research.json unchanged)
    wanted, drawn = index.prerequisites(), set(research_dependency_pairs(graph))
    errors += [f'dependency edge missing: {p} -> {n}' for p, n in sorted(wanted - drawn)]
    errors += [f'dependency edge extra: {p} -> {n}' for p, n in sorted(drawn - wanted)]
    expected, computed = index.expected_unlocks(), computed_unlocks(graph)
    errors += [f'unlock not placed: research {r} {k} {g}' for r, k, g in sorted(expected - computed)]
    errors += [f'unlock extra by position: research {r} {k} {g}' for r, k, g in sorted(computed - expected)]
    return errors
```

`tools/nodegraph_sync/sync_nodegraph.py`:
```python
#!/usr/bin/env python3
# mooreseditor の nodeGraph.v1.json を統合マスタに合わせる（判断記録 A21。前例 tools/plan6_nodegraph_migration/migrate_nodegraph.py）
# Align mooreseditor's nodeGraph.v1.json with the integrated master (decision A21; precedent tools/plan6_nodegraph_migration/migrate_nodegraph.py)
# 消えた GUID のノードを除き、研究ノードと解放ノードを足し・動かし、検査が1件でも落ちれば exit 1。--check は書き換えず検査だけ
# Drop nodes of removed GUIDs, add/move research and unlock nodes, exit 1 on any failed check; --check only audits without writing
import argparse
import sys
from pathlib import Path

# tools/nodegraph_sync と、共通部品 master_common を置く tools/ の両方を import できるようにする
# Make both tools/nodegraph_sync and tools/ (home of the shared master_common package) importable
sys.path.insert(0, str(Path(__file__).resolve().parent))
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from audit import audit  # noqa: E402
from graph_edits import prune_dangling, sync_dependencies  # noqa: E402
from graph_io import MasterIndex, load_graph, save_graph  # noqa: E402
from unlock_sync import add_missing_research, sync_unlock_nodes  # noqa: E402

DEFAULT_MOD = Path(__file__).resolve().parents[2] / 'server_v8' / 'mods' / 'moorestechAlphaMod_8'


def main(argv):
    parser = argparse.ArgumentParser(description='Sync nodeGraph.v1.json with the master')
    parser.add_argument('--mod-dir', default=str(DEFAULT_MOD))
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args(argv)
    index = MasterIndex(args.mod_dir)
    graph, trailing_newline = load_graph(args.mod_dir)
    if not args.check:
        removed, dropped_edges = prune_dangling(graph, index)
        added_research, unplaced_research = add_missing_research(graph, index)
        added_edges, stale_edges = sync_dependencies(graph, index)
        moved, added, parked, unplaced = sync_unlock_nodes(graph, index)
        print(f'pruned {len(removed)} nodes / {dropped_edges} edges; research nodes +{len(added_research)}; '
              f'dependency edges +{len(added_edges)} -{stale_edges}; unlock nodes moved {len(moved)} added {len(added)} parked {len(parked)}')
        for guid in unplaced_research:
            print(f'UNPLACED research {guid}')
        for research, kind, guid in unplaced:
            print(f'UNPLACED unlock research {research} {kind} {guid}')
    errors = audit(graph, index)
    for error in errors:
        print(f'FAIL {error}')
    if not args.check and not errors:
        save_graph(args.mod_dir, graph, trailing_newline)
    print(f'== NODEGRAPH: {"FAIL" if errors else "PASS"} ({len(errors)} failures)')
    return 1 if errors else 0


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
```

`tools/nodegraph_sync/tests/test_nodegraph_sync.py`:
```python
#!/usr/bin/env python3
# nodeGraph 同期: 消えた GUID を除く・新しい研究と解放を足す・付け替わった解放を動かす・外れた解放を退避する・検査が落ちる
# nodeGraph sync: drops removed GUIDs, adds new research/unlocks, moves re-assigned unlocks, parks dropped ones, and the audit fails
import json
import os
import sys
import tempfile
import unittest

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
import sync_nodegraph  # noqa: E402
from graph_io import MasterIndex, load_graph  # noqa: E402
from spatial_unlock import computed_unlocks  # noqa: E402

R1, R2, R3 = 'r1', 'r2', 'r3'


def research(guid, prev, items, blocks):
    actions = [{'gameActionType': 'unlockItemRecipeView', 'gameActionParam': {'unlockItemGuids': items}},
               {'gameActionType': 'unlockBlock', 'gameActionParam': {'unlockBlockGuids': blocks}}]
    return {'researchNodeGuid': guid, 'prevResearchNodeGuids': prev, 'clearedActions': actions}


def write_mod(root, research_rows, item_guids, block_guids, graph):
    os.makedirs(os.path.join(root, 'master'))
    os.makedirs(os.path.join(root, '.mooreseditor'))
    files = {'research.json': {'data': research_rows}, 'items.json': {'data': [{'itemGuid': g} for g in item_guids]},
             'blocks.json': {'data': [{'blockGuid': g} for g in block_guids]}, 'train.json': {'trainCars': []},
             'craftRecipes.json': {'data': [{'craftRecipeGuid': 'c1'}]}, 'machineRecipes.json': {'data': []}}
    for name, doc in files.items():
        with open(os.path.join(root, 'master', name), 'w', encoding='utf-8') as f:
            json.dump(doc, f)
    with open(os.path.join(root, '.mooreseditor', 'nodeGraph.v1.json'), 'w', encoding='utf-8') as f:
        json.dump(graph, f)


def node(node_id, kind, guid, x, y):
    return {'id': node_id, 'position': {'x': x, 'y': y}, 'type': kind, 'masterGuid': guid}


def base_graph():
    # r1 と r2 が並び、i1 と b1 は r1、i2 は r2 の右下にある。i9 は消えるアイテム
    # r1 and r2 sit side by side; i1 and b1 are right-below r1, i2 right-below r2; i9 is an item being removed
    nodes = [node('research-r1', 'research', R1, 0, 0), node('research-r2', 'research', R2, 1000, 0),
             node('n-i1', 'item', 'i1', 100, 100), node('n-b1', 'block', 'b1', 300, 100),
             node('n-i2', 'item', 'i2', 1100, 100), node('n-i9', 'item', 'i9', 200, 300)]
    edges = [{'id': 'e-dep', 'source': 'research-r1', 'target': 'research-r2', 'edgeType': 'dependency'},
             {'id': 'e-rec', 'source': 'n-i1', 'target': 'n-i9', 'edgeType': 'recipe', 'recipes': [{'masterGuid': 'c1'}]}]
    return {'version': 1, 'nodes': nodes, 'edges': edges}


class NodeGraphSyncTest(unittest.TestCase):
    def run_sync(self, research_rows, items, blocks, check):
        root = tempfile.mkdtemp()
        write_mod(root, research_rows, items, blocks, base_graph())
        code = sync_nodegraph.main(['--mod-dir', root] + (['--check'] if check else []))
        return code, root

    def test_in_sync_graph_passes_unchanged(self):
        rows = [research(R1, [], ['i1', 'i9'], ['b1']), research(R2, [R1], ['i2'], [])]
        code, _ = self.run_sync(rows, ['i1', 'i2', 'i9'], ['b1'], check=True)
        self.assertEqual(0, code)

    def test_removed_item_fails_check(self):
        rows = [research(R1, [], ['i1'], ['b1']), research(R2, [R1], ['i2'], [])]
        code, _ = self.run_sync(rows, ['i1', 'i2'], ['b1'], check=True)
        self.assertEqual(1, code)

    def test_sync_adds_moves_parks_and_then_passes(self):
        # i9 を消し、新しい研究 r3（前提 r2）が新アイテム i3 と既存の b1 を解放し、i2 はどこからも外れる
        # Remove i9; new research r3 (after r2) unlocks new item i3 and existing b1; i2 is no longer unlocked anywhere
        rows = [research(R1, [], ['i1'], []), research(R2, [R1], [], []), research(R3, [R2], ['i3'], ['b1'])]
        code, root = self.run_sync(rows, ['i1', 'i2', 'i3'], ['b1'], check=False)
        self.assertEqual(0, code)
        graph, _ = load_graph(root)
        index = MasterIndex(root)
        self.assertEqual(index.expected_unlocks(), computed_unlocks(graph))
        self.assertNotIn('i9', [n['masterGuid'] for n in graph['nodes']])
        self.assertEqual([], [e for e in graph['edges'] if e['edgeType'] == 'recipe'])
        dependency = {(e['source'], e['target']) for e in graph['edges'] if e['edgeType'] == 'dependency'}
        self.assertEqual({('research-r1', 'research-r2'), ('research-r2', 'research-r3')}, dependency)
        self.assertEqual(0, sync_nodegraph.main(['--mod-dir', root, '--check']))


if __name__ == '__main__':
    unittest.main()
```

- [ ] **Step 9: 同期ツールの自己テストと、統合マスタでの検査を回す**（このタスクでは書き込みモードは流さない。流すのは各スライスの末尾）
```bash
cd $MW && python3 -m unittest discover -s tools/nodegraph_sync/tests 2>&1 | grep -E '^(Ran|OK|FAILED)'
cd $MW && python3 tools/nodegraph_sync/sync_nodegraph.py --check > $GL/p2-2-nodegraph-base.txt; echo "ng=$?"; cat $GL/p2-2-nodegraph-base.txt
```
Expected（本 plan 作成時に、起点 f9370e7 の tree を scratch に展開して実測）:
- 自己テストは `Ran 3 tests` / `OK`（同期済みなら通る・消えたアイテムで落ちる・研究の追加と解放の付け替えと退避の後に検査が通る）。
- `ng=1`、末尾は `== NODEGRAPH: FAIL (10 failures)`。内訳:
  - `dangling node` 2 件（item `5be3a22c-…`・`8412fa32-…`。起点ですでに items.json から消えている）。
  - `dangling recipe ref` 3 件（edge `edge-1776569825240` の 2 件と `edge-1778637438766` の 1 件）。
  - `unlock extra by position` 5 件。上の消えた item 2 件と、S1 のマスタ 32874fe が解放を外したブロック 3 件（核融合炉・宇宙エレベーター拠点・ロケット発射台。配置上はまだ研究の下にある）。
- 参考（scratch で実測）: 同じ起点で書き込みモードを流すと `pruned 2 nodes / 2 edges; research nodes +0; dependency edges +0 -0; unlock nodes moved 0 added 0 parked 3` の後 `== NODEGRAPH: PASS (0 failures)` になり、もう一度流すと何も変えない（冪等）。

- [ ] **Step 10: 行数を確かめる**  Run: `wc -l $B/apply/*.py $MW/tools/tier9_balance/sections/*.py $MW/tools/tier9_balance/sections/s1/*.py $MW/tools/nodegraph_sync/*.py $MW/tools/nodegraph_sync/tests/*.py $MW/tools/master_common/*.py`  Expected: どのファイルも 200 行未満。
- [ ] **Step 11: コミット**
```bash
git -C $MW add design/tier9-balance/apply design/tier9-balance/guids.json design/tier9-balance/README.md tools/tier9_balance/sections tools/nodegraph_sync tools/master_common/json_text.py
git -C $MW commit -m "design/tools: Tier9 マスタの適用スクリプトと GUID 台帳、G6 の S1 区間の照合、nodeGraph 同期ツールを置く"
```

**完了判定:**
- Step 5 で `git status` が空。
- Step 6 で `[s1_semiconductor]` の `target`・`match` 行が状態つき（`ok`・`out_of_range`・`mismatch`・`lookup_failed`）で出て、例外で止まらない。枠の自己テストが `OK`。
- Step 7 で電気炉の行が FAIL になる。
- Step 9 で同期ツールの自己テストが `OK`、統合マスタの検査が `10 failures` で赤い（各スライスの末尾で緑にする）。
- Step 10 ですべて 200 行未満。
**量感:** Python 約 1250 行（apply の下書き 5 ファイル約 360 行と `apply_extras.py` 約 60 行、S1 区間約 370 行、nodeGraph 同期約 360 行と自己テスト約 90 行）。JSON 19 行。

---

### スライス S1（S1 handoff「Part D」の D3〜D7。D1 は G6、D8 は Task 24、D9 は録画の節で扱う）

D1（sim）は Task 3 Step 7 が置いた。このスライスでは値の正本として `$B/sim/params.py` と `$B/sim/timeline.py` を読む。spec の値はそれと同じにする（食い違いは G6 の `[s1_semiconductor] match` が FAIL にする）。

D9（録画 3 本）は出荷マスタ録画 7 段の段 1〜3（チップライン・モジュールと GPU・抽選 UI）として録画の節が実行する。P2 では撮らない。

S1 handoff「設計検査の持ち越し」8 項目の行き先:
- sim の 3 項目と「研究での Lv2 の数え方」は G6（P0）。
- `startswith` は Task 12（`sections/s1/structure_checks.py` の `_die_stacks` がダイを levelFamily で判別し、`recipe_checks._recipe_rows` がレシピの入力を `MasterView.input_guid` の GUID で照合する）。
- `KNOWN_ORPHANS` は Task 15。
- ハッチの搬送上限は Task 15 と Task 23。
- 照合と起動時検証の重複は、G1 を正とすることで解消する（G6 の S1 区間は sim と JSON の値の照合だけを持つ）。

### Task 13: 上流・IPA・鋼鉄フレーム・素材レシピと化学プラントの口、超純水の削除（S1 handoff「Task D3」）

**Files:**
- Create: `moorestech_master:design/tier9-balance/spec/01_upstream_materials.json`
- Modify: `moorestech_master:server_v8/mods/moorestechAlphaMod_8/master/{items,fluids,blocks,machineRecipes}.json`、`.../localization/localization.csv`、`design/tier9-balance/guids.json`

**Interfaces:**
- Consumes: Task 12 の `apply_spec.py`。
- Produces:
  - レシピキー `砂` `金属シリコン` `高純度シリコン` `IPA` `鋼鉄フレーム` `ICチップ基板` `クリーンルームフィルター` `失敗ウェハ再生` `失敗ダイ再生`。
  - 化学プラントの口: アイテム入口 (3,0,0) −z、液体入力 2 個（タンク 0 = 既存口 (3,1,0)、タンク 1 = 新口 (2,1,0)）、液体出力 1 個（新口 (2,1,10)）。
  - この口は S2 のロケット燃料レシピの前提にもなる（Task 18）。ロケット燃料は固形燃料をアイテム入口から入れ、ガソリンを `inputFluids` の先頭としてタンク 0（口 (3,1,0)）で受ける。

- [ ] **Step 1: spec 01 を書く。** 中身は S1 handoff「Task D3 Step 1」の JSON をそのまま使う。値は `$B/sim/params.py` の `RECIPES` と照らし、違えば params の値で書く。

  下書きに対する訂正は無い。超純水の削除（`removeFluids`）、化学プラントの `patchBlocks`、9 本すべての `levelInheritance: "None"` は、下書きに既に入っている。
- [ ] **Step 2: 適用する**  Run: `cd $B && python3 apply/apply_spec.py --mod-dir $MOD --spec-dir spec --ledger guids.json`  Expected: `applied 1 spec files; issued 14 new guids`
- [ ] **Step 3: 結果を確かめる。** S1 handoff「Task D3 Step 3」のコマンドを使う（`$MOD` は上の変数）。

  Expected: 超純水 `019eeaa7-45b6` の GUID が全マスタから消えている。続けて `2 1 2 1 [[3, 0, 0]]` が出る。
- [ ] **Step 4: 2 回目の適用で差分が出ないことを確かめる**  Run: `cd $B && python3 apply/apply_spec.py --mod-dir $MOD --spec-dir spec --ledger guids.json && git -C $MW diff --stat | tail -1`  Expected: `issued 0 new guids`。diff の行数は Step 2 の直後と同じ。
- [ ] **Step 5: コミット**  S1 handoff「Task D3 Step 5」と同じ（`design/tier9-semiconductor-balance` を `design/tier9-balance` に読み替える）。

**完了判定:** Step 3 の 2 行と、Step 4 の `issued 0`。
**量感:** JSON 約 +350 行（レシピ 9 本で約 270 行、化学プラント約 40 行、アイテム 2 件約 26 行）。localization +11 行・−1 行。

### Task 14: ウェハ・ダイ・チップの変種と levelFamilies 6 件（S1 handoff「Task D4」）

**Files:** Create: `moorestech_master:design/tier9-balance/spec/02_wafers_dies_chips.json` / Modify: `.../master/items.json`、`localization.csv`、`guids.json`

**Interfaces:**
- Consumes: Task 13 の状態。
- Produces: S1 handoff「Task D4」Interfaces の名前（ウェハ G1〜G5 の 4 工程、ダイ・チップ Lv1〜Lv5、levelFamily 6 件、スタック表「ダイ」[600]）。ICチップの levelFamily は、Task 19 の研究消費の代用と、S2 の GPU衛星の入力の前提になる。

- [ ] **Step 1: spec 02 を書く。** S1 handoff「Task D4 Step 1」の JSON をそのまま使う。訂正は無い。
- [ ] **Step 2: 適用する**  Expected: `applied 2 spec files; issued 29 new guids`
- [ ] **Step 3: ファミリーを確かめる。** S1 handoff「Task D4 Step 3」のコマンドを使う。Expected: 同節の 6 行。
- [ ] **Step 4: コミット**  S1 handoff「Task D4 Step 4」と同じ（置き場を読み替える）。

**完了判定:** Step 3 の 6 行が一致する。
**量感:** JSON 約 +380 行（アイテム 22 件で約 290 行、ファミリー約 60 行）。localization +26 行。

### Task 15: 専用機 5 種・工程レシピ・クリーンルームの値と建設コスト、旧 EUV レシピと同名孤児アイテムの削除（S1 handoff「Task D5」）

**Files:**
- Create: `moorestech_master:design/tier9-balance/spec/03_cleanroom_machines.json`
- Modify: `.../master/{items,blocks,machineRecipes,cleanRoom}.json`、`localization.csv`、`guids.json`
- Modify: `moorestech_master:tools/research_sync/sync_machine_recipe_unlocks.py`（`KNOWN_ORPHANS` を空にする）

**Interfaces:**
- Consumes: Task 13・Task 14 の名前。S1 本体の prefab アドレス 5 種（S1 handoff「Task D5」Interfaces）。
- Produces: ブロック名とレシピキー（S1 handoff「Task D5」Interfaces の Produces）。CleanRoomMachine 5 種の `allowQualityModule` は、成膜・EUV・ダイシングが true、ウェハ切断機・パッケージング装置が false。

- [ ] **Step 1: spec 03 を書く。** 土台は S1 handoff「Task D5 Step 1」の JSON。次の 3 点を変える。

(a) 下書きは専用機 4 種にブロックと同名のアイテムを作るが、これをやめる。既存の同名アイテム 6 件（クリーンルームブロック `019e3e76-24e5…`・アイテムハッチ `019e3e76-5094…`・パイプコネクタ `019e3e76-8867…`・ドア `019e3e76-b511…`・空気清浄機 `019e3e76-e150…`・EUV `019e3c20-f9f5…`）も消す。
- 根拠（実測）: f9370e7 で 6 件ともどの機械レシピの出力にも無い。ブロックの `itemGuid` キーはスキーマ `blocks.yml` に無い死にキーで、6 行だけが持っている。
- 残すと、G2 の「作り方の無いアイテム」に恒久的に残る（判断を仰ぐ点 1）。

spec の差分:
```json
  "items": [],
  "removeItems": [
    "019e3e76-24e5-7071-86ed-330eb056a99d", "019e3e76-5094-757f-8d49-090b8d63ea07", "019e3e76-8867-735d-b0d6-2b4654173880",
    "019e3e76-b511-76d0-83a3-1e3af1397847", "019e3e76-e150-74af-a956-db52d5706a9e", "019e3c20-f9f5-70fb-ae76-de727e69df61"
  ],
  "deleteBlockKeys": {
    "クリーンルームブロック": ["itemGuid"], "クリーンルームアイテムハッチ": ["itemGuid"], "クリーンルームパイプコネクタ": ["itemGuid"],
    "クリーンルームドア": ["itemGuid"], "クリーンルーム空気清浄機": ["itemGuid"], "EUV露光式半導体製造装置": ["itemGuid"]
  },
```
あわせて、`blocks[].element` 4 件から `"itemGuid": "@item:…"` を外す。

(b) `patchBlocks` の「クリーンルームアイテムハッチ」に、搬送上限 `"blockParam.maxItemsPerSecond": <$B/sim/params.py の値>` を足す。起点の値は 630fdce の暫定値 4。本番値は Task 23 で決める（S1 の裁定 `.decisions/2026-10-23-アイテムハッチに搬送量の上限を足して大部屋集約を有利にする.md`）。

(c) `removeMachineRecipes: ["3a13a5a9-d254-40f9-be93-e24c9ebfdfaf"]` は下書きどおり残す。旧 EUV レシピの削除で、`removeItems` の EUV アイテムを参照する残りも消える。

- [ ] **Step 2: `KNOWN_ORPHANS` を空にする。** 旧 EUV レシピが消えると、`sync_machine_recipe_unlocks.py:146-148` の陳腐化検査が exit 2 を出すため。
```python
# 既知の孤児レシピ → 期待する欠損GUID集合（既存データ）。欠損集合が完全一致するときだけ許可する
# Known orphan recipe -> expected missing GUID set (pre-existing data). Allowed only when the missing set matches exactly
KNOWN_ORPHANS = {}
```
- [ ] **Step 3: 適用する**  Expected: `applied 3 spec files; issued 24 new guids`。内訳はブロック 4・レシピ 7・コネクタ 13。下書きの 28 から、アイテム 4 件の分が減る。
- [ ] **Step 4: 結果を確かめる。** S1 handoff「Task D5 Step 3」のコマンドに 1 行足す:
```bash
python3 -c "import json; i=json.load(open('$MOD/master/items.json'))['data']; b=json.load(open('$MOD/master/blocks.json'))['data']; print(sum('itemGuid' in e for e in b), [e['name'] for e in i if e['name'] in {x['name'] for x in b}])"
```
Expected: 同節の 8 行がそのまま出る（最上位キーは死にキー 2 つが消えて 3 つ、旧 EUV は False、CleanRoomMachine は 5 種、…、`PASS quality module permission`）。続けて `0 []`（同名アイテムと `itemGuid` キーが 0 件）。
- [ ] **Step 5: コミット**
```bash
git -C $MW add design/tier9-balance/spec/03_cleanroom_machines.json design/tier9-balance/guids.json server_v8/mods/moorestechAlphaMod_8/master server_v8/mods/moorestechAlphaMod_8/localization/localization.csv tools/research_sync/sync_machine_recipe_unlocks.py
git -C $MW commit -m "master: Tier9 専用機5種と工程レシピ・クリーンルームの値・建設コストを入れ、旧 EUV レシピとブロック同名の孤児アイテムを消す"
```

**完了判定:** Step 4 の `PASS` 3 行と `0 []`。
**量感:** JSON 約 +800 行。内訳はブロック 4 種で約 380 行、EUV とクリーンルーム系のパッチ約 60 行、レシピ 7 本で約 250 行、cleanRoom.json 約 100 行。削除はアイテム 6 件で約 78 行。

### Task 16: モジュール 4 種 × Lv1〜5 と GPU Lv1〜5（S1 handoff「Task D6」。GPU の表記を揃える）

**Files:** Create: `moorestech_master:design/tier9-balance/spec/04_modules_gpu.json` / Modify: `.../master/{items,machineRecipes}.json`、`localization.csv`、`guids.json`

**Interfaces:**
- Consumes: Task 14 の `ICチップ` ファミリー。
- Produces（S2 と取り決め済みの固定 GUID。S2 plan の Global Constraints「S1 の GPU」と値が一致することを確認済み）:
  - GPU の family `01a167ed-2700-7371-9f95-168966ff60bc`
  - Lv1〜Lv5 `01a167ed-2703…`・`2706…`・`2709…`・`270c…`・`270f…`
  - GPU の継承レシピ `01a167f3-bd94-76d2-ada6-617fdbf3d9b3`
  - モジュールの family・継承レシピ・Lv1 は、S1 handoff「Task D6」Interfaces の値。
  - 名前は `GPULv1`〜`GPULv5`（下の訂正）。Task 18 の GPU衛星レシピと、録画の節のシナリオがこの名前で引く。

- [ ] **Step 1: spec 04 を書く。** 土台は S1 handoff「Task D6 Step 1」の JSON。訂正は GPU の名前だけ。
  - 下書きの `GPU`・`GPU Lv2`〜`GPU Lv5`（空白あり、Lv1 は無印）を、`GPULv1`〜`GPULv5` に揃える。英語名は `GPU Lv1`〜`GPU Lv5`。他の段階アイテム（`ICチップLv2`・`GPU衛星Lv1`）の「空白なし・Lv1 明示」に合わせるため（判断を仰ぐ点 2）。
  - GUID は固定なので、台帳には新しい名前で記録される。
  - 同じ spec の levelFamily の `levels`、レシピの `outputItems`、モジュールが参照する名前も揃える。
```json
    {"name": "GPULv1", "guid": "01a167ed-2703-7014-98e4-11bd15f38571", "en": "GPU Lv1", "sortPriority": 1541},
    {"name": "GPULv2", "guid": "01a167ed-2706-7bfd-8361-a430dd96a569", "en": "GPU Lv2", "sortPriority": 1542},
    {"name": "GPULv3", "guid": "01a167ed-2709-74f0-a5c6-25026c7479d9", "en": "GPU Lv3", "sortPriority": 1543},
    {"name": "GPULv4", "guid": "01a167ed-270c-7873-a83a-5cf02ead6277", "en": "GPU Lv4", "sortPriority": 1544},
    {"name": "GPULv5", "guid": "01a167ed-270f-7276-8700-469ab54a26a2", "en": "GPU Lv5", "sortPriority": 1545}
```
  - levelFamily: `{"name": "GPU", "guid": "01a167ed-2700-7371-9f95-168966ff60bc", "levels": ["GPULv1", "GPULv2", "GPULv3", "GPULv4", "GPULv5"]}`
  - レシピ `GPU` の出力: `[["@item:GPULv1", 1]]`
- [ ] **Step 2: 適用する**  Expected: `applied 4 spec files; issued 36 new guids`
- [ ] **Step 3: 結果を確かめる。** S1 handoff「Task D6 Step 3」のコマンドを使う。Expected: `20 [3, 6, 10]`・ファミリー 5 件・3 行目が `GPULv2`・`PASS modules = params`。
- [ ] **Step 4: コミット**  S1 handoff「Task D6 Step 4」と同じ（置き場を読み替える）。

**完了判定:** Step 3 の 4 行。
**量感:** JSON 約 +730 行（アイテム 25 件で約 325 行、modules 20 件で約 180 行、ファミリー約 50 行、レシピ 5 本で約 150 行）。localization +25 行。

### Task 17: S1 の研究と半導体製造の解放、並び順の再計算、S1 スライスのゲート（S1 handoff「Task D7」）

**Files:** Create: `moorestech_master:design/tier9-balance/spec/05_research.json` / Modify: `.../master/{research,items,blocks}.json`、`localization.csv`、`.../.mooreseditor/nodeGraph.v1.json`（Step 3 の同期） / Create（控え）: `$GL/p2-7-s1-g1.txt`、`$GL/p2-7-s1-g2.txt`、`$GL/p2-7-s1-nodegraph.txt`、`$GL/p2-7-s1-g6.txt`

**Interfaces:**
- Consumes: Task 13〜Task 16 の名前と固定 GUID（S1 handoff「Task D7」Interfaces）。
- Produces:
  - S1 の研究 7 ノード（GUID は S1 handoff「Task D7」Produces）。
  - 機械オーバークロック `019e3ae2-5005-7439-b242-9b7b3191a2c3` の前提を「品質モジュール」へ付け替え、コストと解放を入れる（裁定2 の対象ノード 1 つ目。GUID は変えない）。
  - GPU設計 `01a167f3-bd74-73f8-8c43-1869db833299` は Task 19 のロケット技術の前提になる。

- [ ] **Step 1: spec 05 を書く。** 土台は S1 handoff「Task D7 Step 1」の JSON。訂正は次の 2 点。
  - (a) 「半導体製造」の `unlockItemRecipeView` から、ブロックと同名のアイテム 10 件を外す（Task 15 (a) で消すため）。外すのはクリーンルームブロック・ドア・アイテムハッチ・パイプコネクタ・空気清浄機・ウェハ切断機・成膜装置・EUV露光式半導体製造装置・ダイシング装置・パッケージング装置。表示の解放は 48 件から 38 件になる（素材 8・ウェハ 20・ダイ 5・チップ 5）。`unlockBlock` の 11 件はそのまま残す。
  - (b) 「GPU設計」の `unlockItemRecipeView` と `iconItem` を `@item:GPULv1` にする（Task 16 の改名）。
- [ ] **Step 2: 適用し、研究の解放を同期する**
```bash
cd $B && python3 apply/apply_spec.py --mod-dir $MOD --spec-dir spec --ledger guids.json
cd $MW && python3 tools/research_sync/sync_machine_recipe_unlocks.py | tail -3 && python3 tools/research_sync/sync_machine_recipe_unlocks.py --check; echo "check=$?"
```
Expected: apply が `applied 5 spec files; issued 0 new guids`。sync は `0 orphans`・`0 stale` で、`DEAD-END` 行が無い。最後に `check=0`。
- `MOVE` が出たら、そのレシピを spec 05 の移動先ノードの `unlockMachineRecipe` へ書き直す。そのうえで Step 2 をやり直し、apply 後に `MOVE` が 0 になるまで繰り返す。
- [ ] **Step 3: nodeGraph を統合マスタに合わせる**（判断記録 A21。master-refine は nodeGraph の配置を基準に並び順と座標を計算するので、その前に流す）
```bash
cd $MW && python3 tools/nodegraph_sync/sync_nodegraph.py > $GL/p2-7-s1-nodegraph-sync.txt; echo "ng=$?"; cat $GL/p2-7-s1-nodegraph-sync.txt
git -C $MW diff --stat -- server_v8/mods/moorestechAlphaMod_8/.mooreseditor
```
Expected:
- `ng=0`。末尾は `== NODEGRAPH: PASS (0 failures)` で、`UNPLACED` 行が無い。diff は `nodeGraph.v1.json` の 1 ファイルだけ。
- 1 行目の見込み（推測。Task 12 Step 9 の起点の実測から積み上げた値）:
  - `pruned` は 4 ノード以上。起点から残る消えた item 2 件と、Task 15 で消した孤児アイテムのうちグラフにある 2 件（EUV の `019e3c20-f9f5…`、クリーンルームブロックの `019e3e76-24e5…`）。
  - `research nodes +6`。S1 の新しい研究 6 ノード（品質モジュール・省エネモジュール・生産性モジュール・GPU設計・モジュールスタック拡張I・II）。機械オーバークロックは既存のノードを使う。
  - `parked` に、解放の外れた核融合炉・宇宙エレベーター拠点・ロケット発射台の 3 件が入る（Task 19・Task 22 で研究の下へ戻る）。
- `UNPLACED` が出たら、その研究ノードの周りで候補の格子が埋まっている。検査は緩めず、`placement.py` の候補の範囲（研究は `range(1, 9)`、解放は `range(0, 12)`）を広げて流し直す。
- [ ] **Step 4: sortPriority と研究座標を再計算する。** S1 handoff「Task D7 Step 3」を使う。スクリプトは `$W/.agents/skills/master-refine/scripts/` にある（`.claude/skills` は symlink）。
- [ ] **Step 5: 研究と解放を確かめる。** S1 handoff「Task D7 Step 4」の python（研究の数と解放の数を出す 1 本目）を使う。最後の照合の行は、次の G6 の行に置き換える。
```bash
cd $MW && python3 tools/tier9_balance/run.py | grep -E '^(PASS|FAIL) \[s1_semiconductor\] match [a-z_]+ 研究 '
```
  Expected は `7 <timeline.RESEARCH の chips 合計>`・`11 True 38`。2 つ目は下書きの `11 True 48` から変わる。G6 の行は S1 の 7 ノード × 5 行（4 品の数と余計なコスト）がすべて `PASS` で、`FAIL` が無い。
- [ ] **Step 6: S1 スライスのゲート**
```bash
G1 $MW/server_v8 2>&1 | tee $GL/p2-7-s1-g1.txt
(cd $MW && python3 tools/progression/check.py) > $GL/p2-7-s1-g2.txt 2>&1; echo "G2 exit=$?"
(cd $MW && python3 tools/nodegraph_sync/sync_nodegraph.py --check) > $GL/p2-7-s1-nodegraph.txt; echo "ng=$?"; tail -1 $GL/p2-7-s1-nodegraph.txt
(cd $MW && python3 tools/tier9_balance/run.py) > $GL/p2-7-s1-g6.txt; grep '^FAIL \[s1_semiconductor\] ' $GL/p2-7-s1-g6.txt | grep -v ' target out_of_range '; echo "s1-fail=$(grep '^FAIL \[s1_semiconductor\] ' $GL/p2-7-s1-g6.txt | grep -vc ' target out_of_range ')"
```
Expected:
- G1 は PASS。
- G2 は exit 1 のままでよい（ゴール「最終研究」はまだ無い）。ただし次は入手できる側に移る: 砂・金属シリコン・高純度シリコン・IPA・鋼鉄フレーム・ウェハ G1/G2 の 4 工程・ダイ Lv1/Lv2・ICチップLv1/Lv2・モジュール 4 種の Lv1・GPULv1。研究 7 ノードと機械オーバークロックも到達可能になる。
- ウェハ G3〜G5・ダイ/チップ Lv3〜Lv5・モジュールと GPU の Lv2 以上は、S2 の研究で製作上限が上がるまで赤のままでよい。
- 解放されないブロックの一覧に、専用機 5 種とクリーンルーム系 6 種が出ない。
- nodeGraph は `ng=0`・`== NODEGRAPH: PASS (0 failures)`（Step 4 の master-refine は research.json の座標と sortPriority しか変えないので、Step 3 の結果は崩れない）。
- G6 の S1 区間は `s1-fail=0`（照合 8 項目に `mismatch`・`lookup_failed` などの FAIL が無い）。G6 全体は S2・S3 の区間が空なので exit 1 のままでよい。S1 の `target out_of_range` の FAIL（クラス A の時間など）は Task 23 で直す。
- [ ] **Step 7: コミットして push する**
```bash
git -C $MW add design/tier9-balance/spec/05_research.json server_v8/mods/moorestechAlphaMod_8/master server_v8/mods/moorestechAlphaMod_8/localization/localization.csv server_v8/mods/moorestechAlphaMod_8/.mooreseditor/nodeGraph.v1.json
git -C $MW commit -m "master: Tier9 S1 の研究（モジュール・GPU設計・スタック拡張）と半導体製造の解放内容を入れる"
git -C $MW push
```

**完了判定:**
- `check=0` で、G1 が PASS。
- `$GL/p2-1-base-g2.txt` と `$GL/p2-7-s1-g2.txt` を比べると、上に挙げた S1 のアイテムが入手不能の一覧から消えている。
- nodeGraph の検査が `ng=0`（参照先の消えたノード 0 件・research.json と研究ノードが 1 対 1・配置と解放が一致）。
- G6 の S1 区間が `s1-fail=0`。
**量感:** JSON 約 +430 行（研究 6 ノードで約 300 行、半導体製造と機械オーバークロックのパッチ約 110 行）。これとは別に、sortPriority と UIPosition の再計算で数百行の数値が変わる。

---

### スライス S2（S2 引き継ぎ A〜E。F は Task 23、G は Task 24、H は録画の節）

### Task 18: 計算データ・GPU衛星・積荷表・発射台・ビルドメニュー「生産/宇宙」・S2 のレシピ（S2 引き継ぎ A〜D）

**Files:** Create: `moorestech_master:design/tier9-balance/spec/06_rocket_compute.json` / Modify: `.../master/{items,blocks,machineRecipes,buildMenu}.json`、`localization.csv`、`guids.json`

**Interfaces:**
- Consumes: Task 16 の `GPULv1`〜`GPULv5`。Task 13 の化学プラントのアイテム入口と液体タンク 0。
- 発射台のスキーマ（統合後の `VanillaSchema/blocks.yml` の `when: RocketLaunchPad`。統合前の S3 ブランチ `blocks.yml:1105-1154`）。必須キーは 9 つ: `requiredPower` `buildSeconds` `launchSeconds` `launchMaterials[{itemGuid,count}]` `outputSlotCount` `inventoryConnectors` `maxWireConnectionCount` `connectionRange` `connectionHeightRange`。`allowQualityModule` は無い。
- 読み込み時の検証 `OrbitalPayloadMasterValidator`・`RocketLaunchPadParamValidator`（`wf/S2S3.md` §(4)）:
  - 積荷表が空でないこと。
  - 資材が積荷と重ならないこと。
  - 個数がレベル 1 のスタック以下であること。
  - 全積荷の成果が出力口に入りきること。
- Produces:
  - アイテム: 計算データ `01a167cf-af88-74d3-9cd6-7d61b71052bb`、`GPU衛星Lv1`〜`GPU衛星Lv5`（`01a167cf-af8b…`・`af8e…`・`af91…`・`af94…`・`af97…`）。
  - levelFamily `GPU衛星` `01a167cf-af9a-78e5-9203-1c18ce94cdc6`。
  - 積荷表 5 行。
  - 発射台 `01a162b0-7769-7419-af24-bb35528b93d2` を RocketLaunchPad の全文にする。
  - 小分類「生産/宇宙」`d2000000-0000-4000-8000-000200000005`。
  - レシピキー: `ロケット筐体素材` `01a167cf-afa0-7add-8407-eec71456f4f3`、`ロケット燃料` `01a167cf-afa3-7c4f-82b7-c0d4f6d513c6`、`GPU衛星Lv1`〜`GPU衛星Lv5`（新規採番）。

- [ ] **Step 1: spec 06 を書く。** 値は S2 引き継ぎ A〜D の仮値（D12）。ブロックの `element` は「B. 発射台」の JSON 全文をそのまま使う。下書きとの違いは次のとおり。
  - GPU の名前は Task 16 の `GPULv1`〜`GPULv5` に揃える。
  - レシピ 7 本すべてに `levelInheritance: "None"` を入れる。下書き D には無く、入れないとロード時に落ちる（S1 のスキーマ `machineRecipes.yml:93`）。GPU衛星は継承レシピにしない（S2 の裁定 D13）。
  - 発射台は RocketLaunchPad で、ElectricMachine ではないので `allowQualityModule` を入れない。
  - 「生産/宇宙」を同じ spec に入れる。発射台の `category`/`subCategory` が参照するため。

  下書きに無い行として、ビルドメニューのローカライズ行（`buildMenuSubCategory.<guid>.name`。既存の「半導体」と同じキーの形）も入れる。
```json
{
  "buildMenuSubCategories": [
    {"category": "生産", "name": "宇宙", "en": "Space", "guid": "d2000000-0000-4000-8000-000200000005"}
  ],
  "items": [
    {"name": "計算データ", "guid": "01a167cf-af88-74d3-9cd6-7d61b71052bb", "en": "Compute Data", "sortPriority": 1601},
    {"name": "GPU衛星Lv1", "guid": "01a167cf-af8b-7894-8de6-593ee30289c0", "en": "GPU Satellite Lv1", "sortPriority": 1602},
    {"name": "GPU衛星Lv2", "guid": "01a167cf-af8e-7e95-b299-09eb712bee12", "en": "GPU Satellite Lv2", "sortPriority": 1603},
    {"name": "GPU衛星Lv3", "guid": "01a167cf-af91-7f62-93d1-e499b06f2398", "en": "GPU Satellite Lv3", "sortPriority": 1604},
    {"name": "GPU衛星Lv4", "guid": "01a167cf-af94-7e25-9654-825374c3923b", "en": "GPU Satellite Lv4", "sortPriority": 1605},
    {"name": "GPU衛星Lv5", "guid": "01a167cf-af97-7f67-bf53-7833b8d17a15", "en": "GPU Satellite Lv5", "sortPriority": 1606}
  ],
  "levelFamilies": [
    {"name": "GPU衛星", "guid": "01a167cf-af9a-78e5-9203-1c18ce94cdc6", "levels": ["GPU衛星Lv1", "GPU衛星Lv2", "GPU衛星Lv3", "GPU衛星Lv4", "GPU衛星Lv5"]}
  ],
  "orbitalPayloads": [
    {"payloadItemGuid": "@item:GPU衛星Lv1", "countPerLaunch": 1, "arrivalOutputs": [{"itemGuid": "@item:計算データ", "count": 10}]},
    {"payloadItemGuid": "@item:GPU衛星Lv2", "countPerLaunch": 1, "arrivalOutputs": [{"itemGuid": "@item:計算データ", "count": 20}]},
    {"payloadItemGuid": "@item:GPU衛星Lv3", "countPerLaunch": 1, "arrivalOutputs": [{"itemGuid": "@item:計算データ", "count": 40}]},
    {"payloadItemGuid": "@item:GPU衛星Lv4", "countPerLaunch": 1, "arrivalOutputs": [{"itemGuid": "@item:計算データ", "count": 80}]},
    {"payloadItemGuid": "@item:GPU衛星Lv5", "countPerLaunch": 1, "arrivalOutputs": [{"itemGuid": "@item:計算データ", "count": 160}]}
  ],
  "blocks": [
    {"name": "ロケット発射台", "en": "Rocket Launch Pad", "element": "<S2 引き継ぎ「B. 発射台」の JSON 全文（blockType から blockParam まで）。name・imagePath・initialUnlocked・placementsPerCost は既存行の値を同じ値で書く>"}
  ],
  "machineRecipes": [
    {"key": "ロケット筐体素材", "guid": "01a167cf-afa0-7add-8407-eec71456f4f3", "block": "@block:電気汎用工作装置", "time": 10, "levelInheritance": "None", "inputItems": [["@item:鋼鉄板", 4], ["@item:プラスチック", 2]], "inputFluids": [], "outputItems": [["@item:ロケット筐体素材", 1]], "outputFluids": []},
    {"key": "ロケット燃料", "guid": "01a167cf-afa3-7c4f-82b7-c0d4f6d513c6", "block": "@block:化学プラント", "time": 8, "levelInheritance": "None", "inputItems": [["@item:固形燃料", 2]], "inputFluids": [["@fluid:ガソリン", 100]], "outputItems": [["@item:ロケット燃料", 1]], "outputFluids": []},
    {"key": "GPU衛星Lv1", "block": "@block:電気汎用工作装置", "time": 30, "levelInheritance": "None", "inputItems": [["@item:GPULv1", 1], ["@item:鋼鉄板", 10], ["@item:コンピュータ", 5]], "inputFluids": [], "outputItems": [["@item:GPU衛星Lv1", 1]], "outputFluids": []},
    {"key": "GPU衛星Lv2", "block": "@block:電気汎用工作装置", "time": 30, "levelInheritance": "None", "inputItems": [["@item:GPULv2", 1], ["@item:鋼鉄板", 10], ["@item:コンピュータ", 5]], "inputFluids": [], "outputItems": [["@item:GPU衛星Lv2", 1]], "outputFluids": []},
    {"key": "GPU衛星Lv3", "block": "@block:電気汎用工作装置", "time": 30, "levelInheritance": "None", "inputItems": [["@item:GPULv3", 1], ["@item:鋼鉄板", 10], ["@item:コンピュータ", 5]], "inputFluids": [], "outputItems": [["@item:GPU衛星Lv3", 1]], "outputFluids": []},
    {"key": "GPU衛星Lv4", "block": "@block:電気汎用工作装置", "time": 30, "levelInheritance": "None", "inputItems": [["@item:GPULv4", 1], ["@item:鋼鉄板", 10], ["@item:コンピュータ", 5]], "inputFluids": [], "outputItems": [["@item:GPU衛星Lv4", 1]], "outputFluids": []},
    {"key": "GPU衛星Lv5", "block": "@block:電気汎用工作装置", "time": 30, "levelInheritance": "None", "inputItems": [["@item:GPULv5", 1], ["@item:鋼鉄板", 10], ["@item:コンピュータ", 5]], "inputFluids": [], "outputItems": [["@item:GPU衛星Lv5", 1]], "outputFluids": []}
  ]
}
```
  `element` の中身は文字列ではなく、S2 引き継ぎ「B」の JSON オブジェクト（`blockType` `category` `subCategory` `sortPriority` `blockSize` `blockPrefabAddressablesPath` `openable` `requiredItems` `blockParam`）に、`name` 以外の既存キー（`imagePath: ""`・`initialUnlocked: false`・`placementsPerCost: 1`）を足したものにする。上の山括弧は説明のための置き換え表記で、spec に文字列として書かない。

- [ ] **Step 2: 発射台の prefab の重複を確かめる**（S2 引き継ぎ「B」の末尾の指示）  Run: `grep -rn 'Block/Rocket"' $MW/server_v8/mods`  Expected: `moorestechAlphaMod_8` の中では発射台の 1 件だけ。
- [ ] **Step 3: 適用する**  Expected: `applied 6 spec files; issued 5 new guids`（GPU衛星のレシピ 5 本。他はすべて固定 GUID）
- [ ] **Step 4: 結果を確かめる**
```bash
python3 -c "
import json; m='$MOD/master/'; i=json.load(open(m+'items.json')); b=json.load(open(m+'blocks.json'))['data']; bm=json.load(open(m+'buildMenu.json'))
p=next(e for e in b if e['blockGuid']=='01a162b0-7769-7419-af24-bb35528b93d2')
print(p['blockType'], p['subCategory'], sorted(p['blockParam'].keys()))
print(len(i['orbitalPayloads']), [r['arrivalOutputs'][0]['count'] for r in i['orbitalPayloads']])
print([s['name'] for c in bm['categories'] if c['name']=='生産' for s in c['subCategories']])"
```
Expected:
```
RocketLaunchPad 宇宙 ['buildSeconds', 'connectionHeightRange', 'connectionRange', 'inventoryConnectors', 'launchMaterials', 'launchSeconds', 'maxWireConnectionCount', 'outputSlotCount', 'requiredPower']
5 [10, 20, 40, 80, 160]
['原始加工', '電気機械', '化学', '半導体', '宇宙']
```
- [ ] **Step 5: ロードを確かめる**（発射台を全文に置き換えたので、スライス末尾を待たずに読み込みを確かめる）  Run: `G1 $MW/server_v8`  Expected: PASS。`[OrbitalPayload]` や RocketLaunchPad の検証エラーが出たら、その PropertyPath の要素を spec 06 で直す。
- [ ] **Step 6: コミット**
```bash
git -C $MW add design/tier9-balance/spec/06_rocket_compute.json design/tier9-balance/guids.json server_v8/mods/moorestechAlphaMod_8/master server_v8/mods/moorestechAlphaMod_8/localization/localization.csv
git -C $MW commit -m "master: 計算データ・GPU衛星・積荷表・ロケット発射台・生産/宇宙と S2 のレシピを入れる"
```

**完了判定:** Step 4 の 3 行と、Step 5 の G1 PASS。
**量感:** JSON 約 +440 行。内訳はアイテム 6 件約 78 行、ファミリー約 10 行、積荷表約 50 行、発射台約 75 行、レシピ 7 本約 210 行、ビルドメニュー 5 行。localization +8 行。

### Task 19: ロケット技術と半導体のアップグレード研究 Lv2〜Lv5、S2 スライスのゲート（S2 引き継ぎ E。裁定4）

**Files:** Create: `moorestech_master:design/tier9-balance/spec/07_rocket_research.json` / Modify: `.../master/research.json`、`localization.csv`、`guids.json`、`.../.mooreseditor/nodeGraph.v1.json`（Step 3 の同期） / Create（G6 の S2 区間。Step 5）: `moorestech_master:tools/tier9_balance/sections/{recipe_rates,s2/__init__,s2/launch_plan,s2/factory_load}.py`、`tools/tier9_balance/tests/{s2s3_fixture,test_s2_compute_loop}.py` / Modify（Step 5。Task 3 の空の区間を全文置き換え）: `tools/tier9_balance/sections/s2_compute_loop.py` / Create（控え）: `$GL/p2-9-s2-g1.txt`、`$GL/p2-9-s2-g2.txt`、`$GL/p2-9-s2-nodegraph.txt`、`$GL/p2-9-s2-g6.txt`

**Interfaces:**
- Consumes:
  - S1 のゲームアクション `unlockLevelInheritanceCraftLevel {targetItemGuids[], level}`（`VanillaSchema/ref/gameAction.yml:191-204`）。
  - 検証 `MachineUpgradeGameActionValidator.cs:49-69`: target は levelFamily の基準アイテムで、かつ InheritUpToUnlocked レシピの出力でなければならない。`level` は 1 からファミリー長まで。
  - Task 16 の 5 製品の基準アイテム（品質・速度・省エネ・生産性モジュールの Lv1、`GPULv1`）。
  - Task 17 の GPU設計。
  - Task 18 の levelFamily「GPU衛星」（G6 の S2 区間は GPU衛星をこの族のレベルで判別する。名前の前方一致では決めない。判断記録 A32）。Task 2 の `tools/master_common/research_ancestors.py`。
- Produces:
  - 研究「ロケット技術」`01a167cf-af9d-7f31-817c-3069b4f6e499`。Task 22 の「軌道エレベーターの建設」の前提になる。
  - 研究 `半導体のアップグレード研究Lv2`〜`Lv5`（新規採番。台帳キーは `research:半導体のアップグレード研究LvN`）。
  - G6 の S2 区間 `sections/s2_compute_loop.evaluate(view) -> SectionResult('s2_compute_loop', checks, hours: Resolved)`（Task 3 の区間の形。Step 5）。共有の `sections/recipe_rates.py`（`output_per_second(view, block, output) -> Resolved[RecipeRate(recipe, output_guid, per_second)]`〈レシピが 1 本に決まらなければ `lookup_failed`、毎秒が 0 以下なら `not_positive`〉・`recipe_inputs(recipe)`・`add_demand(demand, guid, 毎秒)`・`add_entry_demand(demand, 行, 毎秒の回数)`・`chain_machines(demand, rate) -> 台数`〈需要は GUID で持ち、上流の需要の積み上げはこの 1 本〉・`prerequisites_before(view) -> {研究名: 前提の GUID 集合}`〈Task 2 の `master_common.research_ancestors` から自分を引いたもの〉。名前と GUID の相互引き・積荷表の行は Task 3 の `MasterView` を使う。判断記録 A31・A32）と最小マスタ `tests/s2s3_fixture.py`（`build_master()`・`view_of(master)`・`guid(name)`・`find(rows, prefix)`）は Task 22 の S3 区間も使う。

**下書きに対する訂正:**
- S2 下書き E は「S1 の上位モジュールのレシピを `unlockMachineRecipe` で解放する」と書いているが、これは古い。S1 は製品ごとに `InheritUpToUnlocked` のレシピを 1 本だけ持つので、解放するレシピはもう無い。各ノードは `unlockLevelInheritanceCraftLevel` で、5 製品の製作上限を 1 段ずつ上げる（裁定4）。
- あわせて、その段の 5 製品の表示を解放する。S1 の判断記録「研究で解放するモジュール・GPU のレシピ表示は Lv1 だけ。Lv2 以上は S2 の研究が解放する」に従う。
- ロケット技術の前提は、下書きの仮の「半導体製造」から「GPU設計」に付け替える（下書き E の補足「GPU を解放する研究は S1 が決めるので付け替える」）。
- GPU衛星 Lv2〜Lv5 のレシピは、解放同期則でアップグレード研究 LvN へ移る見込み（推測。入力の GPU LvN の表示がアップグレード LvN にあるため）。下書きの「ロケット技術が 5 本とも解放する」とは違う。spec は最初から LvN のノードに置き、Step 2 の sync で `MOVE` が 0 になることで確かめる。

- [ ] **Step 1: spec 07 を書く。** コストは仮値。計算データは段ごとに倍にし、Task 23 で G6 が確定する。ロケット技術のコストは S2 下書き E の仮値のまま。
```json
{
  "research": [
    {"name": "ロケット技術", "guid": "01a167cf-af9d-7f31-817c-3069b4f6e499", "en": "Rocket Technology",
     "description": "ロケット発射台と、ロケットの筐体素材・燃料・GPU衛星のレシピを解放する。GPU衛星を打ち上げると計算データが届く",
     "descEn": "Unlocks the rocket launch pad and recipes for rocket casings, fuel and GPU satellites. Launched GPU satellites return compute data",
     "prev": ["@research:GPU設計"],
     "consumeItems": [["@item:鋼鉄板", 1000], ["@item:コンピュータ", 300], ["@item:ICチップLv1", 100]],
     "clearedActions": [
       {"gameActionType": "unlockBlock", "gameActionParam": {"unlockBlockGuids": ["@block:ロケット発射台"]}},
       {"gameActionType": "unlockMachineRecipe", "gameActionParam": {"unlockMachineRecipeGuids": ["@recipe:ロケット筐体素材", "@recipe:ロケット燃料", "@recipe:GPU衛星Lv1"]}},
       {"gameActionType": "unlockItemRecipeView", "gameActionParam": {"unlockItemGuids": ["@item:ロケット筐体素材", "@item:ロケット燃料", "@item:計算データ", "@item:GPU衛星Lv1", "@item:GPU衛星Lv2", "@item:GPU衛星Lv3", "@item:GPU衛星Lv4", "@item:GPU衛星Lv5"]}}],
     "iconItem": "@item:ロケット燃料"},
    {"name": "半導体のアップグレード研究Lv2", "en": "Semiconductor Upgrade Lv2",
     "description": "モジュールとGPUをLv2まで作れるようにする", "descEn": "Lets modules and GPUs be crafted up to Lv2",
     "prev": ["@research:ロケット技術"], "consumeItems": [["@item:計算データ", 100]],
     "clearedActions": [
       {"gameActionType": "unlockLevelInheritanceCraftLevel", "gameActionParam": {"targetItemGuids": ["@item:品質モジュールLv1", "@item:速度モジュールLv1", "@item:省エネモジュールLv1", "@item:生産性モジュールLv1", "@item:GPULv1"], "level": 2}},
       {"gameActionType": "unlockMachineRecipe", "gameActionParam": {"unlockMachineRecipeGuids": ["@recipe:GPU衛星Lv2"]}},
       {"gameActionType": "unlockItemRecipeView", "gameActionParam": {"unlockItemGuids": ["@item:品質モジュールLv2", "@item:速度モジュールLv2", "@item:省エネモジュールLv2", "@item:生産性モジュールLv2", "@item:GPULv2"]}}],
     "iconItem": "@item:GPULv2"}
  ]
}
```
  Lv3〜Lv5 は Lv2 の行を写し、次の 6 か所だけ変える。
  - `name`・`en`・`description`・`descEn` の「Lv2」を「LvN」に。
  - `prev` を 1 つ前の段のノードに（Lv3 は `@research:半導体のアップグレード研究Lv2`）。
  - 計算データを Lv3 200・Lv4 400・Lv5 800 に。
  - `level` を N に。
  - `@recipe:GPU衛星LvN` に。
  - 表示 5 件と `iconItem` を LvN に。
- [ ] **Step 2: 適用し、研究の解放を同期する**（S1 と S2 の研究がそろったので sync が通る）
```bash
cd $B && python3 apply/apply_spec.py --mod-dir $MOD --spec-dir spec --ledger guids.json
cd $MW && python3 tools/research_sync/sync_machine_recipe_unlocks.py | grep -E "^(MOVE|ORPHAN|STALE|DEAD-END)|moves," ; python3 tools/research_sync/sync_machine_recipe_unlocks.py --check; echo "check=$?"
```
Expected: apply が `applied 7 spec files; issued 4 new guids`。sync は `0 moves, … 0 orphans, 0 stale` で、`DEAD-END` が無い。`check=0`。`MOVE` が出たら Task 17 Step 2 と同じ手順で spec 07 を直す。
- [ ] **Step 3: nodeGraph を統合マスタに合わせる**（判断記録 A21。master-refine はこの Task に無いので、ゲートの前に流す）
```bash
cd $MW && python3 tools/nodegraph_sync/sync_nodegraph.py > $GL/p2-9-s2-nodegraph-sync.txt; echo "ng=$?"; cat $GL/p2-9-s2-nodegraph-sync.txt
git -C $MW diff --stat -- server_v8/mods/moorestechAlphaMod_8/.mooreseditor
```
Expected:
- `ng=0`。末尾は `== NODEGRAPH: PASS (0 failures)` で、`UNPLACED` 行が無い。diff は `nodeGraph.v1.json` の 1 ファイルだけ。
- 1 行目の見込み（推測）: `research nodes +5`（ロケット技術と半導体のアップグレード研究 Lv2〜Lv5）、`dependency edges +5`（ロケット技術 ← GPU設計、Lv2 ← ロケット技術、Lv3〜Lv5 ← 1 つ前の段）。`moved` にロケット発射台（Task 17 Step 3 の退避先からロケット技術の下へ）。GPU衛星 Lv1〜Lv5・計算データなど表示の解放のノードは `moved` か `added` に入る。
- `UNPLACED` が出たら、検査は緩めず、`placement.py` の候補の範囲（研究は `range(1, 9)`、解放は `range(0, 12)`）を広げて流し直す。
- [ ] **Step 4: S2 スライスのゲート**
```bash
G1 $MW/server_v8 2>&1 | tee $GL/p2-9-s2-g1.txt
(cd $MW && python3 tools/progression/check.py) > $GL/p2-9-s2-g2.txt 2>&1; echo "G2 exit=$?"
(cd $MW && python3 tools/nodegraph_sync/sync_nodegraph.py --check) > $GL/p2-9-s2-nodegraph.txt; echo "ng=$?"; tail -1 $GL/p2-9-s2-nodegraph.txt
```
Expected:
- G1 は PASS（`[MachineUpgrade]` の検証エラーが無い）。
- G2 は exit 1 のままでよい（ゴールは S3 で入る）。ただし次は入手できる側に移る: 計算データ（積荷表のモデル）、GPU衛星Lv1〜Lv5、ウェハ G3〜G5、ダイ/チップ Lv3〜Lv5、モジュール 4 種と GPU の Lv2〜Lv5（継承と製作上限のモデル）。
- 研究ロケット技術とアップグレード Lv2〜Lv5 は到達可能。
- 解放されないブロックの一覧から発射台が消える。
- nodeGraph は `ng=0`・`== NODEGRAPH: PASS (0 failures)`。
- [ ] **Step 5: G6 の S2 区間を書く**（判断記録 A25。S2 のアイテム・発射台・研究がそろうのはこの Task なのでここで書く。値の確定は Task 23）
  - 所要時間: 計算データを消費する研究ごとに便数を出す。払うのは、前提の研究（祖先）で解放済みの GPU衛星のうち、1 便の計算データが最も多いもの。便数の合計 × 1 便の秒（`buildSeconds`＋`launchSeconds`）÷ 発射台 1 台を時間にする。便数・1 便の秒・時間はどれも `Resolved` で持ち、1 つでも引けなければ時間はその状態を引き継ぐ（判断記録 A31）。
  - GPU衛星の判別: levelFamily「GPU衛星」の各レベルを衛星とし、1 便の計算データは積荷表の行を GUID で引く（`MasterView.payload_row`）。積荷表の行が無いレベルは `target lookup_failed` の行を出す。名前の前方一致と既定値 `''` では判定しない（判断記録 A32）。
  - 研究コストの照合: 計算データを消費する研究の顔ぶれが `COMPUTE_CONSUMERS`（アップグレード Lv2〜Lv5・軌道エレベーターの建設・反物質爆弾）と一致すること。アップグレード LvN の `unlockLevelInheritanceCraftLevel.level` が N であること（Lv2 の行を写して作るので写し残しを拾う。action が 0 個と 2 個以上は `checks.single` で理由を分ける）。どちらも `checks.matches` の行で、コストが無いものは `match lookup_failed`、想定外の消費先は `match mismatch`。
  - 工場の台数: 発射台 1 台を止めずに回すのに要る台数を `target_range` で 1 レシピ 4 台以下と判定する。対象は打ち上げ資材と GPU衛星Lv1 の直のレシピと、その 1 段上流（鋼鉄板・プラスチック・ガソリン・固形燃料）。

`tools/tier9_balance/sections/recipe_rates.py`（S2・S3 の区間が共有する）:
```python
#!/usr/bin/env python3
# S2・S3 区間が共有するレシピの読み方。1台あたりの毎秒の出力、上流の需要の積み上げ（GUID で持つ）、研究の前提の集合
# Recipe helpers shared by the S2 and S3 sections: per-machine output per second, upstream demand (keyed by GUID) and research prerequisites
import sys
from pathlib import Path

import checks
from master_view import entry_amount, entry_guid

# 研究の祖先集合は G2 と共有する tools/master_common の 1 本を使うので、tools/ を検索パスへ足す（判断記録 A32）
# Research ancestors come from the single tools/master_common helper shared with G2, so put tools/ on the search path (decision A32)
TOOLS_DIR = Path(__file__).resolve().parents[2]
if str(TOOLS_DIR) not in sys.path:
    sys.path.insert(0, str(TOOLS_DIR))
from master_common.research_ancestors import ancestors_including_self  # noqa: E402


class RecipeRate:
    def __init__(self, recipe, output_guid, per_second):
        self.recipe = recipe
        self.output_guid = output_guid
        self.per_second = per_second


def output_per_second(view, block_name, output_name):
    # 機械1台が毎秒出す量を RecipeRate の Resolved で返す。レシピが1本に決まらなければ lookup_failed、毎秒が0以下なら not_positive
    # Output per second of one machine as a Resolved RecipeRate; lookup_failed unless one recipe, not_positive when the rate is zero or less
    recipe = view.recipe_by_output(block_name, output_name)
    if not recipe.ok:
        return recipe
    guid = view.guid_of(output_name).value
    produced = sum(entry_amount(o) for o in recipe.value['outputItems'] + recipe.value['outputFluids'] if entry_guid(o) == guid)
    rate = checks.require_positive(checks.resolved(produced / recipe.value['time']), f'{block_name}（{output_name}）の毎秒')
    return rate.map(lambda per_second: RecipeRate(recipe.value, guid, per_second))


def recipe_inputs(recipe):
    return recipe['inputItems'] + recipe['inputFluids']


def add_demand(demand, guid, per_second):
    demand[guid] = demand.get(guid, 0.0) + per_second


def add_entry_demand(demand, entries, runs_per_second):
    # 1秒あたり runs_per_second 回ぶんの行（レシピの入力・打ち上げ資材）を、GUID ごとの毎秒の需要へ足す
    # Add runs_per_second runs of the rows (recipe inputs, launch materials) to the per-second demand keyed by GUID
    for entry in entries:
        add_demand(demand, entry_guid(entry), runs_per_second * entry_amount(entry))


def chain_machines(demand, rate):
    # 需要を満たす台数を出し、その台数で回す分の入力を上流の需要へ積む（下流から順に呼ぶ）
    # Count machines meeting the demand and add the inputs they consume to the upstream demand (call downstream first)
    machines = demand.get(rate.output_guid, 0.0) / rate.per_second
    add_entry_demand(demand, recipe_inputs(rate.recipe), machines / rate.recipe['time'])
    return machines


def prerequisites_before(view):
    # 研究名 → 前提を末端までたどった GUID の集合（自分は除く。自分の解放は研究前には使えない）
    # Research name -> GUIDs of all prerequisites up to the roots, excluding itself because its own unlocks are not usable before it
    ancestors = ancestors_including_self(list(view.research.values()))
    return {name: ancestors[n['researchNodeGuid']] - {n['researchNodeGuid']} for name, n in view.research.items()}
```

`tools/tier9_balance/sections/s2/__init__.py`:
```python
# S2 計算データのループの部品（打ち上げの便数と所要時間、1便を回す工場の台数）
# Parts of the S2 compute-data loop (launch count and hours, and the factory size that keeps one launch going)
```

`tools/tier9_balance/sections/s2/launch_plan.py`:
```python
#!/usr/bin/env python3
# 計算データを消費する研究ごとに、前提の研究で解放済みの最上位の GPU衛星で払う便数を出し、所要時間に直す
# For each research that consumes compute data, count launches of the best GPU satellite its prerequisites unlock, then turn them into hours
import math

import checks
from master_view import entry_amount, entry_guid
from sections import recipe_rates

COMPUTE_ITEM = '計算データ'
# GPU衛星は levelFamily「GPU衛星」の各レベルで判別する（名前の前方一致では決めない。判断記録 A32）
# GPU satellites are the levels of levelFamily "GPU衛星" (never decided by name prefix; decision A32)
SATELLITE_FAMILY = 'GPU衛星'
UPGRADE_PREFIX = '半導体のアップグレード研究Lv'
# 計算データを消費する研究の正本（S2 引き継ぎ E と S3 の R12）。ここに無い消費先も、ここにあって消費しない研究も FAIL
# The authoritative compute-data consumers (S2 handoff E and S3 R12); an unlisted consumer or a listed non-consumer both fail
COMPUTE_CONSUMERS = tuple(f'{UPGRADE_PREFIX}{n}' for n in range(2, 6)) + ('軌道エレベーターの建設', '反物質爆弾')
# 発射台は1台で回す（S2 引き継ぎ F「1便/135秒を1台で回す」）
# One launch pad keeps the loop going (S2 handoff F, "one launch per 135 s on one pad")
LAUNCH_PADS = 1


def evaluate_launches(view):
    # 戻り値は（検査の行, 所要時間[h] の Resolved, 1便の秒の Resolved）
    # Returns (check rows, hours as a Resolved, seconds per launch as a Resolved)
    cycle = launch_cycle_seconds(view)
    rows = [] if cycle.ok else [checks.unreadable('target', 'ロケット発射台の1便の秒', cycle)]
    payload_rows, data_per_launch = satellite_payloads(view)
    rows += payload_rows + consumer_set_rows(view) + upgrade_level_rows(view)
    before = recipe_rates.prerequisites_before(view)
    unlocker = data_per_launch.map(lambda data: satellite_unlockers(view, set(data)))
    launches = []
    for node in (n for n in COMPUTE_CONSUMERS if n in view.research):
        count = unlocker.then(lambda found: launches_for(view, node, data_per_launch.value, found, before[node]))
        rows += [] if count.ok else [checks.unreadable('target', f'{node} の便数', count)]
        launches.append(count)
    if checks.failures([cycle] + launches):
        return rows, checks.carried_failure([cycle] + launches, 'S2 の所要時間'), cycle
    return rows, checks.resolved(sum(c.value for c in launches) * cycle.value / LAUNCH_PADS / 3600), cycle


def launches_for(view, node, data_per_launch, unlocker, before):
    # 前提の研究で解放済みの GPU衛星のうち、1便の計算データが最も多いもので払う（研究順に依らず必ず使える衛星で数える）
    # Pay with the unlocked satellite returning the most data per launch (counting only satellites guaranteed regardless of research order)
    cost = view.research_cost(node, COMPUTE_ITEM)
    usable = [data for satellite, data in data_per_launch.items() if unlocker.get(satellite) in before and data > 0]
    if not cost.ok:
        return cost
    if not usable:
        return checks.unresolved('前提の研究で解放され、積荷表で計算データが届く GPU衛星が無い')
    return checks.resolved(math.ceil(cost.value / max(usable)))


def launch_cycle_seconds(view):
    build, launch = view.block_param('ロケット発射台', 'buildSeconds'), view.block_param('ロケット発射台', 'launchSeconds')
    if checks.failures([build, launch]):
        return checks.carried_failure([build, launch], 'ロケット発射台の1便の秒')
    return checks.require_positive(checks.resolved(build.value + launch.value), 'ロケット発射台の1便の秒')


def satellite_payloads(view):
    # 戻り値は（積荷表の行が無い衛星の検査の行, 衛星 GUID → 1便で届く計算データの数 の Resolved）
    # Returns (rows for satellites lacking a payload row, a Resolved of satellite GUID -> compute data per launch)
    family = checks.field(view.families, SATELLITE_FAMILY, 'items.json の levelFamilies')
    compute = view.guid_of(COMPUTE_ITEM)
    if checks.failures([family, compute]):
        return [], checks.carried_failure([family, compute], 'GPU衛星の積荷')
    payloads = {g: view.payload_row(g) for g in family.value['levelItemGuids']}
    rows = [checks.unreadable('target', f'{view.label_of(g)} の積荷表の行', r) for g, r in payloads.items() if not r.ok]
    data = {g: sum(entry_amount(o) for o in r.value['arrivalOutputs'] if entry_guid(o) == compute.value)
            for g, r in payloads.items() if r.ok}
    return rows, checks.resolved(data)


def satellite_unlockers(view, satellites):
    # 出力が GPU衛星のレシピを unlockMachineRecipe で解放する研究の GUID（衛星 GUID → 研究 GUID）
    # The research GUID whose unlockMachineRecipe unlocks a recipe that outputs a GPU satellite (satellite GUID -> research GUID)
    recipe_outputs = {r['machineRecipeGuid']: [entry_guid(o) for o in r['outputItems']] for r in view.recipes}
    unlocker = {}
    for node in view.research.values():
        for action in node['clearedActions']:
            if action['gameActionType'] != 'unlockMachineRecipe':
                continue
            for guid in action['gameActionParam']['unlockMachineRecipeGuids']:
                # silent-ok: 研究が指す未知のレシピ GUID は衛星の解放に数えない / 未知の GUID は G1 のロード検証が止め、衛星の解放が欠ければ「便数」の行が FAIL する
                unlocker.update({o: node['researchNodeGuid'] for o in recipe_outputs.get(guid, []) if o in satellites})
    return unlocker


def consumer_set_rows(view):
    compute = view.guid_of(COMPUTE_ITEM)
    if not compute.ok:
        return [checks.unreadable('match', '計算データを消費する研究の数', compute)]
    actual = {name for name, n in view.research.items() if any(c['itemGuid'] == compute.value for c in n['consumeItems'])}
    rows = [checks.lookup_failed('match', f'{n} の計算データのコスト', 'research.json の consumeItems に計算データが無い')
            for n in COMPUTE_CONSUMERS if n not in actual]
    rows += [checks.Check('match', checks.MISMATCH, f'{n} の計算データのコスト', '想定外の消費先（COMPUTE_CONSUMERS に無い）')
             for n in sorted(actual - set(COMPUTE_CONSUMERS))]
    return rows or [checks.matches('計算データを消費する研究の数', len(COMPUTE_CONSUMERS), checks.resolved(len(actual)), 0)]


def upgrade_level_rows(view):
    # アップグレード LvN の製作上限は N（Lv2 の行を写して作るので、写し残しをここで拾う）。action が 0 個と 2 個以上は理由を分ける
    # Upgrade LvN raises the craft cap to N (copied from Lv2, so a leftover is caught here); zero and several actions differ in reason
    rows = []
    for n in range(2, 6):
        name = f'{UPGRADE_PREFIX}{n}'
        if name not in view.research:
            rows.append(checks.lookup_failed('match', f'{name} の製作上限', f'研究「{name}」が research.json に無い'))
            continue
        levels = [a['gameActionParam']['level'] for a in view.research[name]['clearedActions']
                  if a['gameActionType'] == 'unlockLevelInheritanceCraftLevel']
        rows.append(checks.matches(f'{name} の製作上限', n, checks.single(levels, f'{name} の unlockLevelInheritanceCraftLevel'), 0))
    return rows
```

`tools/tier9_balance/sections/s2/factory_load.py`:
```python
#!/usr/bin/env python3
# 発射台1台を止めずに回すのに要る機械の台数（S2 引き継ぎ F）。打ち上げ資材と GPU衛星の直のレシピ、その1段上流を数える
# Machines needed to keep one launch pad busy (S2 handoff F): the direct recipes for launch materials and the satellite, plus one step upstream
import checks
from sections import recipe_rates

# 1レシピあたりの台数の上限。超えると既存の工場規模から外れる（agent前提。S2 引き継ぎ F の作者メモ「変な工場建設は要求しない」）
# Max machines per recipe; above it the factory leaves the existing scale (agent assumption from the S2 handoff F author note)
MAX_MACHINES_PER_RECIPE = 4.0
# 下流から順に並べる（上流の需要は下流のレシピの入力から積み上がる）
# Ordered downstream first, because upstream demand accumulates from downstream inputs
LAUNCH_RECIPES = (('電気汎用工作装置', 'ロケット筐体素材'), ('化学プラント', 'ロケット燃料'), ('電気汎用工作装置', 'GPU衛星Lv1'))
UPSTREAM_RECIPES = (('電気炉', '鋼鉄板'), ('化学プラント', 'プラスチック'), ('石油蒸留機', 'ガソリン'), ('化学プラント', '固形燃料'))
FACTORY_SATELLITE = 'GPU衛星Lv1'


def check_factory(view, cycle_seconds):
    if not cycle_seconds.ok:
        return [checks.unreadable('target', '1便を回す工場の台数', cycle_seconds)]
    cycle = cycle_seconds.value
    demand, rows = launch_demand(view, cycle)
    for block, output in LAUNCH_RECIPES + UPSTREAM_RECIPES:
        name = f'1便/{cycle}秒に要る{block}（{output}）の台数'
        rate = recipe_rates.output_per_second(view, block, output)
        if not rate.ok:
            rows.append(checks.unreadable('target', name, rate))
            continue
        machines = recipe_rates.chain_machines(demand, rate.value)
        rows.append(checks.target_range(name, checks.resolved(round(machines, 2)), 0.0, MAX_MACHINES_PER_RECIPE))
    return rows


def launch_demand(view, cycle):
    # 1便ごとに消える打ち上げ資材と、積荷の GPU衛星（1便の個数は積荷表の countPerLaunch）を GUID ごとの毎秒の需要にする
    # Per-second demand by GUID: launch materials consumed per launch plus the GPU satellite payload (countPerLaunch from the payload table)
    rows, demand = [], {}
    materials = view.block_param('ロケット発射台', 'launchMaterials')
    if materials.ok:
        recipe_rates.add_entry_demand(demand, materials.value, 1 / cycle)
    else:
        rows.append(checks.unreadable('target', 'ロケット発射台の launchMaterials', materials))
    payload = view.guid_of(FACTORY_SATELLITE).then(view.payload_row)
    if payload.ok:
        recipe_rates.add_demand(demand, payload.value['payloadItemGuid'], payload.value['countPerLaunch'] / cycle)
    else:
        rows.append(checks.unreadable('target', f'{FACTORY_SATELLITE} の積荷表の行', payload))
    return demand, rows
```

`tools/tier9_balance/sections/s2_compute_loop.py`（Task 3 の空の区間を全文置き換える）:
```python
#!/usr/bin/env python3
# S2 計算データのループ（打ち上げ→計算データ→研究で消費）。研究コストの照合・1便を回す工場の台数・所要時間を返す
# S2 compute-data loop (launch, compute data, consumed by research): research-cost matching, factory size per launch and hours
# 所要時間は「計算データを消費する研究の便数の合計 × 1便の秒 ÷ 発射台の台数」の Resolved（判断記録 A25・A31）
# The hours are a Resolved of "sum of launches over compute-consuming research x seconds per launch / pads" (decisions A25, A31)
from checks import SectionResult
from sections.s2 import factory_load, launch_plan


def evaluate(view):
    rows, hours, cycle = launch_plan.evaluate_launches(view)
    rows += factory_load.check_factory(view, cycle)
    return SectionResult('s2_compute_loop', rows, hours)
```

  自己テスト（Task 3 の `tests/` の流儀。最小マスタを一時ディレクトリに書き、本物の `MasterView` で読む。値を 1 か所食い違わせると該当の行が FAIL になることを確かめる）:

`tools/tier9_balance/tests/s2s3_fixture.py`（S2・S3 の自己テストが共有する最小マスタ）:
```python
#!/usr/bin/env python3
# S2・S3 区間の自己テスト用の最小マスタ。区間の検査が全部通る値で作り、テストが1か所だけ書き換えて食い違いを作る
# Minimal master for the S2/S3 section self-tests; built with values that pass every section check, and each test breaks one spot
import json
import os
import tempfile
import uuid

from master_view import MasterView

ITEMS = ['計算データ', 'ロケット筐体素材', 'ロケット燃料', 'コンピュータ', '鋼鉄板', 'プラスチック', '固形燃料', '鉄板',
         'ICチップLv1', '制御モータ', '反物質', '反物質爆弾', 'グラフェンケーブル', 'グラフェンシート', '使用済み銅板', '高純度炭素', '石炭', '銅板',
         '発展電子回路', '被覆銅線', 'モーター'] + [f'GPU衛星Lv{n}' for n in range(1, 6)] + [f'GPULv{n}' for n in range(1, 6)]
FLUIDS = ['水', '重水素', 'ガソリン', 'ナフサ', '残油', '原油']
UPGRADES = [f'半導体のアップグレード研究Lv{n}' for n in range(2, 6)]


def guid(name):
    return str(uuid.uuid5(uuid.NAMESPACE_URL, name))


def stacks(pairs, key):
    # [(名前, 量)] をマスタの行の形にする。液体は amount、アイテムは count
    # Turn [(name, amount)] into master rows; fluids use amount and items use count
    return [{('fluidGuid' if n in FLUIDS else 'itemGuid'): guid(n), key: a} for n, a in pairs]


def recipe(block, time, inputs, outputs):
    items_in, fluids_in = [p for p in inputs if p[0] not in FLUIDS], [p for p in inputs if p[0] in FLUIDS]
    items_out, fluids_out = [p for p in outputs if p[0] not in FLUIDS], [p for p in outputs if p[0] in FLUIDS]
    return {'machineRecipeGuid': guid(f'recipe:{block}:{outputs[0][0]}'), 'blockGuid': guid(block), 'time': time,
            'inputItems': stacks(items_in, 'count'), 'inputFluids': stacks(fluids_in, 'amount'),
            'outputItems': stacks(items_out, 'count'), 'outputFluids': stacks(fluids_out, 'amount')}


def node(name, prev, costs, actions):
    return {'researchNodeGuid': guid(name), 'researchNodeName': name, 'prevResearchNodeGuids': [guid(p) for p in prev],
            'consumeItems': [{'itemGuid': guid(n), 'itemCount': c} for n, c in costs], 'clearedActions': actions}


def unlock_recipes(*outputs):
    return {'gameActionType': 'unlockMachineRecipe', 'gameActionParam': {'unlockMachineRecipeGuids': [guid(f'recipe:電気汎用工作装置:{o}') for o in outputs]}}


def unlock_block(name):
    return {'gameActionType': 'unlockBlock', 'gameActionParam': {'unlockBlockGuids': [guid(name)]}}


def build_master():
    blocks = {
        'ロケット発射台': {'requiredPower': 200, 'buildSeconds': 120, 'launchSeconds': 15,
                    'launchMaterials': stacks([('ロケット筐体素材', 20), ('ロケット燃料', 20), ('コンピュータ', 5)], 'count')},
        'ガソリンエンジン発電機': {'fuelFluids': [{'fluidGuid': guid('ガソリン'), 'amount': 10, 'time': 4, 'power': 1000}]},
        '核融合炉': {'fuelFluids': [{'fluidGuid': guid('重水素'), 'amount': 10, 'time': 2, 'power': 20000}]},
        '重水素抽出装置': {'requiredPower': 4000}, '反物質製造装置': {'requiredPower': 12000},
        '宇宙エレベーター拠点': {'cablesPerElevator': 1000, 'climbSeconds': 360, 'requiredPower': 1000},
        '電気汎用工作装置': {}, '化学プラント': {}, '電気炉': {}, '石油蒸留機': {},
    }
    recipes = [
        recipe('電気汎用工作装置', 10, [('鋼鉄板', 4), ('プラスチック', 2)], [('ロケット筐体素材', 1)]),
        recipe('化学プラント', 8, [('ガソリン', 100), ('固形燃料', 2)], [('ロケット燃料', 1)]),
        recipe('電気炉', 3, [('鉄板', 5)], [('鋼鉄板', 1)]),
        recipe('化学プラント', 4, [('ナフサ', 5)], [('プラスチック', 2)]),
        recipe('石油蒸留機', 5, [('原油', 100)], [('ナフサ', 20), ('ガソリン', 30), ('残油', 50)]),
        recipe('化学プラント', 2, [('残油', 10)], [('固形燃料', 1)]),
        recipe('電気汎用工作装置', 3, [('発展電子回路', 10), ('被覆銅線', 10)], [('コンピュータ', 1)]),
        recipe('電気汎用工作装置', 5, [('発展電子回路', 10), ('モーター', 10)], [('制御モータ', 1)]),
        recipe('重水素抽出装置', 8, [('水', 100)], [('重水素', 10)]),
        recipe('反物質製造装置', 30, [('重水素', 50)], [('反物質', 1)]),
        recipe('電気汎用工作装置', 60, [('反物質', 20), ('ICチップLv1', 50), ('鋼鉄板', 100), ('コンピュータ', 20)], [('反物質爆弾', 1)]),
        recipe('電気炉', 3, [('石炭', 2)], [('高純度炭素', 1)]),
        recipe('電気炉', 6, [('高純度炭素', 2), ('銅板', 1)], [('グラフェンシート', 1), ('使用済み銅板', 1)]),
        recipe('電気汎用工作装置', 6, [('グラフェンシート', 1)], [('グラフェンケーブル', 5)]),
    ] + [recipe('電気汎用工作装置', 30, [(f'GPULv{n}', 1), ('鋼鉄板', 10), ('コンピュータ', 5)], [(f'GPU衛星Lv{n}', 1)]) for n in range(1, 6)]
    research = [node('ロケット技術', [], [('鋼鉄板', 1000)], [unlock_recipes('GPU衛星Lv1')])]
    for n, cost in zip(range(2, 6), (100, 200, 400, 800)):
        level = {'gameActionType': 'unlockLevelInheritanceCraftLevel', 'gameActionParam': {'targetItemGuids': [guid('GPULv1')], 'level': n}}
        research.append(node(f'半導体のアップグレード研究Lv{n}', [UPGRADES[n - 3] if n > 2 else 'ロケット技術'], [('計算データ', cost)],
                             [level, unlock_recipes(f'GPU衛星Lv{n}')]))
    research += [
        node('金属超精密加工', [], [('ICチップLv1', 300), ('鋼鉄板', 1500), ('制御モータ', 300)], [unlock_block('金属3Dプリンタ')]),
        node('核融合炉', ['金属超精密加工'], [('ICチップLv1', 600), ('鋼鉄板', 3000), ('コンピュータ', 600), ('制御モータ', 600)], [unlock_block('核融合炉')]),
        node('軌道エレベーターの建設', ['金属超精密加工', 'ロケット技術'], [('ICチップLv1', 800), ('鋼鉄板', 3000), ('計算データ', 200)], [unlock_block('宇宙エレベーター拠点')]),
        node('反物質爆弾', ['核融合炉', '軌道エレベーターの建設'], [('ICチップLv1', 1000), ('鋼鉄板', 4000), ('コンピュータ', 1000), ('計算データ', 400)],
             [unlock_block('反物質製造装置')]),
        node('最終研究', ['反物質爆弾'], [('反物質爆弾', 10)], []),
    ]
    payloads = [{'payloadItemGuid': guid(f'GPU衛星Lv{n}'), 'countPerLaunch': 1, 'arrivalOutputs': stacks([('計算データ', c)], 'count')}
                for n, c in zip(range(1, 6), (10, 20, 40, 80, 160))]
    payloads.append({'payloadItemGuid': guid('グラフェンケーブル'), 'countPerLaunch': 1000, 'arrivalOutputs': []})
    # GPU衛星は levelFamily で判別するので、族を items.json に置く
    # GPU satellites are identified by levelFamily, so the family goes into items.json
    families = [{'familyGuid': guid('family:GPU衛星'), 'name': 'GPU衛星', 'baseItemGuid': guid('GPU衛星Lv1'),
                 'levelItemGuids': [guid(f'GPU衛星Lv{n}') for n in range(1, 6)]}]
    return {
        'items.json': {'data': [{'itemGuid': guid(n), 'name': n} for n in ITEMS], 'orbitalPayloads': payloads, 'levelFamilies': families},
        'fluids.json': {'data': [{'fluidGuid': guid(n), 'name': n} for n in FLUIDS]},
        'blocks.json': {'data': [{'blockGuid': guid(n), 'name': n, 'blockParam': p} for n, p in blocks.items()]},
        'machineRecipes.json': {'data': recipes},
        'research.json': {'data': research},
        'cleanRoom.json': {'data': [{}]},
    }


def view_of(master):
    # 書き換えた最小マスタを一時ディレクトリへ書き、本物の MasterView で読む（読み込みは ctor で終わるので消してよい）
    # Write the edited minimal master to a temp dir and read it with the real MasterView (it loads in the ctor, so the dir can go)
    with tempfile.TemporaryDirectory(prefix='tier9-balance-fixture-') as directory:
        for name, doc in master.items():
            with open(os.path.join(directory, name), 'w', encoding='utf-8') as f:
                json.dump(doc, f, ensure_ascii=False)
        return MasterView(directory)


def find(rows, prefix):
    return [c for c in rows if c.name.startswith(prefix)]
```

`tools/tier9_balance/tests/test_s2_compute_loop.py`:
```python
#!/usr/bin/env python3
# S2 区間: 最小マスタで全検査が通り、製作上限・計算データのコスト・資材の量を1か所変えると該当の行だけが FAIL になる
# S2 section: every check passes on the minimal master, and changing one craft cap, compute cost or material count fails only that row
import os
import sys
import unittest

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
from s2s3_fixture import build_master, find, guid, view_of  # noqa: E402
from sections import s2_compute_loop  # noqa: E402


def failed(result):
    return [c.name for c in result.checks if not c.passed]


class S2ComputeLoopTest(unittest.TestCase):
    def test_minimal_master_passes_with_launch_hours(self):
        result = s2_compute_loop.evaluate(view_of(build_master()))
        self.assertEqual([], failed(result))
        # 便数は Lv2〜Lv5 が 10 ずつ、軌道エレベーター 20、反物質爆弾 40（前提で使えるのは Lv1 だけ）。100 便 × 135 秒
        # Launches: 10 each for Lv2-Lv5, 20 for the elevator and 40 for the bomb (only Lv1 is a prerequisite); 100 x 135 s
        self.assertAlmostEqual(3.75, result.hours.value)

    def test_copied_upgrade_level_fails(self):
        master = build_master()
        lv3 = next(n for n in master['research.json']['data'] if n['researchNodeName'] == '半導体のアップグレード研究Lv3')
        lv3['clearedActions'][0]['gameActionParam']['level'] = 2
        self.assertEqual(['半導体のアップグレード研究Lv3 の製作上限'], failed(s2_compute_loop.evaluate(view_of(master))))

    def test_dropped_compute_cost_fails_and_hours_vanish(self):
        master = build_master()
        bomb = next(n for n in master['research.json']['data'] if n['researchNodeName'] == '反物質爆弾')
        bomb['consumeItems'] = [c for c in bomb['consumeItems'] if c['itemGuid'] != guid('計算データ')]
        result = s2_compute_loop.evaluate(view_of(master))
        self.assertEqual(['反物質爆弾 の計算データのコスト', '反物質爆弾 の便数'], failed(result))
        self.assertEqual('lookup_failed', result.hours.status)

    def test_satellite_without_family_is_not_counted(self):
        # 名前が「GPU衛星Lv」で始まっても、levelFamily「GPU衛星」が無ければ衛星として数えず、便数がすべて引けない
        # Names starting with "GPU衛星Lv" do not count without levelFamily "GPU衛星", so no launch count is readable
        master = build_master()
        master['items.json']['levelFamilies'] = []
        result = s2_compute_loop.evaluate(view_of(master))
        self.assertEqual(6, len([n for n in failed(result) if n.endswith(' の便数')]))
        self.assertEqual('lookup_failed', result.hours.status)

    def test_oversized_launch_material_fails_factory(self):
        master = build_master()
        pad = next(b for b in master['blocks.json']['data'] if b['name'] == 'ロケット発射台')
        pad['blockParam']['launchMaterials'][0]['count'] = 200
        rows = s2_compute_loop.evaluate(view_of(master)).checks
        self.assertFalse(find(rows, '1便/135秒に要る電気汎用工作装置（ロケット筐体素材）')[0].passed)


if __name__ == '__main__':
    unittest.main()
```

  Run:
```bash
cd $MW && python3 -m unittest discover -s tools/tier9_balance/tests 2>&1 | tail -3
(cd $MW && python3 tools/tier9_balance/run.py) > $GL/p2-9-s2-g6.txt; grep '\[s2_compute_loop\]' $GL/p2-9-s2-g6.txt; echo "s2-fail=$(grep -c '^FAIL \[s2_compute_loop\]' $GL/p2-9-s2-g6.txt)"
wc -l $MW/tools/tier9_balance/sections/recipe_rates.py $MW/tools/tier9_balance/sections/s2/*.py $MW/tools/tier9_balance/tests/*.py | sort -n | tail -2
```
  Expected:
  - 自己テストは `Ran 13 tests` / `OK`（枠 8・S2 5）。
  - `[s2_compute_loop]` の行は 15 行。
  - `s2-fail=4`。FAIL は「軌道エレベーターの建設」「反物質爆弾」の「計算データのコスト」「便数」の 4 行だけ。2 ノードの計算データは Task 22 が入れる。
  - 製作上限の 4 行は PASS。
  - 工場の台数の 7 行は PASS。仮値のままなら値は 1.48・1.19・0.22・2.0・0.59・2.47・0.59（最小マスタで実測した値。出荷マスタでの一致は推測）。
  - `FAIL [overall] target lookup_failed s2_compute_loop の所要時間[h]: S2 の所要時間: …` は FAIL のまま（便数の引けない 2 ノードの状態を引き継ぐ）。数値は Task 22 の後に出る。
  - どのファイルも 200 行以下（最長は `s2s3_fixture.py` の 116 行）。
  - 工場の台数が `target lookup_failed`（レシピが 1 本に決まらない）か `target not_positive`（毎秒が 0 以下）で FAIL したら、検査は緩めない。その機械に同じ出力のレシピが 2 本ある原因を spec で確かめて直す。起点 f9370e7 では、`UPSTREAM_RECIPES` の 4 品目はどれも 1 本だった（実測）。
- [ ] **Step 6: コミットして push する**
```bash
git -C $MW add design/tier9-balance/spec/07_rocket_research.json design/tier9-balance/guids.json server_v8/mods/moorestechAlphaMod_8/master server_v8/mods/moorestechAlphaMod_8/localization/localization.csv server_v8/mods/moorestechAlphaMod_8/.mooreseditor/nodeGraph.v1.json tools/tier9_balance
git -C $MW commit -m "master: ロケット技術と半導体のアップグレード研究 Lv2〜Lv5（製作上限を段ごとに上げる）と G6 の S2 区間を入れる"
git -C $MW push
```

**完了判定:** `check=0` で、G1 が PASS。`$GL/p2-7-s1-g2.txt` と `$GL/p2-9-s2-g2.txt` を比べると、上に挙げた S2 のアイテムが入手不能の一覧から消えている。nodeGraph の検査が `ng=0`。G6 の自己テストが `OK` で、`s2-fail=4`（残りは Task 22 が入れる 2 ノードの分だけ）。
**量感:** JSON 約 +250 行（ロケット技術約 70 行、アップグレード 4 ノードで約 180 行）。localization +10 行。Python 約 +380 行（区間 5 ファイル約 220 行、自己テスト 2 ファイル約 160 行）。

---

### スライス S3（S3 引き継ぎの旧 Task 1・2・5・6 のマスタ部分。旧 Task 4・12 は G6、旧 Task 3 Step 4 と旧 Task 10 Step 6 は Task 24、旧 Task 13 は録画の節）

### Task 20: 重水素・核融合炉・重水素抽出装置・反物質・反物質製造装置・反物質爆弾レシピ（S3 引き継ぎ「旧 Task 1」）

**Files:** Create: `moorestech_master:design/tier9-balance/spec/08_fusion_antimatter.json` / Modify: `.../master/{fluids,items,blocks,machineRecipes}.json`、`localization.csv`、`guids.json`

**Interfaces:**
- Consumes: 統合後のスキーマの ElectricGenerator（必須キーは fuelItemSlotCount・isInfinityPower・infinityPower・inventoryConnectors・fuelFluidTankCapacity・fluidInventoryConnectors と電線 3 キー。fuelItems と fuelFluids は optional。S1 ブランチ `blocks.yml:349-421`）。ElectricMachine の必須キー `allowQualityModule`（`blocks.yml:318`）。
- Produces（S3 引き継ぎ「共通の GUID・参照表」の新規 GUID）:
  - 重水素 `01a167e9-21b2…`、反物質 `01a167e9-21b5…`
  - 重水素抽出装置 `01a167e9-21b8…`、反物質製造装置 `01a167e9-21bb…`
  - 核融合炉 `01a162f1-ab04-76e2-94cf-dc713edeb63b` を ElectricGenerator の全文にする
  - レシピキー `重水素` `反物質` `反物質爆弾`

**流体量（nano 化への合わせ方）:**
- 本体 master の ADR 0090（c506300c0a で追記。937a305daf が初版）で整数単位になったのは**パイプ網だけ**。「機械のタンク（`FluidContainer`）・`FluidStack`・レシピ・UI の流体量は double のまま」と書かれている（`docs/adr/0090-fluid-network-fixed-point-nano-units-and-gather-stepper.md` 決定 2 項目）。
- マスタの容量と上限は、読み込み時に `FluidUnits.ToUnitsNearest` で網の単位へ変える（`Game.Fluid/FluidUnits.cs` のコメント「マスタの容量・上限…は最近傍」）。両コミットとも `VanillaSchema` を変えていない（`git log 937a305daf^..c506300c0a -- VanillaSchema` が空）。
- したがって、マスタの JSON の流体量は nano 化では換算しない。
- 単位の基準は液体 10 倍化（moorestech_master 0ac8b45、ADR 0086）。これは S3 下書きの土台 b2149a0 より前に入っている。つまり S3 の値は 10 倍化後の単位で書かれている。比較すると、ガソリンエンジン発電機が 4 秒で 10 消費して 1000、核融合炉が 2 秒で 10 消費して 20000。
- 下書きの燃料量（10 / 2 秒）と抽出レシピ（水 100 → 重水素 10、8 秒）はそのまま使い、妥当かどうかは Task 23 の G6（炉 1 基の差し引き電力がガソリン発電機 3〜5 台分）で判定する。
- 換算しない理由は Task 23 の判断記録に書く。

- [ ] **Step 1: 既存モデルの寸法を測る。** S3 引き継ぎ「旧 Task 1 Step 3」の dynamic code を使う。`cd $W && uloop execute-dynamic-code --project-path ./moorestech_client --code '<同節の C#>'` で 5 モデルの size を読む。各 blockSize は size を切り上げた整数にし、コネクタの offset は同節の規則で直す。
- [ ] **Step 2: spec 08 を書く。** 値は S3 引き継ぎ「旧 Task 1 Step 2〜4」の JSON（液体 1・アイテム 1・ブロック 3・レシピ 3）。下書きに対する訂正は次の 4 点。
  - (a) ElectricMachine 2 行（重水素抽出装置・反物質製造装置）の `blockParam` に `"allowQualityModule": false` を足す。品質が効く抽選が無いため。S1 の判断記録「品質モジュールは抽選に品質が効く機械だけ true」に従う。下書きのままではロード時に落ちる。
  - (b) レシピ 3 本に `levelInheritance: "None"` を足す。
  - (c) 核融合炉は ElectricGenerator なので `allowQualityModule` を入れない。`openable: true` は下書きどおり（Task 24 の e2e fixture に効く）。
  - (d) S3 の独・韓の訳は書かない。`LocalizationRows.put` は他の言語を空にして english へ戻す方針（S1 handoff「Task D2」）だから。

  spec での書き方:
  - 液体は `"fluids": [{"name": "重水素", "guid": "01a167e9-21b2-7bb0-b6a3-0f17d349704e", "en": "Deuterium", "color": "#7FD8FF"}]`。
  - アイテムは `"items": [{"name": "反物質", "guid": "01a167e9-21b5-7479-9060-995505f43137", "en": "Antimatter", "sortPriority": 1605}]`。
  - ブロックは `"blocks"` に 3 件。`element` は下書きの JSON から `blockGuid` と `name` を除いたもの。新規 2 件は `"guid"` を固定 GUID にする。核融合炉は既存の名前で解決する。
  - レシピは `"machineRecipes"` に 3 件。`guid` は表の固定値、入力と出力は名前参照（例: `{"key": "重水素", "guid": "01a167e9-21dc-7eb1-a240-8345ed8fbd40", "block": "@block:重水素抽出装置", "time": 8, "levelInheritance": "None", "inputItems": [], "inputFluids": [["@fluid:水", 100]], "outputItems": [], "outputFluids": [["@fluid:重水素", 10]]}`）。
- [ ] **Step 3: 適用する**  Expected: `applied 8 spec files; issued 0 new guids`（すべて固定 GUID）
- [ ] **Step 4: 結果とロードを確かめる**
```bash
python3 -c "
import json; m='$MOD/master/'; b={e['name']:e for e in json.load(open(m+'blocks.json'))['data']}
print(b['核融合炉']['blockType'], b['核融合炉']['blockParam']['fuelFluids'][0]['power'], b['重水素抽出装置']['blockParam']['allowQualityModule'], b['反物質製造装置']['blockParam']['allowQualityModule'])
r=json.load(open(m+'machineRecipes.json'))['data']; print(all('levelInheritance' in x for x in r))"
G1 $MW/server_v8
```
Expected: `ElectricGenerator 20000 False False`、`True`、G1 PASS。
- [ ] **Step 5: コミット**
```bash
git -C $MW add design/tier9-balance/spec/08_fusion_antimatter.json design/tier9-balance/guids.json server_v8/mods/moorestechAlphaMod_8/master server_v8/mods/moorestechAlphaMod_8/localization/localization.csv
git -C $MW commit -m "master: 重水素・核融合炉・重水素抽出装置・反物質・反物質製造装置と爆弾レシピを入れる"
```

**完了判定:** Step 4 の 2 行と G1 PASS。
**量感:** JSON 約 +270 行（ブロック 3 件で約 160 行、レシピ 3 本約 90 行、液体とアイテム約 20 行）。localization +5 行。

### Task 21: グラフェン工程・金属 3D プリンタ・ケーブルの積荷表とスタック表・拠点の全文（S3 引き継ぎ「旧 Task 2 Step 1〜2」「旧 Task 5 Step 3」「旧 Task 6 Step 5」）

**Files:** Create: `moorestech_master:design/tier9-balance/spec/09_graphene_printer_elevator.json` / Modify: `.../master/{items,blocks,machineRecipes}.json`、`localization.csv`、`guids.json`

**Interfaces:**
- Consumes:
  - 拠点のスキーマ（統合後の `when: SpaceElevatorBase`。統合前の S3 `blocks.yml:1156-1198`）。必須キーは 10 個: carriedPayloadItemGuids・cableItemGuid・cablesPerElevator・climbSeconds・requiredPower・outputSlotCount・inventoryConnectors と電線 3 キー。
  - 検証 `SpaceElevatorBaseParamValidator`（`wf/S2S3.md` §(4)）: ケーブルが積荷表に載っていること、carried の各行が `countPerLaunch==1` であること、成果が出口に入りきること。
  - 積荷表の検証: `countPerLaunch` はレベル 1 のスタック上限以下でなければならない。Task 18 の GPU衛星 5 種と積荷表の upsert。
- Produces:
  - 金属3Dプリンタ（ブロック）`01a167e9-21be…`。旧アイテム `019e3af5-e750…` は消す。
  - グラフェン 4 レシピと 3D プリンタの 2 レシピ。
  - スタック表「グラフェンケーブル」[1000] `01a167e9-21f9-70a8-be5e-08c2ba6895c9`。
  - 積荷表のケーブル行（1 便 1000 本・成果なし）。
  - 拠点 `01a162aa-cec0-738c-93c5-f58adc87fa7f` を SpaceElevatorBase の全文にする。

- [ ] **Step 1: spec 09 を書く。** 値は S3 引き継ぎの各節の JSON をそのまま使う。
  - 「旧 Task 2 Step 1」: 3D プリンタのブロック。blockSize は Task 20 Step 1 の実測値を使う。
  - 「旧 Task 2 Step 2」: レシピ 6 本。
  - 「旧 Task 5 Step 3」: スタック表と積荷表の行。
  - 「旧 Task 6 Step 5」: 拠点の全文。

  下書きに対する訂正:
  - (a) 金属3Dプリンタ（ElectricMachine）に `"allowQualityModule": false` を足す。
  - (b) レシピ 6 本に `levelInheritance: "None"` を足す。
  - (c) 旧アイテムは `"removeItems": ["019e3af5-e750-71ae-ab09-2b98f3db9913"]` で消す。研究「金属超精密加工」の `unlockItemRecipeView` に残っている参照は、Task 22 の `patchResearch` が解放を丸ごと置き換えることで消える。消し残しがあれば `assert_removed_absent` が止める。
  - (d) 拠点は SpaceElevatorBase で、ElectricMachine ではないので `allowQualityModule` は入れない。`category: 生産`・`subCategory: 宇宙` は Task 18 の小分類を使う。
  - (e) 積荷表のケーブル行は `"orbitalPayloads": [{"payloadItemGuid": "@item:グラフェンケーブル", "countPerLaunch": 1000, "arrivalOutputs": []}]`、スタック表は `"stackTables": [{"name": "グラフェンケーブル", "guid": "01a167e9-21f9-70a8-be5e-08c2ba6895c9", "stackCounts": [1000]}]`、割り当ては `"patchItems": [{"name": "グラフェンケーブル", "set": {"stackLevelTableGuid": "@stackTable:グラフェンケーブル"}}]` と書く。キー名は S2 の `items.yml` の `orbitalPayloads`（`payloadItemGuid`・`countPerLaunch`・`arrivalOutputs`）で確認済み（`wf/S2S3.md` §(1)）。
- [ ] **Step 2: 適用する**（Task 22 の研究がまだ無いので、`assert_removed_absent` が「金属超精密加工」に残る旧アイテムの参照で止まる。そのため、このタスクは spec 09 と spec 10 を同時に置いてから適用する。手順上は Step 1 の後に Task 22 Step 1 を書き、ここで両方を適用する）  Run: `cd $B && python3 apply/apply_spec.py --mod-dir $MOD --spec-dir spec --ledger guids.json`  Expected: `applied 10 spec files; issued 0 new guids`
- [ ] **Step 3: 結果を確かめる**
```bash
python3 -c "
import json; m='$MOD/master/'; i=json.load(open(m+'items.json')); b={e['blockGuid']:e for e in json.load(open(m+'blocks.json'))['data']}
t={x['tableGuid']:x['stackCounts'] for x in i['itemStackLevelTables']}; c=next(e for e in i['data'] if e['name']=='グラフェンケーブル')
print(t[c['stackLevelTableGuid']], [r['countPerLaunch'] for r in i['orbitalPayloads'] if r['payloadItemGuid']==c['itemGuid']])
s=b['01a162aa-cec0-738c-93c5-f58adc87fa7f']; print(s['blockType'], s['subCategory'], len(s['blockParam']['carriedPayloadItemGuids']))
print(any(e['itemGuid'].startswith('019e3af5-e750') for e in i['data']))"
```
Expected: `[1000] [1000]`、`SpaceElevatorBase 宇宙 5`、`False`。
- [ ] **Step 4: コミット**（Task 22 Step 8 のコミットとまとめる。spec 09 と 10 は同時に適用しないと途中の状態が作れないため）

**完了判定:** Step 3 の 3 行。ロードは Task 22 Step 5 の G1 で確かめる。
**量感:** JSON 約 +300 行（3D プリンタ約 55 行、拠点約 55 行、レシピ 6 本約 180 行、スタック表と積荷表約 15 行。アイテム 1 件約 −13 行）。localization +2 行・−1 行。

### 裁定2 との整合（Task 22 の前提。旧セーブでコスト0で完了した Tier9 研究のリセット）

**前提:** 裁定2（`.decisions/2026-10-25-Tier9統合で未実装3ブロックの解放取り消しをやめコスト0で完了済みのTier9研究をリセットする.md`）のリセットは、P1 のセーブ移行 V7→V8 の部品で行う（裁定1）。

**マスタ側（案A・推奨・この節の既定）:**
- 6 ノードのうち 5 ノードは GUID を変えずにコストと解放を入れる。
- 「フィルター分岐器」`019e3aab-32c6-7166-a184-d761153c2498` は S3 の R10 で消す。
- 各ノードの中身は Task 17（機械オーバークロック）と Task 22（残り 4 ノード）。

| ノード | GUID | 統合後の前提 | 統合後の消費（仮値。Task 23 で確定） | 統合後の解放 |
|---|---|---|---|---|
| フィルター分岐器 | 019e3aab-32c6-7166-a184-d761153c2498 | （削除） | — | — |
| 機械オーバークロック | 019e3ae2-5005-7439-b242-9b7b3191a2c3 | 品質モジュール | ICチップLv1 1500・鋼鉄フレーム150・制御モータ120 | 速度モジュールのレシピと表示 |
| 金属超精密加工 | 019e3af5-f2e6-76de-a80f-5e66e79fa35d | 機械オーバークロック | ICチップLv1 300・鋼鉄板1500・制御モータ300 | 金属3Dプリンタ（ブロック） |
| 核融合炉 | 019e3af0-297f-726e-8a02-0a8518124e6d | 金属超精密加工 | ICチップLv1 600・鋼鉄板3000・コンピュータ600・制御モータ600 | 核融合炉・重水素抽出装置（ブロック） |
| 軌道エレベーターの建設 | 01a16291-4094-7369-a87f-5dfe2197d25e | 金属超精密加工・ロケット技術 | ICチップLv1 800・鋼鉄板3000・計算データ200 | 宇宙エレベーター拠点（ブロック、裁定3）、グラフェン 4 種の表示 |
| 反物質爆弾 | 019e3aef-a149-746b-8fad-159e0434cb51 | 核融合炉・軌道エレベーターの建設 | ICチップLv1 1000・鋼鉄板4000・コンピュータ1000・計算データ400 | 反物質製造装置（ブロック）、反物質・反物質爆弾の表示 |

**P1 の部品へ渡す表**（リセットで取り消すもの。GUID を変えないので、マスタから自動では導けない）:
- 完了を外す研究: 上の 6 GUID（フィルター分岐器を含む）。
- 取り消す解放（旧マスタでこれらのノードが付けていたもの）: b2149a0 の research.json を読んで和集合をとった（実測）。
  - ブロック: 宇宙エレベーター拠点 `01a162aa-cec0-738c-93c5-f58adc87fa7f`、ロケット発射台 `01a162b0-7769-7419-af24-bb35528b93d2`、核融合炉 `01a162f1-ab04-76e2-94cf-dc713edeb63b`
  - レシピ表示: 反物質爆弾 `01a162ab-07ba-73c1-9dd6-38e1e867663b`、金属3Dプリンタ（旧アイテム）`019e3af5-e750-71ae-ab09-2b98f3db9913`、グラフェンケーブル `01a16290-f793-748b-a0b0-91d44be0d4ab`、グラフェンシート `01a16297-4724-73b9-82c3-2e77120be77d`、使用済み銅板 `01a16297-6665-765d-8e19-96d357394c3e`、高純度炭素 `01a16297-a7f5-759b-91ba-be4021ca3bf2`、ロケット筐体素材 `01a162a4-9334-7286-a8ba-7daf9c0f407a`、ロケット燃料 `01a162a8-e435-73d0-bddb-08533819b80e`
- 補足（実測）: 本体 master のピン 0d129c8（テスターの版）には、まだ「軌道エレベーターの建設」が無い。3 ブロックの解放も無く、解放は金属3Dプリンタの表示 1 件だけだった。上の表は、開発者のセーブ（b2149a0 以降のピン）も覆う和集合にしてある。
- ロード時には、統合マスタで完了済みの研究の ClearedActions がもう一度実行される（`WorldSaveDataRestorer.cs:103`→`:124`、`ResearchDataStore.cs:139-157`）。そのため、リセットしたノードの新しい解放はリセット後に付かない。

**案B（GUID を振り直す）を選ぶ場合の差分:** P1 と相談して切り替えるときに使う。
- spec 05・10 で、5 ノードを `removeResearch`（旧 GUID）と `research`（同名で新しい GUID）に分ける。
  - `NameResolver.declare` は同名が既にあると旧 GUID を返す（S1 handoff「Task D2」`resolver.py`）。そのため、新しいノードの名前を変えるか、宣言の前に旧ノードを除く処理を `apply_extras.declare_extra` に足す必要がある。
- 参照の付け替え: 前提（反物質爆弾 → 核融合炉・軌道エレベーターの建設、最終研究 → 反物質爆弾、金属超精密加工 → 機械オーバークロック、核融合炉 → 金属超精密加工、モジュールスタック拡張I → 機械オーバークロック）と、localization のキー `research.<旧GUID>.*` → `research.<新GUID>.*`。
- それでも P1 の部品は必要になる。旧 GUID の完了は MissingMasterPruner と LoadResearchData の未知 GUID の扱いで落ちるが、保存済みの解放状態（`blockUnlockStateInfos` の isUnlocked=true）は残るからだ。上の「取り消す解放」は案B でも要る。
- **推奨は案A。** 案B は差分が増えるうえに P1 の仕事を減らさない。

### Task 22: S3 の研究（核融合炉・反物質爆弾・金属超精密加工・軌道エレベーターの建設・最終研究）、フィルター分岐器の削除、S3 スライスのゲート

**Files:** Create: `moorestech_master:design/tier9-balance/spec/10_endgame_research.json` / Modify: `.../master/research.json`、`localization.csv`、`guids.json`、`.../.mooreseditor/nodeGraph.v1.json`（Step 3 の同期） / Create（G6 の S3 区間。Step 6）: `moorestech_master:tools/tier9_balance/sections/s3/{__init__,power_checks,graphene_elevator_checks,hours_model}.py`、`tools/tier9_balance/tests/test_s3_endgame.py` / Modify（Step 6。Task 3 の空の区間を全文置き換え）: `tools/tier9_balance/sections/s3_endgame.py` / Create（控え）: `$GL/p2-12-s3-g1.txt`、`$GL/p2-12-s3-g2.txt`、`$GL/p2-12-s3-nodegraph.txt`、`$GL/p2-12-s3-g6.txt`

**Interfaces:**
- Consumes: Task 19 のロケット技術と、G6 の `sections/recipe_rates.py`・`tests/s2s3_fixture.py`。Task 20・Task 21 のブロック・レシピ・アイテム。上の「裁定2 との整合」の表。S1 の sim の `timeline.line_rate(class, done)`・`timeline.RESEARCH`・`timeline.EXTRA_LINES_AT_H`（Task 3 Step 7）。
- Produces:
  - G6 の S3 区間 `sections/s3_endgame.evaluate(view) -> SectionResult('s3_endgame', checks, hours: Resolved)`（Step 6）。
  - 研究「最終研究」`01a167e9-21f6-7d26-a990-e7d6d78750d0`（反物質爆弾 10 個を消費し、何も解放しない。ゴール）。
  - 5 ノードのコストと解放。
  - フィルター分岐器のノードは無くなる。

- [ ] **Step 1: spec 10 を書く。** 値は S3 引き継ぎの 3 節（「旧 Task 1 Step 5」の核融合炉・反物質爆弾、「旧 Task 2 Step 3」の金属超精密加工・軌道エレベーターの建設・最終研究、「旧 Task 5 Step 4」の (a)(b)(c) と反物質爆弾の計算データ）。下書きに対する訂正は次の 4 点。
  - (a) **拠点の解放を「軌道エレベーターの建設」に戻す（裁定3）。** S3 の (a)「unlockBlock から発射台を外す」は、S1 のマスタ 32874fe が action を丸ごと消す前の形を前提にしている。統合では、解放を `unlockBlock [宇宙エレベーター拠点]` として新しく書く。
  - (b) 「軌道エレベーターの建設」の表示からロケット筐体素材・燃料を外す（S3 の (a)）。これらの表示はロケット技術（Task 19）へ移っている。
  - (c) 「軌道エレベーターの建設」の UIPosition の固定値 `[4590, 0]` は書かない。Step 4 の再計算（Task 17 Step 4 と同じ master-refine）に任せる。
  - (d) 計算データの量は S3 の仮値（軌道エレベーター 200、反物質爆弾 400）で入れ、Task 23 で確定する。
```json
{
  "removeResearch": ["019e3aab-32c6-7166-a184-d761153c2498"],
  "patchResearch": [
    {"name": "金属超精密加工", "set": {"prevResearchNodeGuids": ["@research:機械オーバークロック"],
      "consumeItems": [{"itemGuid": "@item:ICチップLv1", "itemCount": 300}, {"itemGuid": "@item:鋼鉄板", "itemCount": 1500}, {"itemGuid": "@item:制御モータ", "itemCount": 300}],
      "researchNodeDescription": "ICチップから部品を1工程で作る金属3Dプリンタを解放する",
      "clearedActions": [{"gameActionType": "unlockBlock", "gameActionParam": {"unlockBlockGuids": ["@block:金属3Dプリンタ"]}}]},
     "descEn": "Unlocks the Metal 3D Printer, which makes parts from IC chips in one step"},
    {"name": "核融合炉", "set": {
      "consumeItems": [{"itemGuid": "@item:ICチップLv1", "itemCount": 600}, {"itemGuid": "@item:鋼鉄板", "itemCount": 3000}, {"itemGuid": "@item:コンピュータ", "itemCount": 600}, {"itemGuid": "@item:制御モータ", "itemCount": 600}],
      "clearedActions": [{"gameActionType": "unlockBlock", "gameActionParam": {"unlockBlockGuids": ["@block:核融合炉", "@block:重水素抽出装置"]}}]}},
    {"name": "軌道エレベーターの建設", "set": {"prevResearchNodeGuids": ["@research:金属超精密加工", "@research:ロケット技術"],
      "consumeItems": [{"itemGuid": "@item:ICチップLv1", "itemCount": 800}, {"itemGuid": "@item:鋼鉄板", "itemCount": 3000}, {"itemGuid": "@item:計算データ", "itemCount": 200}],
      "researchNodeDescription": "グラフェンケーブルと宇宙エレベーター拠点を解放する",
      "clearedActions": [
        {"gameActionType": "unlockBlock", "gameActionParam": {"unlockBlockGuids": ["@block:宇宙エレベーター拠点"]}},
        {"gameActionType": "unlockItemRecipeView", "gameActionParam": {"unlockItemGuids": ["@item:グラフェンケーブル", "@item:グラフェンシート", "@item:使用済み銅板", "@item:高純度炭素"]}}]},
     "descEn": "Unlocks graphene cables and the space elevator base"},
    {"name": "反物質爆弾", "set": {
      "consumeItems": [{"itemGuid": "@item:ICチップLv1", "itemCount": 1000}, {"itemGuid": "@item:鋼鉄板", "itemCount": 4000}, {"itemGuid": "@item:コンピュータ", "itemCount": 1000}, {"itemGuid": "@item:計算データ", "itemCount": 400}],
      "clearedActions": [
        {"gameActionType": "unlockBlock", "gameActionParam": {"unlockBlockGuids": ["@block:反物質製造装置"]}},
        {"gameActionType": "unlockItemRecipeView", "gameActionParam": {"unlockItemGuids": ["@item:反物質", "@item:反物質爆弾"]}}]}}
  ],
  "research": [
    {"name": "最終研究", "guid": "01a167e9-21f6-7d26-a990-e7d6d78750d0", "en": "Final Research",
     "description": "反物質爆弾を規定量そろえたことを記録する仮の研究。現段階のゲームの終点（解放物なし）",
     "descEn": "A provisional research that records stockpiling the required antimatter bombs. The current end of the game (unlocks nothing)",
     "prev": ["@research:反物質爆弾"], "consumeItems": [["@item:反物質爆弾", 10]], "clearedActions": [], "iconItem": "@item:反物質爆弾"}
  ]
}
```
- [ ] **Step 2: 適用し、研究の解放を揃える**（Task 21 Step 2 の適用はこの手順と同じ 1 回）
```bash
cd $B && python3 apply/apply_spec.py --mod-dir $MOD --spec-dir spec --ledger guids.json
cd $MW && python3 tools/research_sync/sync_machine_recipe_unlocks.py | grep -E "^(MOVE|ORPHAN|STALE|DEAD-END)|moves," ; python3 tools/research_sync/sync_machine_recipe_unlocks.py --check; echo "check=$?"
```
Expected:
- apply が `applied 10 spec files; issued 0 new guids`。
- sync は `0 orphans, 0 stale` で、`DEAD-END` が無い。レシピの行き先は S3 引き継ぎ「旧 Task 1 Step 7」「旧 Task 2 Step 5」の Expected どおり: 重水素は核融合炉、反物質と爆弾は反物質爆弾、グラフェン 4 本は軌道エレベーターの建設、3D プリンタの 2 本は金属超精密加工。
- `check=0`。
- [ ] **Step 3: nodeGraph を統合マスタに合わせる**（判断記録 A21。Step 4 の master-refine の前に流す）
```bash
cd $MW && python3 tools/nodegraph_sync/sync_nodegraph.py > $GL/p2-12-s3-nodegraph-sync.txt; echo "ng=$?"; cat $GL/p2-12-s3-nodegraph-sync.txt
git -C $MW diff --stat -- server_v8/mods/moorestechAlphaMod_8/.mooreseditor
```
Expected:
- `ng=0`。末尾は `== NODEGRAPH: PASS (0 failures)` で、`UNPLACED` 行が無い。diff は `nodeGraph.v1.json` の 1 ファイルだけ。
- 1 行目の見込み（推測）: `pruned` に研究「フィルター分岐器」`019e3aab-32c6…` と旧「金属3Dプリンタ」アイテム `019e3af5-e750…` のノード（設計検査 V2 が #87 で実測した 4 件のうち、Task 17 で消えなかった 2 件）。`research nodes +1`（最終研究）。`dependency edges` の増減は前提の張り替え分（金属超精密加工 ← 機械オーバークロック、軌道エレベーターの建設 ← 金属超精密加工・ロケット技術、最終研究 ← 反物質爆弾。フィルター分岐器の線はノードと一緒に消える）。`moved` に宇宙エレベーター拠点（軌道エレベーターの建設の下へ）と核融合炉（核融合炉の研究の下へ）。
- `UNPLACED` が出たら、検査は緩めず、`placement.py` の候補の範囲（研究は `range(1, 9)`、解放は `range(0, 12)`）を広げて流し直す。
- [ ] **Step 4: sortPriority と研究座標を再計算する**（Task 17 Step 4 と同じ master-refine の 2 本）
```bash
cd $W && python3 .agents/skills/master-refine/scripts/recalc_sort_priority.py --mod-dir "$MOD" --quiet && python3 .agents/skills/master-refine/scripts/recalc_research_positions.py --mod-dir "$MOD" | tail -1
```
Expected: 並び順と座標の再計算が通る（`updated <件数> nodes -> …/research.json`）。
- [ ] **Step 5: S3 スライスのゲート（ここで G2 を緑にする）**
```bash
G1 $MW/server_v8 2>&1 | tee $GL/p2-12-s3-g1.txt
(cd $MW && python3 tools/progression/check.py) > $GL/p2-12-s3-g2.txt 2>&1; echo "G2 exit=$?"
(cd $MW && python3 tools/nodegraph_sync/sync_nodegraph.py --check) > $GL/p2-12-s3-nodegraph.txt; echo "ng=$?"; tail -1 $GL/p2-12-s3-nodegraph.txt
git -C $MW diff --stat origin/feat/tier9-integrated -- tools/progression/allowlist.json
```
Expected:
- G1 は PASS（核融合炉・拠点・3 機械の全文で読み込みと検証が通る）。
- **G2 は exit 0。** ゴール「最終研究」に届き、通った経路が印字される。入手できない Tier9 アイテムは 0 件、どこからも解放されないブロックは 0 件（拠点が軌道エレベーターの建設から解放される）、機械が解放されないレシピは 0 件。
- nodeGraph は `ng=0`・`== NODEGRAPH: PASS (0 failures)`。
- 許可リストの差分は空。
- 赤が残ったら、その行の要素を spec 06〜10 で直す。許可リストに足して黙らせることはしない。
- [ ] **Step 6: G6 の S3 区間を書く**（判断記録 A26。S3 の機械・拠点・研究がそろうのはこの Task なのでここで書く。値の確定は Task 23）
  - 検査: S3 引き継ぎ「旧 Task 4」「旧 Task 12」の `check` 行を関数に直す。`sys.exit` は引けない `Resolved`（`lookup_failed`）と `checks.unreadable` の行に、`check(..., 1, 1)` は `checks.matches` に替える。`PowerResult`（差し引き電力・爆弾をそろえる時間）、品目の毎秒、研究を払う時間、区間の時間はどれも `Resolved` で返し、差し引きが 0 以下なら `require_positive` で `not_positive` にして、それで割る比も同じ状態で落とす（判断記録 A31）。上流の需要の積み上げは `recipe_rates.chain_machines`、積荷表の行は `MasterView.payload_row`、研究のコストの品目は GUID で引く（判断記録 A32）。
  - 旧 Task 12 の `ROCKET_BUILD_SECONDS_KEY` は `buildSeconds`（Task 3 の引き継ぎ）。
  - 所要時間: 次の 3 つを足す。
    - S3 の研究 4 つ（金属超精密加工→核融合炉→軌道エレベーターの建設→反物質爆弾）を順に払う時間。1 つの研究は、品目ごとに最も遅い品目で決まる。
    - unlockBlock を持つ研究 1 つにつき 0.5 h の建設時間。
    - 反物質製造装置 2 台で最終研究の爆弾をそろえる時間。
  - 毎秒の量: ICチップLv1 は S1 sim のクラス B（S1 研究完了後の 3 ライン）から引く。鋼鉄板・コンピュータ・制御モータは `FACTORY` の台数 × マスタのレシピから出す。
  - 計算データは S2 区間の便数で数えるので、ここでは払う時間に入れない。

`tools/tier9_balance/sections/s3/__init__.py`:
```python
# S3 終盤の部品（核融合と反物質の電力、グラフェンとエレベーター、所要時間の見積もり）
# Parts of the S3 endgame (fusion and antimatter power, graphene and the elevator, and the hour estimate)
```

`tools/tier9_balance/sections/s3/power_checks.py`:
```python
#!/usr/bin/env python3
# 核融合と反物質の検査（S3 引き継ぎ「旧 Task 4」）。炉1基の差し引き電力・抽出装置の台数・反物質機の電力・爆弾をそろえる時間
# Fusion and antimatter checks (S3 handoff "old Task 4"): reactor net power, extractor count, fabricator power and bomb stocking time
# 目標の根拠: ADR 0093（核融合の差し引き電力。統合で 0090 から振り直し。差し引きはガソリン発電機数台分）、S3 の R3・R5
# Targets: ADR 0090 (net equals a few gasoline generators) and S3 R3/R5
import checks
from sections import recipe_rates

# 最終研究の爆弾は反物質製造装置2台でそろえる前提（S3 引き継ぎ「旧 Task 4」）
# Bombs for the final research are stocked with two antimatter fabricators (S3 handoff "old Task 4")
FABRICATORS_FOR_BOMBS = 2


class PowerResult:
    # 後段（エレベーターの電力比・所要時間）が使う中間値。どちらも Resolved で、差し引きが 0 以下なら not_positive（判断記録 A31）
    # Intermediate values reused later (elevator power ratio, hours); both are Resolved, and a net of zero or less is not_positive (decision A31)
    def __init__(self, net_power, bomb_hours):
        self.net_power = net_power
        self.bomb_hours = bomb_hours


def check_power(view):
    gasoline = view.block_param('ガソリンエンジン発電機', 'fuelFluids').then(lambda f: checks.element(f, 0, '発電機の fuelFluids'))
    reactor = view.block_param('核融合炉', 'fuelFluids').then(lambda f: checks.element(f, 0, '核融合炉の fuelFluids'))
    extractor_power = view.block_param('重水素抽出装置', 'requiredPower')
    extract = recipe_rates.output_per_second(view, '重水素抽出装置', '重水素')
    inputs = [gasoline, reactor, extractor_power, extract]
    if checks.failures(inputs):
        net = checks.carried_failure(inputs, '核融合炉の差し引き電力')
        bomb_hours = checks.carried_failure(inputs, '反物質の爆弾をそろえる時間')
        return [checks.unreadable('target', '核融合炉の差し引き電力', net)], PowerResult(net, bomb_hours)

    # 炉1基の重水素消費を賄う抽出装置の台数と差し引き電力
    # Extractors needed to feed one reactor and the resulting net power
    extractors = (reactor.value['amount'] / reactor.value['time']) / extract.value.per_second
    net_value = reactor.value['power'] - extractors * extractor_power.value
    net = checks.require_positive(checks.resolved(net_value), '炉1基の発電から抽出装置の電力を引いた差し引き')
    rows = [checks.target_range('炉1基の差し引き電力（ガソリン発電機何台分）', checks.resolved(round(net_value / gasoline.value['power'], 2)), 3, 5),
            checks.target_range('炉1基に要る抽出装置の台数', checks.resolved(round(extractors, 2)), 2, 8)]
    antimatter_rows, bomb_hours = check_antimatter(view, net, extract.value.per_second, extractor_power.value)
    return rows + antimatter_rows, PowerResult(net, bomb_hours)


def check_antimatter(view, net, deuterium_per_extractor, extractor_power):
    # 戻り値は（検査の行, 爆弾をそろえる時間[h] の Resolved）
    # Returns (check rows, bomb stocking hours as a Resolved)
    fabricator_power = view.block_param('反物質製造装置', 'requiredPower')
    antimatter = recipe_rates.output_per_second(view, '反物質製造装置', '反物質')
    deuterium_in = antimatter.then(lambda rate: view.recipe_input_amount(rate.recipe, '重水素'))
    antimatter_per_bomb = view.recipe_by_output('電気汎用工作装置', '反物質爆弾').then(lambda r: view.recipe_input_amount(r, '反物質'))
    final_bombs = view.research_cost('最終研究', '反物質爆弾')
    inputs = [fabricator_power, antimatter, deuterium_in, antimatter_per_bomb, final_bombs]
    if checks.failures(inputs):
        failure = checks.carried_failure(inputs, '反物質の電力と爆弾')
        return [checks.unreadable('target', '反物質の電力と爆弾', failure)], failure

    # 反物質機1台の電力（自分の重水素をつくる抽出装置を含む）。差し引きが 0 以下なら比は not_positive のまま FAIL
    # Power per fabricator including the extractors that make its deuterium; a non-positive net keeps the ratio not_positive
    deuterium_per_second = deuterium_in.value / antimatter.value.recipe['time']
    total_power = fabricator_power.value + deuterium_per_second / deuterium_per_extractor * extractor_power
    rows = [checks.target_range('反物質製造装置1台の電力（炉の差し引き何基分）', net.map(lambda n: round(total_power / n, 2)), 3, 6)]

    # 2台で最終研究の爆弾をそろえる時間
    # Time for two fabricators to stock the final research bombs
    hours = final_bombs.value * antimatter_per_bomb.value / (FABRICATORS_FOR_BOMBS * antimatter.value.per_second) / 3600
    rows.append(checks.target_range(f'反物質製造装置{FABRICATORS_FOR_BOMBS}台で最終研究の爆弾をそろえる時間[h]', checks.resolved(round(hours, 2)), 0.5, 3))
    return rows, checks.resolved(hours)
```

`tools/tier9_balance/sections/s3/graphene_elevator_checks.py`:
```python
#!/usr/bin/env python3
# グラフェンと宇宙エレベーターの検査（S3 引き継ぎ「旧 Task 4」のグラフェン、「旧 Task 12」のエレベーター）
# Graphene and space elevator checks (graphene from S3 handoff "old Task 4", the elevator from "old Task 12")
import checks
from sections import recipe_rates

CABLE = 'グラフェンケーブル'
# ケーブル1便分を1時間で作る（S3 引き継ぎ「旧 Task 4」）。戻し炉（使用済み銅板→銅板）は数えない
# Make one launch of cables per hour (S3 handoff "old Task 4"); the copper-return furnace is not counted
CABLE_CHAIN = (('電気汎用工作装置', CABLE), ('電気炉', 'グラフェンシート'), ('電気炉', '高純度炭素'))


def check_graphene(view):
    cable_row = cable_payload_row(view)
    if not cable_row.ok:
        return [checks.unreadable('target', f'{CABLE} の積荷表の行', cable_row)]
    demand, machines = {}, 0.0
    recipe_rates.add_demand(demand, cable_row.value['payloadItemGuid'], cable_row.value['countPerLaunch'] / 3600)
    for block, output in CABLE_CHAIN:
        rate = recipe_rates.output_per_second(view, block, output)
        if not rate.ok:
            return [checks.unreadable('target', f'{block}（{output}）のレシピ', rate)]
        machines += recipe_rates.chain_machines(demand, rate.value)
    return [checks.target_range('1便分のケーブルを1時間で作る機械台数（戻し炉を除く）', checks.resolved(round(machines, 2)), 1, 12)]


def check_elevator(view, net_power):
    # 1エレベーター分はケーブル1便、昇降はロケット建造より遅く、電力は炉1基の差し引きの一部
    # One elevator equals one cable launch, a climb is slower than a rocket build, and its power is a fraction of one reactor's net
    cables = view.block_param('宇宙エレベーター拠点', 'cablesPerElevator')
    climb = view.block_param('宇宙エレベーター拠点', 'climbSeconds')
    power = view.block_param('宇宙エレベーター拠点', 'requiredPower')
    rocket_build = checks.require_positive(view.block_param('ロケット発射台', 'buildSeconds'), 'ロケット発射台の buildSeconds')
    cable_row = cable_payload_row(view)
    inputs = [cables, climb, power, rocket_build, cable_row]
    if checks.failures(inputs):
        return [checks.unreadable('target', '宇宙エレベーター拠点の値', checks.carried_failure(inputs, '宇宙エレベーター拠点の値'))]
    return [checks.matches('1エレベーター分のケーブル（積荷表の1便）', cable_row.value['countPerLaunch'], cables, 0),
            checks.target_range('昇降時間 / ロケット建造時間', checks.resolved(round(climb.value / rocket_build.value, 2)), 1.5, 4),
            checks.target_range('昇降中の電力 / 炉1基の差し引き電力', net_power.map(lambda n: round(power.value / n, 2)), 0.1, 0.5)]


def cable_payload_row(view):
    return view.guid_of(CABLE).then(view.payload_row)
```

`tools/tier9_balance/sections/s3/hours_model.py`:
```python
#!/usr/bin/env python3
# S3 区間の所要時間の見積もり（判断記録 A26）。S3 の研究を順に払う時間＋解放した設備の建設時間＋最終研究の爆弾をそろえる時間
# Hour estimate of the S3 section (decision A26): paying the S3 research in order, building the unlocked facilities and stocking the final bombs
import checks
from sections import recipe_rates
from sections.s1 import sim_bridge  # noqa: F401  S1 sim を検索パスへ足す / puts the S1 sim on the search path
import timeline

# 最終研究へ一本道で並ぶ S3 の研究（最終研究の爆弾は爆弾をそろえる時間で数える）
# The S3 research chain toward the final research (the final research's bombs count as bomb stocking time)
S3_RESEARCH = ('金属超精密加工', '核融合炉', '軌道エレベーターの建設', '反物質爆弾')
# 計算データは S2 区間の便数で数えるので、ここでは払う時間に入れない
# Compute data is counted by the S2 section's launches, so it is not paid here
COUNTED_ELSEWHERE = ('計算データ',)
CHIP = 'ICチップLv1'
# S3 に回せる Tier8 終盤の工場（機械と台数。agent前提。毎秒の量はマスタのレシピから出す）
# The late-Tier8 factory share for S3 (machine and count; agent assumption, per-second rates come from master recipes)
FACTORY = {'鋼鉄板': ('電気炉', 4), 'コンピュータ': ('電気汎用工作装置', 2), '制御モータ': ('電気汎用工作装置', 2)}
# unlockBlock を持つ研究1つにつき、解放した設備を建てる時間（agent前提）
# Build time per research carrying unlockBlock (agent assumption)
BUILD_HOURS_PER_UNLOCK = 0.5


def estimate_hours(view, bomb_hours):
    # 戻り値は（検査の行, 所要時間[h] の Resolved）。研究・毎秒・爆弾のどれかが引けなければ、その状態を時間へ引き継ぐ
    # Returns (check rows, hours as a Resolved); an unreadable research, rate or bomb time passes its state on to the hours
    rows, rates = item_rates(view)
    research = {node: rates.then(lambda found: research_hours_for(view, node, found)) for node in S3_RESEARCH}
    if rates.ok:
        rows += [checks.unreadable('target', f'研究 {node} を払う時間', h) for node, h in research.items() if not h.ok]
    values = [bomb_hours] + list(research.values())
    if checks.failures(values):
        return rows, checks.carried_failure(values, 'S3 の所要時間')
    builds = sum(1 for node in S3_RESEARCH for a in view.research[node]['clearedActions'] if a['gameActionType'] == 'unlockBlock')
    return rows, checks.resolved(sum(h.value for h in research.values()) + builds * BUILD_HOURS_PER_UNLOCK + bomb_hours.value)


def item_rates(view):
    # 戻り値は（毎秒が引けない品目の行, 品目 GUID → 毎秒 の Resolved）
    # Returns (rows for items without a readable rate, a Resolved of item GUID -> per second)
    # ICチップは S1 sim のクラス B・S1 研究完了後のライン構成で毎秒（判断記録 A8 と同じクラス）
    # IC chips per second from the S1 sim's class-B lines after all S1 research (same class as decision A8)
    lines = 1 + len(timeline.EXTRA_LINES_AT_H)
    chip_per_second = timeline.line_rate('B', {n['name'] for n in timeline.RESEARCH})[0] * lines / 60
    found = {CHIP: view.guid_of(CHIP).map(lambda guid: (guid, chip_per_second))}
    for item, (block, count) in FACTORY.items():
        found[item] = recipe_rates.output_per_second(view, block, item).map(lambda rate: (rate.output_guid, rate.per_second * count))
    rows = [checks.unreadable('target', f'{item} の毎秒', value) for item, value in found.items() if not value.ok]
    if rows:
        return rows, checks.carried_failure(list(found.values()), 'S3 の品目の毎秒')
    return rows, checks.resolved(dict(value.value for value in found.values()))


def research_hours_for(view, node_name, rates):
    # 研究1つを払う時間の Resolved。品目ごとのラインは並行して動くので、最も遅い品目で決まる
    # Time to pay one research as a Resolved; item lines run in parallel, so the slowest item decides it
    if node_name not in view.research:
        return checks.unresolved(f'研究「{node_name}」が research.json に無い')
    # silent-ok: マスタに無い品目は研究のコストにも正しく現れないので除外の対象が無いだけ / コストに未知の GUID があれば毎秒の無い品目として lookup_failed の行が出る
    elsewhere = {g.value for g in map(view.guid_of, COUNTED_ELSEWHERE) if g.ok}
    seconds = [0.0]
    for cost in view.research[node_name]['consumeItems']:
        if cost['itemGuid'] in elsewhere:
            continue
        if cost['itemGuid'] not in rates:
            return checks.unresolved(f'{view.label_of(cost["itemGuid"])} の毎秒が無い（FACTORY に足す）')
        seconds.append(cost['itemCount'] / rates[cost['itemGuid']])
    return checks.resolved(max(seconds) / 3600)
```

`tools/tier9_balance/sections/s3_endgame.py`（Task 3 の空の区間を全文置き換える）:
```python
#!/usr/bin/env python3
# S3 終盤（核融合・反物質・グラフェン・エレベーター）。S3 引き継ぎ「旧 Task 4」「旧 Task 12」の検査と所要時間を返す
# S3 endgame (fusion, antimatter, graphene, elevator): the S3 handoff "old Task 4/12" checks and the hour estimate
from checks import SectionResult
from sections.s3 import graphene_elevator_checks, hours_model, power_checks


def evaluate(view):
    rows, power = power_checks.check_power(view)
    rows += graphene_elevator_checks.check_graphene(view)
    rows += graphene_elevator_checks.check_elevator(view, power.net_power)
    hour_rows, hours = hours_model.estimate_hours(view, power.bomb_hours)
    return SectionResult('s3_endgame', rows + hour_rows, hours)
```

  自己テスト（Task 19 の最小マスタ `tests/s2s3_fixture.py` を使う。値を 1 か所食い違わせると該当の行が FAIL になることを確かめる）:

`tools/tier9_balance/tests/test_s3_endgame.py`:
```python
#!/usr/bin/env python3
# S3 区間: 最小マスタで全検査が通り、ケーブル数・抽出レシピ・研究の品目を1か所変えると該当の行が FAIL になる
# S3 section: every check passes on the minimal master, and changing cable count, the extract recipe or a research item fails that row
import os
import sys
import unittest

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'))
from s2s3_fixture import build_master, find, guid, view_of  # noqa: E402
from sections import s3_endgame  # noqa: E402


def failed(result):
    return [c.name for c in result.checks if not c.passed]


def block_param(master, name):
    return next(b for b in master['blocks.json']['data'] if b['name'] == name)['blockParam']


class S3EndgameTest(unittest.TestCase):
    def test_minimal_master_passes_with_hours(self):
        result = s3_endgame.evaluate(view_of(build_master()))
        self.assertEqual([], failed(result))
        # 研究は鋼鉄板が律速で 8625 秒、建設 4 回 × 0.5 h、爆弾 3000 秒
        # Research is steel-bound at 8625 s, four builds x 0.5 h, and bombs take 3000 s
        self.assertAlmostEqual((8625 + 3000) / 3600 + 2.0, result.hours.value, places=3)

    def test_cable_count_mismatch_fails(self):
        master = build_master()
        block_param(master, '宇宙エレベーター拠点')['cablesPerElevator'] = 900
        self.assertEqual(['1エレベーター分のケーブル（積荷表の1便）'], failed(s3_endgame.evaluate(view_of(master))))

    def test_slow_extractor_fails_net_power(self):
        master = build_master()
        extract = next(r for r in master['machineRecipes.json']['data'] if r['outputFluids'] and r['outputFluids'][0]['fluidGuid'] == guid('重水素'))
        extract['time'] = 16
        rows = s3_endgame.evaluate(view_of(master)).checks
        self.assertFalse(find(rows, '炉1基の差し引き電力')[0].passed)
        # 差し引きが 0 以下になると、それで割る比は欠損とは別の not_positive で落ちる
        # Once the net is zero or less, ratios divided by it fail as not_positive, distinct from a missing value
        self.assertEqual('not_positive', find(rows, '反物質製造装置1台の電力')[0].status)
        self.assertEqual('not_positive', find(rows, '昇降中の電力')[0].status)

    def test_unknown_research_item_fails_and_hours_vanish(self):
        master = build_master()
        fusion = next(n for n in master['research.json']['data'] if n['researchNodeName'] == '核融合炉')
        fusion['consumeItems'].append({'itemGuid': guid('銅板'), 'itemCount': 10})
        result = s3_endgame.evaluate(view_of(master))
        self.assertEqual(['研究 核融合炉 を払う時間'], failed(result))
        self.assertIn('銅板 の毎秒が無い', find(result.checks, '研究 核融合炉 を払う時間')[0].detail)
        self.assertEqual('lookup_failed', result.hours.status)


if __name__ == '__main__':
    unittest.main()
```

  Run:
```bash
cd $MW && python3 -m unittest discover -s tools/tier9_balance/tests 2>&1 | tail -3
ls $MW/tools/tier9_balance/sections | wc -l; wc -l $MW/tools/tier9_balance/sections/s3/*.py $MW/tools/tier9_balance/sections/s3_endgame.py | tail -1
```
  Expected:
  - 自己テストは `Ran 17 tests` / `OK`（枠 8・S2 5・S3 4）。
  - `sections/` の直下は 8 件（`.py` 5 本と `s1/`・`s2/`・`s3/`。`__pycache__` があれば 9 件）。1 ディレクトリ 10 ファイル以下に収まる。
  - S3 の 5 ファイルは合計 194 行で、どれも 200 行以下。
- [ ] **Step 7: 照合と出荷検査**
```bash
(cd $MW && python3 tools/tier9_balance/run.py) > $GL/p2-12-s3-g6.txt; echo "s1-fail=$(grep '^FAIL \[s1_semiconductor\] ' $GL/p2-12-s3-g6.txt | grep -vc ' target out_of_range ')"
echo "s23-fail=$(grep -E '^FAIL \[(s2_compute_loop|s3_endgame)\] ' $GL/p2-12-s3-g6.txt | grep -vc ' target out_of_range ')"; grep -E '^FAIL \[(s2_compute_loop|s3_endgame)\]|^(PASS|FAIL) \[overall\]' $GL/p2-12-s3-g6.txt
cd $MW && python3 scripts/check_shipped_data_contracts.py; echo "contracts=$?"
grep -c "019e3aab-32c6\|019e3af5-e750\|019eeaa7-45b6\|3a13a5a9-d254" $MOD/master/*.json $MOD/localization/localization.csv $MOD/.mooreseditor/nodeGraph.v1.json | grep -v ":0$"
```
Expected:
- `s1-fail=0`（G6 の S1 区間の照合 8 項目に FAIL が無い）。
- `s23-fail=0`（S2・S3 の区間に、目標外〈`target out_of_range`〉以外の FAIL が無い。照合の `mismatch` も、引き当ての `lookup_failed`・`not_positive` も無い。Task 19 で残っていた 2 ノードの計算データはこの Task で入る）。
- 2 つ目の grep は、目標（`target`）の FAIL と `[overall]` の 4 行を出す。仮値のままなら次のとおり（最小マスタで同じ値を実測。出荷マスタでの一致は推測）:
  - `FAIL [s3_endgame] target out_of_range 昇降時間 / ロケット建造時間: 5.0（目標 1.5〜4）`（climbSeconds 600 ÷ buildSeconds 120）
  - `FAIL [overall] target out_of_range s2_compute_loop の所要時間[h]: 3.75（目標 4.0〜6.0）`（便数 100 × 135 秒）
  - `PASS [overall] target ok s1_semiconductor の所要時間[h]: 6.266666666666667（目標 6.0〜8.0）`（Task 12 Step 6 と同じ）
  - `PASS [overall] target ok s3_endgame の所要時間[h]: 5.229…（目標 5.0〜11.0）`（研究 8625 秒＋建設 4 回 × 0.5 h＋爆弾 3000 秒）
  - `PASS [overall] target ok Tier9 全体の所要時間[h]: 15.24…（目標 15.0〜25.0）`
  - S2・S3 の目標時間と値は Task 23 で緑にする。
- `contracts=0`。
- 最後の grep は何も出さない（フィルター分岐器・旧 3D プリンタ・超純水・旧 EUV レシピが全マスタと nodeGraph から消えている）。
- [ ] **Step 8: コミットして push する**（Task 21 の分とまとめる）
```bash
git -C $MW add design/tier9-balance/spec/09_graphene_printer_elevator.json design/tier9-balance/spec/10_endgame_research.json design/tier9-balance/guids.json server_v8/mods/moorestechAlphaMod_8/master server_v8/mods/moorestechAlphaMod_8/localization/localization.csv server_v8/mods/moorestechAlphaMod_8/.mooreseditor/nodeGraph.v1.json tools/tier9_balance
git -C $MW commit -m "master: グラフェン工程・金属3Dプリンタ・宇宙エレベーター拠点・Tier9 終盤の研究と最終研究と G6 の S3 区間を入れ、フィルター分岐器ノードを消す"
git -C $MW push
```

**完了判定:**
- G2 が exit 0 で、「最終研究」への経路が `$GL/p2-12-s3-g2.txt` に印字されている。
- G1 が PASS、許可リストの差分が空。
- nodeGraph の検査が `ng=0`（G2 と同じ並びで、参照先の消えたノード 0 件・research.json と研究ノードが 1 対 1・配置と解放が一致）。
- 赤から緑への進み方が `$GL/p2-1-base-g2.txt` → `p2-7-s1-g2.txt` → `p2-9-s2-g2.txt` → `p2-12-s3-g2.txt` の 4 ファイルで追える。
- G6 の自己テストが `Ran 17 tests` / `OK`、`s1-fail=0`・`s23-fail=0`。`[s2_compute_loop] section empty`・`[s3_endgame] section empty` の行が無い。
**量感:** JSON 約 +180 行（パッチ 4 ノードで約 150 行、最終研究約 25 行。フィルター分岐器のノード約 −20 行）。localization +4 行・−2 行。Python 約 +240 行（区間 5 ファイル約 190 行、自己テスト約 50 行）。

---

### Task 23: バランスを G6 で確定する（S1 の目標・S2 引き継ぎ F・S3 の旧 Task 4/12 の目標）

**Files:**
- Modify: `moorestech_master:design/tier9-balance/spec/0[1-9]_*.json`・`10_endgame_research.json`（値だけ）、`design/tier9-balance/sim/params.py`・`sim/timeline.py`（S1 の値の正本。Task 3 Step 7 が置いたファイル）
- Modify（再適用の結果）: `.../master/*.json`
- Create: `moorestech_master:design/tier9-balance/DECISIONS.md`（値を決めた記録。sim の出力の行を引く）
- 変えないもの: G6 の区間の前提の定数。対象は `sections/s2/launch_plan.py` の `LAUNCH_PADS`、`sections/s2/factory_load.py` の `MAX_MACHINES_PER_RECIPE`、`sections/s3/power_checks.py` の `FABRICATORS_FOR_BOMBS`、`sections/s3/hours_model.py` の `FACTORY`・`BUILD_HOURS_PER_UNLOCK`（判断記録 A25・A26）。これらは目標に合わせて動かす値ではなく、見積もりの前提だから。動かさないと収まらないときは「実装中に人へ上げる条件」の 3 つ目として人へ上げる。

**Interfaces:**
- Consumes: G6 の CLI `cd $MW && python3 tools/tier9_balance/run.py`（入口はこれだけ。判断記録 A22）と目標判定。Task 22 までのマスタ。S2 区間（Task 19 Step 5）と S3 区間（Task 22 Step 6）。
- Produces: G6 が exit 0 になる値一式。PR 本文に貼る G6 の出力（`$GL/p2-13-g6.txt`）。

**決める値と目標:**

| 決める値 | 今の仮値（出所） | 判定する目標 |
|---|---|---|
| S1 の研究コスト（特に後半 3 ノードの ICチップ） | S1 handoff「S1 の研究コストと所要時間」の表（合計 20000）。感度は S1 handoff「判断記録」の、スタック拡張II 7000→10000 で全 PASS | 半導体製造の解放から S1 の最後の研究まで 6〜8 h（クラス A/B/C）。Lv2 チップは 0% より多く 15% 以下 |
| アイテムハッチの搬送上限 `maxItemsPerSecond` | 4/秒（630fdce の暫定値） | 「大部屋集約が有利」（G6 の部屋の比較に搬送上限を入れる。S1 handoff「設計検査の持ち越し」7 項目目） |
| 計算データのコスト: アップグレード Lv2〜Lv5、軌道エレベーターの建設、反物質爆弾 | 100/200/400/800（Task 19）、200・400（S3 の仮値） | 計算データのループ（打ち上げ → 研究消費）で 4〜6 h（S2 引き継ぎ F） |
| GPU衛星の成果と 1 便の資材・建造秒 | 10/20/40/80/160、資材は筐体20・燃料20・コンピュータ5、建造120 秒＋打ち上げ15 秒（S2 引き継ぎ A・B） | 同上。上流（鋼鉄板・プラスチック・ガソリン・固形燃料）が既存の工場規模から外れないこと（S2 引き継ぎ F） |
| S3 の機械の値（炉の出力と燃料、抽出装置・反物質機の電力とレシピ、拠点の昇降秒と電力） | S3 引き継ぎ「旧 Task 1」「旧 Task 6 Step 5」 | 炉 1 基の差し引き電力がガソリン発電機 3〜5 台分。抽出装置は 2〜8 台。反物質機 1 台の電力は炉の差し引き 3〜6 基分。爆弾をそろえる時間 0.5〜3 h。昇降時間はロケット建造の 1.5〜4 倍（S3 引き継ぎ「旧 Task 4」「旧 Task 12」の check 行） |
| 全体 | — | Tier9 全体 15〜25 h、S1 区間 6〜8 h、計算データ 4〜6 h、S3 区間 5〜11 h（`.decisions/2026-10-23-Tier9の目標プレイ時間は半導体解放からクリアまでで15〜25時間.md`） |

**録画の制約（G6 の目標と別に守る）:** 段4（Task 28 Step 5）は GPU衛星Lv1 の建造・打ち上げ・飛行・到着を実時間で待つ。録画は `run-scenario.sh:56` の `RESULT_TIMEOUT=420` 秒を超えると result.json を待たずに NG になる。そのため、段4 が実時間で待つ秒の合計（1 便目の `BuildSeconds`＋打ち上げ秒＋Lv1 の飛行秒、2 便目の自動建造を最後まで待つならその `BuildSeconds` も）を 300 秒以下に保つ（残り 120 秒は PlayMode の起動約 26 秒、段の前準備、建造待ちの余裕 60 秒の分）。G6 の目標と両立しないときは、値を動かさずに人へ上げる条件に回す（判断記録「実装中に人へ上げる条件」）。録画のために `RESULT_TIMEOUT` を伸ばす変更はしない（やらないこと: 録画 DSL への時間加速の追加と同じく、録画の枠を変える変更だから）。

- [ ] **Step 1: G6 を回し、FAIL を見る**  Run: `cd $MW && python3 tools/tier9_balance/run.py > $GL/p2-13-g6-first.txt; echo "exit=$?"; grep '^FAIL' $GL/p2-13-g6-first.txt`  Expected:
- 区間ごとの `target`・`match` の行（状態つき）と、`[overall]` の目標時間の行が出る。
- `section empty` の行は無い（S2・S3 の区間は Task 19・Task 22 が入れた）。`lookup_failed`・`not_positive` の FAIL も無い（あれば値でなくマスタの欠けなので、Task 22 Step 7 に戻る）。
- 初回は exit 1 でよい。仮値のままなら、S2・S3 の FAIL は Task 22 Step 7 の 2 行（昇降時間の比 5.0 と S2 の所要時間 3.75 h）。
- [ ] **Step 2: FAIL を決まった順に 1 つずつ直して回し直す。** 1 回の試行で動かすのは 1 つの値だけにする。
  - S1 区間は S1 handoff「Task D1 Step 3」の順。
  - 計算データは (1) アップグレード 4 ノードのコスト → (2) GPU衛星の成果 → (3) 軌道エレベーター・反物質爆弾のコスト → (4) 1 便の資材の順。
    - S2 の所要時間は「便数の合計 × 1 便の秒 ÷ 3600」なので、1 便 135 秒のままなら便数の合計を 107〜160 にすれば 4〜6 h に入る。
    - 軌道エレベーターの建設と反物質爆弾は、前提にアップグレード研究が無いので GPU衛星Lv1 の成果で数える（判断記録 A25）。仮値では 20 便と 40 便。
    - (4) 1 便の資材を変えたら、工場の台数の 7 行（1 レシピ 4 台以下）も見る。
  - S3 区間は (5) S3 の研究の ICチップと鋼鉄板 → (6) S3 引き継ぎ「旧 Task 4 Step 2」の機械の値の順。
    - 昇降時間の比は、拠点の `climbSeconds` を発射台の `buildSeconds` の 1.5〜4 倍（建造 120 秒なら 180〜480 秒）にする。
    - S3 の所要時間は、研究の律速品目（仮値では鋼鉄板）・建設回数・爆弾をそろえる時間で決まる（判断記録 A26）。

  値は `sim/params.py`・`sim/timeline.py` を正とし、同じ値を該当する spec に書く。そのうえで「apply → sync → sync --check」を流し、G6 を回し直す（食い違いは `[s1_semiconductor] match mismatch` の FAIL で出る）。
  - GPU衛星の成果を 400 より大きくすると、出力口 4 × スタック 100 を超えて発射台と拠点の検証で落ちる（`wf/S2S3.md` §(4)）。その場合は `outputSlotCount` も同時に上げる。
- [ ] **Step 3: 確定した状態で全ゲートを回す**
```bash
cd $MW && python3 tools/tier9_balance/run.py > $GL/p2-13-g6.txt; echo "g6=$?"; tail -1 $GL/p2-13-g6.txt
cd $MW && python3 tools/research_sync/sync_machine_recipe_unlocks.py --check; echo "check=$?"
cd $MW && python3 tools/nodegraph_sync/sync_nodegraph.py --check | tail -1; echo "ng=${pipestatus[1]}"
G1 $MW/server_v8; (cd $MW && python3 tools/progression/check.py > /dev/null); echo "g2=$?"
```
Expected: `g6=0`・`== RESULT: PASS (0 failures / … checks)`（全目標 PASS、S1 の照合 8 項目を含む `mismatch`・`lookup_failed`・`not_positive` の FAIL 0）、`check=0`、`ng=0`（値だけの変更で配置と解放はずれない）、G1 PASS、`g2=0`。
- [ ] **Step 4: 判断記録を書く。** `DECISIONS.md` に、動かした値ごとに「前の値 → 確定値・効いた目標の行（`p2-13-g6.txt` から引用）・試行の順番」を書く。あわせて次の 1 行を書く: 「流体量は nano 化で換算しない（ADR 0090 決定 2 項目: レシピと機械タンクは double のまま。マスタの値は読み込み時に最近傍で網の単位へ変える）」。
- [ ] **Step 5: コミットして push する**
```bash
git -C $MW add design/tier9-balance server_v8/mods/moorestechAlphaMod_8/master
git -C $MW commit -m "master: Tier9 の値を G6 で確定する（S1 6〜8h・計算データ 4〜6h・S3 5〜11h・全体 15〜25h）"
git -C $MW push
```

**完了判定:** Step 3 の 5 つ（G6・sync・nodeGraph・G1・G2）がすべて 0 か PASS。`$GL/p2-13-g6.txt` に全目標の PASS 行があり、末尾が `== RESULT: PASS`。
**量感:** 数値の変更は数十行（spec と JSON）。`DECISIONS.md` は約 60 行。

---

### Task 24: マスタ PR の作成・本体ピンの更新・e2e registry fixture（S1 handoff「Task D8」・S2 引き継ぎ G・S3 引き継ぎ「旧 Task 3 Step 4」「旧 Task 10 Step 6」）

**Files:**
- Modify: `.moorestech-external-revisions.json`（本体 `$W`。`moorestech_master.commitHash`）
- Modify: `moorestech_web/webui/e2e/fixtures/v8-block-ui-registry.json`
- Modify: `moorestech_web/webui/e2e/tests/block/blockRegistryCoverage.spec.ts`（`intentionalGeneric` に `ElectricGenerator`）

場所は git grep で確認した: `git grep -l v8-block-ui-registry origin/feat/tier9-fusion-elevator-endgame` → `blockRegistryCoverage.spec.ts`。fixture の再生成方法は同 spec の 24〜25 行目に書いてある。

**Interfaces:**
- Consumes: Task 23 まで push 済みの `$MW` の HEAD（`<MASTER_SHA>`）。
- Produces:
  - moorestech_master の PR（`feat/tier9-integrated` → `master`）。
  - 本体ピン `moorestech_master.commitHash == <MASTER_SHA>`。
  - `run-scenario.sh` が HEAD とピンの一致する master worktree として `$MW` を選べる状態（S1 handoff「Task D9」Interfaces の自動選択）。

- [ ] **Step 1: マスタ PR を作る**（AGENTS.md「関連リポジトリ」: 別 repo の変更も push して PR を作る。マージはしない。#87 と #88 は最終節で「統合 PR に置き換え」として閉じる）
```bash
MASTER_SHA=$(git -C $MW rev-parse HEAD); git -C $MW status --short; git -C $MW log origin/feat/tier9-integrated..HEAD --oneline   # 未 push が無いこと
gh pr create -R moorestech/moorestech_master --base master --head feat/tier9-integrated \
  --title "Tier9 統合マスタ（#88 S1 + #87 S2 + 中身の投入）" --body-file $GL/master-pr-body.md
```
`master-pr-body.md` に入れるもの:
- 構成: #88・#87 のマージと、S1/S2/S3 のスライス。
- `$GL/p2-1-base-g2.txt` から `p2-12-s3-g2.txt` までの G2 の赤から緑への要約。
- `p2-13-g6.txt` の目標表。
- 裁定2 の P1 へ渡す表（この節の「裁定2 との整合」）。
- 末尾の生成の記載（system の帰属行）。

Expected: PR の URL が出る。
- [ ] **Step 2: 本体のピンを向ける。** S1 handoff「Task D8 Step 3」の Python を `$W` で `MASTER_SHA` に対して流す。  Expected: `1 file changed, 1 insertion(+), 1 deletion(-)`
- [ ] **Step 3: e2e fixture を出荷マスタから作り直す**
```bash
cd $W && python3 - "$MOD/master/blocks.json" moorestech_web/webui/e2e/fixtures/v8-block-ui-registry.json <<'EOF'
import json, sys
# blocks.json の [blockType, openable] を sort -u して fixture の書式で書く（blockRegistryCoverage.spec.ts:24 の再生成手順）
# Write sort -u of [blockType, openable] from blocks.json in the fixture format (regeneration step in blockRegistryCoverage.spec.ts:24)
pairs = sorted({(b["blockType"], b["openable"]) for b in json.load(open(sys.argv[1], encoding="utf-8"))["data"]})
lines = [f'  {{ "blockType": "{t}", "openable": {"true" if o else "false"} }}' for t, o in pairs]
open(sys.argv[2], "w", encoding="utf-8").write("[\n" + ",\n".join(lines) + "\n]\n")
EOF
git diff -U0 moorestech_web/webui/e2e/fixtures/v8-block-ui-registry.json
```
Expected: 差分は 3 行の追加だけ: `ElectricGenerator` true（核融合炉）、`RocketLaunchPad` true、`SpaceElevatorBase` true。
- `ElectricGenerator` false の行は、ガソリンエンジン発電機などが残るので消えない。
- `Block` false も、基本土台・アスファルト土台が残るので消えない。
- それ以外の差分が出たら（P1 のマージ結果と出荷マスタの openable の食い違い。例: `CleanRoomAirFilter`）、P1 の解決と照らして原因を特定する。
- [ ] **Step 4: 発電機を意図的 Generic に入れる。** 専用 view を作らず、汎用の SectionStackView で出すため（S3 引き継ぎ「旧 Task 3 Step 4」）。`RocketLaunchPad` と `SpaceElevatorBase` はレジストリ登録済み（S3 ブランチ `blockComponentRegistry.ts:28-29`）なので足さない。`blockRegistryCoverage.spec.ts` の `intentionalGeneric` の `"ElectricMachine",` の前に 1 行足す:
```ts
  "ElectricGenerator",
```
- [ ] **Step 5: e2e とロードを確かめる**
```bash
cd $W/moorestech_web/webui && npx playwright test --config e2e/playwright.config.ts e2e/tests/block/blockRegistryCoverage.spec.ts
cd $W && uloop get-logs --project-path ./moorestech_client --log-type Error | grep -E "MooresmasterLoaderException|\[BlockMaster\]|\[ItemMaster\]" ; echo "grep=$?"
```
Expected: playwright は 1 passed。Editor でピンのマスタを読み直した後（RepositorySync がピンへ checkout する。S2 引き継ぎ G）、grep は `grep=1`（該当行なし）。
- [ ] **Step 6: コミット**
```bash
cd $W && git add .moorestech-external-revisions.json moorestech_web/webui/e2e/fixtures/v8-block-ui-registry.json moorestech_web/webui/e2e/tests/block/blockRegistryCoverage.spec.ts
git commit -m "chore: Tier9 統合マスタ（moorestech_master feat/tier9-integrated）へピンを向け、e2e の block UI fixture を出荷マスタから作り直す"
git push
```

**完了判定:**
- マスタ PR の URL がある。
- `git -C $W show HEAD:.moorestech-external-revisions.json` の commitHash が `git -C $MW rev-parse origin/feat/tier9-integrated` と一致する。
- playwright が 1 passed。
- Editor のエラーログにマスタのロード失敗が無い。
**量感:** 本体の差分は 5 行（ピン 1 行、fixture 3 行、spec.ts 1 行）。

---



## P4 動的検証（G4 結合テスト・G5 ゴールデンセーブ）

担当は B（副）の W2。作業場所は本体 worktree `~/hermes-agent/data/worktrees/moorestech/tier9-integrated-tests`（branch `feat/tier9-integrated-tests`。J1 後の `feat/tier9-integrated` から切る）。CurrentVersion は触らない（A の担当。ORCH「セーブ移行の版は A だけが触る」）。Task 26 は Task 25 のテスト mod 追加（研究 `c0de…011`/`013`）を使うので、Task 25 → Task 26 の順に進める。

前提の事実（根拠）:
- 発射台は、初打ち上げ前だけ「建造の合図→発射の合図」を待つ。2便目からは自動で進む。到着は Launching に入った時点で起きる（`Tests/CombinedTest/Game/RocketLaunchPad/RocketLaunchPadLaunchFlowTest.cs:32-56`・`:107-123`、S3 先端）。
- `processor.Phase` は private。段階は `RocketLaunchPadTestUtil.ReadState(processor).Phase` で読む（`Tests/Util/Rocket/RocketLaunchPadTestUtil.cs:24-28`）。
- 網の再構築で、テスト用の発電機が外れる（`RocketLaunchPadLaunchFlowTest.cs:131-132` のコメント）。電気の消費ブロックを全部置いてから給電する。
- ケーブルは発射台の出力口に入らず、プールへ入る（`Game.Block/Blocks/RocketLaunchPad/RocketLaunchPadOutputSlots.cs:39-41`）。接続は `SpaceElevatorConnectionService.Connect` で、1基分をプールから引く（`Game.SpaceElevator/SpaceElevatorConnectionService.cs:75-85`）。
- 研究は、プレイヤーのメインインベントリから素材を消費する。完了すると ClearedActions を実行する（`Game.Research/ResearchDataStore.cs:33-53`、S1 先端）。
- 製作上限は、継承先ファミリーの基準 GUID ごとに持つ（`Game.Block/Blocks/Machine/Upgrade/LevelInheritanceCraftCapDataStore.cs:35-48`）。

### Task 25: G4 結合テスト（ICチップ→GPU→GPU衛星→打ち上げ→計算データ→研究で消費→拠点の解放→ケーブル到着→接続→昇降）

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/items.json`（アイテム6・levelFamilies 2・orbitalPayloads 2）
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/machineRecipes.json`（レシピ3）
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/research.json`（研究3）
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/blocks.json`（`TestSpaceElevatorBase` の `carriedPayloadItemGuids` に2件を足す）
- Create: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTestTier9ChainIds.cs`
- Create: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/Tier9/Tier9ComputeLoopTestHelper.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/Tier9/Tier9ComputeLoopEndToEndTest.cs`

**Interfaces:**
- Consumes（すべて統合ブランチに既にある）:
  - S1: `LevelInheritanceTestHelper.PlaceMachine(Vector3Int)`・`Recipe(Guid)`・`Create(ItemId,int)`・`CreateTest1(int)`・`AdvanceTicksWithFullPower(int, VanillaMachineProcessorComponent)`・`TicksToComplete(MachineRecipeMasterElement)`・`CountItem(IOpenableInventory,int,int,ItemId)`・`InputSlotCount`/`OutputSlotCount`（`Tests/CombinedTest/Core/Machine/Upgrade/LevelInheritanceTestHelper.cs:21-84`）。`MachineRecipeSelectTestUtil.SelectRecipe(IBlock, MachineRecipeMasterElement)`（Tests.Util）。`VanillaMachineBlockInventoryComponent.CheckPlacement(int, IItemStack)` → `MachineSlotPlacementCheck`。`ILevelInheritanceCraftCapLookup.GetCraftCap(Guid)`。`MasterHolder.ItemMaster.GetLevelVariantItemId(ItemId,int)`（`Tests.Module/TestMod/ForUnitTestLevelInheritanceIds.cs:33-36`）
  - S2: `RocketLaunchPadTestUtil.PlacePoweredPad`・`ReadState`・`GetLayout(...).OutputSlots`・`FillOneLaunch`・`RunUntilPhase`。`RocketLaunchPadProcessorComponent.RequestBuild()`/`RequestLaunch()` → `RocketLaunchPadCommandResult`。`RocketLaunchHistory.HasLaunchedOnce`（Game.Orbit）。`ForUnitTestItemId.ComputeDataId`
  - S3: `SpaceElevatorTestUtil.PlaceBase`・`PowerBase`・`RunSeconds`。`SpaceElevatorCablePool.Amount`。`SpaceElevatorConnectionService.Connect(IBlock)` → `SpaceElevatorConnectResult`。`SpaceElevatorBaseInventoryComponent`（入口はスロット0、出口はスロット1）。`ForUnitTestItemId.TestElevatorCable`
  - 共通: `IResearchDataStore.CompleteResearch(Guid,int)`/`GetResearchNodeStates(int)`。`IGameUnlockStateDataController.BlockUnlockStateInfos[Guid].IsUnlocked`。`PlayerIdentityTestHelper.Register(...).PlayerId`。`IPlayerInventoryDataStore.GetInventoryData(int).MainOpenableInventory`
- Produces:
  - `Tests.Module.TestMod.ForUnitTestTier9ChainIds`（下の Step 2 の定数と `IcChip(int)`/`Gpu(int)`/`GpuSatellite(int)`）
  - テスト mod の研究 `c0de0000-0000-4000-8000-000000000011`（ロケット技術。テスト用の発射台を解放）と `c0de0000-0000-4000-8000-000000000013`（軌道エレベーターの建設。テスト用の拠点を解放）。Task 26 がこの2件を使う
  - `Tier9ComputeLoopTestHelper` の `RunOneCraft`・`TakeOutput`・`LaunchFirst`・`LaunchNext`・`MovePadOutputsToPlayer`・`CountPlayerItem`

- [ ] **Step 1: テスト mod にデータを足す**

GUID は既存と衝突しない（既存は items が `c0de-…011〜033`、レシピが `0000c0de0001〜0003`、研究が `c0de0000-…001〜003`、family が `9876-…001〜004`。S1 先端の items.json・machineRecipes.json・research.json で確認した）。sortPriority の最大は S3 で 860、S1 で 878 なので、901〜906 を使う。

`items.json` の `data` の末尾に足す:
```json
{"name": "TestTier9IcChipLv1", "itemGuid": "00000000-0000-0000-c0de-000000000041", "imagePath": "Test1", "sortPriority": 901, "recipeViewType": "ForceView", "initialUnlocked": true, "stackLevelTableGuid": "11111111-1111-1111-1111-111111111111"},
{"name": "TestTier9IcChipLv2", "itemGuid": "00000000-0000-0000-c0de-000000000042", "imagePath": "Test1", "sortPriority": 902, "recipeViewType": "ForceView", "initialUnlocked": true, "stackLevelTableGuid": "11111111-1111-1111-1111-111111111111"},
{"name": "TestTier9GpuLv1", "itemGuid": "00000000-0000-0000-c0de-000000000051", "imagePath": "Test1", "sortPriority": 903, "recipeViewType": "ForceView", "initialUnlocked": true, "stackLevelTableGuid": "11111111-1111-1111-1111-111111111111"},
{"name": "TestTier9GpuLv2", "itemGuid": "00000000-0000-0000-c0de-000000000052", "imagePath": "Test1", "sortPriority": 904, "recipeViewType": "ForceView", "initialUnlocked": true, "stackLevelTableGuid": "11111111-1111-1111-1111-111111111111"},
{"name": "TestTier9GpuSatelliteLv1", "itemGuid": "00000000-0000-0000-c0de-000000000061", "imagePath": "Test1", "sortPriority": 905, "recipeViewType": "ForceView", "initialUnlocked": true, "stackLevelTableGuid": "11111111-1111-1111-1111-111111111111"},
{"name": "TestTier9GpuSatelliteLv2", "itemGuid": "00000000-0000-0000-c0de-000000000062", "imagePath": "Test1", "sortPriority": 906, "recipeViewType": "ForceView", "initialUnlocked": true, "stackLevelTableGuid": "11111111-1111-1111-1111-111111111111"}
```
衛星はレベル別の独立アイテムにし、family を作らない（既存裁定「GPU衛星はレベル別アイテム」。family に入れると、代用の自動揃え W13 が衛星にも効いて、検証したい経路が濁る）。

`items.json` の `levelFamilies` の末尾に足す:
```json
{"familyGuid": "00000000-0000-0000-9876-000000000011", "name": "TestTier9IcChipFamily", "baseItemGuid": "00000000-0000-0000-c0de-000000000041", "levelItemGuids": ["00000000-0000-0000-c0de-000000000041", "00000000-0000-0000-c0de-000000000042"]},
{"familyGuid": "00000000-0000-0000-9876-000000000012", "name": "TestTier9GpuFamily", "baseItemGuid": "00000000-0000-0000-c0de-000000000051", "levelItemGuids": ["00000000-0000-0000-c0de-000000000051", "00000000-0000-0000-c0de-000000000052"]}
```

`items.json` の `orbitalPayloads` の末尾に足す（S3 先端の items.json:802-826 と同じ形）:
```json
{"payloadItemGuid": "00000000-0000-0000-c0de-000000000061", "countPerLaunch": 1, "arrivalOutputs": [{"itemGuid": "5c8e0000-0000-4000-8000-000000000006", "count": 10}]},
{"payloadItemGuid": "00000000-0000-0000-c0de-000000000062", "countPerLaunch": 1, "arrivalOutputs": [{"itemGuid": "5c8e0000-0000-4000-8000-000000000006", "count": 20}]}
```

`machineRecipes.json` の `data` の末尾に足す。機械はすべて S1 の TestLevelInheritMachine `ffffffff-0000-4000-8000-00000000c0de` を使う（入力2・出力2。形は S1 の `0000c0de0002` と同じ）:
```json
{"time": 1, "blockGuid": "ffffffff-0000-4000-8000-00000000c0de", "initialUnlocked": true, "inputItems": [{"count": 2, "itemGuid": "00000000-0000-0000-c0de-000000000041"}], "inputFluids": [], "outputItems": [{"percent": 1, "count": 1, "itemGuid": "00000000-0000-0000-c0de-000000000051"}], "outputFluids": [], "machineRecipeGuid": "00000000-0000-4000-8000-0000c0de0011", "levelInheritance": "InheritUpToUnlocked"},
{"time": 1, "blockGuid": "ffffffff-0000-4000-8000-00000000c0de", "initialUnlocked": true, "inputItems": [{"count": 1, "itemGuid": "00000000-0000-0000-c0de-000000000051"}, {"count": 1, "itemGuid": "00000000-0000-0000-1234-000000000001"}], "inputFluids": [], "outputItems": [{"percent": 1, "count": 1, "itemGuid": "00000000-0000-0000-c0de-000000000061"}], "outputFluids": [], "machineRecipeGuid": "00000000-0000-4000-8000-0000c0de0012", "levelInheritance": "None"},
{"time": 1, "blockGuid": "ffffffff-0000-4000-8000-00000000c0de", "initialUnlocked": true, "inputItems": [{"count": 1, "itemGuid": "00000000-0000-0000-c0de-000000000052"}, {"count": 1, "itemGuid": "00000000-0000-0000-1234-000000000001"}], "inputFluids": [], "outputItems": [{"percent": 1, "count": 1, "itemGuid": "00000000-0000-0000-c0de-000000000062"}], "outputFluids": [], "machineRecipeGuid": "00000000-0000-4000-8000-0000c0de0013", "levelInheritance": "None"}
```

`research.json` の `data` の末尾に足す。`graphViewSettings` は S1 の `c0de0000-…0001` の値をそのまま写す（research.json:291-320）。下では読みやすさのため `graphViewSettings` を省いて書く。実ファイルには必ず入れる:
```json
{"researchNodeGuid": "c0de0000-0000-4000-8000-000000000011", "researchNodeName": "Test Tier9 Rocket Technology", "researchNodeDescription": "G4: unlocks the test launch pad", "prevResearchNodeGuid": "", "clearedActions": [{"gameActionType": "unlockBlock", "gameActionParam": {"unlockBlockGuids": ["5c8e0000-0000-4000-8000-000000000100"]}}], "consumeItems": [], "prevResearchNodeGuids": []},
{"researchNodeGuid": "c0de0000-0000-4000-8000-000000000012", "researchNodeName": "Test Tier9 Gpu Upgrade Lv2", "researchNodeDescription": "G4: raises the test GPU craft cap with compute data", "prevResearchNodeGuid": "", "clearedActions": [{"gameActionType": "unlockLevelInheritanceCraftLevel", "gameActionParam": {"targetItemGuids": ["00000000-0000-0000-c0de-000000000051"], "level": 2}}], "consumeItems": [{"itemGuid": "5c8e0000-0000-4000-8000-000000000006", "itemCount": 10}], "prevResearchNodeGuids": ["c0de0000-0000-4000-8000-000000000011"]},
{"researchNodeGuid": "c0de0000-0000-4000-8000-000000000013", "researchNodeName": "Test Tier9 Orbital Elevator Construction", "researchNodeDescription": "G4: unlocks the test space elevator base with compute data", "prevResearchNodeGuid": "", "clearedActions": [{"gameActionType": "unlockBlock", "gameActionParam": {"unlockBlockGuids": ["5e1ea7a0-0001-4000-8000-000000000001"]}}], "consumeItems": [{"itemGuid": "5c8e0000-0000-4000-8000-000000000006", "itemCount": 10}], "prevResearchNodeGuids": ["c0de0000-0000-4000-8000-000000000011"]}
```

`blocks.json` の `TestSpaceElevatorBase`（`5e1ea7a0-0001-4000-8000-000000000001`）の `blockParam.carriedPayloadItemGuids` を次にする:
```json
"carriedPayloadItemGuids": ["5e1ea7a0-0002-4000-8000-000000000002", "00000000-0000-0000-c0de-000000000061", "00000000-0000-0000-c0de-000000000062"]
```
テスト用の発射台と拠点の `blocks.json` 行には `initialUnlocked` が無い（S3 先端の blocks.json。拠点の行は summary「initialUnlocked 無し＝false」）。研究で解放されたことを assert できるのはこのため。要確認（実装時に Step 6 のテストの最初の `Assert.IsFalse` で確かめる）。

- [ ] **Step 2: ID の定数クラスを作る**

`moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTestTier9ChainIds.cs`:
```csharp
using System;
using Core.Master;

namespace Tests.Module.TestMod
{
    /// <summary>
    ///     G4 結合テストで使う、ICチップ→GPU→GPU衛星→計算データの連鎖の GUID と ID
    ///     GUIDs and IDs of the IC chip -> GPU -> GPU satellite -> compute data chain used by the G4 test
    /// </summary>
    public static class ForUnitTestTier9ChainIds
    {
        public static readonly Guid IcChipBaseGuid = Guid.Parse("00000000-0000-0000-c0de-000000000041");
        public static readonly Guid GpuBaseGuid = Guid.Parse("00000000-0000-0000-c0de-000000000051");
        public static readonly Guid GpuSatelliteLv1Guid = Guid.Parse("00000000-0000-0000-c0de-000000000061");
        public static readonly Guid GpuSatelliteLv2Guid = Guid.Parse("00000000-0000-0000-c0de-000000000062");

        public static readonly Guid GpuRecipeGuid = Guid.Parse("00000000-0000-4000-8000-0000c0de0011");
        public static readonly Guid GpuSatelliteLv1RecipeGuid = Guid.Parse("00000000-0000-4000-8000-0000c0de0012");
        public static readonly Guid GpuSatelliteLv2RecipeGuid = Guid.Parse("00000000-0000-4000-8000-0000c0de0013");

        public static readonly Guid RocketTechnologyResearchGuid = Guid.Parse("c0de0000-0000-4000-8000-000000000011");
        public static readonly Guid GpuUpgradeLv2ResearchGuid = Guid.Parse("c0de0000-0000-4000-8000-000000000012");
        public static readonly Guid OrbitalElevatorConstructionResearchGuid = Guid.Parse("c0de0000-0000-4000-8000-000000000013");

        public static readonly Guid RocketLaunchPadBlockGuid = Guid.Parse("5c8e0000-0000-4000-8000-000000000100");
        public static readonly Guid SpaceElevatorBaseBlockGuid = Guid.Parse("5e1ea7a0-0001-4000-8000-000000000001");

        public static ItemId IcChip(int level) => Variant(IcChipBaseGuid, level);
        public static ItemId Gpu(int level) => Variant(GpuBaseGuid, level);
        public static ItemId GpuSatellite(int level) => MasterHolder.ItemMaster.GetItemId(level == 1 ? GpuSatelliteLv1Guid : GpuSatelliteLv2Guid);

        private static ItemId Variant(Guid baseGuid, int level)
        {
            return MasterHolder.ItemMaster.GetLevelVariantItemId(MasterHolder.ItemMaster.GetItemId(baseGuid), level);
        }
    }
}
```

- [ ] **Step 3: 補助クラスを作る**

`moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/Tier9/Tier9ComputeLoopTestHelper.cs`:
```csharp
using Core.Item.Interface;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Blocks.RocketLaunchPad;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Block.Interface.State;
using Game.Context;
using Game.PlayerInventory.Interface;
using NUnit.Framework;
using Tests.Util.Rocket;
using static Tests.CombinedTest.Core.Machine.Upgrade.LevelInheritanceTestHelper;

namespace Tests.CombinedTest.Game.Tier9
{
    // G4 の連鎖で使う、1回分の加工・出力口からの取り出し・打ち上げ・プレイヤーへの移し替え
    // One craft, output take-out, launch and hand-over to the player used by the G4 chain
    public static class Tier9ComputeLoopTestHelper
    {
        private static int PhaseTimeoutTicks => (int)GameUpdater.SecondsToTicks(10);

        public static void RunOneCraft(VanillaMachineProcessorComponent processor, System.Guid recipeGuid)
        {
            AdvanceTicksWithFullPower(TicksToComplete(Recipe(recipeGuid)), processor);
        }

        // 出力口から指定数を抜き、抜いた分のスタックを返す
        // Take the given count out of the output slots and return it as a stack
        public static IItemStack TakeOutput(VanillaMachineBlockInventoryComponent inventory, ItemId itemId, int count)
        {
            for (var slot = InputSlotCount; slot < InputSlotCount + OutputSlotCount; slot++)
            {
                var stack = inventory.GetItem(slot);
                if (stack.Id != itemId || stack.Count < count) continue;
                inventory.SetItem(slot, stack.SubItem(count));
                return Create(itemId, count);
            }
            Assert.Fail($"出力口に {itemId} が {count} 個ありません");
            return null;
        }

        // 初打ち上げは建造と発射の合図を本番と同じ口から送り、到着まで進める
        // The first launch sends build and launch commands through the production entry and runs until arrival
        public static void LaunchFirst(IBlock pad)
        {
            var processor = pad.GetComponent<RocketLaunchPadProcessorComponent>();
            Assert.IsTrue(RocketLaunchPadTestUtil.RunUntilPhase(pad, RocketLaunchPadPhase.AwaitingBuildCommand, PhaseTimeoutTicks));
            Assert.AreEqual(RocketLaunchPadCommandResult.Accepted, processor.RequestBuild());
            Assert.IsTrue(RocketLaunchPadTestUtil.RunUntilPhase(pad, RocketLaunchPadPhase.AwaitingLaunchCommand, PhaseTimeoutTicks));
            Assert.AreEqual(RocketLaunchPadCommandResult.Accepted, processor.RequestLaunch());
            Assert.IsTrue(RocketLaunchPadTestUtil.RunUntilPhase(pad, RocketLaunchPadPhase.Launching, PhaseTimeoutTicks));
        }

        // 2便目以降は前の便が終わるのを待ってから積み、合図なしで到着まで進める
        // Later launches wait for the previous flight, load, and run to arrival without commands
        public static void LaunchNext(IBlock pad, ItemId payloadItemId, int payloadCount)
        {
            Assert.IsTrue(RocketLaunchPadTestUtil.RunUntilPhase(pad, RocketLaunchPadPhase.Idle, PhaseTimeoutTicks));
            RocketLaunchPadTestUtil.FillOneLaunch(pad, payloadItemId, payloadCount);
            Assert.IsTrue(RocketLaunchPadTestUtil.RunUntilPhase(pad, RocketLaunchPadPhase.Launching, PhaseTimeoutTicks));
        }

        // 発射台の出力口の中身を全部プレイヤーへ移し、移した数を返す
        // Move everything in the pad's output slots to the player and return the moved count
        public static int MovePadOutputsToPlayer(IBlock pad, int playerId)
        {
            var padInventory = pad.GetComponent<IOpenableBlockInventoryComponent>();
            var playerInventory = ServerContext.GetService<IPlayerInventoryDataStore>().GetInventoryData(playerId).MainOpenableInventory;
            var moved = 0;
            foreach (var slot in RocketLaunchPadTestUtil.GetLayout(pad).OutputSlots)
            {
                var stack = padInventory.GetItem(slot);
                if (stack.Count == 0) continue;
                moved += stack.Count;
                Assert.AreEqual(0, playerInventory.InsertItem(stack).Count);
                padInventory.SetItem(slot, ServerContext.ItemStackFactory.CreatEmpty());
            }
            return moved;
        }

        public static int CountPlayerItem(int playerId, ItemId itemId)
        {
            var playerInventory = ServerContext.GetService<IPlayerInventoryDataStore>().GetInventoryData(playerId).MainOpenableInventory;
            return CountItem(playerInventory, 0, playerInventory.GetSlotSize(), itemId);
        }
    }
}
```
`stack.SubItem(count)` の出所は `MachineInputSlotBinding.cs:112`。出力口へ `SetItem` で書くのは `RocketLaunchPadTestUtil.cs:60-66` と同じ扱い（取り出し専用の口は置き場の検査を通らない）。

- [ ] **Step 4: テスト本体を書く**

`moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/Tier9/Tier9ComputeLoopEndToEndTest.cs`:
```csharp
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Blocks.SpaceElevator;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Block.Interface.MachineUpgrade;
using Game.Context;
using Game.Orbit;
using Game.PlayerIdentity;
using Game.Research;
using Game.SpaceElevator;
using Game.UnlockState;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.CombinedTest.Game.SpaceElevator;
using Tests.Module.TestMod;
using Tests.Util;
using Tests.Util.PlayerIdentity;
using Tests.Util.Rocket;
using UnityEngine;
using static Tests.CombinedTest.Core.Machine.Upgrade.LevelInheritanceTestHelper;
using static Tests.CombinedTest.Game.Tier9.Tier9ComputeLoopTestHelper;
using Ids = Tests.Module.TestMod.ForUnitTestTier9ChainIds;

namespace Tests.CombinedTest.Game.Tier9
{
    /// <summary>
    ///     ICチップから GPU 衛星を作って打ち上げ、計算データで研究し、ケーブルで拠点を接続して昇らせるまでを1本で通す
    ///     Runs IC chip -> GPU satellite -> launch -> compute-data research -> cable connection -> climb in one test
    /// </summary>
    public class Tier9ComputeLoopEndToEndTest
    {
        [Test]
        public void ICチップから計算データの研究と拠点の昇降まで通る()
        {
            var (_, provider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var playerId = PlayerIdentityTestHelper.Register(provider.GetRequiredService<PlayerIdentityRegistry>(), "steam:1").PlayerId;
            var research = provider.GetRequiredService<IResearchDataStore>();
            var unlocks = provider.GetRequiredService<IGameUnlockStateDataController>();

            // 拠点と機械を先に置き、発射台は最後に給電つきで置く（網の再構築でテスト発電機が外れるため）
            // Place the base and machines first and the powered pad last (rebuilds drop injected generators)
            var basePosition = new Vector3Int(10, 0, 0);
            var elevatorBase = SpaceElevatorTestUtil.PlaceBase(basePosition);
            var (gpuBlock, gpuInventory, gpuProcessor) = PlaceMachine(new Vector3Int(20, 0, 0));
            var (sat1Block, sat1Inventory, sat1Processor) = PlaceMachine(new Vector3Int(24, 0, 0));
            var (sat2Block, sat2Inventory, sat2Processor) = PlaceMachine(new Vector3Int(28, 0, 0));
            var pad = RocketLaunchPadTestUtil.PlacePoweredPad(Vector3Int.zero, 100);
            MachineRecipeSelectTestUtil.SelectRecipe(gpuBlock, Recipe(Ids.GpuRecipeGuid));
            MachineRecipeSelectTestUtil.SelectRecipe(sat1Block, Recipe(Ids.GpuSatelliteLv1RecipeGuid));
            MachineRecipeSelectTestUtil.SelectRecipe(sat2Block, Recipe(Ids.GpuSatelliteLv2RecipeGuid));

            // 上限1の間は ICチップLv2 を断り、Lv1 から GPU Lv1 を2個作る
            // While the cap is 1 the IC chip Lv2 is refused; Lv1 chips make two GPU Lv1
            Assert.AreEqual(MachineSlotPlacementCheck.LevelAboveCraftCap, gpuInventory.CheckPlacement(0, Create(Ids.IcChip(2), 1)));
            gpuInventory.InsertItem(Create(Ids.IcChip(1), 4));
            RunOneCraft(gpuProcessor, Ids.GpuRecipeGuid);
            RunOneCraft(gpuProcessor, Ids.GpuRecipeGuid);
            Assert.AreEqual(2, CountItem(gpuInventory, InputSlotCount, InputSlotCount + OutputSlotCount, Ids.Gpu(1)));

            // GPU Lv1 から衛星Lv1 を2個作る（打ち上げ用と拠点用）
            // Make two satellite Lv1 from GPU Lv1 (one to launch, one for the elevator)
            sat1Inventory.InsertItem(TakeOutput(gpuInventory, Ids.Gpu(1), 2));
            sat1Inventory.InsertItem(CreateTest1(2));
            RunOneCraft(sat1Processor, Ids.GpuSatelliteLv1RecipeGuid);
            RunOneCraft(sat1Processor, Ids.GpuSatelliteLv1RecipeGuid);
            Assert.AreEqual(2, CountItem(sat1Inventory, InputSlotCount, InputSlotCount + OutputSlotCount, Ids.GpuSatellite(1)));

            // ロケット技術で発射台が解放され、初打ち上げで計算データ10が出力口に出る
            // Rocket technology unlocks the pad; the first launch puts 10 compute data in the outputs
            Assert.IsFalse(unlocks.BlockUnlockStateInfos[Ids.RocketLaunchPadBlockGuid].IsUnlocked);
            Assert.IsTrue(research.CompleteResearch(Ids.RocketTechnologyResearchGuid, playerId));
            Assert.IsTrue(unlocks.BlockUnlockStateInfos[Ids.RocketLaunchPadBlockGuid].IsUnlocked);
            TakeOutput(sat1Inventory, Ids.GpuSatellite(1), 1);
            RocketLaunchPadTestUtil.FillOneLaunch(pad, Ids.GpuSatellite(1), 1);
            LaunchFirst(pad);
            Assert.IsTrue(ServerContext.GetService<RocketLaunchHistory>().HasLaunchedOnce);
            Assert.AreEqual(10, MovePadOutputsToPlayer(pad, playerId));

            // 計算データ10を研究で消費して GPU の上限が2になり、ICチップLv2 から GPU Lv2 ができる
            // Spending 10 compute data raises the GPU cap to 2, and IC chip Lv2 makes GPU Lv2
            Assert.AreEqual(ResearchNodeState.Researchable, research.GetResearchNodeStates(playerId)[Ids.GpuUpgradeLv2ResearchGuid]);
            Assert.IsTrue(research.CompleteResearch(Ids.GpuUpgradeLv2ResearchGuid, playerId));
            Assert.AreEqual(0, CountPlayerItem(playerId, ForUnitTestItemId.ComputeDataId));
            Assert.AreEqual(2, provider.GetRequiredService<ILevelInheritanceCraftCapLookup>().GetCraftCap(Ids.GpuBaseGuid));
            gpuInventory.InsertItem(Create(Ids.IcChip(2), 2));
            RunOneCraft(gpuProcessor, Ids.GpuRecipeGuid);

            // GPU Lv2 から衛星Lv2 を作り、2便目は合図なしで飛んで計算データ20を出す
            // Make satellite Lv2 from GPU Lv2; the second flight needs no command and yields 20 compute data
            sat2Inventory.InsertItem(TakeOutput(gpuInventory, Ids.Gpu(2), 1));
            sat2Inventory.InsertItem(CreateTest1(1));
            RunOneCraft(sat2Processor, Ids.GpuSatelliteLv2RecipeGuid);
            TakeOutput(sat2Inventory, Ids.GpuSatellite(2), 1);
            LaunchNext(pad, Ids.GpuSatellite(2), 1);
            Assert.AreEqual(20, MovePadOutputsToPlayer(pad, playerId));

            // 軌道エレベーターの建設で拠点が解放され、計算データが10残る
            // Orbital elevator construction unlocks the base and leaves 10 compute data
            Assert.IsFalse(unlocks.BlockUnlockStateInfos[Ids.SpaceElevatorBaseBlockGuid].IsUnlocked);
            Assert.IsTrue(research.CompleteResearch(Ids.OrbitalElevatorConstructionResearchGuid, playerId));
            Assert.IsTrue(unlocks.BlockUnlockStateInfos[Ids.SpaceElevatorBaseBlockGuid].IsUnlocked);
            Assert.AreEqual(10, CountPlayerItem(playerId, ForUnitTestItemId.ComputeDataId));

            // ケーブル50本の便を本当に2回打ち上げてプールを100にし、拠点を接続する
            // Really launch two 50-cable flights to fill the pool to 100, then connect the base
            var pool = ServerContext.GetService<SpaceElevatorCablePool>();
            LaunchNext(pad, ForUnitTestItemId.TestElevatorCable, 50);
            LaunchNext(pad, ForUnitTestItemId.TestElevatorCable, 50);
            Assert.AreEqual(100, pool.Amount);
            Assert.AreEqual(SpaceElevatorConnectResult.Connected, ServerContext.GetService<SpaceElevatorConnectionService>().Connect(elevatorBase));
            Assert.AreEqual(0, pool.Amount);

            // 給電した拠点で衛星Lv1 が昇り、出口に計算データ10が出る
            // On the powered base satellite Lv1 climbs and 10 compute data come out
            SpaceElevatorTestUtil.PowerBase(elevatorBase, basePosition, 100f);
            var baseInventory = elevatorBase.GetComponent<SpaceElevatorBaseInventoryComponent>();
            baseInventory.SetItem(0, TakeOutput(sat1Inventory, Ids.GpuSatellite(1), 1));
            SpaceElevatorTestUtil.RunSeconds(2.2);
            Assert.AreEqual(ForUnitTestItemId.ComputeDataId, baseInventory.GetItem(1).Id);
            Assert.AreEqual(10, baseInventory.GetItem(1).Count);
        }
    }
}
```
要確認（実装時に Step 6 の実行で確かめる）は次の4点:
- (a) `SpaceElevatorBaseInventoryComponent.SetItem(int, IItemStack)` の多重定義がある。`SpaceElevatorClimbTest.cs:73` は `SetItem(1, CreatEmpty())` を使っている。
- (b) 拠点に給電する前でも、ケーブルの到着をプールが受け付ける（`RocketLaunchPadOutputSlots.cs:39` の `CanAcceptArrival`）。
- (c) `PowerBase` が電柱を置くと網が再構築される。そのとき発射台の発電機が外れても、以降に打ち上げは無いので結果は変わらない。
- (d) TestLevelInheritMachine の大きさが 1×1×1 で、x=20/24/28 の配置が重ならない。

- [ ] **Step 5: コンパイル**  Run: `uloop compile --project-path ./moorestech_client`  Expected: `ErrorCount: 0`

- [ ] **Step 6: テストを実行する**  Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Tier9ComputeLoopEndToEndTest|SpaceElevator|RocketLaunchPad|LevelInheritance|LevelFamilySubstitute|Orbital|ResearchDataStore"`  Expected: 失敗0（テスト mod に足したデータで、既存の拠点・発射台・継承のテストが壊れていない）

- [ ] **Step 7: テストが本番マスタを読まないことを確かめる**  Run: `python3 .github/scripts/check-test-external-dependencies.py`  Expected: exit 0

- [ ] **Step 8: コミット**
```bash
git add moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/items.json moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/machineRecipes.json moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/research.json moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/blocks.json moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTestTier9ChainIds.cs moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTestTier9ChainIds.cs.meta moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/Tier9
git commit -m "Task 25: G4 ICチップから計算データの研究と拠点の昇降までを1本で通す結合テスト"
```
（`.meta` は Unity がコンパイル時に生成したものだけを足す。手で作らない）

**完了判定:** Step 6 の実行結果で `Tier9ComputeLoopEndToEndTest.ICチップから計算データの研究と拠点の昇降まで通る` が Passed で、同じ正規表現の失敗が0。`git log origin/feat/tier9-integrated-tests` に `Task 25:` のコミットがある。

### Task 26: G5 ゴールデンセーブテスト（master 版7のセーブ → V8 へ変換 → ワールドとしてロード）

**Files:**
- Create: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/SaveLoad/Tier9/Fixtures/master-v7-tier9.json`（Step 1〜3 で、origin/master の Editor から生成する）
- Create: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/SaveLoad/Tier9/Tier9GoldenSaveFixture.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/SaveLoad/Tier9/Tier9GoldenSaveMigrationTest.cs`
- 生成器の記録（本体 repo にはコミットしない）: `../moorestech_logs/harness/tier9-integration/g5-fixture/Tier9GoldenSaveFixtureGeneratorTemporaryTest.cs`

**Interfaces:**
- Consumes:
  - P1: `WorldSaveAllInfo.CurrentVersion == 8`。DI のシングルトン `SaveMigrationChain`（`SaveAndEventServiceRegistration.cs:117`）の `Migrate(JObject)` → `SaveMigrationResult`（`CanLoad`・`BlockedReason`・`FromVersion`・`Migrated`・`Save`。master の `Game.SaveLoad/Migration/SaveMigrationResult.cs:9-17`）。V7→V8 の部品5つ（継承レベル・清浄室機械の液体スロット・初回打ち上げ履歴・ケーブルプール・Tier9 コスト0研究のリセット）。部品の JSON の形は、S1 の `SaveMigrationStepV7ToV8.cs:12-14`（`processor.inheritedLevel`・清浄室機械直下の `inheritedLevel`）、`V8ToV9/CleanRoomMachineFluidSlotFiller.cs:15-16`（`inputFluidSlot`・`outputFluidSlot` が空配列）、S2 の `SaveMigrationStepV6ToV7.cs:22`（`rocketHasLaunchedOnce = false`）、S3 の `SaveMigrationStepV7ToV8.cs:10-11`（`spaceElevatorCablePool.cableCount`）に従う
  - Task 25: テスト mod の研究 `c0de…011`/`013` と `ForUnitTestTier9ChainIds.RocketLaunchPadBlockGuid`/`SpaceElevatorBaseBlockGuid`
  - 既存: `SaveLoadPreparerTestFixture.ArchiveRootForThisRun()`。`WorldDataDirectory.FromWorldRoot`/`FromServerDataMap`/`BackupSaveJsonPath(int)`。`IWorldSaveDataLoader.LoadOrInitialize()`。この組み合わせは `ConnectToolGuidMigrationLoadTest.cs:53-75` と同じ形
- Produces: `Tier9GoldenSaveFixture`（`Load()`・`StateAt(JObject, Vector3Int, string)`・GUID の定数）。ゴールデンセーブ `master-v7-tier9.json`（Task 30 の事前確認でも形の比較に使う）

- [ ] **Step 1: origin/master の使い捨て worktree を作り、Editor を起こす**

Run（Mac mini のメインクローンから実行する。`moores-wt status` で Editor が 3 本未満であることを確かめてから）:
```bash
moores-wt new tmp/g5-fixture-master --dir g5-fixture-master --from origin/master
```
Expected: `~/hermes-agent/data/worktrees/moorestech/g5-fixture-master` ができ、`uloop launch` が成功する。`git -C ~/hermes-agent/data/worktrees/moorestech/g5-fixture-master grep -n "CurrentVersion = 7" -- moorestech_server/Assets/Scripts/Game.SaveLoad/Json/WorldVersions/WorldSaveAllInfo.cs` が1行出る（master は版7: 同ファイル:25）。版が7でなければ止まって A に報告する。master が先に版を上げていると、統合の V7→V8 と衝突するため。

- [ ] **Step 2: 一時の生成テストを master worktree に置いて実行する**

`~/hermes-agent/data/worktrees/moorestech/g5-fixture-master/moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/SaveLoad/Tier9GoldenSaveFixtureGeneratorTemporaryTest.cs`（コミットしない）:
```csharp
using System;
using System.IO;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.Fluid;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Fluid;
using Game.PlayerIdentity;
using Game.PlayerInventory.Interface;
using Game.Research;
using Game.SaveLoad.Json;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.Util;
using Tests.Util.PlayerIdentity;
using UnityEngine;

namespace Tests.CombinedTest.Game.SaveLoad
{
    // master 版7のゴールデンセーブを書き出す一時の生成器（コミットしない）
    // Temporary generator that writes the master version-7 golden save (never committed)
    public class Tier9GoldenSaveFixtureGeneratorTemporaryTest
    {
        private const string OutputPath = "/Users/sakastudio/hermes-agent/data/worktrees/moorestech/tier9-integrated-tests/moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/SaveLoad/Tier9/Fixtures/master-v7-tier9.json";

        [Test]
        public void WriteMasterVersion7GoldenSave()
        {
            var (_, provider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var playerId = PlayerIdentityTestHelper.Register(provider.GetRequiredService<PlayerIdentityRegistry>(), "steam:1").PlayerId;
            var world = ServerContext.WorldBlockDatastore;

            // 加工中の機械: Test1×3＋Test2×1→Test3（1.5秒）を半分だけ進める
            // A machine mid-job: run Test1x3 + Test2x1 -> Test3 (1.5 s) halfway
            world.TryAddBlock(ForUnitTestModBlockId.MachineId, new Vector3Int(0, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var machine);
            MachineRecipeSelectTestUtil.SelectRecipe(machine, MasterHolder.MachineRecipesMaster.GetRecipeElement(Guid.Parse("bd3d4d7d-9c3b-4ae1-875b-950327eedd9d")));
            var machineInventory = machine.GetComponent<VanillaMachineBlockInventoryComponent>();
            machineInventory.InsertItem(ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 3));
            machineInventory.InsertItem(ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId2, 1));
            var processor = machine.GetComponent<VanillaMachineProcessorComponent>();
            for (var i = 0; i < (int)GameUpdater.SecondsToTicks(0.75); i++)
            {
                processor.SupplyExternalPower(processor.EffectiveRequestPower);
                GameUpdater.UpdateOneTick();
            }
            Assert.AreEqual(ProcessState.Processing, processor.CurrentState);

            // 清浄室機械（部屋なしで待機）と、流体を入れたパイプ
            // A clean-room machine (idle without a room) and a pipe holding fluid
            world.TryAddBlock(ForUnitTestModBlockId.CleanRoomMachineId, new Vector3Int(10, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            world.TryAddBlock(ForUnitTestModBlockId.FluidPipe, new Vector3Int(20, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var pipe);
            var pipeComponent = pipe.GetComponent<FluidPipeComponent>();
            pipeComponent.AddLiquid(new FluidStack(5, MasterHolder.FluidMaster.GetFluidId(Guid.Parse("00000000-0000-0000-1234-000000000001"))), default);
            Assert.Greater(pipeComponent.AmountUnits, 0);

            // テスト研究1を本当に完了させる（Test1 を1個消費）
            // Really complete Test Research 1 (spends one Test1)
            provider.GetRequiredService<IPlayerInventoryDataStore>().GetInventoryData(playerId).MainOpenableInventory.InsertItem(ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 1));
            Assert.IsTrue(provider.GetRequiredService<IResearchDataStore>().CompleteResearch(Guid.Parse("cd05e30d-d599-46d3-a079-769113cbbf17"), playerId));

            // 旧マスタでコスト0のまま完了できた6ノードと G4 の研究2件を完了済みに、それらが解放していた本番3ブロックを解放済みに書き足す
            // Append the six zero-cost Tier9 nodes and the two G4 nodes as completed, and the three production blocks they unlocked as unlocked
            var save = JObject.Parse(provider.GetRequiredService<AssembleSaveJsonText>().AssembleSaveJson());
            Assert.AreEqual(7, save["worldVersion"].Value<int>());
            var completed = (JArray)save["research"]["CompletedResearchGuids"];
            foreach (var guid in new[]
                     {
                         "019e3aab-32c6-7166-a184-d761153c2498", "019e3ae2-5005-7439-b242-9b7b3191a2c3", "019e3af5-f2e6-76de-a80f-5e66e79fa35d",
                         "019e3af0-297f-726e-8a02-0a8518124e6d", "019e3aef-a149-746b-8fad-159e0434cb51", "01a16291-4094-7369-a87f-5dfe2197d25e",
                         "c0de0000-0000-4000-8000-000000000011", "c0de0000-0000-4000-8000-000000000013",
                     }) completed.Add(guid);
            var blockUnlocks = (JArray)save["gameUnlockState"]["blockUnlockStateInfos"];
            foreach (var guid in new[] { "01a162f1-ab04-76e2-94cf-dc713edeb63b", "01a162aa-cec0-738c-93c5-f58adc87fa7f", "01a162b0-7769-7419-af24-bb35528b93d2" })
                blockUnlocks.Add(new JObject { ["guid"] = guid, ["isUnlocked"] = true });
            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
            File.WriteAllText(OutputPath, save.ToString(Formatting.Indented));
        }
    }
}
```
6ノードと3ブロックの GUID は、マスタ b2149a0 の research.json・blocks.json で引いた値（summary の調査結果）。核融合炉 `019e3af0-297f…` は核融合炉 `01a162f1…` を解放し、軌道エレベーターの建設 `01a16291…` は拠点 `01a162aa…` と発射台 `01a162b0…` を解放する。

Run（`cd ~/hermes-agent/data/worktrees/moorestech/g5-fixture-master` で実行する）: `uloop compile --project-path ./moorestech_client` を実行し、続けて `uloop run-tests --project-path ./moorestech_client --filter-type class --filter-value "Tests.CombinedTest.Game.SaveLoad.Tier9GoldenSaveFixtureGeneratorTemporaryTest"`  Expected: Passed 1。`OutputPath` に JSON ができ、`python3 -c "import json;d=json.load(open('<OutputPath>'));print(d['worldVersion'], len(d['research']['CompletedResearchGuids']))"` が `7 9` を出す。

要確認（実装時にこの Step で確かめる）は次の3点。食い違えば、生成器だけを直して出し直す:
- `MachineRecipeSelectTestUtil` が master にある。
- master のテスト用の機械 `…0001` で、レシピを選んでから投入すると加工が始まる。
- master の研究の保存キーが `CompletedResearchGuids` である（master の `IResearchDataStore.cs:17`）。

- [ ] **Step 3: 生成器を記録に残し、master worktree を畳む**

Run:
```bash
mkdir -p ../moorestech_logs/harness/tier9-integration/g5-fixture
cp ~/hermes-agent/data/worktrees/moorestech/g5-fixture-master/moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/SaveLoad/Tier9GoldenSaveFixtureGeneratorTemporaryTest.cs ../moorestech_logs/harness/tier9-integration/g5-fixture/
git -C ~/hermes-agent/data/worktrees/moorestech/g5-fixture-master rev-parse HEAD > ../moorestech_logs/harness/tier9-integration/g5-fixture/master-commit.txt
moores-wt rm g5-fixture-master --force --prune-branch
```
Expected: `moores-wt status` に `g5-fixture-master` が無い。`--force` を使うのは、コミットしない一時テストを捨てるため。`../moorestech_logs` の README を読んでから書く（CLAUDE.md の規約）。

- [ ] **Step 4: fixture の読み口を作る**

`moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/SaveLoad/Tier9/Tier9GoldenSaveFixture.cs`:
```csharp
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Tests.CombinedTest.Game.SaveLoad.Tier9
{
    /// <summary>
    ///     master 版7のゴールデンセーブと、その中の目印。手書き JSON では形の食い違いに気づけないため、master の Editor で生成したものを使う
    ///     The master version-7 golden save and its landmarks; generated on the master Editor because hand-written JSON hides shape drift
    /// </summary>
    public static class Tier9GoldenSaveFixture
    {
        public static readonly string[] ResetResearchGuids =
        {
            "019e3aab-32c6-7166-a184-d761153c2498", "019e3ae2-5005-7439-b242-9b7b3191a2c3", "019e3af5-f2e6-76de-a80f-5e66e79fa35d",
            "019e3af0-297f-726e-8a02-0a8518124e6d", "019e3aef-a149-746b-8fad-159e0434cb51", "01a16291-4094-7369-a87f-5dfe2197d25e",
        };
        public static readonly string[] ResetBlockGuids = { "01a162f1-ab04-76e2-94cf-dc713edeb63b", "01a162aa-cec0-738c-93c5-f58adc87fa7f", "01a162b0-7769-7419-af24-bb35528b93d2" };
        public static readonly string[] ResetItemViewGuids = { "01a162ab-07ba-73c1-9dd6-38e1e867663b", "019e3af5-e750-71ae-ab09-2b98f3db9913", "01a16290-f793-748b-a0b0-91d44be0d4ab", "01a16297-4724-73b9-82c3-2e77120be77d", "01a16297-6665-765d-8e19-96d357394c3e", "01a16297-a7f5-759b-91ba-be4021ca3bf2", "01a162a4-9334-7286-a8ba-7daf9c0f407a", "01a162a8-e435-73d0-bddb-08533819b80e" };
        public static readonly string[] KeptResearchGuids = { "cd05e30d-d599-46d3-a079-769113cbbf17", "c0de0000-0000-4000-8000-000000000011", "c0de0000-0000-4000-8000-000000000013" };

        public static readonly Vector3Int MachinePosition = new(0, 0, 0);
        public static readonly Vector3Int CleanRoomMachinePosition = new(10, 0, 0);
        public static readonly Vector3Int PipePosition = new(20, 0, 0);
        public const string VanillaMachineSaveKey = "Game.Block.Blocks.Machine.VanillaMachineSaveComponent";
        public const string CleanRoomMachineSaveKey = "Game.Block.Blocks.CleanRoom.Machine.CleanRoomMachineProcessorComponent";
        public const string PipeSaveKey = "Game.Block.Blocks.Fluid.FluidPipeSaveComponent";

        public static JObject Load()
        {
            var repositoryRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
            return JObject.Parse(File.ReadAllText(Path.Combine(repositoryRoot, "moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/SaveLoad/Tier9/Fixtures/master-v7-tier9.json")));
        }

        // 座標で world の1ブロックを探し、そのコンポーネントの state を返す
        // Find one world block by position and return that component's state
        public static JObject StateAt(JObject save, Vector3Int position, string saveKey)
        {
            var block = ((JArray)save["world"]).Single(token => token["X"].Value<int>() == position.x && token["Y"].Value<int>() == position.y && token["Z"].Value<int>() == position.z);
            Assert.IsTrue(block["state"]?[saveKey] is JObject, $"{position} に {saveKey} がありません");
            return (JObject)block["state"][saveKey];
        }
    }
}
```
`X`/`Y`/`Z`・`state` のキーは master の `BlockJsonObject`（`[JsonProperty("X")]` ほか）に従う。fixture の読み方は前例 `Tests/UnitTest/Game/MapGeneration/Surface/Fixtures/legacy-v4-seed196.json` と同じ。

- [ ] **Step 5: テストを書く**

`moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/SaveLoad/Tier9/Tier9GoldenSaveMigrationTest.cs`:
```csharp
using System;
using System.IO;
using System.Linq;
using Core.Update;
using Game.Block.Blocks.Fluid;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Orbit;
using Game.Paths;
using Game.Research;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json.WorldVersions;
using Game.SaveLoad.Migration;
using Game.SpaceElevator;
using Game.UnlockState;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.CombinedTest.Core.Machine.Upgrade;
using Tests.Module.TestMod;
using static Tests.CombinedTest.Game.SaveLoad.Tier9.Tier9GoldenSaveFixture;

namespace Tests.CombinedTest.Game.SaveLoad.Tier9
{
    /// <summary>
    ///     master 版7のゴールデンセーブが V8 へ変換され、部品の効果を保ったままワールドとしてロードできることを確かめる
    ///     Verifies the master version-7 golden save converts to V8 and loads as a world with every component's effect intact
    /// </summary>
    public class Tier9GoldenSaveMigrationTest
    {
        private string _archiveRoot;

        [SetUp]
        public void SetUp()
        {
            _archiveRoot = SaveLoadPreparerTestFixture.ArchiveRootForThisRun();
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_archiveRoot)) Directory.Delete(_archiveRoot, true);
        }

        [Test]
        public void 版7のゴールデンセーブを版8へ変換すると部品が全部効く()
        {
            var (_, provider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var original = Load();
            Assert.AreEqual(7, original["worldVersion"].Value<int>());
            var result = provider.GetRequiredService<SaveMigrationChain>().Migrate((JObject)original.DeepClone());
            Assert.IsTrue(result.CanLoad, result.BlockedReason);
            Assert.AreEqual(7, result.FromVersion);
            Assert.IsTrue(result.Migrated);
            var save = result.Save;
            Assert.AreEqual(WorldSaveAllInfo.CurrentVersion, save["worldVersion"].Value<int>());

            // マスタの刈り込みより前の段で、6ノードの完了とそれが解放していた本番3ブロックが外れる
            // Before missing-master pruning, the six nodes and the three production blocks they unlocked are reset
            var completed = save["research"]["CompletedResearchGuids"].Values<string>().ToList();
            foreach (var guid in ResetResearchGuids) CollectionAssert.DoesNotContain(completed, guid);
            CollectionAssert.IsSubsetOf(KeptResearchGuids, completed);
            var blockUnlocks = (JArray)save["gameUnlockState"]["blockUnlockStateInfos"];
            // Task 7 は解放の記録を false にせず記録ごと消す。ブロック3件と表示8件の記録が残っていないことを見る
            // Task 7 deletes the unlock records instead of writing false; assert none remain for the 3 blocks and 8 recipe views
            foreach (var guid in ResetBlockGuids) Assert.IsFalse(blockUnlocks.Any(token => token["guid"].Value<string>() == guid), guid);
            var itemUnlocks = (JArray)save["gameUnlockState"]["itemUnlockStateInfos"];
            foreach (var guid in ResetItemViewGuids) Assert.IsFalse(itemUnlocks.Any(token => token["guid"].Value<string>() == guid), guid);

            // 継承レベル・液体スロット・打ち上げ履歴・ケーブルプールが足され、パイプの nano 量は変わらない
            // Inherited level, fluid slots, launch history and cable pool are added; the pipe's nano amount is unchanged
            Assert.AreEqual(JTokenType.Null, StateAt(save, MachinePosition, VanillaMachineSaveKey)["processor"]["inheritedLevel"].Type);
            var cleanRoom = StateAt(save, CleanRoomMachinePosition, CleanRoomMachineSaveKey);
            Assert.AreEqual(JTokenType.Null, cleanRoom["inheritedLevel"].Type);
            Assert.AreEqual(0, ((JArray)cleanRoom["inputFluidSlot"]).Count);
            Assert.AreEqual(0, ((JArray)cleanRoom["outputFluidSlot"]).Count);
            Assert.IsFalse(save["rocketHasLaunchedOnce"].Value<bool>());
            Assert.AreEqual(0, save["spaceElevatorCablePool"]["cableCount"].Value<int>());
            Assert.AreEqual(StateAt(original, PipePosition, PipeSaveKey)["amountNano"].Value<long>(), StateAt(save, PipePosition, PipeSaveKey)["amountNano"].Value<long>());
        }

        [Test]
        public void 変換したゴールデンセーブをワールドとしてロードし残した研究の再実行で解放が戻る()
        {
            // 一時ファイルを実セーブパスに指定して、本番と同じ入口でロードする
            // Point the DI save path at a temporary file and load through the production entry
            var original = Load().ToString();
            Directory.CreateDirectory(_archiveRoot);
            var sourcePath = WorldDataDirectory.FromWorldRoot(_archiveRoot).SaveJsonFilePath;
            File.WriteAllText(sourcePath, original);
            var options = new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory) { worldDataDirectory = WorldDataDirectory.FromServerDataMap(TestModDirectory.ForUnitTestModDirectory, sourcePath) };
            var (_, provider) = new MoorestechServerDIContainerGenerator().Create(options);
            provider.GetRequiredService<IWorldSaveDataLoader>().LoadOrInitialize();
            Assert.AreEqual(original, File.ReadAllText(provider.GetRequiredService<WorldDataDirectory>().BackupSaveJsonPath(7)));

            // 残した研究の再実行でテスト用の発射台と拠点が解放され、リセット対象は完了していない
            // Replaying the kept research unlocks the test pad and base; reset nodes stay incomplete
            var research = provider.GetRequiredService<IResearchDataStore>();
            foreach (var guid in KeptResearchGuids) Assert.IsTrue(research.IsResearchCompleted(Guid.Parse(guid)), guid);
            foreach (var guid in ResetResearchGuids) Assert.IsFalse(research.IsResearchCompleted(Guid.Parse(guid)), guid);
            var unlocks = provider.GetRequiredService<IGameUnlockStateDataController>();
            Assert.IsTrue(unlocks.BlockUnlockStateInfos[ForUnitTestTier9ChainIds.RocketLaunchPadBlockGuid].IsUnlocked);
            Assert.IsTrue(unlocks.BlockUnlockStateInfos[ForUnitTestTier9ChainIds.SpaceElevatorBaseBlockGuid].IsUnlocked);

            // 打ち上げ履歴とケーブルプールは空、パイプの量と清浄室機械は保たれる
            // Launch history and cable pool are empty; the pipe amount and the clean-room machine survive
            Assert.IsFalse(ServerContext.GetService<RocketLaunchHistory>().HasLaunchedOnce);
            Assert.AreEqual(0, ServerContext.GetService<SpaceElevatorCablePool>().Amount);
            var savedNano = StateAt(JObject.Parse(original), PipePosition, PipeSaveKey)["amountNano"].Value<long>();
            Assert.AreEqual(savedNano, ServerContext.WorldBlockDatastore.GetBlock(PipePosition).GetComponent<FluidPipeComponent>().AmountUnits);
            Assert.IsNotNull(ServerContext.WorldBlockDatastore.GetBlock(CleanRoomMachinePosition));

            // 加工中のジョブは継承なしのまま最後まで進む（W14 の例外が出ない）
            // The mid-job craft finishes without inheritance (no W14 exception)
            var machine = ServerContext.WorldBlockDatastore.GetBlock(MachinePosition);
            var processor = machine.GetComponent<VanillaMachineProcessorComponent>();
            Assert.AreEqual(ProcessState.Processing, processor.CurrentState);
            LevelInheritanceTestHelper.AdvanceTicksWithFullPower((int)GameUpdater.SecondsToTicks(1.5) + 5, processor);
            var inventory = machine.GetComponent<VanillaMachineBlockInventoryComponent>();
            Assert.AreEqual(1, LevelInheritanceTestHelper.CountItem(inventory, 0, inventory.GetSlotSize(), ForUnitTestItemId.ItemId3));
        }
    }
}
```
1本目は、変換の連鎖の出力そのものを見る。理由は、本番の6ノードの GUID がテスト mod に無く、`MissingMasterPruner` がロード時にどのみち落とすため。2本目だけで assert すると、リセットの部品が無くても通ってしまう（SaveLoadPreparer の順は chain.Migrate → 退避 → MissingMasterPruner）。本番3ブロックの GUID はテスト mod に無いので、ロード時は `BlockUnlockStateHolder.Load` が読み飛ばす（master の `Game.UnlockState/Holders/BlockUnlockStateHolder.cs:46-48`）。

要確認（実装時に Step 7 で確かめる）は次の3点:
- (a) `ForUnitTestItemId.ItemId3` がある。無ければ `ServerContext.ItemStackFactory.Create(Guid.Parse("00000000-0000-0000-1234-000000000003"), 1).Id` に替える。
- (b) 変換の連鎖が `worldVersion` を 8 に書く。
- (c) P1 の清浄室機械の液体スロットが、S1 と同じ空配列の形である。違えば、P1 の部品テストと同じ形に合わせる。

- [ ] **Step 6: コンパイル**  Run: `uloop compile --project-path ./moorestech_client`  Expected: `ErrorCount: 0`

- [ ] **Step 7: テストを実行する**  Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Tier9GoldenSaveMigrationTest|SaveMigration|SaveLoadPreparer|ConnectToolGuidMigrationLoadTest"`  Expected: 失敗0

- [ ] **Step 8: テストが本番マスタを読まないことを確かめる**  Run: `python3 .github/scripts/check-test-external-dependencies.py`  Expected: exit 0（fixture の中の本番 GUID は文字列で、本番マスタのファイルは読まない）

- [ ] **Step 9: コミット**
```bash
git add moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/SaveLoad/Tier9
git commit -m "Task 26: G5 master版7のゴールデンセーブをV8へ変換しワールドとしてロードするテスト"
git -C ../moorestech_logs add harness/tier9-integration/g5-fixture && git -C ../moorestech_logs commit -m "tier9: G5 ゴールデンセーブの生成器と元コミット"
```

**完了判定:** Step 7 の2テストが Passed、同じ正規表現の失敗が0。`master-v7-tier9.json` の `worldVersion` が 7 で、`../moorestech_logs/harness/tier9-integration/g5-fixture/master-commit.txt` が origin/master のコミットを指す。`git log origin/feat/tier9-integrated-tests` に `Task 26:` のコミットがある。

---

## P5 録画7段（出荷マスタ）

担当は A。J2 の後に、ピンをマスタ統合 PR の push 済みコミットへ更新してから着手する。作業場所は `~/hermes-agent/data/worktrees/moorestech/tier9-integrated`。マスタは `~/hermes-agent/data/worktrees/moorestech_master/tier9-integrated` を第3引数で渡す。シナリオはすべて新しいカテゴリ `scenarios/tier9/` に置く。下書きは3本とも別の場所を指しているが、置かない理由は次のとおり:
- D9 は `misc/` を指している。`misc/` は直下が10ファイルを超えている（`review-s1h-placement.md` V2）。
- S2 H は `rocket/` を指している。
- S3 Task 13 は `building/` を指している。`building/` は直下が23ファイルある（S3 先端の `git ls-tree` で数えた）。

`tier9/` には 9 ファイル（7段＝8本と、Task 30 の事前確認1本）を置く。10ファイル以下に収まる。

共通の決まり:
- 実行は前景で行う。Bash の timeout は 600000 にする（ユーザー規約。`references/run-scenario.md:30` は「バックグラウンド実行」と書いているが、こちらを優先する）。コマンドは `SKILL=.agents/skills/unity-playmode-recorded-playtest; MW="$(cd ~/hermes-agent/data/worktrees/moorestech_master/tier9-integrated && pwd)"; "$SKILL/scripts/run-scenario.sh" ./moorestech_client "$SKILL/scenarios/tier9/<file>.cs" "$MW"`
- 各段の前に `uloop clear-console --project-path ./moorestech_client` を実行する。各段の後に `uloop get-logs --project-path ./moorestech_client --log-type Error` を実行し、0件であることを確かめる。
- 前提（研究・素材・配置）は裏口で揃えてよい。手段は `LoadResearchData` に前提の閉包を渡す方法か、直置き・直投入（D9 の判断記録 `_s1h.md:2826`）。各段の最後の工程だけは、本物の加工・本物の飛行・UI 経由の研究で動かす。
- 期待値をマスタの値から読める箇所は、数字を直書きしない。次の4つをサーバーから読む: 研究の消費量、到着の成果（`IOrbitalArrivalService.PreviewArrivalOutputs`）、建造秒数（`RocketLaunchPadBlockParam.BuildSeconds`）、レシピ時間。P2 のバランス調整で値が変わっても、録画が壊れないようにするため。
- `result.json` の期待値は全段共通で、`Success: true`、`Asserts` が全部 `Passed: true`、`Error` が空。段ごとのスクショ名は各段に書く。
- 共有: 全段が通ったら `share-put <録画ディレクトリ群> -n tier9-recorded-7` を実行し、出た URL を統合 PR の本文と `../moorestech_logs/harness/tier9-integration/p5/README.md` に書く（ORCH J3 の証拠）。

### Task 28: 7段のシナリオを書いて通す

**Files:**
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/tier9/tier9-1a-chip-line.cs`（段1 チップライン）
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/tier9/tier9-1b-mixed-level-sorting.cs`（段1 混在ベルトの仕分け）
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/tier9/tier9-2-modules-gpu.cs`
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/tier9/tier9-3-yield-ui.cs`
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/tier9/tier9-4-rocket-launch.cs`
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/tier9/tier9-5-compute-research.cs`
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/tier9/tier9-6-fusion-elevator.cs`
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/tier9/tier9-7-antimatter-final-research.cs`
- Modify: `.agents/skills/unity-playmode-recorded-playtest/SKILL.md`（カテゴリ一覧に `tier9` を1語足す。`review-s1h-placement.md:74`）
- 記録: `../moorestech_logs/harness/tier9-integration/p5/`（result.json の写し・Error ログ・README）

**Interfaces:**
- Consumes: ピン先の統合マスタ（P2）、Task 27 の W13 の裁定（段1b の期待値）、DSL（`SetupFlatGround`・`SkipOpeningSkitIfPlaying`・`WarpPlayer`・`PlaceBlockDirect`・`GiveItem`/`GiveItemDirect`・`ServerService<T>()`・`Until`・`Assert`・`Note`・`Screenshot`・`AimAt`・`PressInteract`・`PressKey`・`WaitUiState`・`UntilWebUiElement`・`ClickWebUi`・`WaitWebUiTextContains`・`RemoveBlock`。`moorestech_client/Assets/Scripts/Client.Playtest/PlaytestDriver.cs:69-184`・`Operations/PlaytestWebUiOps.cs:28-83`）。WebUI の testid は次のとおり:
  - 研究: `research-node-{guid}`・`research-button-{guid}`・`research-node-state-{guid}`（`moorestech_web/webui/src/features/research/ResearchNodeCard.tsx:30,37`・`ResearchDetailPane.tsx:63`）
  - 機械: `machine-input-stall`（`features/blockInventory/details/machineInputStall/MachineInputStallSection.tsx:14`）
  - 発射台: `rocket-launch-pad-build`/`rocket-launch-pad-launch`
  - 拠点: `space-elevator-connect`/`space-elevator-section`/`space-elevator-status`
  - 発電機: `generator-section`/`generator-fluid-slots`
  - 共通: `block-inventory-close`
- Produces: 8本の `result.json`（全 pass）と録画。ORCH J3 の証拠になる

- [ ] **Step 1: 段1a チップライン（D9 の下書きを移す）**

下書き: `docs/superpowers/plans/2026-10-23-tier9-s1-master-content-handoff.md` の「Task D9」にある `tier9-chip-line.cs`（目安 2268〜2470行）を、`scenarios/tier9/tier9-1a-chip-line.cs` に写す。差分は次の4点:
1. `PlaytestRunner.Run("tier9-chip-line", …)` → `PlaytestRunner.Run("tier9-1a-chip-line", …)`
2. 研究 GUID の配列には、6ノードの1つ 機械オーバークロック `019e3ae2…` が入っている。P2 でこのノードにコストが付いても、`LoadResearchData` の閉包は前提として完了扱いにするだけなので、そのまま使ってよい。P2 がリセットの手段として「GUID の振り直し」を選んだ場合（`review-s1h-placement.md:101` J4）は、`019e3ae2…` をこう替える:
```csharp
MasterHolder.ResearchMaster.GetAllResearches().Single(r => r.ResearchNodeName == "機械オーバークロック").ResearchNodeGuid
```
3. 末尾の `#endregion` の直前に、Error ログを数えるために `p.Note("段1a 完了");` を足す（どこまで進んだかを Timeline に残す）。
4. 最後の工程は、パッケージングで ICチップLv1・Lv2 が本当に出るところ（`Until(... Count(packaging, "ICチップLv2") > 0 ...)`）。下書きのままでよい。

前提: 研究の閉包、部屋（`BuildRoom`）、給電（`PowerAll`）、各工程への素材の直投入。本物の工程: IPA の化学プラントでの生成、切断・成膜・EUV・ダイシング・パッケージングの各加工。Assert: 下書きの `Until` 8件と `clean room classified`。スクショ: `01-room-and-line`・`02-dies`・`03-chips`。

- [ ] **Step 2: 段1b 混在ベルトの仕分けと止まった理由（新規。下書きにコードが無い。`review-s1h-typeclosure.md:10` L2841）**

裁定 2026-10-24「レベルの混ざったチップはプレイヤーが仕分け、止まった理由を機械UIに出す」の通しの確認。ダイシングの出力は Lv1 と Lv2 が混ざる。混ざったダイがパッケージング（継承レシピ。継承元スロットは混ぜない＝`MachineInputSlotBinding.cs:100-102`）へ届くと止まり、機械 UI に「レベルの違う品（…）が届いて止まっています」が出る。フィルター分岐器で Lv1 と Lv2 を別のパッケージングへ分けると、両方が流れる。最後の工程は、仕分けたあとに両方のパッケージングが ICチップを本当に作るところ。

`scenarios/tier9/tier9-1b-mixed-level-sorting.cs`。`BuildRoom`・`PowerAll`・`SelectRecipe`・`WithPrerequisites`・`Place`・`Give`・`Count` は、段1a（D9 下書き）の同名ローカル関数をそのまま写す（`#region Internal` の中に置く）。下のコードは、それに足す本体だけ:
```csharp
// ダイシングの出力のような Lv1/Lv2 の混ざったダイが届くとパッケージングが止まって理由を出し、フィルター分岐器で仕分けると両方流れることを確かめる
// Mixed Lv1/Lv2 dies (like dicing output) stall packaging with a shown reason; sorting with a filter splitter lets both flow
using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Client.Playtest.Operations;
using Core.Inventory;
using Core.Master;
using Game.Block.Blocks.FilterSplitter;
using Game.Block.Blocks.PowerGenerator;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.CleanRoom;
using Game.Context;
using Game.EnergySystem;
using Game.Fluid;
using Game.Research;
using Server.Protocol.PacketResponse.Util.ElectricWire.Connection;
using UnityEngine;

var research = new[] { "019e3ad9-46e2-73fd-9555-8c1d0245d26d" }.Select(Guid.Parse).ToArray(); // 半導体製造（D9 と同じ先頭 GUID）。閉包で前提ごと完了させる
var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("tier9-1b-mixed-level-sorting", options, async p =>
{
    await p.SetupFlatGround();
    await p.SkipOpeningSkitIfPlaying();
    p.WarpPlayer(new Vector3(7f, 33f, 4f));
    var consumers = new List<IBlock>();
    var researchStore = p.ServerService<IResearchDataStore>();
    researchStore.LoadResearchData(new ResearchSaveJsonObject { CompletedResearchGuids = WithPrerequisites(research).Select(g => g.ToString()).ToList() });
    await p.Until(() => research.All(researchStore.IsResearchCompleted), 15f, "半導体製造の研究が完了扱いになった");

    // 部屋の中にパッケージング2台・分岐器・ベルトを置く（入口のベルトに混ざったダイを流す）
    // Place two packaging machines, a splitter and belts in the room (mixed dies go onto the inlet belt)
    BuildRoom();
    var packagingA = Place("パッケージング装置", new Vector3Int(9, 33, 2));
    var packagingB = Place("パッケージング装置", new Vector3Int(9, 33, 6));
    var packagingRecipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data.First(r => r.BlockGuid == packagingA.BlockMasterElement.BlockGuid).MachineRecipeGuid;
    SelectRecipe(packagingA, packagingRecipe);
    SelectRecipe(packagingB, packagingRecipe);
    foreach (var x in new[] { 2, 4 }) Give(Place("クリーンルーム空気清浄機", new Vector3Int(x, 33, 8)), 0, "クリーンルームフィルター", 5);
    PowerAll();
    await p.Until(() => p.ServerService<CleanRoomDatastore>().TryGetCleanRoom(packagingA, out var r) && r.ThresholdIndex < MasterHolder.CleanRoomMaster.OutThresholdIndex, 30f, "クリーンルームが判定された");
    Give(packagingA, 1, "ICチップ基板", 20);
    Give(packagingB, 1, "ICチップ基板", 20);

    // A の継承元に Lv1 を置き、ベルトから Lv2 を届けると止まって理由が出る
    // With Lv1 in A's source slot, delivering Lv2 by belt stalls it and shows the reason
    p.Note("パッケージングA に Lv1 ダイを置き、ベルトから Lv2 ダイを届ける");
    Give(packagingA, 0, "シリコンダイLv1", 1);
    var beltIntoA = p.PlaceBlockDirect("ベルトコンベア", new Vector3Int(8, 33, 3), BlockDirection.East);
    beltIntoA.GetComponent<IBlockInventory>().InsertItem(ServerContext.ItemStackFactory.Create(PlaytestItemOps.ResolveItemId("シリコンダイLv2"), 1), InsertItemContext.Empty);
    await OpenMachine(packagingA);
    await p.UntilWebUiElement("machine-input-stall", 10f);
    await p.WaitWebUiTextContains("machine-input-stall", "シリコンダイLv2", 5f);
    await p.Screenshot("04-mixed-level-stall");
    await p.ClickWebUi("block-inventory-close");

    // 分岐器を入口に置き、Lv1 は A へ、Lv2 は B へ流す。混ざったダイを入口へ入れる
    // Put the splitter at the inlet, Lv1 to A and Lv2 to B, then feed mixed dies
    p.Note("フィルター分岐器で Lv1 を A、Lv2 を B へ仕分ける");
    p.RemoveBlock(new Vector3Int(8, 33, 3));
    var splitter = p.PlaceBlockDirect("フィルター分岐器", new Vector3Int(7, 33, 4), BlockDirection.East).GetComponent<VanillaFilterSplitterComponent>();
    ConfigureSplitter(splitter);
    var inlet = p.PlaceBlockDirect("ベルトコンベア", new Vector3Int(6, 33, 4), BlockDirection.East).GetComponent<IBlockInventory>();
    foreach (var die in new[] { "シリコンダイLv1", "シリコンダイLv2", "シリコンダイLv1", "シリコンダイLv2" })
        await p.Until(() => inlet.InsertItem(ServerContext.ItemStackFactory.Create(PlaytestItemOps.ResolveItemId(die), 1), InsertItemContext.Empty).Count == 0, 10f, $"入口のベルトへ {die} を入れた");

    // 両方のパッケージングが本当に ICチップを作り、A の止まった理由は消える
    // Both packaging machines really make IC chips and A's stall reason clears
    await p.Until(() => Count(packagingA, "ICチップLv1") >= 1 && Count(packagingB, "ICチップLv2") >= 1, 90f, "仕分けた後に Lv1 と Lv2 の ICチップが両方出た");
    await OpenMachine(packagingA);
    var stallAfterSorting = await Client.Playtest.WebUi.PlaytestDomQuery.Query("machine-input-stall", 3f);
    p.Assert(!stallAfterSorting.Found, "仕分けた後は A の止まった理由が消えた");
    await p.Screenshot("05-sorted-both-flowing");

    #region Internal

    // ここに段1a の BuildRoom・PowerAll・SelectRecipe・WithPrerequisites・Place・Give・Count を写す

    // 分岐器の方向の添字。コネクタ順で決まるので、実装時に GetBlueprintSettingsJson の出力で確かめて書き直す（要確認）
    // Splitter direction indices follow connector order; verify with GetBlueprintSettingsJson at implementation and rewrite
    const int DirectionTowardA = 0;
    const int DirectionTowardB = 1;

    void ConfigureSplitter(VanillaFilterSplitterComponent component)
    {
        // A 側の方向は Lv1 だけ、B 側の方向は Lv2 だけを許す（方向の並びはコネクタ順。要確認）
        // A's direction allows only Lv1 and B's only Lv2 (direction order follows connectors; to verify)
        component.SetMode(DirectionTowardA, FilterSplitterMode.Whitelist);
        component.SetFilterItem(DirectionTowardA, 0, PlaytestItemOps.ResolveItemId("シリコンダイLv1"));
        component.SetMode(DirectionTowardB, FilterSplitterMode.Whitelist);
        component.SetFilterItem(DirectionTowardB, 0, PlaytestItemOps.ResolveItemId("シリコンダイLv2"));
    }

    async Cysharp.Threading.Tasks.UniTask OpenMachine(IBlock block)
    {
        await p.AimAt(block.BlockPositionInfo.OriginalPos + new Vector3(0.5f, 1f, 0.5f));
        await p.PressInteract();
        await p.WaitUiState(UIStateEnum.SubInventory, 10f);
    }

    #endregion
});
```
次の3点は、実装時に下書きを詰める（要確認。どれも `scenarios/building/belt-segment-flow.cs` と `Tests/CombinedTest/Game/BlueprintFilterSplitterSettingsTest.cs` を読んで確かめる）:
- 座標とベルトの向き。分岐器の出口が A と B の入口に向くよう、ベルトを1本ずつ足す。
- `DirectionTowardA`/`DirectionTowardB` の添字。`VanillaFilterSplitterComponent.DirectionCount` を数え、各方向の `ConnectorGuid` と出口の向きを `GetBlueprintSettingsJson()` の出力で対応づけて、上の定数を書き直す。
- 理由が消えたことの確認は、`PlaytestDomQuery.Query(testid, 3f)`（`Client.Playtest/WebUi/PlaytestDomQuery.cs:53`。期限まで探して `Found` を返す）が `Found=false` を返すことで見ている。表示の更新は最大1秒（D9 の Interfaces）なので、期限は3秒にした。

フィルター分岐器のブロック名と「ベルトコンベア」の名前は、出荷マスタの `name` に合わせる。分岐器は 6ノードの1つ「フィルター分岐器」の研究で解放される。S3 plan はこのノードを「削除」と書いている（`s3plan.md` 旧 Task 表 47行）。P2 でノードが消えた場合、分岐器を解放する研究を `GetAllResearches()` から unlockBlock の対象で引き、閉包に足す。

W13 の扱い: Task 27 で「仕様」と裁定された場合は、この段に検査を足さない。理由: W13 が効くのは代用スロット（GPU衛星Lv1 の GPU 入力など）で、継承元スロットを使う本段には現れない（`MachineInputSlotBinding.cs:100-102`）。「不具合」と裁定された場合は、Task 27 の Step 5 の修正の後に、本段の最後に次を足す。代用スロットへ GPU Lv1 と Lv2 を混ぜて流すと、Lv2 が Lv1 に変わらず戻ることを assert する:
```csharp
p.Assert(Count(gpuSatelliteMachine, "GPULv2") == 0 && beltRemainderHasLv2, "代用スロットで上位品が下位へ変換されない");
```
（`gpuSatelliteMachine` と `beltRemainderHasLv2` は、そのとき足す GPU 衛星の機械とベルトの残量の読み取り）

Assert: 研究完了、部屋の判定、止まった理由の出現と文言、入口への投入4件、両方の ICチップが出たこと、理由が消えたこと。スクショ: `04-mixed-level-stall`・`05-sorted-both-flowing`。

- [ ] **Step 3: 段2 モジュールと GPU（D9 の下書きを移す）**

下書き: 同じ handoff の Task D9 にある `tier9-modules-gpu.cs`（目安 2472〜2604行）を、`scenarios/tier9/tier9-2-modules-gpu.cs` に写す。差分は次の2点:
1. `Run` の名前を `"tier9-2-modules-gpu"` にする。
2. GPU の名前は `GPULv1`〜`GPULv5`（判断記録 A12。改名は Task 16）に合わせる。

最後の工程は、GPU の機械が GPU を本当に出すところ。下書きの最後の `Until` がそれに当たる。スクショ名は下書きのとおり。

- [ ] **Step 4: 段3 抽選 UI（D9 の下書きを移す）**

下書き: 同じ Task D9 にある `tier9-yield-ui.cs`（目安 2606〜2768行）を、`scenarios/tier9/tier9-3-yield-ui.cs` に写す。差分は `Run` の名前を `"tier9-3-yield-ui"` にするだけ。最後の工程は、成膜・EUV・ダイシングの各機械で抽選欄の行（`clean-room-yield-max-grade`・`clean-room-yield-promotion-chance`・`clean-room-yield-layer-fail-rate`・`clean-room-yield-die-loss-rate`）が描画されるところ。描画は UI 経由の本物で、下書きのとおり。

- [ ] **Step 5: 段4 打ち上げ→計算データ（S2 の下書きを移し、誤り2つを直す。飛行を本当に待つ段）**

下書き: `docs/superpowers/plans/2026-10-23-tier9-compute-data-loop.md` の「H. unityプレイ録画テスト（D37）」（目安 3580〜3692行）を、`scenarios/tier9/tier9-4-rocket-launch.cs` に写す。差分は次の6点:
1. `processor.Phase` は private で読めない。全部を `Phase()` に替え、`#region Internal` に次を足す（読み方は `RocketLaunchPadTestUtil.cs:24-28` と同じ観測口）:
```csharp
RocketLaunchPadPhase Phase()
{
    var detail = processor.GetBlockStateDetails().Single(d => d.Key == RocketLaunchPadStateDetail.BlockStateDetailKey);
    return MessagePack.MessagePackSerializer.Deserialize<RocketLaunchPadStateDetail>(detail.Value).Phase;
}
```
2. `IRocketLaunchHistory` は存在しない。`p.ServerService<RocketLaunchHistory>().HasLaunchedOnce` に替える（`using Game.Orbit;` は下書きにある）。
3. 建造の待ちを、直書きの 180 秒から master の値に替える: `await p.UntilWebUiElement("rocket-launch-pad-launch", ((RocketLaunchPadBlockParam)pad.BlockMasterElement.BlockParam).BuildSeconds + 60f);`。建造は本当に待つ。DSL に時間を進める手段は無い（下書きの「建造120秒の待ち」の注記）。
4. 計算データの期待値 `10` を、到着の表から読む: `var expected = p.ServerService<IOrbitalArrivalService>().PreviewArrivalOutputs(satelliteId, 1).Where(s => s.Id == computeDataId).Sum(s => s.Count);` にして、`CountInOutputs() == expected` で待つ。
5. 研究「ロケット技術」を前提として閉包で完了扱いにする。冒頭に D9 と同じ `WithPrerequisites` と `LoadResearchData` を足す。ロケット技術の GUID は S2 plan の `01a167cf-af9d…` で、全桁は P2 の research.json から写す。
6. `Run` の名前を `"tier9-4-rocket-launch"` にする。

`using` に `System.Linq` と `Game.Block.Interface.State`（`RocketLaunchPadStateDetail`）を足す。前提: 発射台・発電機の直置き、資材と GPU衛星Lv1 の直投入、研究の閉包。本物の工程: UI の建造ボタン、建造の実時間、UI の発射ボタン、上昇、到着。Assert: 下書きの Until 群、`first launch recorded`、2便目の自動建造。スクショ: `01-awaiting-build`〜`07-auto-building`。画像で確かめる点は下書きの3点（01〜03 で本体が見えない、04 で見える、05 でロケットが台より上）。

- [ ] **Step 6: 段5 計算データでの研究→製作上限→上位レベルの本物の加工（新規）**

前提: 計算データの入手は段4で本物を見たので、ここではプレイヤーへ直接渡す。研究ツリー（R キー）から UI で「半導体のアップグレード Lv2」を研究し、計算データが消え、製作上限が 2 になる。それまで断られていた ICチップLv2 から、GPU Lv2 が本当に作られる。最後の工程は、UI 経由の研究と、そのあとの本物の加工。

アップグレードノードは、名前ではなく効果で引く（`unlockLevelInheritanceCraftLevel` で Level 2）。GUID と名前は P2 で決まるため、それに依存しない。型は `Mooresmaster.Model.GameActionModule.UnlockLevelInheritanceCraftLevelGameActionParam`（`TargetItemGuids`・`Level`。`LevelInheritanceCraftCapDataStore.cs:43-47`）。

`scenarios/tier9/tier9-5-compute-research.cs`。`PowerAll`・`SelectRecipe`・`WithPrerequisites`・`Place`・`Give`・`Count` は段1a の同名ローカル関数を写す。製品の機械がクリーンルーム機械なら、`BuildRoom` と清浄機の配置も写す。本体:
```csharp
// 計算データを研究ツリーで消費して製作上限を Lv2 へ上げ、断られていた Lv2 の継承元から上位品が本当に作られることを確かめる
// Spend compute data in the research tree to raise the craft cap to Lv2, then a refused Lv2 source really makes the higher product
using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Client.Playtest.Operations;
using Core.Inventory;
using Core.Master;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Blocks.PowerGenerator;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Block.Interface.MachineUpgrade;
using Game.Context;
using Game.EnergySystem;
using Game.Fluid;
using Game.Research;
using Mooresmaster.Model.GameActionModule;
using Mooresmaster.Model.ResearchModule;
using Server.Protocol.PacketResponse.Util.ElectricWire.Connection;
using UnityEngine;
using UnityEngine.InputSystem;

var productBaseName = "GPULv1"; // 判断記録 A12 の名前（族の基底 = Lv1）。下で研究の対象に入っているかを assert して食い違いを捕まえる
var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("tier9-5-compute-research", options, async p =>
{
    await p.SetupFlatGround();
    await p.SkipOpeningSkitIfPlaying();
    p.WarpPlayer(new Vector3(0f, 33f, -6f));
    var consumers = new List<IBlock>();

    // Lv2 へ上げるアップグレード研究を効果で引き、前提だけを完了扱いにする
    // Find the Lv2 upgrade by its effect and mark only its prerequisites done
    var upgrade = MasterHolder.ResearchMaster.GetAllResearches().Single(r => CraftLevel(r)?.Level == 2);
    var productBaseGuid = MasterHolder.ItemMaster.GetItemMaster(PlaytestItemOps.ResolveItemId(productBaseName)).ItemGuid;
    p.Assert(CraftLevel(upgrade).TargetItemGuids.Contains(productBaseGuid), $"{productBaseName} がアップグレード Lv2 の対象に入っている");
    var researchStore = p.ServerService<IResearchDataStore>();
    researchStore.LoadResearchData(new ResearchSaveJsonObject { CompletedResearchGuids = WithPrerequisites(upgrade.PrevResearchNodeGuids).Select(g => g.ToString()).ToList() });
    await p.Until(() => upgrade.PrevResearchNodeGuids.All(researchStore.IsResearchCompleted), 15f, "アップグレード Lv2 の前提が完了扱いになった");

    // 製品の上限つき継承レシピの機械を置いて給電し、上限1では Lv2 の継承元が断られる
    // Place and power the machine of the product's capped recipe; at cap 1 the Lv2 source is refused
    var recipe = MasterHolder.MachineRecipesMaster.MachineRecipes.Data.Single(r => r.OutputItems.Any(o => o.ItemGuid == productBaseGuid) && r.LevelInheritance.ToString() == "InheritUpToUnlocked");
    var machine = Place(MasterHolder.BlockMaster.GetBlockMaster(recipe.BlockGuid).Name, new Vector3Int(0, 32, 0));
    PowerAll();
    SelectRecipe(machine, recipe.MachineRecipeGuid);
    var inventory = machine.GetComponent<VanillaMachineBlockInventoryComponent>();
    p.Assert(MachineRecipeLevelInheritanceUtil.TryGetShape(recipe, out var shape), "製品のレシピが継承レシピである");
    var sourceLv2 = MachineRecipeLevelInheritanceUtil.ResolveInputItemId(recipe, shape.SourceInputIndex, 2);
    p.Assert(Enumerable.Range(0, recipe.InputItems.Length).Any(s => inventory.CheckPlacement(s, ServerContext.ItemStackFactory.Create(sourceLv2, 1)) == MachineSlotPlacementCheck.LevelAboveCraftCap), "上限1では Lv2 の継承元が断られる");
    await p.Screenshot("01-lv2-refused-before-research");

    // 計算データを渡し、研究ツリーから UI で研究する
    // Hand over compute data and research through the tree UI
    var cost = upgrade.ConsumeItems.Single();
    var computeDataName = MasterHolder.ItemMaster.GetItemMaster(cost.ItemGuid).Name;
    await p.GiveItem(computeDataName, cost.ItemCount);
    await p.PressKey(Key.R);
    await p.WaitUiState(UIStateEnum.ResearchTree, 10f);
    await p.ClickWebUi($"research-node-{upgrade.ResearchNodeGuid}");
    await p.UntilWebUiElement($"research-button-{upgrade.ResearchNodeGuid}", 10f);
    await p.Screenshot("02-upgrade-node-detail");
    await p.ClickWebUi($"research-button-{upgrade.ResearchNodeGuid}");
    await p.Until(() => researchStore.IsResearchCompleted(upgrade.ResearchNodeGuid), 10f, "UI からアップグレード Lv2 を研究した");
    p.Assert(p.CountItem(computeDataName) == 0, "計算データが研究で消費された");
    p.Assert(p.ServerService<ILevelInheritanceCraftCapLookup>().GetCraftCap(productBaseGuid) == 2, "製作上限が 2 になった");
    await p.Screenshot("03-upgrade-researched");
    await p.PressKey(Key.Escape);

    // 断られていた Lv2 の継承元を入れると、上位品が本当に作られる
    // The formerly refused Lv2 source now really makes the higher product
    for (var i = 0; i < recipe.InputItems.Length; i++)
        inventory.InsertItem(ServerContext.ItemStackFactory.Create(MachineRecipeLevelInheritanceUtil.ResolveInputItemId(recipe, i, 2), recipe.InputItems[i].Count));
    var productLv2 = MachineRecipeLevelInheritanceUtil.ResolveOutputItemId(recipe, 0, 2);
    await p.Until(() => Count(machine, MasterHolder.ItemMaster.GetItemMaster(productLv2).Name) >= 1, recipe.Time + 30f, $"{productBaseName} の Lv2 が本当に作られた");
    await p.Screenshot("04-lv2-product-crafted");

    #region Internal

    UnlockLevelInheritanceCraftLevelGameActionParam CraftLevel(ResearchNodeMasterElement node) =>
        node.ClearedActions.items.Where(a => a.GameActionType == "unlockLevelInheritanceCraftLevel").Select(a => (UnlockLevelInheritanceCraftLevelGameActionParam)a.GameActionParam).FirstOrDefault();

    // ここに段1a の PowerAll・SelectRecipe・WithPrerequisites・Place・Give・Count を写す

    #endregion
});
```
継承元と各入力の品は、本番の `MachineRecipeLevelInheritanceUtil`（S1 先端の `Game.Block.Interface/MachineUpgrade/MachineRecipeLevelInheritanceUtil.cs:21`（`TryGetShape`）・`:95`（`ResolveInputItemId`）・`:103`（`ResolveOutputItemId`））で引く。名前や GUID の直書きに頼らない。

要確認（実装時に確かめる）は次の3点:
- (a) 研究ノードがツリーの表示範囲外にあるとき、`ClickWebUi` が届くか。届かなければ `ScrollDownUntilWebUiElementIsClickable`（`PlaytestWebUiOps.cs:66`）を使う。
- (b) `GameActionType` が文字列か定数か。`LevelInheritanceCraftCapDataStore.cs:37` は `GameActionElement.GameActionTypeConst.unlockLevelInheritanceCraftLevel` と比べているので、それに合わせる。
- (c) `LevelInheritance` の生成型。

前提: 研究の閉包、機械の直置き・給電、計算データの直渡し。本物の工程: UI の研究と、Lv2 の加工。スクショ: `01-lv2-refused-before-research`・`02-upgrade-node-detail`・`03-upgrade-researched`・`04-lv2-product-crafted`。

- [ ] **Step 7: 段6 核融合とエレベーター（S3 の下書きを移し、最後の工程を「昇り切って成果が出る」まで延ばす）**

下書き: `docs/superpowers/plans/2026-10-23-tier9-fusion-elevator-endgame.md` の「旧 Task 13: unityプレイ録画テスト」（目安 3904〜4077行）を、`scenarios/tier9/tier9-6-fusion-elevator.cs` に写す。差分は次の4点:
1. `Run` の名前を `"tier9-6-fusion-elevator"` にする。
2. 冒頭で、研究「軌道エレベーターの建設」（`01a16291-4094-7369-a87f-5dfe2197d25e`）と「核融合炉」（`019e3af0-297f-726e-8a02-0a8518124e6d`）を前提ごと閉包で完了扱いにする。そのうえで、拠点が研究から解放されたことを assert する（裁定 2026-10-25「拠点は軌道エレベーターの建設で解放」）:
```csharp
p.Assert(p.ServerService<Game.UnlockState.IGameUnlockStateDataController>().BlockUnlockStateInfos[Guid.Parse("01a162aa-cec0-738c-93c5-f58adc87fa7f")].IsUnlocked, "軌道エレベーターの建設で拠点が解放された");
```
3. 下書きの昇降の確認は「昇り始めた」まで（下書き 75〜82行相当）。その後に、昇り切って出口に成果が出るまでの待ちを足す。成果は到着の表から読む:
```csharp
var climbSeconds = ((SpaceElevatorBaseBlockParam)elevator.BlockMasterElement.BlockParam).ClimbSeconds;
var expected = p.ServerService<IOrbitalArrivalService>().PreviewArrivalOutputs(satelliteId, 1);
await p.Until(() => elevator.GetComponent<SpaceElevatorBaseInventoryComponent>().GetItem(1).Id == expected[0].Id, climbSeconds * 3f + 10f, "昇り切って出口に成果が出た");
await p.Screenshot("05-elevator-delivered");
```
（`satelliteId` は下書きの GPU衛星Lv1 の ID。電力が足りずに遅くなる分を見込んで、待ちを3倍にする）
4. `Arrive(cableId, 1000)` は前提として残す。理由: 本物の打ち上げは段4と G4 で確かめた。1000 は「1便の量」なので、下書きのまま `countPerLaunch` の倍数にする。

撤去でプールが戻る確認（下書き 88〜90行）は残す。Assert とスクショは下書きの `01`〜`04` に `05-elevator-delivered` を足す。Expected の Error ログの検査（下書き 131行）は、共通の決まりの「Error 0件」に含まれる。

- [ ] **Step 8: 段7 反物質→反物質爆弾→最終研究（新規）**

前提: 研究の閉包（最終研究 `01a167e9-21f6-7d26-a990-e7d6d78750d0` の前提）。核融合炉を電源にする（炉は 20000、反物質製造装置は 12000。`s3plan.md` 旧 Task 1 の blocks.json 行 114・145。値は P2 で変わりうるので、assert で確かめる）。

本物の工程は次の3つ:
1. 反物質製造装置で反物質を1個作る（レシピ `01a167e9-21df-7b2a-81e3-04be55cb89b0`）。
2. 電気汎用工作装置で反物質爆弾を1個作る（レシピ `01a167e9-21e2-7c4e-91be-8ad747466bde`）。不足分の素材は直渡しで補う。
3. UI から最終研究を行い、反物質爆弾10個を消費する。

最後の工程は、UI 経由の最終研究。

`scenarios/tier9/tier9-7-antimatter-final-research.cs`。`PowerAll`・`SelectRecipe`・`WithPrerequisites`・`Place`・`Give`・`Count`・`FluidIdOf` は段1a の同名ローカル関数を写す。`PowerAll` の発電機は、ガソリン発電機から核融合炉に差し替える（下の `PowerByFusion`）。本体:
```csharp
// 反物質を作って反物質爆弾を作り、UI の最終研究で10個消費して終わることを確かめる
// Make antimatter, then an antimatter bomb, and finish by spending ten in the final research through the UI
using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.UI.UIState;
using Client.Playtest;
using Client.Playtest.Operations;
using Core.Inventory;
using Core.Master;
using Game.Block.Blocks.PowerGenerator;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using Game.Fluid;
using Game.Research;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse.Util.ElectricWire.Connection;
using UnityEngine;
using UnityEngine.InputSystem;

var antimatterRecipeGuid = Guid.Parse("01a167e9-21df-7b2a-81e3-04be55cb89b0");
var bombRecipeGuid = Guid.Parse("01a167e9-21e2-7c4e-91be-8ad747466bde");
var finalResearchGuid = Guid.Parse("01a167e9-21f6-7d26-a990-e7d6d78750d0");
var deuteriumGuid = Guid.Parse("01a167e9-21b2-7bb0-b6a3-0f17d349704e");
var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("tier9-7-antimatter-final-research", options, async p =>
{
    await p.SetupFlatGround();
    await p.SkipOpeningSkitIfPlaying();
    p.WarpPlayer(new Vector3(0f, 33f, -8f));
    var consumers = new List<IBlock>();
    var finalResearch = MasterHolder.ResearchMaster.GetResearch(finalResearchGuid);
    var researchStore = p.ServerService<IResearchDataStore>();
    researchStore.LoadResearchData(new ResearchSaveJsonObject { CompletedResearchGuids = WithPrerequisites(finalResearch.PrevResearchNodeGuids).Select(g => g.ToString()).ToList() });
    await p.Until(() => finalResearch.PrevResearchNodeGuids.All(researchStore.IsResearchCompleted), 15f, "最終研究の前提が完了扱いになった");

    // 反物質製造装置と電気汎用工作装置を置き、核融合炉で給電する
    // Place the antimatter maker and the electric workbench, powered by a fusion reactor
    var antimatterRecipe = MasterHolder.MachineRecipesMaster.GetRecipeElement(antimatterRecipeGuid);
    var bombRecipe = MasterHolder.MachineRecipesMaster.GetRecipeElement(bombRecipeGuid);
    var maker = Place(MasterHolder.BlockMaster.GetBlockMaster(antimatterRecipe.BlockGuid).Name, new Vector3Int(0, 32, 0));
    var workbench = Place(MasterHolder.BlockMaster.GetBlockMaster(bombRecipe.BlockGuid).Name, new Vector3Int(8, 32, 0));
    var deuteriumPipe = p.PlaceBlockDirect("鋼鉄のパイプ", new Vector3Int(1, 33, -1), BlockDirection.North); // 入口 = [floor(x/2),0,0] 向き -z（s3plan 旧 Task 1 の規則、寸法 [3,4,3]）
    PowerByFusion(new Vector3Int(-30, 32, -30));
    SelectRecipe(maker, antimatterRecipeGuid);
    SelectRecipe(workbench, bombRecipeGuid);

    // 反物質を1個本当に作る
    // Really make one antimatter
    var deuterium = MasterHolder.FluidMaster.GetFluidId(deuteriumGuid);
    await p.Until(() => { deuteriumPipe.GetComponent<IFluidInventory>().AddLiquid(new FluidStack(50, deuterium), default); return Count(maker, OutputName(antimatterRecipe)) >= 1; }, antimatterRecipe.Time * 2f + 30f, "反物質が本当に1個できた");
    await p.Screenshot("01-antimatter-made");

    // 作った反物質に不足分を足して、反物質爆弾を1個本当に作る
    // Add the missing inputs to the made antimatter and really make one bomb
    var antimatterId = MasterHolder.ItemMaster.GetItemId(antimatterRecipe.OutputItems[0].ItemGuid);
    TakeFromOutputs(maker, antimatterId, 1);
    // 作った1個は出力口から抜いたので、レシピの必要数を丸ごと入れる（反物質は1個が本物、残りが直渡し）
    // The made one was taken out of the outputs, so insert each full requirement (one antimatter is real, the rest handed over)
    foreach (var input in bombRecipe.InputItems)
        workbench.GetComponent<IOpenableBlockInventoryComponent>().InsertItem(ServerContext.ItemStackFactory.Create(MasterHolder.ItemMaster.GetItemId(input.ItemGuid), input.Count));
    await p.Until(() => Count(workbench, OutputName(bombRecipe)) >= 1, bombRecipe.Time * 2f + 30f, "反物質爆弾が本当に1個できた");
    await p.Screenshot("02-bomb-made");

    // 作った1個を持ち、残りを渡して、UI から最終研究を行う
    // Carry the made bomb, receive the rest, and run the final research through the UI
    var bombName = OutputName(bombRecipe);
    var bombId = MasterHolder.ItemMaster.GetItemId(bombRecipe.OutputItems[0].ItemGuid);
    TakeFromOutputs(workbench, bombId, 1);
    p.GiveItemDirect(bombName, 1);
    var bombCost = finalResearch.ConsumeItems.Single(c => c.ItemGuid == bombRecipe.OutputItems[0].ItemGuid).ItemCount;
    await p.GiveItem(bombName, bombCost - 1);
    await p.PressKey(Key.R);
    await p.WaitUiState(UIStateEnum.ResearchTree, 10f);
    await p.ClickWebUi($"research-node-{finalResearchGuid}");
    await p.UntilWebUiElement($"research-button-{finalResearchGuid}", 10f);
    await p.Screenshot("03-final-research-detail");
    await p.ClickWebUi($"research-button-{finalResearchGuid}");
    await p.Until(() => researchStore.IsResearchCompleted(finalResearchGuid), 10f, "UI から最終研究を完了した");
    p.Assert(p.CountItem(bombName) == 0, $"反物質爆弾 {bombCost} 個が消費された");
    await p.Screenshot("04-final-research-complete");

    #region Internal

    string OutputName(Mooresmaster.Model.MachineRecipesModule.MachineRecipeMasterElement recipe) => MasterHolder.ItemMaster.GetItemMaster(MasterHolder.ItemMaster.GetItemId(recipe.OutputItems[0].ItemGuid)).Name;

    void TakeFromOutputs(IBlock block, Core.Master.ItemId itemId, int count)
    {
        var inventory = block.GetComponent<IOpenableBlockInventoryComponent>();
        var slot = Enumerable.Range(0, inventory.GetSlotSize()).First(s => inventory.GetItem(s).Id == itemId && inventory.GetItem(s).Count >= count);
        inventory.SetItem(slot, itemId, inventory.GetItem(slot).Count - count);
    }

    void PowerByFusion(Vector3Int reactorPos)
    {
        // 核融合炉へ重水素を直接入れ、広範囲電柱で全消費ブロックへ配線する。炉の出力が反物質製造装置の要求以上であることを先に確かめる
        // Feed deuterium straight into the reactor and wire every consumer through a wide-range pole; first check reactor output covers the maker
        var reactor = p.PlaceBlockDirect("核融合炉", reactorPos, BlockDirection.North);
        var reactorPower = ((ElectricGeneratorBlockParam)reactor.BlockMasterElement.BlockParam).FuelFluids.Max(f => f.Power);
        p.Assert(reactorPower >= ((ElectricMachineBlockParam)maker.BlockMasterElement.BlockParam).RequiredPower, $"炉の出力 {reactorPower} が反物質製造装置の要求以上");
        reactor.GetComponent<VanillaElectricGeneratorComponent>().AddLiquid(new FluidStack(2000, MasterHolder.FluidMaster.GetFluidId(deuteriumGuid)), FluidContainer.Empty);
        // ここから先は段1a の PowerAll の配線部分（広範囲電柱・TryConnectBothSides・RebuildAround）を、generator を reactor に替えて写す
    }

    // ここに段1a の SelectRecipe・WithPrerequisites・Place・Give・Count を写す

    #endregion
});
```
要確認（実装時に確かめる）は次の3点:
- (a) `ElectricGeneratorBlockParam.FuelFluids[].Power` と `ElectricMachineBlockParam.RequiredPower` の生成名。`_s3blocks.yml:347-386` の `fuelFluids`/`power`、S3 の `blocks.json` の `requiredPower` に対応する。
- (b) 核融合炉 [19,10,17] と広範囲電柱の届く距離。届かなければ電柱を中継に1本足す。
- (c) 電気汎用工作装置の入力スロットの数が、レシピの入力4種以上ある。

スクショ: `01-antimatter-made`・`02-bomb-made`・`03-final-research-detail`・`04-final-research-complete`。

- [ ] **Step 9: SKILL.md のカテゴリ一覧に tier9 を足す**

`.agents/skills/unity-playmode-recorded-playtest/SKILL.md` で、シナリオのカテゴリを並べている行（`review-s1h-placement.md:72` が「SKILL.md:90」と指す行）に、`tier9`（Tier9 の録画7段）を1語足す。

- [ ] **Step 10: 8本を順に前景で流す**

Run（段ごとに。`<file>` を `tier9-1a-chip-line` → `tier9-1b-mixed-level-sorting` → `tier9-2-modules-gpu` → `tier9-3-yield-ui` → `tier9-4-rocket-launch` → `tier9-5-compute-research` → `tier9-6-fusion-elevator` → `tier9-7-antimatter-final-research` の順に替える）:
```bash
uloop clear-console --project-path ./moorestech_client
"$SKILL/scripts/run-scenario.sh" ./moorestech_client "$SKILL/scenarios/tier9/<file>.cs" "$MW"
uloop get-logs --project-path ./moorestech_client --log-type Error
```
Expected: どの段も run-scenario.sh が exit 0 で終わり、`result.json` が `Success: true`、Asserts が全部 Passed。Error ログが0件。落ちたら原因を直して流し直す。座標だけの問題ならシナリオを直す。本体の不具合なら本体を直し、bd に記録し、該当する自動テストを足す。各段の `result.json` と Error ログの出力を `../moorestech_logs/harness/tier9-integration/p5/<file>/` に写す。

- [ ] **Step 11: 動画とスクショを目で確かめる**

各段のスクショを Read で開き、段ごとに書いた見え方を確かめる。段4は下書きの3点、段1b は理由の文言と、仕分け後に両方の機械に製品があること。結果を `../moorestech_logs/harness/tier9-integration/p5/README.md` に1段1行で書く。全段の録画を `share-put` し、URL を同じ README に書く。

- [ ] **Step 12: コミット**
```bash
git add .agents/skills/unity-playmode-recorded-playtest/scenarios/tier9 .agents/skills/unity-playmode-recorded-playtest/SKILL.md
git commit -m "Task 28: 出荷マスタでのTier9録画7段（チップライン・混在仕分け・モジュールGPU・抽選UI・打ち上げ・計算データ研究・核融合エレベーター・最終研究）"
git -C ../moorestech_logs add harness/tier9-integration/p5 && git -C ../moorestech_logs commit -m "tier9: P5 録画7段の result.json と確認記録"
```

**完了判定:** `../moorestech_logs/harness/tier9-integration/p5/` に8本分の `result.json` があり、全部 `Success: true` で、Asserts が全部 Passed、Error ログが0件。README に録画の共有 URL と、目で確かめた結果が1段1行ずつある。段4の録画で、建造（BuildSeconds 秒）と上昇が早送りなしで映っている（ORCH J3）。

### Task 29: A11 の撮り直しと「State is null」の再観測

**Files:**
- Modify（崩れていた場合だけ）: `.agents/skills/unity-playmode-recorded-playtest/scenarios/misc/cleanroom-room-usability.cs`
- 記録: `../moorestech_logs/harness/tier9-integration/p5/a11/`

**Interfaces:**
- Consumes: Task 28 と同じピン先マスタ。既存シナリオ A11 `scenarios/misc/cleanroom-room-usability.cs`（S1.md:157 によると、旧 EUV（3×2×3）と 630fdce の値を前提にしている）
- Produces: A11 の `result.json`（全 pass）と、「State is null」の観測結果（行数と、出たブロックの Name/Pos）

事実と仮説:
- 事実: S1 の PR 本文は、A11 の録画中に発電機の `CommonMachineBlockStateChangeProcessor` が「State is null」を 3 回出したと書いている（S1.md:179）。LogError の出所は `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/StateProcessor/CommonMachineBlockStateChangeProcessor.cs:24-27`。`CommonMachineBlockStateDetail` が無い状態を受けると出る。
- 事実: S1 では `ElectricGenerator` も、この処理を付ける型に入っている（S1 先端の `Client.Game/InGame/Context/BlockGameObjectPrefabContainer.cs:146-154`）。S3 はこの型から `ElectricGenerator` を外した（S3 先端の同ファイル:146-153）。
- 推測: 統合で S3 側の変更が入っていれば、発電機にこの処理が付かなくなり、0 回になる。Step 1 と Step 3 で確かめる。

- [ ] **Step 1: 統合ブランチで発電機が外れていることを確かめる**  Run: `git show HEAD:moorestech_client/Assets/Scripts/Client.Game/InGame/Context/BlockGameObjectPrefabContainer.cs | grep -n -A8 "bool IsCommonMachine"`  Expected: 列挙に `ElectricGenerator` が無い（S3 と同じ）。有れば、P1 の衝突解消で S1 側が残っている。P1 の担当に戻し、S3 側へ直す。

- [ ] **Step 2: A11 を流す**
```bash
uloop clear-console --project-path ./moorestech_client
"$SKILL/scripts/run-scenario.sh" ./moorestech_client "$SKILL/scenarios/misc/cleanroom-room-usability.cs" "$MW"
uloop get-logs --project-path ./moorestech_client --log-type Error
```
Expected: `Success: true`。崩れた場合（S1.md:157 の推測。EUV が IPA 必須・モジュール 3 スロットになった）は、`result.json` の Error の先頭行と、最後に PASS した Assert で地点を特定する。シナリオの前提値（EUV の寸法・IPA の注入・モジュール数）を出荷マスタに合わせて直し、流し直す。本体のコードは直さない（A11 は検証資産。本体の不具合が見えたら bd に積んで Task 28 と同じ扱いにする）。

- [ ] **Step 3: 「State is null」を数える**  Run: `uloop get-logs --project-path ./moorestech_client --log-type Error | grep -c "State is null"`  Expected: `0`。1以上なら、ログの `Name:` と `Pos:` から、どのブロックかを記録する。そのブロックの型がサーバーで `CommonMachineBlockStateDetail` を出しているか（`GetBlockStateDetails`）を読み、原因を「推測」と明記して bd に積む。直し方の推奨は、そのブロックの型を `IsCommonMachine` から外すか、正しい状態処理を付けるか。Task 28 の段6・段7（核融合炉＝ElectricGenerator を使う）の Error ログにも同じ行が無いことを、Task 28 の記録で突き合わせる。

- [ ] **Step 4: 記録してコミットする**  `result.json`・Error ログ・観測の1行（「State is null: N 回。出たブロック: …」）を `../moorestech_logs/harness/tier9-integration/p5/a11/` に置く。
```bash
git add .agents/skills/unity-playmode-recorded-playtest/scenarios/misc/cleanroom-room-usability.cs
git commit -m "Task 29: A11 を出荷マスタの EUV・IPA に合わせて撮り直す"
git -C ../moorestech_logs add harness/tier9-integration/p5/a11 && git -C ../moorestech_logs commit -m "tier9: A11 撮り直しと State is null の再観測"
```
（シナリオに差分が無ければ、本体のコミットは作らない）

**完了判定:** A11 の `result.json` が `Success: true`。「State is null」の件数が記録にある。0 件なら完了。1 件以上なら、bd の課題番号と、出たブロックの Name/Pos が記録にある。統合 PR の本文の既知の制約欄を、この結果で書き換える（0 件なら「解消」）。

### Task 27: W13（混在ベルトで代用スロットの上位品が下位へ変わる）を仕様か不具合か確かめる

**Files:**
- 裁定（plan 作成時に記録済み）: `.decisions/2026-10-25-混在ベルトの代用スロットで上位品を下位へ揃える挙動は仕様とする.md`
- 記録: `../moorestech_logs/harness/tier9-integration/p5/w13.md`
- Modify（不具合の場合だけ）: Task 28 段1b の末尾（Step 2 に書いた assert）

**Interfaces:**
- Consumes: `MachineInputSlotBinding.AlignSubstituteVariant`（S1 先端の `Game.Block/Blocks/Machine/Inventory/Input/MachineInputSlotBinding.cs:94-117`）。呼び出し元は `VanillaMachineInputSimulationUtil.cs:71`（自動搬入だけ）。既存テストは `Tests/CombinedTest/Core/Machine/Upgrade/LevelFamilySubstituteMachineTest.cs` の `AutomatedInflowAlignsToLowerLevelTest`（76〜103行）と `InheritanceSourceSlotNeverAlignsTest`（105〜119行）
- Produces: ユーザー裁定（`.decisions/`）と、段1b の期待値

事実:
- この挙動は意図して作られている。コメントに「1スロット1IDのまま混在ベルトで詰まらせない」とあり（同ファイル:94-95）、テストで固定されている。設計の出所は S1 plan の RC15（`s1plan.md` 56行・11606行）。
- `.decisions/` に、この挙動の裁定は無い。PR #1554 の本文には「意図確認のみ」とある。
- 2026-10-24 の裁定（混ぜない・格下げしない）が対象にしているのは、継承元スロット。代用スロットではない。

- [ ] **Step 1: 挙動を実測する**  Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "LevelFamilySubstituteMachineTest"`  Expected: 全 Passed。特に `AutomatedInflowAlignsToLowerLevelTest`（スロットの Lv2 に Lv1 が来ると両方 Lv1 になる）と `InheritanceSourceSlotNeverAlignsTest`（継承元では揃えない）が通る。これで「代用スロットでは下位へ揃い、継承元では揃わない」が観測できる。

- [ ] **Step 2: Tier9 での故障シナリオを1つ書く**  `../moorestech_logs/harness/tier9-integration/p5/w13.md` に次を書く（実測の値は Step 1 の結果から写す）:
  - 入力: GPU衛星Lv1 のレシピ（入力は GPU の基準＝Lv1 を要求する代用）を選んだ機械に、GPU Lv1 と Lv2 が混ざったベルトがつながっている。
  - 状態: 機械の GPU スロットに Lv2 が先に入っている。
  - 結果: 後から Lv1 が来ると、スロットの Lv2 が全部 Lv1 に変わる。その Lv2 は GPU衛星Lv2 の材料に使えなくなる（Lv2 を作るには、継承元で研究の上限まで上げた分の材料と時間がかかる）。
  - 揃えない場合の帰結: 混在ベルトで Lv2 がスロットに入ったままになり、Lv1 が積めずにベルトが詰まる。止まった理由の表示（2026-10-24 の裁定）は、継承元スロット向けの表示である。

- [ ] **Step 3: 裁定どおり仕様として扱う**  裁定は plan 作成時に出ている（(A) 揃えて流し続ける）。`w13.md` の末尾に「裁定: 仕様（`.decisions/2026-10-25-混在ベルトの代用スロットで上位品を下位へ揃える挙動は仕様とする.md`）」と書く。段1b の期待値は Step 2 の本文どおりで、本段に W13 は現れない。コードは変えない。
```bash
git -C ../moorestech_logs add harness/tier9-integration/p5/w13.md
git -C ../moorestech_logs commit -m "tier9-integration: W13 の実測と故障シナリオ（裁定: 仕様）"
```

**完了判定:** `w13.md` に Step 1 の実測（2 テストが Passed）・故障シナリオ・裁定の行がある。

---

## P6 最終検証の受け渡し

裁定 2026-10-25「Tier9 統合の最終検証の通しプレイはユーザー本人が行う」の (3) と (4)。(1) 自動ゲートと CI、(2) 録画7段がすべて通ってから着手する（ORCH の合流 J3 の後）。順序は Task 30（ビルド・セーブ・チェックリスト）→ Task 31（評価役2体）→ Task 32（全ブランチレビュー。反映が判定経路に触れたら評価役をやり直す）→ Task 33（PR）。評価役は PR を出す前に判定する（ユーザー規約「外に出す前に別の評価役2体」）。

### Task 31: 目的だけを渡す別の評価役2体

**Files:**
- Create: `../moorestech_logs/harness/tier9-integration/eval/prompt.md`（下の依頼文の本文）
- 記録: `../moorestech_logs/harness/tier9-integration/eval/<round>-<evaluator>/verdict.md`（評価役が書く）

**Interfaces:**
- Consumes: 統合 PR の head コミットとピン先マスタ。Task 30 の Step 3 で用意した Tier8 終盤セーブの「入れ物」。評価役も同じセーブで始められるよう、Task 30 の Step 1〜3 を先に済ませる
- Produces: 2体の `verdict.md`（`VERDICT: PASS` か `VERDICT: FAIL` と、根拠つきの指摘）

評価役の構成:
- 評価役A: Claude Code（`~/bin/cmux-agent-pane -C eval-a -a ccd -m opus --permission-mode full --prompt-file <prompt>`）
- 評価役B: Codex（`~/bin/cmux-agent-pane -C eval-b -a cxd -m gpt-6.1-sol --prompt-file <prompt>`）

どちらも、起動の前に `moores-wt new tmp/tier9-eval-a --dir tier9-eval-a --from origin/feat/tier9-integrated`（B も同じ形）で自分の worktree を作る。Editor の数は合わせて 2〜3 本に抑える。A（本体）の Editor は、評価の間は畳む。Codex の週次枠は pi 本体と共有している（メモリ「pi本体とcodexレビューは同じ枠」）。起動の前に、オーケストレーション §2「Codex 枠の確認」の方法で枠を確かめる。足りなければ、評価役B を Claude Code（opus）の別セッションで起動する（判断記録 A28）。ユーザーには聞かない。

- [ ] **Step 1: 依頼文を書く**  `../moorestech_logs/harness/tier9-integration/eval/prompt.md`（`<…>` は起動時に埋める）:
```markdown
あなたは moorestech のプレイ可能性を判定する評価役です。判定の基準はあなた自身が決めます。こちらの検査結果は渡しません。

## 目的
ブランチ `feat/tier9-integrated`（コミット <SHA>）と、そのピン先のマスタ（<master SHA>）で、Tier8 を終えたプレイヤーが Tier9 を進め、最終研究（反物質爆弾を規定数消費する研究）まで到達できるかを判定してください。条件は、デバッグ機能・コマンド・セーブの直接編集・サーバーへの直接の書き込みを使わずにプレイヤーが到達できることです。
次のどれかがあれば不合格の理由です:
- 到達できない（作り方が無い・解放されない・機械が置けない・搬送できない）
- 止まっても理由が画面に出ず、プレイヤーが原因を知れない
- 待ち時間や必要量が、遊びとして明らかにおかしい

## 使ってよいもの
- この worktree: `<worktree path>`。Unity Editor はここで自分のものを使う（`moores-wt status` で確かめる）
- コードとマスタ JSON の読解。Unity のテスト実行（uloop）
- unityプレイ録画テスト（`.agents/skills/unity-playmode-recorded-playtest`）で、自分でシナリオを書いて実際に遊ぶこと。Tier8 終盤のセーブ `<world dir>` は `PLAYTEST_WORLD_DIRECTORY=<world dir>`（`PLAYTEST_MAP_MODE`・`PLAYTEST_SEED` も同じスキルの run-scenario.sh の説明に従う）で起動できる
- Tier9 を実時間で通すと 15〜25 時間かかり、録画 1 回は約 7 分で打ち切られる（時間加速は無い）。そのため、区切りごとの前提はシナリオの裏口（アイテム付与・ブロック解放・研究の完了扱い）で揃えてよい。ただし各区切りの最後の工程（その区切りで初めて作るもの・初めて動く機械・打ち上げや昇降の 1 回）は、本物の加工・本物の待ち・UI 経由の操作で通すこと
- 使った裏口は、verdict.md に 1 件ずつ「何を・どの区切りで・なぜ」の形で申告する。申告の無い裏口を使った判定は無効とする

## 読まないもの
`docs/superpowers/plans/2026-10-25-tier9-*`、`../moorestech_logs/harness/tier9-integration/`（`eval/<round>-<自分>/` を除く）、`.agents/skills/unity-playmode-recorded-playtest/scenarios/tier9/`。理由: 実装側の見立てに引きずられないため。

## 出し方
- `../moorestech_logs/harness/tier9-integration/eval/<round>-<evaluator>/verdict.md` に書く。1行目は `VERDICT: PASS` か `VERDICT: FAIL`。
- 不合格の指摘は、1件ずつ「入力・状態 → 起きたこと → 期待したこと」の故障シナリオで書く。証拠（スクショのパス・result.json・ログの行・ファイル:行）を添える。証拠の無い主張には「推測」と書く。
- 合格の場合も、通した経路（研究の順、各段で実際に確かめたこと）と証拠を書く。
- 待つときはバックグラウンドのポーリングを使わず、全部前景で実行する。区切りごとに、自分の worktree のブランチと `../moorestech_logs` にコミットする。マージや push は判定に要らないので行わない。
- 最後の返答は3行: verdict.md のパス、PASS/FAIL、指摘の件数。
```

- [ ] **Step 2: 2体を同時に起動する**  `<round>` は `r1` から始める。起動は上の「評価役の構成」のとおり。待ちは cmux の画面を見て前景で行う（バックグラウンドのポーリングはしない）。

- [ ] **Step 3: 完了の主張を確かめる**  評価役の返答を、すぐには信じない（ユーザー規約）。`verdict.md` が実在すること、1行目が `VERDICT:` であること、証拠のパスが実在することを確かめる。無ければ、cmux で評価役がまだ動いているかを見る。動いていれば再派遣しない。

- [ ] **Step 4: 合否を扱う**  合格は、2体とも `VERDICT: PASS` のときだけ。1体でも FAIL なら次の3つを行う:
  - (a) 指摘を1件ずつ再現する。再現は自分の worktree で、評価役の証拠と同じ手順で行う。
  - (b) 再現できたものは bd に積んで直す。直したら、該当する自動テストか録画の段を足す。
  - (c) 再現できなかった指摘も、こちらの判断では退けない。「評価役の主張・こちらの再現手順と結果」を並べて、ユーザーの判断に回す（基準を緩めない）。

  直したら `<round>` を1つ進め（`r2`）、2体とも新しいセッションで最初から評価し直す（前の verdict は渡さない）。前に PASS を出した評価役も、もう一度評価する。理由: 直した内容が、別の評価役の通った経路を壊していないことを確かめるため。

- [ ] **Step 5: 記録してコミットする**
```bash
git -C ../moorestech_logs add harness/tier9-integration/eval && git -C ../moorestech_logs commit -m "tier9: 評価役 <round> の判定"
```
評価役の worktree は、判定が出たら `moores-wt rm tier9-eval-a --force --prune-branch`（B も同じ）で畳む。評価役のシナリオは検証の証拠なので、`verdict.md` から参照されているものを、畳む前に `eval/<round>-<evaluator>/` へ写す。

**完了判定:** 同じ `<round>` の `eval/<round>-a/verdict.md` と `eval/<round>-b/verdict.md` が、どちらも1行目 `VERDICT: PASS`。それより前の round の FAIL の指摘は、全部が「直した（bd 番号と追加した検査）」か「ユーザーの判断済み（`.decisions/` のファイル）」のどちらかで閉じている。

### Task 30: 配布ビルド（staging）・Tier8 終盤セーブ・ユーザーに渡すチェックリスト

**Files:**
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/tier9/tier9-0-tier8-save-smoke.cs`（Tier8 終盤セーブの事前確認）
- Create: `../moorestech_logs/harness/tier9-integration/handoff/checklist.html`（ユーザーに渡すチェックリスト）
- 記録: `../moorestech_logs/harness/tier9-integration/handoff/`（ビルドの run ディレクトリ名・promotion.md の写し・セーブの出所・事前確認の result.json）

**Interfaces:**
- Consumes: 統合 PR の head（`origin/feat/tier9-integrated`）。ピンが指すマスタ統合 PR の push 済みコミット。`scripts/playtest/release-playtest.sh`（S3 先端の `scripts/playtest/README.md:69-96`）。`GameSystemPaths`: Windows のセーブは `C:\Users\<user>\AppData\Roaming\.moorestech\Saves\world_1`（master の `Game.Paths/GameSystemPaths.cs:20,33,45`）。ワールドの直下には `save.json` などがある（master の `Game.Paths/WorldDataDirectory.cs:150-152`）
- Produces: `playtest-staging` に上がった Windows ビルドの ID。検証済みの Tier8 終盤セーブ。チェックリストの共有 URL

- [ ] **Step 1: Tier8 終盤セーブを用意する**

出所は裁定 `2026-10-25-通しプレイの開始セーブはMacBookProのセーブに旧コスト0研究の完了を手で立ててから移行する` のとおり、MacBook Pro（`ssh devmac`。蓋が開いているときだけ届く）の `~/Library/Application Support/moorestech/saves/world_1`。plan 作成時の実測: `save.json` は 10-22 15:29・worldVersion 6・完了研究 38 件・sha256 `454bab3830ca910f688598f16b787886927e33d4d81ecd78497cc891cb02c9c0`、`world.json` は `mapMode: template`・`seed: 0`。リセット対象の 6 ノードと旧「ロケット」はどれも未完了だった。版 6 なので、統合版では V6→V7（流体）と V7→V8 の 2 段が掛かる。

1) コピーを取る。ワールドの直下のうち、ゲームが読むものだけを持ってくる（`backup/`・`pruned/`・`snapshots/`・`*.bak*`・`save.before-*` は持ってこない）。
```bash
mkdir -p <scratch>/tier8-world-original
scp devmac:'~/Library/Application\ Support/moorestech/saves/world_1/{save.json,map.json,world.json}' <scratch>/tier8-world-original/
shasum -a 256 <scratch>/tier8-world-original/save.json
```
Expected: sha256 が上の実測値と同じ。違えば、ユーザーがその後も遊んだということなので、新しい値を `save-origin.md` に書いて進める。

2) コピーに、旧マスタでコスト0だった研究の完了と、それらが解放していたものの解放を手で立てる。テスターのセーブと同じ状態を作り、リセット部品（Task 7）を実物で通すため。立てるのは Task 26 の `ResetResearchGuids` 6 件と旧「ロケット」`019e3af0-7b1c-77de-9197-87d276c84392`（統合マスタに無いので `ResearchSectionPruner` が落とす。A4）、`ResetBlockGuids` 3 件、`ResetItemViewGuids` 8 件。値は Task 26 の定数から写す。
```bash
cp -R <scratch>/tier8-world-original <scratch>/tier8-world
python3 - <scratch>/tier8-world/save.json <<'PY'
import json, sys
path = sys.argv[1]
save = json.load(open(path))
research = ["019e3aab-32c6-7166-a184-d761153c2498", "019e3ae2-5005-7439-b242-9b7b3191a2c3", "019e3af5-f2e6-76de-a80f-5e66e79fa35d",
            "019e3af0-297f-726e-8a02-0a8518124e6d", "019e3aef-a149-746b-8fad-159e0434cb51", "01a16291-4094-7369-a87f-5dfe2197d25e",
            "019e3af0-7b1c-77de-9197-87d276c84392"]
blocks = ["01a162f1-ab04-76e2-94cf-dc713edeb63b", "01a162aa-cec0-738c-93c5-f58adc87fa7f", "01a162b0-7769-7419-af24-bb35528b93d2"]
item_views = ["01a162ab-07ba-73c1-9dd6-38e1e867663b", "019e3af5-e750-71ae-ab09-2b98f3db9913", "01a16290-f793-748b-a0b0-91d44be0d4ab", "01a16297-4724-73b9-82c3-2e77120be77d",
              "01a16297-6665-765d-8e19-96d357394c3e", "01a16297-a7f5-759b-91ba-be4021ca3bf2", "01a162a4-9334-7286-a8ba-7daf9c0f407a", "01a162a8-e435-73d0-bddb-08533819b80e"]
completed = save["research"]["CompletedResearchGuids"]
completed.extend(g for g in research if g not in completed)
unlock = save["gameUnlockState"]
print("block の例:", json.dumps(unlock["blockUnlockStateInfos"][:1]))
print("item の例:", json.dumps(unlock["itemUnlockStateInfos"][:1]))
PY
```
最後の 2 行の出力で、`blockUnlockStateInfos` と `itemUnlockStateInfos` の 1 件の形（キー名と、解放済みを表す値）を見る。同じ形で、`blocks` と `item_views` の GUID を解放済みとして足す（既にある GUID は値だけ解放済みに変える）。形は G5 の版 7 fixture（Task 26 Step 1）と同じはずなので、そちらの書き方を写してよい。足したら `json.dump(save, open(path, "w"))` で書き戻す。版（`worldVersion`）は 6 のまま変えない。

3) 記録する。`../moorestech_logs/harness/tier9-integration/handoff/save-origin.md` に、出所のパス・元の sha256・足した GUID の一覧・足した後の sha256・元の完了研究の件数（38）を書く。あわせて、元の 38 件のうち統合マスタの `research.json` に無い GUID の数を数えて書く（`ResearchSectionPruner` が落とすので、残る件数から引く）。

ユーザーのセーブはリポジトリにコミットしない。置き場は scratch と `../moorestech_logs` の非公開の場所に限る。

- [ ] **Step 2: Tier8 終盤セーブの事前確認シナリオを書く**

`scenarios/tier9/tier9-0-tier8-save-smoke.cs`:
```csharp
// Tier8 終盤のセーブ（版6）が統合版でロードでき、コスト0で完了していた Tier9 研究とその解放が外れ、他の進行が残ることを確かめる
// Confirm the late-Tier8 save loads on the integrated build, zero-cost Tier9 research and its unlocks are reset, and other progress remains
using System;
using System.Linq;
using Client.Playtest;
using Core.Master;
using Game.Research;
using Game.UnlockState;
using UnityEngine;

var resetResearch = new[]
{
    "019e3aab-32c6-7166-a184-d761153c2498", "019e3ae2-5005-7439-b242-9b7b3191a2c3", "019e3af5-f2e6-76de-a80f-5e66e79fa35d",
    "019e3af0-297f-726e-8a02-0a8518124e6d", "019e3aef-a149-746b-8fad-159e0434cb51", "01a16291-4094-7369-a87f-5dfe2197d25e",
}.Select(Guid.Parse).ToArray();
var resetBlocks = new[] { "01a162f1-ab04-76e2-94cf-dc713edeb63b", "01a162aa-cec0-738c-93c5-f58adc87fa7f", "01a162b0-7769-7419-af24-bb35528b93d2" }.Select(Guid.Parse).ToArray();
var minimumKeptResearch = 38; // 元の完了38件。統合マスタに無い件数を Step 1 の記録から引いて置き換える。大きく減ったら移行の事故として止める
var options = new PlaytestRunOptions { Record = false };
return PlaytestRunner.Run("tier9-0-tier8-save-smoke", options, async p =>
{
    // ロード直後の研究と解放を読み、リセットの効果と他の進行の残りを確かめる
    // Read research and unlocks right after loading to check the reset and that other progress remains
    await p.SkipOpeningSkitIfPlaying();
    var research = p.ServerService<IResearchDataStore>();
    var completed = MasterHolder.ResearchMaster.GetAllResearches().Count(r => research.IsResearchCompleted(r.ResearchNodeGuid));
    p.Note($"完了済みの研究 {completed} 件");
    foreach (var guid in resetResearch) p.Assert(!research.IsResearchCompleted(guid), $"リセット対象 {guid} は未完了");
    var unlocks = p.ServerService<IGameUnlockStateDataController>();
    foreach (var guid in resetBlocks) p.Assert(!unlocks.BlockUnlockStateInfos.TryGetValue(guid, out var info) || !info.IsUnlocked, $"本番ブロック {guid} は未解放");
    p.Assert(completed >= minimumKeptResearch, $"Tier8 までの研究が残っている（{completed} 件）");
    await p.Screenshot("01-loaded-tier8-save");
});
```
`minimumKeptResearch` は、Step 1 の `save-origin.md` の値で置き換える。値は「元の完了件数 38 − そのうち統合マスタに無い件数」。手で足したリセット対象の 7 件は数えない（全部外れるため）。

- [ ] **Step 3: 統合版の Editor で事前確認を流す**

セーブは複製を使う（ロードで `Backups/<版>/save.json` に退避されるため、原本を汚さない）。
```bash
cp -R <scratch>/tier8-world <scratch>/tier8-world-smoke
PLAYTEST_WORLD_DIRECTORY=<scratch>/tier8-world-smoke PLAYTEST_MAP_MODE=template PLAYTEST_SEED=0 "$SKILL/scripts/run-scenario.sh" ./moorestech_client "$SKILL/scenarios/tier9/tier9-0-tier8-save-smoke.cs" "$MW"
uloop get-logs --project-path ./moorestech_client --log-type Error
```
`PLAYTEST_MAP_MODE=template`・`PLAYTEST_SEED=0`（Step 1 の `world.json` の値。`fixed-world-environment.sh` は 3 つの変数が揃っていて種が整数であることを検査する）。

Expected: `Success: true`、Error ログ0件。`<scratch>/tier8-world-smoke` の下に、元の版の退避（`BackupSaveJsonPath(<元の版>)`）ができている。落ちた場合は止まり、セーブの版と Error の先頭行を P1（セーブ移行）の担当に返す。ユーザーには渡さない。

- [ ] **Step 4: 配布ビルドを焼いて staging に上げる**

ユーザーは MacBook Pro の Steam で、全部が完成してから（Task 33 の後に）遊ぶ（ユーザー発言 2026-10-25「通しプレイはproでやるけど、全部が完成してから通しプレイで遊びたい」）。ビルドは Windows と Mac を両方焼く。Windows 版は検証機の通し検証に使い、Mac 版をユーザーが遊ぶ。

moores-playtest-release スキルの手順1を使う（失敗したら手順2）。パッチノート（手順3）と playtest への反映（手順5）は行わない。テスターへの配布は裁定で棄却されている。
```bash
. ~/hermes-agent/data/services/playtest/env.sh
cd ~/hermes-agent/data/repos/moorestech
MOORESTECH_BUILD_BRANCH=feat/tier9-integrated scripts/playtest/release-playtest.sh origin/feat/tier9-integrated
```
`MOORESTECH_BUILD_BRANCH` を付ける理由: 要求コミットが `origin/$MOORESTECH_BUILD_BRANCH` に含まれないと、ビルド前に止まるため（README:80。既定は master）。

Expected: Windows と Mac のビルド・成果物の検査・`steamcmd +run_app_build` が成功する。検証機の phase1/phase2 が通る。Mac 版は moores-playtest-release スキルの Mac の確認手順（Mac mini の Steam で起動を見る）まで通す。`~/hermes-agent/data/services/playtest/runs/<label>/promotion.md` ができる。

Steam Guard のコードを求められたら、ユーザーに入力を頼む（スキルの手順に従う）。staging は共用である。この run の後に、別のビルドを `playtest-staging` に上げない。ユーザーの通しプレイが終わるまで、staging を占有する。run ディレクトリ名とビルド ID を `handoff/build.md` に書く。

- [ ] **Step 5: MacBook Pro にセーブを置く手順を用意する（置くのは Task 33 Step 4）**

ユーザーは全部が完成してから遊ぶので、セーブを置くのは PR 作成と CI の全 pass の後（Task 33 Step 4）にする。ここでは手順を `../moorestech_logs/harness/tier9-integration/handoff/place-save.sh` に書いて、`bash -n` で構文を確かめるだけにする。MacBook Pro の `world_1` はユーザーが今遊んでいる本物のワールドなので、消さずに日時つきの名前へ退避する。
```bash
#!/bin/bash
# MacBook Pro のワールドを退避し、旧コスト0研究の完了を立てた Tier8 終盤セーブを world_1 とやり直し用の原本として置く
# Back up the MacBook Pro world, then place the flagged late-Tier8 save as world_1 and as a pristine copy for retries
set -euo pipefail
SRC="$1"   # <scratch>/tier8-world
SAVES='~/Library/Application\ Support/moorestech/saves'
STAMP=$(date +%Y%m%d-%H%M)
ssh -o ConnectTimeout=8 devmac "cd $SAVES && if [ -d world_1 ]; then mv world_1 world_1.bak-$STAMP; fi && rm -rf tier9-start-original"
scp -r "$SRC" "devmac:$SAVES/world_1"
scp -r "$SRC" "devmac:$SAVES/tier9-start-original"
LOCAL=$(shasum -a 256 "$SRC/save.json" | cut -d' ' -f1)
REMOTE=$(ssh devmac "shasum -a 256 $SAVES/world_1/save.json" | cut -d' ' -f1)
[ "$LOCAL" = "$REMOTE" ] || { echo "NG: sha256 mismatch local=$LOCAL remote=$REMOTE" >&2; exit 1; }
echo "OK: placed world_1 (sha256 $LOCAL), backup world_1.bak-$STAMP"
```
Expected: `bash -n place-save.sh` が何も出さない。複製を置くので、事前確認で退避ができた方（`tier8-world-smoke`）ではなく、原本 `tier8-world` を渡す。

- [ ] **Step 6: チェックリストを作って共有する**

`../moorestech_logs/harness/tier9-integration/handoff/checklist.html` を簡易 HTML で書く。中身は次の5つ:
- 起動: MacBook Pro の Steam → moorestech → プロパティ → ベータで `playtest-staging` を選ぶ（パスワードはチェックリストに書かず、ユーザーに別に伝える）。ビルド ID が `build.md` の値であることを確かめる。ワールド `world_1` を開く。元のワールドは `world_1.bak-<日時>` に退避してあること、やり直すときは `world_1` を消して `tier9-start-original` を `world_1` にコピーすることを書く。
- 裏口なしの定義: コマンド・デバッグメニュー・セーブの編集を使わない。
- 確かめる道筋。各項目に「できた／詰まった（どこで・何分）」の記入欄を付ける:
  1. 半導体ライン: クリーンルーム・IPA・切断・成膜・EUV・ダイシング・パッケージング。混ざったダイの仕分けと、止まった理由の表示。
  2. モジュールと GPU。
  3. ロケット技術の研究、発射台の建造（実時間）、初打ち上げ、計算データ。
  4. 計算データで、半導体のアップグレード Lv2〜Lv5 を順に研究する。
  5. 核融合炉と重水素。
  6. 軌道エレベーターの建設の研究。拠点の解放、ケーブルの打ち上げ、接続、昇降。
  7. 反物質と反物質爆弾。
  8. 最終研究。
- 移行の確認: 開いた直後に、コスト0で完了していた Tier9 研究（フィルター分岐器・機械オーバークロック・金属超精密加工・核融合炉・反物質爆弾・軌道エレベーターの建設）が未完了に戻り、それらの解放（核融合炉・拠点・発射台）が外れていること。他の進行は残っていること。
- 報告: 詰まったら、ゲーム内の報告を送る（MacBook Pro の Mac 版からでも同じ受け口に届く）。報告のバンドルは `../moorestech_logs/harness/playtest/reports/<steamid>/` に届く。合計のプレイ時間と、待ちが長いと感じた箇所を最後に書く。

Run: `share-put ../moorestech_logs/harness/tier9-integration/handoff/checklist.html -n tier9-playthrough-checklist`。出た URL を、統合 PR の本文と最終報告に書く。
```bash
git -C ../moorestech_logs add harness/tier9-integration/handoff && git -C ../moorestech_logs commit -m "tier9: 通しプレイの受け渡し（ビルド・セーブの出所・チェックリスト）"
git add .agents/skills/unity-playmode-recorded-playtest/scenarios/tier9/tier9-0-tier8-save-smoke.cs
git commit -m "Task 30: Tier8 終盤セーブの事前確認シナリオ"
```

**完了判定:** 次の4つが揃っている:
- `handoff/build.md` に `playtest-staging` のビルド ID と run ディレクトリがあり、その run の `promotion.md` がある（Windows の phase1/phase2 が合格）。
- `handoff/save-origin.md` にセーブの出所と sha256 がある。事前確認の `result.json` が `Success: true`。
- `handoff/place-save.sh` があり、`bash -n` が通る（実際に置くのは Task 33 Step 4）。
- チェックリストの共有 URL が、統合 PR の本文にある。

---

## Final tasks

### Task 32: 全ブランチレビュー

**Files:**
- Modify: レビュー指摘の反映先（指摘ごとに決まる）

**Interfaces:**
- Consumes: Task 1〜31 の全コミット（本体 `feat/tier9-integrated` とマスタ `feat/tier9-integrated`）
- Produces: 指摘反映済みの両ブランチ

- [ ] **Step 1: 必ずmoores-code-reviewスキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）**
  - 対象は本体 `origin/master...feat/tier9-integrated` とマスタ `origin/master...feat/tier9-integrated`（マスタは別 repo なので、同スキルの分割深掘りに含めて 1 回で回す）
  - 記録は `../moorestech_logs/harness/` へ書く（featureブランチに記録ファイルをコミットしない）
- [ ] **Step 2: 指摘の反映がソースの判定経路・条件式・その評価時点に触れたら、Task 28 の録画（該当段）と Task 31 の評価役 2 体を反映後のバイナリでやり直す**（ユニットテスト通過・ログ無音は代替にならない）。反映がランタイムのコードかマスタに触れたら、Task 30 の Step 4（staging のビルド）もやり直す。ユーザーが遊ぶビルドを最後のコードに揃えるため
- [ ] **Step 3: compile と P1 の健全性ゲート（Task 9 の正規表現）、G1・G2・G3・G6 を再実行する**
  Run: Task 9 Step のコマンド、Task 2・10・11・3 の各 Run 行
  Expected: すべて PASS
- [ ] **Step 4: コミットして push する**

**完了判定:** moores-code-review の統合レポートで未対応の Critical/Important が 0、Step 3 の全ゲートが PASS

### Task 33: PR 作成とセッション終了可能状態

**Files:**
- Modify: `.moorestech-external-revisions.json`（マスタ統合 PR の最終 push 済みコミットを指していなければ更新）

**Interfaces:**
- Consumes: Task 32 の両ブランチ、Task 31 の評価役の合格、Task 30 のビルド・セーブ・チェックリスト
- Produces: 本体統合 PR とマスタ統合 PR の URL

- [ ] **Step 1: セッション終了可能状態にすること: pr-createスキルでPRを作成し、masterとのコンフリクトがあればmasterをマージして解消・コンパイル確認のうえpushする（解消の実作業はpr-create経由でopus subagentに委譲される）。全作業がコミット・push済みで、このセッションをそのまま閉じてもPRがマージ可能な状態になっていることを確認して終える**
  - マスタ統合 PR を先に作り（base: moorestech_master の master）、本体ピンがその PR の push 済みコミットを指すことを `git show HEAD:.moorestech-external-revisions.json` で確かめてから本体統合 PR を作る
  - 本体 PR 本文に: G1・G2（通った経路と許可した詰みの印字）・G3・G6 の出力、G4・G5 の結果、録画 7 段の result.json と動画、評価役 2 体の判定、ユーザー通しプレイのチェックリストとセーブの場所、マスタ統合 PR の URL
  - 版の確認はオーケストレーション §3 の J2・J3 で済ませてある。ここでもう一度確かめ、master が版8へ上がっていたら、セーブ移行を V8→V9 へ繰り下げ、Task 26（G5）と Task 30 Step 3（Tier8 セーブの事前確認）をやり直してから出す（AGENTS.md のセーブ形式の規則）。繰り下げが Task 31 の後に起きたら、Task 32 Step 2 の規則で評価役もやり直す
- [ ] **Step 2: #1551・#1552・#1554（本体）と #87・#88（マスタ）に、統合 PR へのリンクを付けたコメントを書いて閉じる**
  Run: `gh pr close <番号> --comment "統合 PR <URL> に置き換えました"`（マスタは `-R moorestech/moorestech_master`）
- [ ] **Step 4: MacBook Pro にセーブを置き、チェックリストの URL を渡す**  両 PR が作成済みで CI が全 pass になってから行う（ユーザーは全部が完成してから遊ぶ）。staging のビルドが PR の head より古ければ、先に Task 30 Step 4 をやり直す。
  Run: `bash ../moorestech_logs/harness/tier9-integration/handoff/place-save.sh <scratch>/tier8-world`
  Expected: `OK: placed world_1 …`。`ssh devmac` が届かない（MacBook Pro の蓋が閉じている）ときは止まらず、最終報告の「ユーザーの残タスク」に「MacBook Pro を開いて、セーブを置くよう伝える」と書く
- [ ] **Step 3: bd `moorestech-xyuaf.2` に PR の URL と「ユーザー通しプレイ待ち」を note し、`moores-wt rm` で作業 worktree を畳む（セッション B の worktree も含む。未 push があれば畳まない）**

**完了判定:** 両 PR が作成済みで CI が全 pass（Unity 全 shard・Web・Mooresmaster・Win/Mac ビルド・`shipped-master-gate`）、旧 5 PR が閉じられている、`git status` が clean で全コミットが push 済み、Step 4 の `place-save.sh` が `OK` を出し、staging のビルド ID が `handoff/build.md` の値と同じで、そのビルドが PR の head のコミットから焼かれている

全実装タスク完了＝完了ではない。PR未作成のまま終わるのはplan未完了である。
## 死活表（統合で置き換え・削除するものにぶら下がる操作）

「テスターのセーブ」は本体 master のピン 0d129c8 で遊んだ版7のセーブを指す（裁定1）。各判定の根拠はこのセッションの実測。

| 対象 | ぶら下がる操作 | 統合後 | 根拠 |
|---|---|---|---|
| 超純水 `019eeaa7-45b6` を削除 | パイプ・タンク・機械の液体スロットに入れる、運ぶ、ロードする | 生きる | 0d129c8・#88・#87 のどれにも、超純水を作る機械レシピ・ブロック・鉱脈が 0 件。セーブに存在しえない。注: 液体は MissingMasterPruner の対象外で、仮にあれば `FluidContainerSaveJsonObject.FluidId`（`FluidMaster.GetFluidId` が例外を投げる）でロードが落ちる。開発者のセーブだけのリスクで、A16 の範囲 |
| 旧 EUV レシピ `3a13a5a9` を削除 | EUV 機でレシピを選ぶ、加工中ジョブをロードする、レシピビューで見る | 生きる | 0d129c8 では EUV ブロックを解放する研究が無く、設置できない（半導体製造はレシピを解放するだけ）。仮に選んであっても `BlockTemplateUtil.cs:145-152` が未選択に戻す。`machineRecipeUnlockStateInfos` に古い記録が残るが、クライアントはマスタのレシピから引く（`ItemRecipeViewerDataContainer.cs:167-182`）ので害は無い |
| 研究「フィルター分岐器」を削除 | 研究ツリーにノードが出る | 消える（意図どおり。S3 の R10 と裁定2） | 金属超精密加工の前提を張り替える。完了済みの記録はリセット部品と ResearchSectionPruner が消す |
| 同上 | フィルター分岐器ブロックの設置、ブループリントの仕分け設定 | 生きる | ブロック `019e34c4` は残り、解放は「酸素の精製」`019e0721`（0d129c8・#87・#88 で確認）。消すノードは何も解放していない |
| 同上 | mooreseditor の研究グラフ | 生きる | Task 12 の nodeGraph 同期ツールを各スライス末尾で回し、参照先の消えたノード 0 件を検査する（判断記録 A21） |
| 旧「金属3Dプリンタ」アイテム `019e3af5-e750` を削除 | 所持する、レシピ表示 | 生きる | 0d129c8 で作り方が 0 件なので、所持しえない。表示の解放記録はリセット部品の表が消す。グラフは A21 で同期 |
| 孤児アイテム 6 件を削除＋ブロックの `itemGuid` 死にキーを削除 | 所持する、レシピ表示、ブロックからアイテムを引く | 生きる | 作り方 0 件。解放の記録（0d129c8 の半導体製造が付ける）は GameUnlockStateSectionPruner が消し、刈り込みの報告が出る。`itemGuid` は `blocks.yml` のスキーマに無く、読み手がいない。ブロックのアイコンは撮影方式（`BlockIconImagePhotographer`）。グラフは A21 で同期 |
| `UnimplementedBlockUnlockRevoker` を削除 | 3 ブロックの保存済み解放を戻す | 置き換え（研究リセット部品） | 0d129c8 の research.json・blocks.json に 3 ブロックの GUID は 0 件。テスターのセーブに解放の記録は無い。b2149a0 以降の開発セーブはリセット表の和集合で覆う |
| 機械オーバークロックの中身（コスト0・解放なし → 前提「品質モジュール」・コスト・速度モジュールの解放） | 完了済みの扱い、後続研究（金属超精密加工・モジュールスタック拡張I）の前提 | 生きる | テスターの完了はリセットで外れ、研究し直しになる（裁定2）。challenges.json からの参照は 0 件（3 ブランチで確認） |
| 化学プラントのタンク構成（液体入力 1 → 2、液体出力 1・アイテム入口 (3,0,0) を追加） | 既存 4 レシピ（液体 1 → アイテム 1）で動かす、既存の口 (3,1,0) と出力 (3,0,10) を使う、ロードする | 生きる | タンク 0 と口 (3,1,0) は変わらない（Task 13 の Produces）。ロードは min(保存数, 今のタンク数) で読む（`BlockTemplateUtil.cs:121-138`）。挙動の変化（死ではない）: 新しい口 (2,1,0)/(2,1,10) の隣に既に置かれていたパイプは新たにつながる |
| 研究リセット（6 ノードの完了と、解放記録 11 件を消す） | 6 ノードが付けていた解放 | 意図どおり外れる | 対象 GUID を解放する経路は research.json だけ（challenges は 0 件）。記録を消すとマスタの初期値（どれも `initialUnlocked:false`）になり、他の研究・チャレンジのやり直しが付け直す分だけ戻る |
| 旧ステップ（S1 V8→V9・S2 V6→V7・S3 V7→V8）を畳む | 開発者のセーブ（版8・9）のロード | テスターは生きる、開発セーブは対象外 | 裁定1 と A16 で確定済み（裁定事項ではない） |
| nodeGraph と master-refine | mooreseditor のグラフ表示・編集、Tier9 アイテムの sortPriority | 生きる | 同上。新規の研究・アイテムもグラフへ足してから master-refine を回す（A21） |

退化する操作は無い（nodeGraph は A21 で同期する）。

## 判断記録（ADR）

### ユーザー裁定（`.decisions/`）
- `2026-10-25-混在ベルトの代用スロットで上位品を下位へ揃える挙動は仕様とする.md`（W13。シミュレーター予測→ユーザー承認 2026-10-25）
- `2026-10-25-通しプレイの開始セーブはMacBookProのセーブに旧コスト0研究の完了を手で立ててから移行する.md`（Task 30 Step 1）
- `2026-10-25-Tier9統合PRのセーブ移行はV7からV8の1本に部品として畳む.md`
- `2026-10-25-Tier9統合で未実装3ブロックの解放取り消しをやめコスト0で完了済みのTier9研究をリセットする.md`（S1 の `2026-10-25-未実装の仮ブロック3種は研究解放を外し保存済みの解放も戻す.md` を覆す）
- `2026-10-25-宇宙エレベーター拠点は最終研究の必須経路にせず軌道エレベーターの建設で解放する.md`
- `2026-10-25-半導体のアップグレード研究はLv2からLv5の4ノードにする.md`
- `2026-10-25-Tier9統合の最終検証の通しプレイはユーザー本人が行う.md`
- 既存の S1/S2/S3 時点の裁定（各 plan の判断記録と `.decisions/2026-10-2[3-5]-*`）はそのまま有効。特に `2026-10-23-Tier9のマスタの中身はS1からS3の実装後に統合タスクでまとめて入れる.md`、`2026-10-23-ストーリークリアは定義せず反物質爆弾を規定量作る仮の研究で終端にする.md`、`2026-10-23-アイテムハッチに搬送量の上限を足して大部屋集約を有利にする.md`（N は統合のバランスで決める）

### planning 中の判断（agent 判断。出所「plan 作成時の agent 判断」）
- A1 研究リセットの手段は案A: 研究ノードの GUID は変えず、セーブ移行の部品 `ZeroCostTier9ResearchResetter`（Task 7）が研究完了と、それらが解放していたブロック・表示の保存済み解放の記録を消す（旧ノードは機械レシピ・クラフトレシピを解放していない）。案B（GUID 振り直し）は保存済み解放が残るので P1 の仕事が減らず、S1 handoff・シナリオ・S3 plan の GUID 参照も食い違う。リセット表（研究6・ブロック3・表示8）は P2 の起草で確定した値を P1 の部品が持つ
- A2 W17（清浄室機械の processor が JSON の null）は、移行で Failed にせず「飛ばして台数をログに残す」。ロード側 `BlockComponentStateReader.TryRead` が明示 null を警告して復元を見送る扱いで、移行が止めるとローダーなら読めるセーブを止めてしまうため。同じ step の継承レベル部品と扱いを揃える
- A3 初回打ち上げ履歴は既存の真偽値を素通し、ケーブルプールの明示 null は欠損と同じく 0 で補う（冪等性。save-migration スキルの要求）
- A4 出荷中ピン 0d129c8 のコスト0「ロケット」ノード `019e3af0-7b1c-77de-9197-87d276c84392` はリセット表に入れず、統合マスタに無いので `ResearchSectionPruner` が落とす。P2 はこの GUID を再利用しない
- A5 マスタ統合ブランチ `feat/tier9-integrated`（moorestech_master）は W1 の最初にセッション B が作る（#88 → #87 の順で通常マージ）。W1 の間は B だけが書き、J1 の後は A だけが書く（同じブランチを 2 セッションで書かない）
- A6 G2 は `tools/progression/`、G6 は `tools/tier9_balance/` に新設する。`tools/research_sync/progression_check.py` は `sync_machine_recipe_unlocks.py` が import する `ProgressionModel` だけを残し、G2 の入口は `tools/progression/check.py` の 1 つにする（設計検査 R5。単体実行は案内を出して exit 2）。S1 の sim は `design/tier9-balance/sim/` に置き（Task 3 Step 7）、G6 の区間 `s1_semiconductor` から呼ぶ。値の正本の向きは S1 handoff の params→master→照合にそろえる。照合の置き場と S3 の検査は A22
- A7 G1・G3 はテストではなく Editor の `-executeMethod` で作る。テスト配下から本番マスタを読む禁止（`check-test-external-dependencies.py`）を守るため。置き場は A23（client 側に置く当初案は A23 で置き換え）
- A8 G6 の S1 区間の所要時間はクラス B の値で合計する。全体 15〜25 時間（S3 plan R23）の判定は Task 3 のとおり残す。区間の範囲の和と一致するため区間が全部通れば必ず通る冗長な判定だが、区間の所要時間が欠けたときに全体も FAIL として出る（区間の欠落を二重に見せる）ので外さない
- A9 研究の機械レシピ解放の正本は `sync_machine_recipe_unlocks.py` とする。spec に書いた `unlockMachineRecipe` は下書きで、各スライスの末尾で sync → `--check` → G2 を回し、sync が出す `MOVE` が 0 になるまで spec を直す（S1 handoff の D7 の手書き割り当てをそのまま正にしない。S1 handoff 配置検査 V1）。旧 EUV レシピ削除と同時に `KNOWN_ORPHANS` から外す
- A10 録画シナリオは `scenarios/tier9/` を新設して置く（`scenarios/misc/` は直下が 10 ファイル超。S1 handoff 配置検査 V2、カテゴリ別サブディレクトリの前例は `scenarios/train/`・`scenarios/building/`）
- A11 ブロックと同名で作り方の無い孤児アイテム 6 件（EUV とクリーンルーム系）は削除し、G2 の許可リストに足さない
- A12 GPU の名前は `GPULv1`〜`GPULv5`（ICチップLv2・GPU衛星Lv1 と同じく空白なし）。録画シナリオも同じ名前で引く
- A13 G2 の許可リストに Tier9 の項目を 1 行も足さない。Tier9 範囲外の既存の詰み（暗黙の前提 10 件、タングステン系・ガラス・真空管の孤児）は理由つきで許可し、出力に印字する
- A14 ハッチの搬送上限 N は G6 の S1 sim で決めて blocks.json に入れる（暫定の毎秒 4 個のまま出さない。S1 handoff 型閉包 7-1）
- A15 S1 handoff の持ち越し（L2830–2836・L2841）は P2 の S1 スライスと P5 段1 で拾う（S1 handoff 型閉包 7-3）
- A16 開発ブランチの手元セーブ（S1 版9・S3 版8・S2/S3 版7）は救わない。テスターのセーブは master の版7だけ（裁定1の範囲）
- A17 ランタイム挙動に触れるので unityプレイ録画テストを含める（P5 の 7 段＋A11 撮り直し）
- A18 移行部品の null の扱いを揃える: 通常機械・清浄室機械とも processor の明示 null は飛ばして台数を step のログに残し（A2）、清浄室機械の液体キーの明示 null は欠損と同じく空配列で補う（A3）。設計検査 Phase 2.6 の備考から
- A19 G3 は研究コストを最大スタックで分けて主インベントリへ入れる（`ItemStack` は最大スタック超で例外を投げるため、1 スタックで作ると検査が例外で落ちて「入り切らない」を判定できない）
- A20 G5 は、Task 7 が解放の記録を消すことに合わせ、ブロック 3 件と表示 8 件の記録が残っていないことを assert する
- A21 mooreseditor の `server_v8/mods/moorestechAlphaMod_8/.mooreseditor/nodeGraph.v1.json` を統合マスタに合わせる。消した GUID のノードを除き、新しい研究・アイテムのノードを足し、「参照先の消えたノード 0 件・research.json と研究ノードが 1 対 1」を検査する（前例 `tools/plan6_nodegraph_migration/migrate_nodegraph.py`。nodeGraph の取り残しで master-refine の並び順が壊れた事故がある）。設計検査 Phase 2.5 の V2
- A22 G6 の照合は `tools/tier9_balance/` の 1 か所に集め、入口は `run.py` だけにする。`design/tier9-balance/` は sim・spec・apply・guids の置き場に限る（S1 handoff の `verify_master.py`・`verify_checks.py` は作らず、8 項目は `sections/s1/` 配下と入口 `sections/s1_semiconductor.py` の `checks.matches` として書く）。S2・S3 の検査は `sections/s2/`・`sections/s3/` 配下と入口 `sections/s2_compute_loop.py`・`sections/s3_endgame.py`。設計検査 V1。A6 のうち「S3 の検査も同じ apply・verify に乗せる」はこれで置き換える
- A23 G1・G3 は `moorestech_server/Assets/Scripts/Editor/` の Editor asmdef に置く（CI ゲートを `-executeMethod` で走らせる前例 `PlatformCompileCheck.cs` と同じ置き場。マスタの場所は常に `-masterDataRoot` で明示して渡す）。A7 の client 側の置き場はこれで置き換える。設計検査 N1
- A27 nodeGraph の `--check` は CI に載せず、各スライス末尾と Task 23 の手元ゲートで回す（強制は規則が破られてから足す。再検査 B5）
- A24 移行部品は「全部品を先に検査し、全部が通ってから書き換える」2 段の interface（`IV7ToV8MigrationPart.TryPrepare` / `ApplyAndSummarize`）にする。master の前例（V3ToV4 の static Filler と結果型）と S1 の V8ToV9（先に検査）で前例が割れているため、部品が 5 つある本 step では、1 部品の拒否で途中まで書き換えた JObject が残らない形を選ぶ。新しい定型として扱う。設計検査 N2
- A25 G6 の S2 区間（Task 19 Step 5）の所要時間は、計算データを消費する研究ごとの便数の合計 × 1 便の秒（`buildSeconds`＋`launchSeconds`）÷ 発射台 1 台とする（S2 引き継ぎ F「1便/135秒を1台で回す」）。各研究の便数は、その研究の前提（祖先）で解放済みの GPU衛星のうち、1 便の計算データが最も多いもので払った数にする。プレイヤーの研究順に依らず、必ず使える衛星だけで数えるため。研究コストの照合は、消費先の顔ぶれ（`COMPUTE_CONSUMERS`）とアップグレード LvN の製作上限 = N の 2 つ。工場の台数は、打ち上げ資材と GPU衛星Lv1 の直のレシピと、その 1 段上流を、1 レシピ 4 台以下で判定する。4 台は S2 引き継ぎ F の作者メモ「変な工場建設は要求しない」を数にした agent 前提で、Task 23 の調整では動かさない。研究コストの値そのものは照合せず、所要時間の判定に任せる。Task 23 で動かす値であり、二重に持つと調整のたびに 2 か所を直すことになるため 打ち上げ資材のうちコンピュータ（Tier8 までの既存の製品）と、GPU衛星Lv1 の入力 GPULv1（S1 区間の工程）は、このレシピ台数の判定の対象外とする（需要は `demand` に積むが、台数はそれぞれの既存の工場・S1 区間で見る）。再検査の備考から
- A26 G6 の S3 区間（Task 22 Step 6）の所要時間は、次の 3 つの和とする。S3 の下書きに式が無かったので、統合で足した。
  - 研究 4 つ（金属超精密加工・核融合炉・軌道エレベーターの建設・反物質爆弾）を順に払う時間。1 つの研究は、品目ごとに並行して作り、最も遅い品目で決まる。
  - unlockBlock を持つ研究 1 つにつき 0.5 h の建設時間。
  - 反物質製造装置 2 台で最終研究の爆弾をそろえる時間。
  毎秒の量は次のとおり。ICチップLv1 は S1 sim のクラス B（S1 研究完了後の 3 ライン。A8 と同じクラス）から引く。鋼鉄板・コンピュータ・制御モータは Tier8 終盤の工場から S3 に回せる台数（電気炉 4・電気汎用工作装置 2・同 2。agent 前提）× マスタのレシピで出す。計算データは S2 区間の便数で数え、二重に数えない。台数・建設時間・爆弾の機械 2 台は前提であり、Task 23 の調整では動かさない
- A28 Codex の週次枠が足りないときは、セッション A・B・評価役B・Task 32 のレビューを Claude Code（opus）で起動し直す。基準はオーケストレーション §2「Codex 枠の確認」。無人で止まらないように、ユーザーに聞かずに既定で切り替える（出所: シミュレーター予測の Warning → agent 判断）
- A29 ユーザーは MacBook Pro の Steam（Mac 版）で、全部が完成してから遊ぶ（ユーザー発言 2026-10-25）。セーブは Task 33 Step 4 で、PR と CI の後に置く。MacBook Pro の `world_1` は本物のワールドなので、日時つきの名前へ退避し、やり直し用の原本 `tier9-start-original` も並べる。検証機（winbuild）の smoke が既定ワールドを消す件（`StandalonePlaytestSmokeBootstrap.cs:86-90`）は、ユーザーが winbuild で遊ばないので対象外（出所: ユーザー発言と実コードの確認 → agent 判断）
- A30 段4 の録画が待つ実時間を 300 秒以下に保つことを、Task 23 のバランス調整の制約にする。`RESULT_TIMEOUT` は伸ばさない（出所: シミュレーター予測の Warning → agent 判断）
- A31 G6 の「マスタから引けない」は判別子 1 本で表す。`Check` は状態値（`ok`／`lookup_failed`／`not_positive` など）を 1 つ持ち、None を合図に使わない。値を返す関数（`output_per_second`・`evaluate_launches`・`launches_for`・`PowerResult`・`item_rates`・`research_hours_for`）も、値と状態の組を返す。セーブ移行の `TryGetNestedArray` は「無い／null／配列」の 3 状態を返す（型閉包 5-A。出所: シミュレーター予測・確信高→前提宣言、ユーザーの拒否権の行使なし 2026-10-25）
- A32 型閉包の弱い指摘は次の 3 束を直す: (1) nodegraph_sync と tier9_balance の中で 2 回以上出る述語・導出を 1 本にまとめる（検査6 の (a)〜(g)）、(2) G1 の `MasterHolder.Load` を呼ぶ口を 1 つにし、static のマスタを書き換える所有者を決める（5-F）、(3) GPU衛星かどうか・レシピの入力を、名前の前方一致と既定値 `''` でなく GUID か levelFamily で判定する（5-B）。他の弱（5-C の合成 bool・失敗理由の bool＋string・`unlockMachineRecipe` の書き手 2 者・DI コンテナの終了の所有者・網羅 switch）は Task 32 の全ブランチレビューに任せる（出所: 同上）

### 実装中に人へ上げる条件（SDD の deferred-questions に溜め、まとめて 1 回提示）
- G3 で研究コストが主インベントリに入り切らない（コスト・スタック上限・払い方のどれを変えるかは裁定）
- G6 の目標時間に、パラメータ調整で収まらない区間がある
- 段4 の録画の実時間（建造＋打ち上げ＋Lv1 の飛行で 300 秒以下）と G6 の目標が両立しない
- 評価役 2 体のどちらかが不合格を出し、直し方が仕様の選択になる

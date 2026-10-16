# ユースケース: シナリオを実行して結果を見る

## 手順（この順で・省略不可）

```bash
cd <repo-root>   # 必ずpwdで確認（git worktree頻用のため）

# 1. Unity Editorが未起動なら起動（初回のみ・1〜2分かかる）
uloop launch ./moorestech_client

# 2. PlayMode中なら止める（前回のワールド状態を持ち越さないため・必須。
#    失敗後の再実行でも毎回必ずstopする — 使い回すと初期化破損のNREスパムでnot readyタイムアウトになる）
uloop control-play-mode --project-path ./moorestech_client --action stop

# 3. 一発実行（preflight→PlayMode起動→ready待ち→シナリオ投入→result.json回収まで全自動）
#    ランナーとシナリオは本スキル同梱（repoルート相対で指定する）
SKILL=.claude/skills/unity-playmode-recorded-playtest
SCRATCH="$PWD/moorestech_client/PlaytestResults/scratch"
mkdir -p "$SCRATCH"
cp "$SKILL/scenarios/<カテゴリ>/<シナリオ名>.cs" "$SCRATCH/<シナリオ名>.cs"
```

調査ではコピー内の`PlaytestRunOptions.Record`を`false`へ変更してから実行する。提出用の最終実行は`Record=true`のシナリオを使い、実行後チェックリストの動画・スクショ・目視条件も満たす。

```bash
"$SKILL/scripts/run-scenario.sh" ./moorestech_client "$SCRATCH/<シナリオ名>.cs"
```

- 第3引数でmasterデータを差し替え可能。省略時は作業中プロジェクトの`.moorestech-external-revisions.json`の互換コミットにHEADが一致するmoorestech_master worktreeを自動解決する（スキル固定パスは無い）。該当worktree未作成なら自動解決が失敗しエラーになるので、`git -C ../moorestech_master worktree add <path> <互換コミット>`で作るか第3引数で明示する
- 所要: preflight ~30秒 + ready ~15〜30秒 + シナリオ本体。**バックグラウンド実行してresult.json出現を待つ**（固定sleepの多段待ちをしない）

## 固定world・map mode・seedで起動する

generated mapや同一worldの再起動を検証するときは、次の3環境変数を全て指定する。
調査には上で準備した`Record=false`のscratchコピーを使う。

```bash
PLAYTEST_WORLD_DIRECTORY="$PWD/moorestech_client/PlaytestResults/worlds/seed-12345-world" \
PLAYTEST_MAP_MODE=generated \
PLAYTEST_SEED=12345 \
"$SKILL/scripts/run-scenario.sh" ./moorestech_client "$SCRATCH/<シナリオ名>.cs"
```

- 3変数は「全て未設定」または「全て非空」のどちらかだけを許可する。部分指定・空値・整数でないseedは、preflight前に不足変数名付きで失敗する
- 新規生成では `PLAYTEST_WORLD_DIRECTORY` に未作成の子パスを渡す。`mktemp -d` の戻り値そのものは既存の空ディレクトリなので使わない
- cache miss/hit比較では、初回と2回目に同じ `PLAYTEST_WORLD_DIRECTORY` とseedを渡す。2回目は既存 `world.json` とterrain visual cacheを再利用する
- 別seed比較では別の未作成worldパスを使う。既存worldは保存済みseedを権威とするため、同じworldパスへ別seedを渡しても再生成されない
- 固定world入口は `AutoSave=false` で起動し、隔離debug cacheの `DebugEnvironmentTypeKey=PureNature(1)` をPlayMode前に設定する

## ランナー内部の流れ（すべて自動・リトライ内蔵）

1. **preflight** (スキル同梱 `scripts/preflight.sh`): CLI Loop疎通（タイムアウト=モーダル/ビジー検出兼務）→ コンパイル → master実在 → **マスタロードのドライラン**（EditモードでMasterHolder.Loadを試しスキーマ不整合をPlayMode前に検出）
2. **boot**: 通常は `PlaytestBoot.PrepareAndEnterPlayMode(masterDir, noSave:true)`、固定worldの3変数指定時は `PrepareWorldAndEnterPlayMode(masterDir, worldDir, mapMode, seed)` をEDC 1回で実行してPlayMode突入
3. **ready待ち**: ゲーム初期化完了イベントで書かれる `ready.marker` をファイルポーリング（EDCを連打しない）
4. **シナリオ投入**: キー/マウスを使う場合は先に[input-injection.md](input-injection.md)のde-risk probeを読み、ready後かつ`PlaytestRunner.Run`呼び出し前のスニペットトップレベルで完了する。Web UIをprobeする場合はreadyとCEF利用可能状態を確認し、`PlaytestDomQuery.RegisterHandler()` → `CefInputForwarder.StartForwarding()` → probeの順に実行する。probe失敗でRunへ進まない場合は`CefInputForwarder.StopForwarding()`と`PlaytestDomQuery.ResetPending()`で片付け、失敗理由を記録する。必要なEditor／GameViewフォーカス回復、有効状態の再確認、単体入力成立確認をこの段階で行い、録画開始後のコールバック内には置かない。シナリオ全文をEDC 1回で `PlaytestRunner.Run` に渡す（DSLは事前コンパイル済み）
5. **回収**: `result.json` の出現を待って表示。`Success` で exit 0/1

## 結果の読み方

成果物: `moorestech_client/PlaytestResults/<セッション>/<ラン名>/`（git管理外）

- `result.json` — `Success` / `Error`(スタックトレース) / `Asserts`配列(Label・Passed・Message) / `Timeline`(操作・ナレーション・待機の時系列ログ) / `Screenshots` / `RecordingPath`
- `recording.mp4` — 提出用の最終実行（`PlaytestRunOptions { Record = true }`）で生成。調査用（`Record = false`）では生成しない
- `*.png` — シナリオ内 `p.Screenshot(name)` の出力

**Assertは失敗しても実行が続く。Untilのタイムアウトだけは例外で中断し`Error`に入る。**
Success=false のときは Error の先頭行と「最後にPASSしたAssert」で進行地点を特定する。

## 実行後チェックリスト

1. 調査用（`Record = false`）は`Success: true`か / Assertsが全てPassedかを確認する
2. 提出用の最終実行（`Record = true`）は1に加え、0 byteでない動画・期待UIのスクショ・実プレイ視点（アバター・地面・HUD）を**必ず目視**する。調査用の成功を提出用録画に代えない
3. `git status` → `.moorestech-external-revisions.json` が書き換わっていたら `git checkout --` で戻す（Unityの自動書き換え。スキーマ未更新ならコミット禁止）

## よくある失敗と即応

| 症状 | 原因 | 即応 |
|---|---|---|
| `NG: game not ready within 300s` | masterスキーマ不整合が典型 | preflight [4/4]の出力確認 → troubleshooting.md |
| `Unity is reloading (Domain Reload...)` | コンパイル/リロード直後 | 45秒待って再実行 |
| `Unity CLI Loop is not installed` | run-testsの退避 or **bootのPlayMode突入ドメインリロード**（run-tests非並走でも発生する） | `cp .../UserSettings/UnityMcpSettings.json.bak .../UnityMcpSettings.json` → PlayMode停止→フレッシュ再実行 |
| シナリオ投入で `error CS****` | シナリオのコンパイルエラー | 型名/usingを実ファイルで確認して修正 |
| Untilタイムアウト | 期待した状態変化が起きていない | troubleshooting.md のライブ診断へ |

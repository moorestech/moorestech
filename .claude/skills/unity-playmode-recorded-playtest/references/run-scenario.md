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
"$SKILL/scripts/run-scenario.sh" ./moorestech_client "$SKILL/scenarios/<シナリオ名>.cs"
```

- 第3引数でmasterデータを差し替え可能。省略時は作業中プロジェクトの`.moorestech-external-revisions.json`の互換コミットにHEADが一致するmoorestech_master worktreeを自動解決する（スキル固定パスは無い）。該当worktree未作成なら自動解決が失敗しエラーになるので、`git -C ../moorestech_master worktree add <path> <互換コミット>`で作るか第3引数で明示する
- 所要: preflight ~30秒 + ready ~15〜30秒 + シナリオ本体。**バックグラウンド実行してresult.json出現を待つ**（固定sleepの多段待ちをしない）

## ランナー内部の流れ（すべて自動・リトライ内蔵）

1. **preflight** (スキル同梱 `scripts/preflight.sh`): CLI Loop疎通（タイムアウト=モーダル/ビジー検出兼務）→ コンパイル → master実在 → **マスタロードのドライラン**（EditモードでMasterHolder.Loadを試しスキーマ不整合をPlayMode前に検出）→ **サーバーポート11564の空き確認**
2. **boot**: `PlaytestBoot.PrepareAndEnterPlayMode(masterDir, noSave:true)` をEDC 1回で実行。NoSaveフラグ・`DebugServerDirectory`・`DebugObjectsBootstrap_Disabled` を設定してPlayMode突入
3. **ready待ち**: ゲーム初期化完了イベントで書かれる `ready.marker` をファイルポーリング（EDCを連打しない）
4. **シナリオ投入**: シナリオ全文をEDC 1回で `PlaytestRunner.Run` に渡す（DSLは事前コンパイル済みなのでAPI推測ミスが構造的に起きない）
5. **回収**: `result.json` の出現を待って表示。`Success` で exit 0/1

## 結果の読み方

成果物: `moorestech_client/PlaytestResults/<セッション>/<ラン名>/`（git管理外）

- `result.json` — `Success` / `Error`(スタックトレース) / `Asserts`配列(Label・Passed・Message) / `Timeline`(操作・ナレーション・待機の時系列ログ) / `Screenshots` / `RecordingPath`
- `recording.mp4` — 全編録画（`PlaytestRunOptions { Record = true }` のとき）
- `*.png` — シナリオ内 `p.Screenshot(name)` の出力

**Assertは失敗しても実行が続く。Untilのタイムアウトだけは例外で中断し`Error`に入る。**
Success=false のときは Error の先頭行と「最後にPASSしたAssert」で進行地点を特定する。

## 実行後チェックリスト

1. `Success: true` か / Assertsが全てPassedか
2. スクショ・録画を**必ず目視**（設置位置ズレ・見た目の破綻はassertで捕まらない）
3. `git status` → `.moorestech-external-revisions.json` が書き換わっていたら `git checkout --` で戻す（Unityの自動書き換え。スキーマ未更新ならコミット禁止）

## よくある失敗と即応

| 症状 | 原因 | 即応 |
|---|---|---|
| `NG: game not ready within 300s` | ポート11564占有 or masterスキーマ不整合 | preflight [5/5]/[4/5]の出力確認 → troubleshooting.md |
| `Unity is reloading (Domain Reload...)` | コンパイル/リロード直後 | 45秒待って再実行 |
| `Unity CLI Loop is not installed` | run-testsの退避 or **bootのPlayMode突入ドメインリロード**（run-tests非並走でも発生する） | `cp .../UserSettings/UnityMcpSettings.json.bak .../UnityMcpSettings.json` → PlayMode停止→フレッシュ再実行 |
| シナリオ投入で `error CS****` | シナリオのコンパイルエラー | 型名/usingを実ファイルで確認して修正 |
| Untilタイムアウト | 期待した状態変化が起きていない | troubleshooting.md のライブ診断へ |

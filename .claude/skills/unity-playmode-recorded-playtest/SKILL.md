---
name: unity-playmode-recorded-playtest
description: 'Unity Editor を PlayMode 起動し、録画付きで end-to-end gameplay を検証する枠組み。第一選択はプレイテストDSL（Client.Playtest asmdef + 本スキル同梱の scripts/run-scenario.sh）による1コマンド一発実行で、preflight→PlayMode起動→シナリオ投入→result.json回収まで自動化される（実測ready~26秒）。UI経路設置（ビルドメニュー→クリック/ドラッグ）とホットバー手持ち駆動システム（歯車チェーンポール等）もDSLで操作可能。ユースケース別の詳細は references/ を参照（本文にルーティング表）。DSLが無いブランチのみレガシー手動フローへフォールバック。Use When: 「Unity をコードで動かして録画したい」「フォーカス無しで PlayMode テスト」「Recorder を CLI 制御」「実プレイで動くか確認」「キーマウ操作でE2E検証」「MonoBehaviour Update を回した状態で API 叩いて検証」「ロジック単体テストでは捕まらないシナリオを通しで確認」「プレイテストDSLでシナリオ実行」と言われた場合。フォーカス不要が必須要件のとき積極的に起動する。入力は必ず InputSystem QueueStateEvent で注入し OS simulate-keyboard/simulate-mouse-input は使わない（前面化して注入を汚染する・最重要）。masterデータはブランチ互換コミットへピン留めした worktree を使う（スキーマ不整合は MooresmasterLoaderException で初期化が無言死する）。サーバーポート11564は固定のため他worktreeのPlayModeと同時実行不可。'
---

# unity-playmode-recorded-playtest

Unity PlayModeをCLIから自動操作し、シナリオを録画付きでend-to-end検証する。フォーカス不要・本物のMonoBehaviour Updateを回した状態で動作する。EditModeテストでは走らない本番アセット・UI Prefab・操作系のバグを機械的に捕まえるのが目的（実績: PlaceBlock中のTab遷移死・歯車ポール設置不能の実バグ2件をUI経路E2Eが検出）。

## 実行方式の選択（最初に判定する）

```bash
ls <repo-root>/moorestech_client/Assets/Scripts/Client.Playtest/ 2>/dev/null
```

- **ある → プレイテストDSL（第一選択）**。下のユースケース表から該当リファレンスを読む
- **無い →** [references/legacy-manual-flow.md](references/legacy-manual-flow.md) にフォールバック。ただし長期作業ならDSLのあるブランチ（`feature/playtest-stabilization`、ad7535766以降）の取り込みを先に検討

## ユースケース → リファレンス（該当ファイルだけ読めば実行できる）

| やりたいこと | 読むファイル |
|---|---|
| 既存シナリオを実行して結果を見る | [references/run-scenario.md](references/run-scenario.md) |
| 新しいシナリオを書く（**Driver API全リファレンス**含む） | [references/write-scenario.md](references/write-scenario.md) |
| UI操作（ビルドメニュー→クリック/ドラッグ）でブロックを設置する | [references/place-blocks-via-ui.md](references/place-blocks-via-ui.md) |
| ホットバー手持ち駆動システム（歯車チェーンポール・結線等）を操作する | [references/hotbar-driven-systems.md](references/hotbar-driven-systems.md) |
| キー・マウス・uGUIを注入する / 入力が効かない | [references/input-injection.md](references/input-injection.md) |
| 実行が進まない・設置されない・原因不明（診断手順） | [references/troubleshooting.md](references/troubleshooting.md) |
| DSLが無いブランチで手動フロー（Recorder手動制御含む） | [references/legacy-manual-flow.md](references/legacy-manual-flow.md) |

## 最初の1コマンド

ランナー・プリフライト・実証済みシナリオはすべて本スキルに同梱されている（`scripts/` と `scenarios/`）。リポジトリ側に配置は不要で、repoルートからスキルディレクトリ相対で呼ぶ。

```bash
cd <repo-root>   # 必ずpwd確認（worktree頻用）
SKILL=.claude/skills/unity-playmode-recorded-playtest
uloop control-play-mode --project-path ./moorestech_client --action stop   # 前回状態の持ち越し防止
"$SKILL/scripts/run-scenario.sh" ./moorestech_client "$SKILL/scenarios/belt-line-via-ui.cs"
```

これが通れば環境は健全。通らなければ troubleshooting.md。

## 絶対規則（全ユースケース共通・違反すると必ず死ぬ）

1. **masterデータはピン留めworktree**（`/Users/katsumi/moorestech-worktrees/playtest-master/server_v8`）。
   共有moorestech_masterのHEADは別ブランチ用スキーマに進んでいることがあり、使うと`MooresmasterLoaderException`で初期化が**無言死**する。互換コミットは`.moorestech-external-revisions.json`の`commitHash`で特定。worktree作成:
   `git -C /Users/katsumi/moorestech_master worktree add <path> <互換コミット>`
2. **OSレベル入力シミュレート禁止**（simulate-keyboard/simulate-mouse-input）。Editorを前面化させ実OSマウスが毎フレーム注入を上書きし、PlayMode再起動まで回復しない。注入は`SemanticInput`（QueueStateEvent）一択。**スニペットから`InputSystem.Update()`も呼ばない**
3. **固定sleepで待たない**。`p.Until(条件, timeout, ラベル)`かファイル出現ポーリング（result.json / ready.marker）で待つ
4. **サーバーポート11564は固定・全worktree共通**。プレイテストは同時に1つ。他worktreeのPlayModeが占有していると起動不能（PlayMode停止後のソケットリークも起きる→troubleshooting.md）
5. **シナリオ実行前にPlayModeを止める**。走ったままだと前回のワールド状態を引き継ぐ
6. **legacy `UnityEngine.Input`直読みは注入で駆動不可**。主要経路は`Client.Input.HybridInput`へ移行済み。新たに駆動しない入力を見つけたらHybridInput化する（input-injection.md）
7. `.moorestech-external-revisions.json` / `_CompileRequester.cs` はUnityが自動書き換えする。スキーマ未更新なら`git checkout --`で戻しコミットしない
8. **run-testsとプレイテストを並走させない**（run-testsはUnityMcpSettings.jsonを退避し、衝突すると全uloopコマンドが死ぬ）
9. 検証完了の定義は4つ全部: 動画生成（0 byte不可）/ result.jsonのAsserts全PASS / スクショに期待UIが映る / **絵が実プレイ視点**（アバター・地面・HUD）
10. 作業終了前に必ず全てコミットする

## Step 0: 複雑シナリオはサブエージェント探索を先に（必須ゲート）

単純なシナリオ（設置→give→assert程度）ならwrite-scenario.mdのAPI表だけで書ける。**複数ブロック連結・UI操作・列車などは、Plan/general-purposeサブエージェントに以下を調査・出力させてから書く**:

1. 前提手順の連鎖（依存ブロック・必要アイテム・操作順序。例:「列車に乗る」は先にレール→列車設置）
2. 各操作の呼び出し経路（DSL Driver / API直 / 注入 / リフレクション。**対象の入力読み取りをgrepしlegacy Input直読みなら注入不可と明示**）
3. 絶対座標とconnector offsetの表（`inputConnects[].offset`を「OriginalPos + offset = 絶対座標」で計算。**受け側の`inputConnects`が空でないか必ず確認**＝空なら何を置いても繋がらない）
4. スニペットで使うAPI/フィールド名の実在確認（実ファイルをReadして確認。存在しない名前はpollingを無限ループ化させる）
5. Step単位の実行計画（各Stepのコマンドと期待結果）

## Available scripts / scenarios（すべて本スキル同梱）

- `scripts/run-scenario.sh <unity-project-path> <scenario.cs> [master-server-dir]` — preflight→boot→シナリオ投入→result.json回収の一発実行
- `scripts/preflight.sh <unity-project-path> [master-server-dir]` — 疎通/コンパイル/master実在/マスタロードドライラン/ポート空き（run-scenario.shが自動で呼ぶ。単体診断にも使える）
- `scenarios/*.cs` — 実証済みシナリオ集。`belt-line.cs`(direct構築) / `belt-line-via-ui.cs`(UI経路) / `gear-chain-pole-via-ui.cs`(ホットバー駆動) / `gear-chain-connect-via-ui.cs`(クリック結線) / `train-rail-connect-via-ui.cs`(レール結線) / `sample-chest.cs`(最小例)。新規シナリオもここに追加する
- `scripts/start-recording.sh` / `scripts/stop-recording.sh` — Recorder手動制御の参考実装（レガシー方式B用）

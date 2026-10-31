---
name: unity-playmode-recorded-playtest
description: 'Unity Editor を PlayMode 起動し、録画付きで end-to-end gameplay を検証する枠組み。第一選択はプレイテストDSL（Client.Playtest asmdef + 本スキル同梱の scripts/run-scenario.sh）による1コマンド一発実行で、preflight→PlayMode起動→シナリオ投入→result.json回収まで自動化される（実測ready~26秒）。UI経路設置（ビルドメニュー→クリック/ドラッグ）とホットバー割当（建築ショートカット。歯車チェーンポール等）もDSLで操作可能。ユースケース別の詳細は references/ を参照（本文にルーティング表）。DSLが無いブランチのみレガシー手動フローへフォールバック。Use When: 「Unity をコードで動かして録画したい」「フォーカス無しで PlayMode テスト」「Recorder を CLI 制御」「実プレイで動くか確認」「キーマウ操作でE2E検証」「MonoBehaviour Update を回した状態で API 叩いて検証」「ロジック単体テストでは捕まらないシナリオを通しで確認」「プレイテストDSLでシナリオ実行」と言われた場合。背景動作が必要なときは入力デバイスの有効状態を実測する。入力は必ず InputSystem QueueStateEvent で注入し OS simulate-keyboard/simulate-mouse-input は使わない（OS入力とInputSystem注入を混在させない）。masterデータはブランチ互換コミットへピン留めした worktree を使う（スキーマ不整合は MooresmasterLoaderException で初期化が無言死する）。録画には操作オーバーレイ（アクションログ・キー・カーソル）が自動で焼き込まれ、アクション間0.5秒インターバルが自動挿入される。'
---

# unity-playmode-recorded-playtest

Unity PlayModeをCLIから自動操作し、本物のMonoBehaviour Updateを回した状態で検証する。入力にはフォーカスとデバイスの有効状態を事前確認する。EditModeテストでは走らない本番アセット・UI Prefab・操作系のバグを機械的に捕まえるのが目的（実績: PlaceBlock中のTab遷移死・歯車ポール設置不能の実バグ2件をUI経路E2Eが検出）。

## 実行方式の選択（最初に判定する）

```bash
ls <repo-root>/moorestech_client/Assets/Scripts/Client.Playtest/ 2>/dev/null
```

- **ある → プレイテストDSL（第一選択）**。下のユースケース表から該当リファレンスを読む
- **無い →** DSLはmasterに存在する（ad7535766以降）。masterを取り込んでから進める

## ユースケース → リファレンス（該当ファイルだけ読めば実行できる）

| やりたいこと | 読むファイル |
|---|---|
| 既存シナリオを実行して結果を見る | [references/run-scenario.md](references/run-scenario.md) |
| 新しいシナリオを書く（**Driver API全リファレンス**含む） | [references/write-scenario.md](references/write-scenario.md) |
| UI操作（ビルドメニュー→クリック/ドラッグ）でブロックを設置する | [references/place-blocks-via-ui.md](references/place-blocks-via-ui.md) |
| ホットバー割当（建築ショートカット。歯車チェーンポール・結線等）を操作する | [references/hotbar-driven-systems.md](references/hotbar-driven-systems.md) |
| キー・マウス・画面UIを注入する / 入力が効かない | [references/input-injection.md](references/input-injection.md) |
| 実行が進まない・設置されない・原因不明（診断手順） | [references/troubleshooting.md](references/troubleshooting.md) |
| ランタイム状態を動的コードで観測する（サーバー側API早見表・バグ原因調査） | [references/runtime-state-probe.md](references/runtime-state-probe.md) |

## 最初の1コマンド

ランナー・プリフライト・実証済みシナリオはすべて本スキルに同梱されている（`scripts/` と `scenarios/`）。リポジトリ側に配置は不要で、repoルートからスキルディレクトリ相対で呼ぶ。

```bash
cd <repo-root>   # 必ずpwd確認（worktree頻用）
SKILL=.claude/skills/unity-playmode-recorded-playtest
SCRATCH="$PWD/moorestech_client/PlaytestResults/scratch"
mkdir -p "$SCRATCH"
cp "$SKILL/scenarios/building/belt-segment-flow.cs" "$SCRATCH/belt-segment-flow.cs"
```

調査・環境確認ではコピー内の`PlaytestRunOptions.Record`を`false`へ変更してから、次を実行する。

```bash
uloop control-play-mode --project-path ./moorestech_client --action stop   # 前回状態の持ち越し防止
"$SKILL/scripts/run-scenario.sh" ./moorestech_client "$SCRATCH/belt-segment-flow.cs"
```

これが通れば環境は健全。通らなければ troubleshooting.md。提出用の最終実行は`Record=true`のシナリオを使い、動画・スクショ・実プレイ視点の目視も完了する。

## 絶対規則（全ユースケース共通・違反すると必ず死ぬ）

1. **masterデータは作業中ブランチの互換コミットにピン留めしたworktreeを使う**（スキル固定のパスは持たない）。
   共有moorestech_masterのHEADや他ブランチ用の既存worktreeは別スキーマに進んでいることがあり、使うと`MooresmasterLoaderException`で初期化が**無言死**する。互換コミットは作業中プロジェクトの`.moorestech-external-revisions.json`の`moorestech_master.commitHash`で、Unityが作業ツリー版を実チェックアウト値へ常時書き戻すため**コミット済み(`git show HEAD:`)の値が正**。
   `run-scenario.sh`/`preflight.sh`はこのコミット済みcommitHashにHEADが一致するmoorestech_master worktreeを**自動解決**する（第3/第2引数省略時）。未作成なら自動解決が失敗しエラーになるので、作業中ブランチ用に1つ作る:
   `git -C ../moorestech_master worktree add <path> <互換コミット>`（作成後は第3引数で明示してもよい）
2. **OSレベル入力シミュレート禁止**（simulate-keyboard/simulate-mouse-input）。OS入力と注入を混在させると実OSマウスが注入値を上書きし、PlayMode再起動まで回復しない。注入は`SemanticInput`（QueueStateEvent）一択。**スニペットから`InputSystem.Update()`も呼ばない**
3. **固定sleepで待たない**。`p.Until(条件, timeout, ラベル)`かファイル出現ポーリング（result.json / ready.marker）で待つ
4. **シナリオ実行前にPlayModeを止める**。走ったままだと前回のワールド状態を引き継ぐ
5. **legacy `UnityEngine.Input`直読みは注入で駆動不可**。主要経路は`Client.Input.HybridInput`へ移行済み。新たに駆動しない入力を見つけたらHybridInput化する（input-injection.md）
6. `.moorestech-external-revisions.json` / `_CompileRequester.cs` はUnityが自動書き換えする。スキーマ未更新なら`git checkout --`で戻しコミットしない
7. **run-testsとプレイテストを並走させない**（run-testsはUnityMcpSettings.jsonを退避し、衝突すると全uloopコマンドが死ぬ）
8. 調査用は`PlaytestRunOptions.Record=false`で実行し、`result.json`の`Success`と`Asserts`で判定する。提出用の最終実行は`Record=true`とし、動画生成（0 byte不可）/ Asserts全PASS / 期待UIのスクショ / **実プレイ視点**（アバター・地面・HUD）の目視を完了条件とする。調査用の成功を最終録画の代わりにしない
9. 作業終了前に必ず全てコミットする
10. **検証の副産物は worktree に置いたままにし、`../moorestech_logs` へ写さない。** 録画・連番フレーム・result.json・スクショ・Unity のログは `moorestech_client/PlaytestResults/`（gitignore 下）に残し、worktree を畳めば一緒に消える。記録へ残すのは結論の md と、要るならスクショ3枚程度まで（事後に録画やログが開かれることは無く、logs-sync が永久履歴へ入れて記録repoを膨らませるため）
11. **テスト用ワールドは worktree 内に作り、作業の終わりに `Saves/` を片付ける。** ワールドは `PLAYTEST_WORLD_DIRECTORY` で `moorestech_client/PlaytestResults/` 配下を指す（run-scenario.md）。作業の終わりに `"$SKILL/scripts/list-work-saves.sh"` で worktree 作成後に `Saves/` で作られた・書き換えられたワールドを出し、自分の作業が作った・複製したものを消す。`Saves/` は worktree の外にあり畳んでも残るため。作業前からあったもの（`world_1` 等）と他セッションのものは消さない

## 動画の可読性ルール（オーバーレイ・ナレーション・0.5秒インターバル）

録画には操作オーバーレイが自動で焼き込まれる: 左上=スタック式アクションログ（Step/Note/待機/Assert成否）、右上=押下中キーと直近入力（ゲーム自身の操作ヒント一覧・ホットバーと衝突しない位置）、画面上=注入マウスカーソル（クリックでリップル）。実装は`Client.Playtest/Overlay/`。

- **シナリオには`p.Note("...")`で「今から何をするか・何が起きたか」を書く**（AIナレーション）。動画左上とresult.jsonの`Timeline`に残る。フェーズの区切り（設置開始・検証開始等）ごとに1行が目安
- **1アクションごとに0.5秒のインターバルを必ず置く**（人間が動画で操作を認知できるテンポにするため）。Driverの非同期アクションAPI（PressKey/ClickPlace/PlaceBlockViaUi等）は自動で0.5秒空く。`SemanticInput`直叩きや`PlaceBlockDirect`等の同期APIを連続させる場合は`await p.WaitSeconds(0.5f)`を自分で挟む

## Step 0: 複雑シナリオはサブエージェント探索を先に（必須ゲート）

単純なシナリオ（設置→give→assert程度）ならwrite-scenario.mdのAPI表だけで書ける。**複数ブロック連結・UI操作・列車などは、Plan/general-purposeサブエージェントに以下を調査・出力させてから書く**:

1. 前提手順の連鎖（依存ブロック・必要アイテム・操作順序。例:「列車に乗る」は先にレール→列車設置）
2. 各操作の呼び出し経路（DSL Driver / API直 / 注入 / リフレクション。**対象の入力読み取りをgrepしlegacy Input直読みなら注入不可と明示**）
3. 絶対座標とconnector offsetの表（`inputConnects[].offset`を「OriginalPos + offset = 絶対座標」で計算。**受け側の`inputConnects`が空でないか必ず確認**＝空なら何を置いても繋がらない）
4. スニペットで使うAPI/フィールド名の実在確認（実ファイルをReadして確認。存在しない名前はpollingを無限ループ化させる）
5. Step単位の実行計画（各Stepのコマンドと期待結果）

**scout報告の否定的断定は裏取りするまで採用しない。** 「〜は関係ない・不要・ゲートにならない」型の断定は、コード静的読みだけでは間違うことがある。シナリオが原因不明で失敗したら、scoutが「排除した」要因こそ最小実験（EDCで直接コマンド投入等）で真っ先に検証する。

## Available scripts / scenarios（すべて本スキル同梱）

- `scripts/run-scenario.sh <unity-project-path> <scenario.cs> [master-server-dir]` — preflight→boot→シナリオ投入→result.json回収の一発実行
- `scripts/preflight.sh <unity-project-path> [master-server-dir]` — 疎通/コンパイル/master実在/マスタロードドライラン/ポート空き（run-scenario.shが自動で呼ぶ。単体診断にも使える）
- `scenarios/<カテゴリ>/*.cs` — 実証済みシナリオ集（カテゴリ別サブディレクトリ: `building/`=設置・BP・スポイト・アンドゥ / `connect/`=手持ち/ツール結線 / `train/`=レール・列車 / `misc/`=サンプル・CEF・視点・cleanroom）。`building/belt-segment-flow.cs`(direct構築・ベルト搬送) / `connect/gear-chain-pole-via-ui.cs`(ホットバー駆動) / `connect/gear-chain-connect-via-ui.cs`(クリック結線) / `train/train-rail-connect-via-ui.cs`(レール結線) / `misc/sample-chest.cs`(最小例)。新規シナリオも適切なカテゴリへ追加する。**冒頭に「⚠ 現行masterでは動かない」と書かれたシナリオ（削除済みuGUI型参照・ピン先マスタに無いGUID待ち。2026-09-19棚卸し）は雛形にしない**

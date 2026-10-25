# Tier9 統合 オーケストレーション計画

実装 plan `docs/superpowers/plans/2026-10-25-tier9-integration.md` を、どのセッションが・どの順で・どの合流点を越えて実行するかを決める文書。タスクの中身は plan が正で、ここには書かない。

## 1. 全体像

```
            W1（並行）                     W2（並行）                       W3（直列）       W4（直列）
 code  ── A: P1 コード統合 ─ G1/G3 batch ─┬─ A: P2 マスタ投入 S1→S2→S3 ─ ピン更新 ─┬─ G3 ─ P5 録画7段 ─┬─ T30 ビルド・セーブ・チェックリスト
 master ─ B: G2 チェッカー・G6 の枠 ──────┤                                        │                    │  → T31 評価役2体
 tests                                    └─ B: P4 G4 結合・G5 ゴールデンセーブ ──┘                    │  → T32 全ブランチレビュー
                                                                                                        │  → T33 PR×2
                                                                                                        └─ → ユーザー通しプレイ
              合流点 J1                                                       合流点 J2           合流点 J3
```

- クリティカルパス: P1 → G1 → P2(S1→S2→S3) → ピン更新 → P5 → T30 ビルド・セーブ → T31 評価役 → T32 レビュー → T33 PR → ユーザー通しプレイ（評価役は PR を出す前に判定する。T32 の反映が判定経路に触れたら T31 をやり直す）
- Tier8 終盤セーブ（T30 Step 1）は届くまで日がかかるので、W1 の開始と同時にユーザーへ依頼する（§5）
- 目安: W1 2日 / W2 3日 / W3 2日 / W4 1日（エージェント作業で 7〜10 日）

## 2. セッション

| セッション | ホスト | 作業場所 | 担当 | 寿命 |
|---|---|---|---|---|
| A（主） | Codex `gpt-6.1-sol`、moores-subagent-driven-development の SDD 本体（per-task） | `~/hermes-agent/data/worktrees/moorestech/tier9-integrated`（branch `feat/tier9-integrated`）。J1 以降はマスタ統合 worktree `~/hermes-agent/data/worktrees/moorestech_master/tier9-integrated`（moorestech_master の branch `feat/tier9-integrated`）も使う | W1: Task 4〜11 / W2: Task 12〜24 / W3: Task 27〜29 / W4: Task 30〜33 | 全期間 |
| B（副） | Codex `gpt-6.1-sol`、同 SDD | W1: マスタ統合 worktree `~/hermes-agent/data/worktrees/moorestech_master/tier9-integrated`（Task 1 で作る。moorestech_master の branch `feat/tier9-integrated`）。W2: 本体 worktree `~/hermes-agent/data/worktrees/moorestech/tier9-integrated-tests`（branch `feat/tier9-integrated-tests`、J1 後の本体 `feat/tier9-integrated` から切る） | W1: Task 1〜3 / W2: Task 25〜26 | J2 で終了 |

起動は `~/bin/cmux-agent-pane -C <worktree> -a cxd -m gpt-6.1-sol --prompt-file <依頼文>`。依頼文は §6。

**Codex 枠の確認（起動前と各合流点）:** A・B と moores-code-review の Codex runner は同じ ChatGPT 週次枠を使う。起動前と J1〜J3 で、`~/.codex/sessions` の最新 rollout の `rate_limits.primary.used_percent` と、リセットまでの時間を見る。次のどちらかなら、以後のセッションは Claude Code（`-a ccd -m opus --permission-mode full`）で起動する: 起動前に 50% 以上、または合流点で残りの wave を賄えない見込み（W2 は 1 wave で 30〜40% を目安にする）。切り替えるときは、動いているセッションに区切りで push させてから止める。Task 32 の全ブランチレビューは、残量が 40% 未満なら Codex runner でなく Claude Code ホストの moores-code-review で回す。

**SDD は 1 セッション内で実装 subagent を並列に出さない**（スキルの規則）。並列化はセッションを分けることで得る。同じブランチを 2 セッションで同時に書かない。マスタ統合ブランチは W1 の間は B だけ、J1 以降は A だけが書く（B は J1 で push して手を離す）。W2 の B の成果は本体の `feat/tier9-integrated-tests` に積み、J2 で A がマージする。

## 3. 合流点（越える条件は証拠で判定する）

| 合流点 | 越える条件（全部） | 確かめ方 |
|---|---|---|
| J1（W1→W2） | A: P1 の全タスクと G1/G3 の batch が `feat/tier9-integrated` に push 済み、compile エラー0、P1 の最終テスト正規表現が全 pass。B: Task 1〜3 がマスタ統合ブランチに push 済みで、G2 が#88+#87 のマージ結果に掛けて既知の欠落（入手不能・解放されないブロック）が赤で出て、わざと壊した fixture で exit 1 | `git log origin/feat/tier9-integrated` の `Task N:` コミット、`.superpowers/sdd/progress.md` の complete 行、G2 の実行ログ（`moorestech_logs/harness/tier9-integration/` に保存） |
| J2（W2→W3） | A: Task 12〜24 が完了し、マスタ統合ブランチで G1 緑・G2 緑（許可リスト外の詰み0）・G6 緑、マスタ統合 PR 作成済み。B: Task 25・26 が `feat/tier9-integrated-tests` で pass し push 済み。origin/master のセーブ版が 7 のまま | A が本体へ `feat/tier9-integrated-tests` を通常マージし、compile と G4・G5 を自分の Editor で再実行して pass。版は下の「版の確認」 |
| J3（W3→W4） | ピンがマスタ統合 PR の push 済みコミットを指す。G3 全ノード true。P5 の 7 段の result.json が全 pass、Error ログ0、動画を確認済み。A11 撮り直し済み。origin/master のセーブ版が 7 のまま。Tier8 終盤セーブが手元にある | result.json と動画のパス、G3 のログ、版の確認、`handoff/save-origin.md` |

**版の確認（J2・J3）:** `git fetch origin && git show origin/master:moorestech_server/Assets/Scripts/Game.SaveLoad/Json/WorldVersions/WorldSaveAllInfo.cs | grep CurrentVersion`。7 なら越える。8 以上なら、A が origin/master をマージし、V7→V8 の部品を新しい版の後ろ（例: V8→V9）へ繰り下げ、G5 の入力に master の新しい版のセーブを足してから越える（AGENTS.md のセーブ形式の規則）。繰り下げは P1 のセーブ移行の担当範囲として、同じ wave の中で直す。

越えられないときは、原因が plan の欠陥なら SDD の「質問の振り分け」に回し、そうでなければ同じ wave の中で直す。合流点を飛ばして先へ進まない。

## 4. 共有資源の取り扱い

- **マスタの共有クローン `~/hermes-agent/data/repos/moorestech_master` を checkout し直さない。** 全 moorestech worktree は `../moorestech_master` の symlink 経由でこのクローンを見ている。統合マスタは専用 worktree で編集し、読ませるときは次のどちらかで向ける:
  - 録画: `run-scenario.sh <project> <scenario> <MASTER_DIR>` の第 3 引数にマスタ統合 worktree の `server_v8` を渡す
  - Editor 内のロード・G1・G3: batch メソッドの引数（plan の G1 タスクで定義）にマスタ統合 worktree のパスを渡す
- Unity Editor は A と B が各自の worktree で 1 本ずつ（計 2 本）。他人の Editor を落とさない。起動・停止の前に `moores-wt status`
- セーブ移行の版（CurrentVersion=8）は A だけが触る。B は G5 で読むだけ
- `.decisions/` には明示されたユーザー裁定だけを書く。仮定・未決事項は bd `moorestech-xyuaf.2` へ note

## 5. ユーザーが関わる点

| 時点 | 内容 | 既定 |
|---|---|---|
| W1 開始時 | Tier8 終盤セーブを用意する（plan Task 30 Step 1）。テスターに頼むなら、ゲーム内の報告を今のワールドで 1 回送ってもらう依頼をユーザーが出す | J3 までに届かなければ、ユーザー自身の Tier8 終盤セーブ（winbuild の `Saves`）に切り替える。どちらも無ければ J3 で止めてユーザーに聞く |
| 随時 | plan の欠陥・仕様の選択（SDD の deferred-questions に溜まったもの） | 推奨案で進め、まとめて 1 回提示 |
| P5 着手前 | 混在ベルトで上位品が下位へ変換される挙動（W13）を仕様とするか | plan の判断記録の裁定に従う（Task 27） |
| W4 末 | 通しプレイ（Tier8 終盤セーブ→最終研究、裏口なし） | ユーザー本人（裁定済み）。plan の Task 30 が受け渡しのチェックリストとセーブを用意する。通しプレイが終わるまで staging と winbuild の検証を占有する |

## 6. 開始依頼文

### セッション A
```
Tier9 統合（コード統合・マスタ投入・録画）
moores-subagent-driven-development スキルを使って、以下の実装planを実行してください。

- plan: docs/superpowers/plans/2026-10-25-tier9-integration.md
- オーケストレーション: docs/superpowers/plans/2026-10-25-tier9-integration-orchestration.md（あなたはセッション A。担当タスクと合流点 J1〜J3 はこの文書の §2〜§3 に従う）
- 作業場所: feat/tier9-integrated（worktree /Users/sakastudio/hermes-agent/data/worktrees/moorestech/tier9-integrated）
- まずplan全文を読み、`## Requirements`・`## Global Constraints`・`## 判断記録（ADR）`を全タスク共通の制約として扱ってください
- 進捗管理はmoores-subagent-driven-developmentスキルの規定に従ってください（SDD本体はplanのチェックボックス＋進捗台帳が正）
- 合流点では越える条件を証拠で確かめ、満たさなければ先へ進まないでください。セッション B の成果は B のブランチからマージで取り込みます
- 起動直後と各合流点で、オーケストレーション §2 の「Codex 枠の確認」を行ってください。Tier8 終盤セーブ（Task 30 Step 1）は W1 開始時にユーザーへ依頼済みです。J3 までに届いていなければ §5 の既定に従ってください
- planの最終タスク群（moores-code-reviewによる全ブランチレビュー→pr-createでPR作成・コンフリクト解消まで）は省略不可です。PRが作成されセッションを閉じられる状態になるまで完了扱いにしないでください。moores-code-review は references/codex-host.md の runner で実行してください
- 私は寝ているので、確認を待たず進められるだけ進めてください。止まるのは解決不能な障害か取り返し不能な破壊的操作だけです
```

### セッション B
```
Tier9 統合（判定ゲートと結合テスト）
moores-subagent-driven-development スキルを使って、以下の実装planのうちセッション B の担当タスクを実行してください。

- plan: docs/superpowers/plans/2026-10-25-tier9-integration.md（origin/feat/tier9-integrated から読む）
- オーケストレーション: docs/superpowers/plans/2026-10-25-tier9-integration-orchestration.md（あなたはセッション B。担当タスク・作業場所・合流点はこの文書の §2〜§3 に従う）
- W1 は Task 1〜3。Task 1 がマスタ統合 worktree /Users/sakastudio/hermes-agent/data/worktrees/moorestech_master/tier9-integrated（moorestech_master の branch feat/tier9-integrated）を作る。Task 3 を push したらマスタ統合ブランチから手を離す（以後はセッション A が書く）
- W2 は Task 25〜26。J1（本体 feat/tier9-integrated に Task 4〜11 が push 済み）を確かめてから、moores-wt new feat/tier9-integrated-tests --from origin/feat/tier9-integrated で作った worktree で行う
- W2 の成果は feat/tier9-integrated-tests へ push したら終わりで、本体統合ブランチへのマージはセッション A が行います。閉じタスク（全ブランチレビュー・PR 作成）は担当しません
- まずplan全文を読み、`## Requirements`・`## Global Constraints`・`## 判断記録（ADR）`を全タスク共通の制約として扱ってください
- 私は寝ているので、確認を待たず進められるだけ進めてください。止まるのは解決不能な障害か取り返し不能な破壊的操作だけです
```

## 7. 監視と報告

- 各セッションは区切り（タスク完了・合流点）ごとに bd `moorestech-xyuaf.2` へ 1 行 note を残す
- 完了通知は信じず、`git log` と進捗台帳・ログファイルの実在で確かめる
- W4 末にユーザーへ渡すもの: 統合 PR とマスタ PR の URL、G1〜G6 の出力、録画 7 本、評価役 2 体の判定、通しプレイのチェックリストとセーブ

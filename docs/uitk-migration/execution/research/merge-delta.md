# 調査: master → feature/uitk 取り込み（bd albof.20）の差分と衝突

調査日 2026-11-06。worktree `uitk-execution-plan` で `git fetch origin` の直後に実測。作業ツリー・ref は変更していない（模擬マージは一時 index と `git commit-tree` で作った ref 無しのコミット `083252d303` で行った）。master が進んだら数字は変わるので、取り込みの直前に同じ手順で取り直すこと（末尾「再現手順」）。

## 1. 起点

| 項目 | 値（実測） |
|---|---|
| origin/master | `e95dc17db1`（2026-11-06 12:46 Merge PR #1590 docs/uitk-bigbang-switch） |
| origin/feature/uitk | `54c8a4c8c8`（2026-11-06 10:40） |
| merge-base | `979725bcd1`（2026-11-05 18:48 Merge PR #1559 feature/uitk-stage0）＝ 台帳の束 1 の暗転の起点と同じ |
| master 側の未取り込みコミット | 6 件（`git rev-list --count 979725bcd1..origin/master`） |
| feature/uitk 側の独自コミット | 102 件 |

## 2. master 側に入ったもの（feature/uitk に無い）

6 コミットすべて PR #1590 の中身（`3edcebae82` 裁定 7 件 / `9cb3743458` G1 の走る範囲 / `58f27fb912` plan 改訂 / `0b39262a1c` 台帳の運用ルール / `de88d32729` レビュー反映 / `e95dc17db1` マージ）。

`git diff --name-status 979725bcd1 origin/master` の全件:

- `.decisions/2026-11-06-*.md` 追加 7 本。うち `UITK段階1の画面はWeb版と同じ見た目にする.md` は feature/uitk 側にも同一内容で既にある（`git diff origin/master origin/feature/uitk -- .decisions` に出ない）。実質の新規は 6 本（CEF停止の記録の切り出し／切替の日に CEF と Web を同時撤去／切替まで CEF 由来の不具合は直さない／段階2以降も Web と同じ見た目／長命ブランチで作り一気に切替／全体スイッチ1つで追従はブランチ側で記帳）
- `.github/workflows/uitk-web-follow-gate.yml` 変更: G1 ジョブに `if: github.base_ref == 'feature/uitk' || github.head_ref == 'feature/uitk'` を付け、実台帳テスト `test_uitk_web_follow_real_ledger.py` を G1 ジョブ内へ移し、自己テストを `if` 無しの別ジョブ `uitk-web-follow-gate-selftest` に分けた。`.github/scripts/` 自体は master 側で変更なし
- `docs/uitk-migration/plan.md`（+391/-172 相当の改訂。feature/uitk 側は merge-base 以降この文書を触っていないので無衝突で master 版になる）
- `docs/uitk/TODO.md`（運用ルール・束 1 の所有者・agent 既定の書き換え。→ 衝突。§4）

**master 側の Web（`moorestech_web/`）の変更は 0 件**（`git log --oneline 979725bcd1..origin/master -- moorestech_web | wc -l` → 0）。C# の変更も無い。よってこの回の取り込みで「写す」べき master の Web 変更は無い。

## 3. feature/uitk 側の Web 変更（merge-base 以降・非マージ 6 件）

`git log --no-merges 979725bcd1..origin/feature/uitk -- moorestech_web`:

| コミット | 内容 | 台帳の追従記録（feature/uitk の `docs/uitk/TODO.md:88-92`） |
|---|---|---|
| `a97575e5b5` | Web は webUiSuspended の間 App 全体を unmount（旧 R1） | 記帳済み（写した） |
| `05b9bdefa8` | 休止・カットシーン明けに stage 倍率を測り直す | 記帳済み（捨てた） |
| `1095cab8bf` | 休止解除で送信済みキャッシュを捨て入力状態を送り直す（旧 R1） | 記帳済み（写した） |
| `ccb3d512f7` | 休止解除後 DOM 変化ごとにポインタ位置を当たり判定（旧 R1） | 記帳済み（写した） |
| `5421c7f9f0` | moores-code-review 自動適用。`shared/uiState/useWebInputExclusivity.ts`・`watchPointerHitOnDomChange.ts` を `shared/uiState/inputExclusivity/` へ移動・修正、`App.tsx` 等 | **未記帳** |
| `3e946e4bd3` | D-8。`src/shared/i18n/generated/localizationKeys.ts` の生成物 2 行（拒否理由のローカライズキー） | **未記帳** |

merge-base からの Web の差分は 20 ファイル +459/-105（`git diff --stat 979725bcd1 origin/feature/uitk -- moorestech_web`）。master 側に Web 変更が無いので、今回これらは衝突しない。

## 4. 予想される衝突（`git merge-tree --write-tree --name-only origin/feature/uitk origin/master` の実測）

結果 tree `3e2edd8f28`、終了コード 1。**衝突は `docs/uitk/TODO.md` の 1 ファイル・2 か所だけ**。plan.md・workflow・.decisions・コードは自動マージで通る。

### 4.1 衝突 1: 「## 行」の束 1 の 3 行（結果 tree の 38-46 行）

- feature/uitk 側（`origin/feature/uitk:docs/uitk/TODO.md:35-37`）: pauseMenu・settings・toast の所有者 `UITK`。pauseMenu の実測の結論列に UITK のポーズの撮影 URL（720p / 2560×1080、トップとバグ報告）。toast の agent 既定列が「裁定 2=A。様式化 (a)。通信失敗のトーストは Web に残る」
- master 側（`origin/master:docs/uitk/TODO.md`、同じ 3 行）: 所有者 `Web`、実測の結論列 `—`、toast の agent 既定列が「裁定 2=A。見た目は Web 版と同じ（2026-11-06 裁定）」

### 4.2 衝突 2: 「## agent 既定（ユーザー未裁定）」（結果 tree の 146-151 行）

- feature/uitk 側（`origin/feature/uitk:docs/uitk/TODO.md:137-138`）: 「通信失敗のトースト｜Web に残る（toast 行の所有者は UITK だが…）」と「様式化 Mantine の素の見た目の 4 画面｜未裁定（推奨 (a) 様式化）」
- master 側: 「様式化 Mantine の素の見た目の 4 画面｜2026-11-06 ユーザー裁定: Web 版と同じ見た目（様式化しない）」の 1 行のみ

### 4.3 自動マージで入るが要注意の箇所

- 「## 束」の束 1 行は両側の差が無いとみなされず、**feature/uitk 側の `切替済み` のまま自動マージされる**（結果 tree 26 行 `| 1 | 基盤と命綱（ポーズ・設定・バグ報告・トースト） | 切替済み | 979725bcd1 | \`uitk-lifeline\` |`）。master 側は `| Web | — |`（`origin/master:docs/uitk/TODO.md:25`）。git は衝突にしないので、手で直さないと残る
- 「## 読み方と運用」（結果 tree 5-19 行）は master の新ルール（台帳は feature/uitk 側で記帳・切替 PR で全束一度・束を切替 PR より前に `切替済み` にしない）になる

## 5. 束 1 を「切替済み」→「暗転」へ戻す具体的な編集

plan（`origin/master:docs/uitk-migration/plan.md:70` と `:201`）の指示どおり「束 1 を `暗転` に、束 1 の行の所有者を `Web` に、起点 `979725bcd1` はそのまま」。マージ解決後の TODO.md で次の 4 行を書き換える。

1. 束の表（結果 tree 26 行）
   - 前: `| 1 | 基盤と命綱（ポーズ・設定・バグ報告・トースト） | 切替済み | 979725bcd1 | \`uitk-lifeline\` |`
   - 後: `| 1 | 基盤と命綱（ポーズ・設定・バグ報告・トースト） | 暗転 | 979725bcd1 | \`uitk-lifeline\` |`
2. 行 `pauseMenu（ポーズ・バグ報告・入れ子のポーズ）`: 所有者 `UITK` → `Web`
3. 行 `settings（画質・言語・ダッシュ方式）`: 所有者 `UITK` → `Web`
4. 行 `toast`: 所有者 `UITK` → `Web`

所有者を `UITK` のまま束を `暗転` にすると台帳のパースが落ちる（実測: `.github/scripts/uitk_web_follow/ledger.py:171-176` の検査で「行「pauseMenu…」の所有者「UITK」が束の状態と合わない（Web のはず）」が 3 行分出た）。束 1 を含む他の行（オーバーレイ・tutorialAnchor・shared/ui・i18n・uiState。束 `1 2 3 4 5`）は元から `Web` で変更不要。

衝突の解決の推奨（agent 既定の案）:
- 衝突 1: 列ごとに混ぜる。所有者は master 側（`Web`）、pauseMenu の実測の結論列は feature/uitk 側の UITK ポーズ撮影 URL を残す（段階 1 の実測で、捨てる理由が無い）、toast の agent 既定列は master 側の文（2026-11-06 の見た目の裁定）を採る
- 衝突 2: master 側の「様式化しない」裁定行を採る。「通信失敗のトースト」行は全体スイッチ（albof.17）で前提（ポーズ中だけ Web を休止）が消えるため、そのまま残すと誤りになる。「全体スイッチの UITK 側では Web が起動しないので、通信失敗の通知を UITK のトーストで出すかは作り直しで決める」旨に書き換えて残すか、作り直しのレーンへ渡す（推測: 全体スイッチの UITK 側には WS が無いので通信失敗そのものが起きない可能性が高いが、コードは未確認）

## 6. G1 で取り込み後に未記帳になる件数

上の §5 の解決（衝突は master 側＋束 1 の 4 行の書き換え）をした模擬マージコミット `083252d303`（親 = origin/feature/uitk, origin/master）で、PR into feature/uitk と同じ引数を実測:

```
$ python3 .github/scripts/check-uitk-web-follow.py --base origin/feature/uitk --head 083252d303
G1: 束 1（暗転・起点 979725bcd1）: 未記帳 2 件
G1: 違反なし        （rc=0）
```

- **見込み: 束 1 で未記帳 2 件（`3e946e4bd3`・`5421c7f9f0`）、束 2〜5 は起点が無いので数えない、違反（赤）は 0**。どちらも master 由来ではなく feature/uitk 側の自前の Web 変更（§3）。台帳の運用ルール「取り込みの PR は件数が 0 であることを見てからマージする」（`origin/master:docs/uitk/TODO.md` の「## 読み方と運用」）に従うと、取り込み PR の中でこの 2 件を「## 追従記録」の束 1 へ記帳する必要がある
- 2 件の帰属: 変更ファイルが `src/app/`（束 5 の行）と `src/shared/uiState/`・`src/shared/i18n/`（束 `1 2 3 4 5` の行）に入るため所有束は {1,2,3,4,5}。束 2〜5 は今は `Web` なので数えないが、それぞれの束を暗転にした後に起点を 979725bcd1 以前に置くと同じ 2 件＋旧 R1 の 4 件が数えられる（推測: 起点はレーン開始時の取り込み済み master にするので、実際には数えられない）
- 現状の feature/uitk 単体でも同じ 2 件が未記帳（`--base 979725bcd1 --head origin/feature/uitk` で、束 1 が `切替済み` のため G1-1 違反 2 件として赤になる）。つまり段階 1 の取り込み時点から記帳漏れがある
- 暗転の起点を `979725bcd1` のままにしても、取り込み後の master 先端 `e95dc17db1` に変えても、数える範囲（起点..head の非マージ Web コミット）は同じ 6 件で変わらない（master 側に Web コミットが無いため。導出であり後者は実測していない）
- 模擬マージ tree を一時ディレクトリへ展開し、`.github/scripts/tests/test_uitk_web_follow_*.py` 5 本（boundaries・gate・history・ledger・real_ledger）を回して全部 OK（real_ledger は網羅・機能ディレクトリ・tutorial 共有パス・Tier9 の 4 テスト OK）

## 7. 取り込みの後に続けて効くこと（計画立案への入力）

- **旧 R1 の Web 変更の撤去（albof.17 作り直し）**: plan `:211` は旧 R1 の Web 側変更を feature/uitk から外し master と同じ Web に戻すと決めている（agent 既定）。外すコミットも Web を触る非マージコミットなので、G1 は束 1（と所有束 {1..5}）の未記帳として新たに数える。外した回に `捨てた（理由）` で記帳する。`5421c7f9f0` の `inputExclusivity/` 移動も旧 R1 の一部なので同時に外れる見込み（推測）
- **`3e946e4bd3` の生成物**: `localizationKeys.ts` は `scripts/generate-localization-keys.mjs` の生成物で、C# 側の `localization.csv` 追加に連動して再生成されたもの（推測: 生成元はコミットメッセージと生成ヘッダから）。plan R3（`:431`「feature/uitk で直接 Web を書き換えない」）と、共有のローカライズ CSV を変えると Web 生成物も変わることがぶつかる。記帳は「写した（UITK のトースト文言）」か「捨てた（生成物の追随）」のどちらかで、判断は agent 既定で足りる
- **CEF 停止の記録の切り出し（albof.18）**: plan `:530` により feature/uitk の 1〜3 を別 PR で master へ入れ、次の取り込みで feature/uitk が同じ変更を受けて重複を解く。項目 3（Web 無応答時の入力排他の無視）は master 側で Web に触れる形になり得るので、その取り込みでは master 由来の Web コミットが初めて未記帳に出る見込み（推測）。また feature/uitk 側の C# の同等物と衝突する可能性が高い（推測。どのファイルかは albof.18 の PR が無いので未確認）
- **見た目の裁定との不一致**: feature/uitk の台帳は toast を「様式化 (a)」と書いていた。2026-11-06 裁定（Web 版と同じ見た目）と食い違うので、段階 1 で作った UITK のトーストが様式化されていないかを albof.19 / 作り直しで確かめる必要がある（実装は未確認）

## 8. 再現手順

```bash
W=/Users/sakastudio/hermes-agent/data/worktrees/moorestech/uitk-execution-plan
git -C $W fetch origin
git -C $W merge-base origin/master origin/feature/uitk
git -C $W merge-tree --write-tree --name-only origin/feature/uitk origin/master   # 衝突一覧
# 解決後の TODO.md を一時 index に入れて ref 無しコミットを作り、G1 を回す
GIT_INDEX_FILE=<scratch>/idx git -C $W read-tree <merge-tree の tree>
GIT_INDEX_FILE=<scratch>/idx git -C $W update-index --cacheinfo 100644,<解決後 blob>,docs/uitk/TODO.md
C=$(git -C $W commit-tree $(GIT_INDEX_FILE=<scratch>/idx git -C $W write-tree) -p origin/feature/uitk -p origin/master -m sim)
(cd $W && python3 .github/scripts/check-uitk-web-follow.py --base origin/feature/uitk --head $C)
```

# pr-independent-review スキル設計

## 背景 / 目的

レビュー免責ロンダリング事故（PR1063系統・`CommonBlockPlaceSystem`の電気ドメイン汚染）の調査から、
実装セッション自身が書いたレビューcontextが「合意済みトレードオフ」として指摘を握り潰す構造欠陥が確認された。
また過去レビューコメントの実測（PR950以降・実質的指摘63件）で、現行moores-code-reviewハーネスの実名機構が
76%（boundary系100%・pattern系93%）を捕捉できることが分かった。

本スキルは「人間レビュー無しのgreenマージ」へ向けたシャドー運用の第一歩として、
**実装セッションと完全に独立したセッション**でPRをレビューし、verdict付きダイジェストを出力する。
実装セッションの自己申告（context・合意主張）を一切受け取らないことが独立性の核。

## スコープ

- 手動発火スキル。freshセッションでPR URL（または番号）を渡して起動する
- v1は静的レビュー専業。マージ判断の自動化・PRコメント投稿・コンパイル/テスト実行はスコープ外
- 成果物: ダイジェストレポート（ローカル保存）＋シャドー台帳への1行追記

## フロー

```
入力: PR URL or 番号
  ↓
1. PR取得        gh pr view（本文・ブランチ・ベース）
  ↓
2. checkout      レビュー専用worktreeで `git reset --hard && git clean -fd` 後に
                 `gh pr checkout --detach`（--detach必須: PRブランチは実装worktreeが
                 保持していることが多くブランチロックで失敗するため。実測再現済み）
  ↓
3. patch生成     git diff <base>...HEAD -- . ':(exclude)*.meta' ':(exclude)*.prefab'
                 ':(exclude)*.asset' ':(exclude)画像/バイナリ'（exclude方式・yml/jsonは残す）
  ↓
4. context再構成  PR本文＋リポジトリ内spec/planの判断台帳（ADR）のみから4カテゴリcontextを作る
                 出所ラベルは本体の正式文法に従う: ユーザー裁定=`[ADR: <spec名>#<台帳項目>]` /
                 それ以外=`[agent前提]`（免責力なし。checks_contextが非実在参照を機械検出する）
  ↓
5. 新規性ゲートL1  新設スクリプト: using新ペア（汎用層起点・層境界逆行）/ asmdef参照追加 /
                 文法要素新設（interface・基底クラス・Subject・プロトコル・スキーマ）→ 新形フラグ
  ↓
6. 本体レビュー    moores-code-review 5系統を発火。ただし:
                 - スクリプト・レンズ・統合ルールは**起動側正典treeの絶対パス**で参照する。
                   cwd（レビューworktree）はレビュー対象コードの読み取り専用。PR側の古い/改変された
                   ハーネスコピーを使うと測定器がPRごとに変わり見逃し率実測が壊れる＋自己弱体化経路になる
                 - **report-onlyモードで起動**: 本体Step 6の確定修正自動適用・uloop compile・
                   Step 6.5の適用後diff再生成・Step 7.3のrecords/eval記録生成を全て停止する
                   （静的レビュー専業のADRと整合させ、レビューworktreeを汚さず、帳簿をシャドー台帳へ一本化）
                 - 免責降格・[agent前提]無免責は本体に実装済み（原則①②改修はmaster到達済み）のため
                   上書き注入はしない。本体に無い項目が判明した場合のみ起動promptで補う
  ↓
7. ダイジェスト    verdict（自動マージ可 / 新形につき裁定行き / Critical差し戻し）を
                 **インフォグラフィックHTML**として生成し `open`（詳細は「出力フォーマット」節）。
                 機械可読なmd版サマリも records/ に保存
  ↓
8. シャドー台帳    PR番号・verdict・新形数・suppressed数・日付を1行追記
                 （後日、人間の実マージ判断と突き合わせて見逃し率を実測する）
```

## コンポーネント

| 要素 | 新規/既存 | 内容 |
|---|---|---|
| SKILL.md | 新規 | 上記フローのオーケストレーション |
| 新規性ゲートL1スクリプト | 新規 | usingペア表構築＋diff照合＋文法要素検出（Python） |
| レビューworktree管理 | 新規（手順） | `git worktree add`＋`gh pr checkout`。場所は `~/moorestech-worktrees/pr-review` 固定・使い回し |
| moores-code-review | 既存 | レビューエンジン本体。無改変・起動側正典treeの絶対パスで呼ぶ（report-onlyモード） |
| records/シャドー台帳 | 新規 | スキル配下 `records/shadow-ledger.md`（moores-code-reviewのrecords/前例踏襲） |

## 出力フォーマット（ダイジェストHTML）

認知コスト最小化のため、裁定に必要な情報を**実コード込みで1画面に**collocateする。
エディタやGitHubを開かずにHTML単体で裁定が完結することが受け入れ基準。

- **生成方法**: create-infographic-light のテンプレート（コメント機能込み・verbatim維持）をベースに、
  PRごとに `/tmp/pr-review-<PR番号>/index.html` を生成して `open` する
- **構成**（上から読み、途中でやめられる順）:
  1. verdictヘッダ（1行: verdict＋Critical/新形/設計判断/suppressed件数）
  2. **裁定カード**（新形フラグ・設計判断の各1件につき1カード）。カード内に必ず:
     - ファイル名（大きく・太字）＋リポジトリ相対フルパス（`<code>`）＋行番号
     - **実コード抜粋**: 当該diffハンク（前後数行のコンテキスト付き・行番号付き・追加行を色分け・
       問題箇所の行をハイライト）
     - PR側の主張（出所ラベル付き）／代替案（シグネチャ付き）
  3. **suppressedカード**（全件・同じく実コード抜粋＋suppressed-by出所付き）
  4. 判断台帳（ユーザー裁定由来 / agent前提の2グループ）
  5. 折りたたみの参考セクション（Critical修正方針詳細・Warning/Info一覧・各系統の生所見）
- **裁定の返し方**: HTMLのコメント機能で各カードにコメント→「すべてコピー」のMarkdownを
  任意のセッションに貼れば裁定として適用できる（既存のインフォグラフィックレビューと同じ動線）
- md版サマリ（records/保存）はHTML と同内容のテキスト縮約で、シャドー台帳突き合わせ・grep用

## verdict判定規則

- **Critical差し戻し**: 統合後Criticalが1件以上（決定論confirmed含む。200行超過は除外＝努力目標）
- **新形につき裁定行き**: Criticalなし、かつ新形フラグ or 設計判断ありが1件以上
- **自動マージ可**: 上記いずれも無し
- suppressedされた指摘はverdictに影響しないが、ダイジェストに必ず全件列挙する（Critical/Warning級）
- 将来検討（自動マージ化の段階で）: suppressed Criticalが1件以上なら最低「裁定行き」への格上げ。
  偽`[ADR:]`参照による免責はchecks_contextが検出するが、verdict層にも保険を置く

## エラー処理・縮退

- `gh`未認証・PR不存在: 即座に明示エラーで終了（黙って縮退しない）
- codex不在等のmoores-code-review内縮退: 同スキルの既存規約に従い報告に明記
- レビューworktreeの前回状態: checkout前の `git reset --hard && git clean -fd` で毎回リセット（手順2）

## 判断記録（ADR）

### ユーザー裁定
- **台帳承認方式の採用**（AskUserQuestion 2026-07-26）: agent前提の提示は台帳1行リスト方式。都度質問・事後可視化のみは不採用
- **完全手動発火・独立セッション・PR URL入力**（ユーザー発言 2026-07-27「完全に独立したセッションでPRのURLを渡し、そこから差分を取ってチェックするskillとして実装したい。一旦完全手動発火skillとして実装していく」）
- **patchフィルタはexclude方式**（設計提示→ユーザー「ok」2026-07-27）: cs/ts限定のincludeではなく.meta/.prefab/.asset/画像を除外。yml/json系レンズ（master-data-defense等・実測data系指摘16件）の盲目化を防ぐ
- **ダイジェストはコード実物入りインフォグラフィックHTML**（ユーザー発言 2026-07-27「認知コストを最低にしたい。だから、実コードと実コードのフルパス、ファイル名、それらのコードの当該箇所が分かるようなインフォグラフィックを作成し、提示するようにして」）: テキストmd単体案は不採用。md版はrecords/用の縮約に格下げ

### agent前提（拒否権つき・免責力なし）
- PRコメント投稿はしない（シャドー期の外向き発信不要・判断汚染回避）
- L2前例引用照合はv1に入れない（引用義務は実装agent側の協力が前提。事後独立レビューでは前例探索をprecedent-alignmentレンズが担う）
- コンパイル・テスト実行はv1スコープ外（レビューworktreeでのUnity起動はライセンス・ポート・時間の制約）
- AskUserQuestion不使用。設計判断も含め全部ダイジェストへ書き出して終了（発火者は結果を後読みする運用）
- シャドー台帳の置き場はスキル配下records/（moores-code-reviewのrecords/前例）

### simulator review適用済み（判事の実検証による修正 2026-07-27）
- 原則①②の起動prompt上書き前提を撤回: 本体改修は既にmaster到達済みと判事がgit走査で確認。上書きは「本体に無い項目のみ」へ縮小
- ハーネスは起動側正典treeの絶対パス参照に固定: cwd相対だとPR側の古い/改変ハーネスに解決され、見逃し率実測が壊れる＋自己弱体化経路になるため
- report-onlyモードを明記: 本体の自動修正適用・uloop compile・records生成は静的レビュー専業ADRと矛盾するため全停止
- `gh pr checkout --detach`＋事前reset/clean: PRブランチの実装worktree保持によるブランチロック失敗を判事が実測再現

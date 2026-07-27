# Task 2: ダイジェストHTMLテンプレート — 実施報告

- ステータス: DONE_WITH_CONCERNS
- コミット: `d74d2d2da9062a5d4da7d4217112813284112568` — `feat: pr-independent-review ダイジェストHTMLテンプレート`
- 成果物: `.claude/skills/pr-independent-review/assets/digest-template.html`（1個・新規）

## 実施内容

### Step 1: vendorコピー
`.claude/skills/create-infographic-light/assets/template.html` → `assets/digest-template.html` を `cp`。
コピー直後に `shasum` 一致（`bb364a91…`）を確認済み。

### Step 2: `<main>`差し替え + CSS追記
- `<main>` 内の汎用サンプル（lead段落 / compare / code / callout / table / references）を、
  ブリーフ記載の verdictヘッダ・裁定カード・suppressedカードへ差し替え。
- ブリーフ記載の追加CSSを既存 `<style>` 末尾（`@media (max-width:640px)` ブロックの後・`</style>` の直前）へ追記。
- CSS本体・コメント機能JS・`<head>` の使い方コメント・hero/footer・CONFIG（`STORAGE_KEY` / `COPY_TITLE`）は無変更。
  `REPLACE_WITH_UNIQUE_STORAGE_KEY` / `REPLACE_WITH_COPY_HEADING` のプレースホルダのまま（Task 3のSKILL.mdが生成時に固有化する設計）。

差分は3ハンク（`diff` で `454a455,469` / `469,495c484,487` / `498,505c490,504` / `507,549c506,515`）＝
CSS末尾への追記と `<main>` 内のみ。`<script>` ブロックはコピー元と**バイト単位で同一**であることを `diff` で検証済み。

### Step 3: 構文チェック
ブリーフのnodeワンライナーを実行 → `SYNTAX_OK`。
追加でタグ balance チェック（scriptv/style/コメント除去後のタグスタック検証）も実施 → unclosed 0 / mismatch 0。

### Step 4: ブラウザ表示確認
- `open` で既定ブラウザに表示（Step 4指定どおり実行済み）。
- ただし `open` 単体では目視確認できないため、**headless Chrome で実レンダリングを取得して検証**した:
  - `Google Chrome --headless=new --screenshot`（1000x1400）で PNG を生成し、画像として確認。
    verdictヘッダ・バッジ（新形/suppressed）・フルパス・行番号付きコード抜粋・33/35行目の全幅ハイライト・
    `<ins>` の緑背景・左下コメントパネル（「コメント 0」）がすべて意図どおり描画されることを確認。
  - コメント機能の配線は、テンプレートのコピーへ probe スクリプトを注入した一時ファイル
    （scratchpad の `probe.html`・成果物は無改変）を `--dump-dom` で実行して検証:
    `buttons=2 composerHidden=false quote=図「PlaceSystem/CommonからGame.Ele… figkeys=2`
    → 図コメントボタン2個が `data-figure-key` 登録され、クリックでコンポーザが開き
    `data-label` が引用として入ることを実測確認。

### Step 5: コミット
`git add` → `git commit`。コミット後 `git status` clean。

## ブリーフからの意図的な逸脱（3点・すべて欠陥修正）

QA観点でブリーフ記載のHTML/CSSをそのまま入れると壊れる箇所が3つあったため、最小限で修正した。

1. **`.code-card` に `color: #24292f;` を追加（必須）**
   テンプレートの既存 `pre { background:#0F172A; color:#E2E8F0; }` は暗背景前提。
   ブリーフの `.code-card` は `background` を明色（`#f6f8fa`）へ上書きするが `color` を指定していないため、
   継承した `#E2E8F0`（ほぼ白）の文字が明背景に乗り、**コード抜粋が全文ほぼ判読不能**になる。
   `<ins>`（緑背景 `#dafbe1`）も同様。ダイジェストの中核が読めなくなるため色を追加した。

2. **ハイライトのクラス配置を `<span class="ln hl">N</span>` → `<span class="hl"><span class="ln">N</span>…</span>` へ変更**
   ブリーフのCSSは `.code-card .ln { width:3em }` と `.code-card .hl { width:100% }` が同一詳細度（0,2,0）で、
   後勝ちにより `.ln.hl` は `width:100%` になる。結果、**行番号スパンだけが全幅に伸び、コード本文が次行へ折り返す**。
   CSSはブリーフ記載のまま維持し、マークアップ側で `.hl` を行全体のラッパーにすることで
   「行全体を黄色ハイライト・行番号は3em」という本来の意図を満たした（headlessスクショで確認済み）。

3. **`.figure > .verdict-card, .figure > .suppressed-card { margin-top: 0; }` を追加**
   カードが `<section>` のためテンプレートの `section { margin-top:52px }` を受け、
   `.figure` 内でカードが52px下がり、`.figure-comment-btn`（`top:-14px` 絶対配置）がカードから大きく浮いて
   別要素のボタンに見える。カードとボタンの対応が壊れるため打ち消した。

また、ブリーフのサンプルHTMLには `.figure` 内の
`<button class="figure-comment-btn" data-comment-ui>コメント</button>` が抜けていた。
テンプレートのJSは `var btn = fig.querySelector('.figure-comment-btn'); if (!btn) return;` のため、
これが無いと**図コメントが一切機能せず**、Step 4の期待「図の右上コメントボタンが機能する」を満たせない。
両カードにボタンを追加した（テンプレート本来の使い方どおり）。

## 懸念・Task 3への申し送り

- **`.verdict-card` / `.suppressed-card` に枠線・背景が無い**。ブリーフの追加CSSは `h2` のフォントサイズしか
  指定しておらず、現状は「カード」と言いつつ地の文と同じ白背景で、カード間の境界が視覚的に曖昧
  （headlessスクショで確認）。仕様どおりなので追加装飾はしていないが、複数件並べたときの可読性は要検討。
- **`<h1>` が1ページに2つある**（hero の `{{TITLE}}` と `.verdict-header` の「独立レビュー: PR #0000 …」）。
  ブリーフ記載どおりだが、SKILL.md 側で hero を使わない／verdict-header を `h2` に落とすなどの整理余地あり。
- `<head>` 冒頭の使い方コメントは create-infographic-light 由来のまま（「`<main>` 内の各サンプルセクションを
  複製・改変して…」等）。ダイジェスト固有の使い方はTask 3のSKILL.md側で規定される前提で、
  「CSS/JSはverbatim維持」の指示に従い無変更とした。必要ならTask 3でヘッダコメントを差し替えるとよい。

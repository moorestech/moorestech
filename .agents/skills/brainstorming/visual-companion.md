# ビジュアルコンパニオンガイド

モックアップ・図・選択肢を見せるための、ブラウザベースのビジュアルブレインストーミングコンパニオン。

## いつ使うか

セッション単位ではなく質問単位で判断する。判断基準：**見せた方が読むよりも理解しやすいか？**

**ブラウザを使う** — 内容自体が視覚的なものの場合：

- **UIモックアップ** — ワイヤーフレーム、レイアウト、ナビゲーション構造、コンポーネントデザイン
- **アーキテクチャ図** — システムコンポーネント、データフロー、関係マップ
- **並列でのビジュアル比較** — 2つのレイアウト、2つの配色、2つのデザイン方向の比較
- **デザインの磨き込み** — 見た目・質感、余白、視覚的な階層についての質問の場合
- **空間的な関係** — ステートマシン、フローチャート、エンティティ関係を図として描画する場合

**ターミナルを使う** — 内容がテキストまたは表形式の場合：

- **要件・スコープに関する質問** — 「Xとは何を意味するか？」「どの機能がスコープ内か？」
- **概念的なA/B/C選択** — 言葉で説明されたアプローチ同士の選択
- **トレードオフの一覧** — 長所短所、比較表
- **技術的な決定** — API設計、データモデリング、アーキテクチャアプローチの選択
- **明確化のための質問** — 答えが視覚的な好みではなく言葉であるもの全般

UIに関する話題*についての*質問が、自動的に視覚的な質問になるわけではない。「どんな種類のウィザードが欲しいか？」は概念的 — ターミナルを使う。「これらのウィザードのレイアウトのどれがしっくりくるか？」は視覚的 — ブラウザを使う。

## 仕組み

サーバーはディレクトリを監視してHTMLファイルを検出し、最新のものをブラウザへ配信する。`screen_dir` にHTMLコンテンツを書き込むと、ユーザーはブラウザでそれを見て、クリックで選択肢を選べる。選択結果は `state_dir/events` に記録され、次のターンで読み取る。

**コンテンツフラグメント vs 完全な文書:** HTMLファイルが `<!DOCTYPE` または `<html` から始まる場合、サーバーはそのまま配信する（ヘルパースクリプトを注入するのみ）。それ以外の場合、サーバーは自動的にコンテンツをフレームテンプレートでラップする — ヘッダー、CSSテーマ、接続状態、あらゆるインタラクティブ基盤が追加される。**基本はコンテンツフラグメントを書くこと。** ページを完全に制御する必要がある場合のみ、完全な文書を書く。

## セッションの開始

```bash
# ユーザーがコンパニオンを承認した後に開始する。--open は最初の画面が表示され次第
# ブラウザを自動で開く。--project-dir はモックアップを永続化し、同ポートでの再起動を可能にする。
scripts/start-server.sh --project-dir /path/to/project --open

# 戻り値: {"type":"server-started","port":52341,
#           "url":"http://localhost:52341/?key=ab12…",
#           "screen_dir":"/path/to/project/.superpowers/brainstorm/12345-1706000000/content",
#           "state_dir":"/path/to/project/.superpowers/brainstorm/12345-1706000000/state"}
```

戻り値から `screen_dir` と `state_dir` を保存しておく。`--open` を付けると、最初の画面をpushした時点でブラウザが自動的に開く — ユーザーに開くよう頼む必要はないが、フォールバックとしてURLは共有しておく（ヘッドレス/リモート環境では自動で開かない）。

**URLにはセッションキー（`?key=…`）が含まれる。** サーバーはキーの無いリクエストを拒否するため、必ず `url` フィールドの**完全な**URLをユーザーに渡すこと — クエリ文字列を取り除いたり、`http://host:port` だけを渡したりしてはならない。このキーはHTTP・WebSocketアクセスをゲートしており、迷い込んだブラウザタブやネットワーク上の別マシンが画面を読んだりイベントを注入したりできないようにしている。初回ロード後はブラウザがCookie経由でキーを記憶するため、リロードや `/files/*` のアセット取得ではキーを繰り返す必要はない。

**接続情報の確認方法:** サーバーは起動時のJSONを `$STATE_DIR/server-info` に書き込む。バックグラウンドでサーバーを起動しstdoutを取得しそびれた場合は、このファイルを読んでURLとポートを取得する。`--project-dir` を使っている場合は `<project>/.superpowers/brainstorm/` 配下のセッションディレクトリを確認する。

**注記:** プロジェクトのルートを `--project-dir` として渡し、モックアップが `.superpowers/brainstorm/` に永続化されサーバー再起動後も残るようにする。指定しない場合、ファイルは `/tmp` に置かれ後で削除される。まだ `.gitignore` に `.superpowers/` が入っていなければ、追加するようユーザーに伝える。

**プラットフォーム別のサーバー起動方法:**

**Claude Code:**
```bash
# デフォルトモードで動作する — スクリプト自身がサーバーをバックグラウンド化する。
scripts/start-server.sh --project-dir /path/to/project --open
```

Windowsでは、スクリプトが自動検出してフォアグラウンドモードに切り替わる（ツール呼び出しをブロックする）。会話のターンをまたいでサーバーを生かしておくため、Bashツール呼び出しで `run_in_background: true` を使い、次のターンで `$STATE_DIR/server-info` を読んでURLとポートを取得する。

**Codex:**
```bash
# Codexはバックグラウンドプロセスを刈り取ってしまう。スクリプトはCODEX_CIを自動検出し
# フォアグラウンドモードに切り替わる。追加フラグ無しで通常通り実行すればよい。
scripts/start-server.sh --project-dir /path/to/project --open
```

**Copilot CLI:**
```bash
# --foreground を使い、bashツールを mode: "async" で起動してプロセスがターンをまたいで
# 生き残るようにする。後でやり取りする必要があれば、戻り値のshellIdを
# read_bash / stop_bash 用に保存しておく。
scripts/start-server.sh --project-dir /path/to/project --open --foreground
```

**その他の環境:** サーバーは会話のターンをまたいでバックグラウンドで動き続けなければならない。環境がデタッチされたプロセスを刈り取ってしまう場合は `--foreground` を使い、プラットフォームのバックグラウンド実行機構でコマンドを起動する。

URLがブラウザから到達不能な場合（リモート/コンテナ環境でよくある）、ループバック以外のホストにバインドする：

```bash
scripts/start-server.sh \
  --project-dir /path/to/project \
  --host 0.0.0.0 \
  --url-host localhost
```

返されるURL JSONにどのホスト名を出力するかは `--url-host` で制御する。

## ループの流れ

1. **サーバーが生きていることを確認**し、次に `screen_dir` 内の新規ファイルへ**HTMLを書き込む**：
   - **必須: URLに言及したり画面をpushしたりする前に、サーバーが生きていることを確認すること。** `$STATE_DIR/server-info` が存在し、`$STATE_DIR/server-stopped` が存在しないことを確認する。停止している場合は**同じ `--project-dir`** で `start-server.sh` を使って再起動する — 同じポートが再利用されるため、ユーザーの開いているタブは自動的に再接続され（サーバー停止中は「一時停止」オーバーレイが表示される）、新しいURLを送る必要はない。サーバーは4時間アイドルで自動終了する（`--idle-timeout-minutes` で設定可能）
   - 意味のあるファイル名を使う: `platform.html`, `visual-style.html`, `layout.html`
   - **ファイル名を使い回さない** — 画面ごとに新しいファイルを作る
   - ファイル作成ツールを使う — **cat/heredocは絶対に使わない**（ターミナルにノイズを吐き出す）
   - サーバーは自動的に最新のファイルを配信する

2. **ユーザーに何が起きるか伝え、ターンを終える:**
   - URLを再度伝える（初回だけでなく毎ステップ）
   - 画面上に何があるか簡潔に文章で要約する（例：「ホームページ用の3つのレイアウト案を表示しています」）
   - ターミナルで返答するよう頼む: 「見ていただいて、どう思うか教えてください。選びたい選択肢があればクリックしてください」

3. **次のターンで** — ユーザーがターミナルで返答した後：
   - `$STATE_DIR/events` が存在すれば読む — これにはユーザーのブラウザ操作（クリック、選択）がJSON行として記録されている
   - ユーザーのターミナルのテキストと合わせて全体像を把握する
   - ターミナルのメッセージが主たるフィードバックであり、`state_dir/events` は構造化されたインタラクションデータを提供する

4. **反復または前進** — フィードバックが現在の画面を変えるものなら、新しいファイルを書く（例：`layout-v2.html`）。現在のステップが検証されて初めて次の質問へ進む

5. **ターミナルへ戻る際はアンロードする** — 次のステップでブラウザが不要な場合（明確化の質問、トレードオフの議論など）、古い内容をクリアするため待機画面をpushする：

   ```html
   <!-- filename: waiting.html (or waiting-2.html, etc.) -->
   <div style="display:flex;align-items:center;justify-content:center;min-height:60vh">
     <p class="subtitle">ターミナルで続けます...</p>
   </div>
   ```

   これにより、会話が先へ進んでいるのにユーザーが解決済みの選択肢を見つめ続けることを防げる。次の視覚的な質問が出てきたら、通常通り新しいコンテンツファイルをpushする

6. 完了するまで繰り返す

## コンテンツフラグメントを書く

ページの中身となるコンテンツだけを書く。サーバーが自動的にフレームテンプレート（ヘッダー、テーマCSS、接続状態、あらゆるインタラクティブ基盤）でラップする。

**最小限の例:**

```html
<h2>Which layout works better?</h2>
<p class="subtitle">Consider readability and visual hierarchy</p>

<div class="options">
  <div class="option" data-choice="a" onclick="toggleSelect(this)">
    <div class="letter">A</div>
    <div class="content">
      <h3>Single Column</h3>
      <p>Clean, focused reading experience</p>
    </div>
  </div>
  <div class="option" data-choice="b" onclick="toggleSelect(this)">
    <div class="letter">B</div>
    <div class="content">
      <h3>Two Column</h3>
      <p>Sidebar navigation with main content</p>
    </div>
  </div>
</div>
```

これだけでよい。`<html>` もCSSも `<script>` タグも不要。サーバー側がすべて提供する。

## 利用可能なCSSクラス

フレームテンプレートは、コンテンツ向けに以下のCSSクラスを提供する：

### Options（A/B/C選択肢）

```html
<div class="options">
  <div class="option" data-choice="a" onclick="toggleSelect(this)">
    <div class="letter">A</div>
    <div class="content">
      <h3>Title</h3>
      <p>Description</p>
    </div>
  </div>
</div>
```

**複数選択:** コンテナに `data-multiselect` を追加すると、ユーザーが複数の選択肢を選べるようになる。クリックのたびに項目の選択スタイルがトグルされる。

```html
<div class="options" data-multiselect>
  <!-- 選択肢のマークアップは同じ — ユーザーは複数を選択/解除できる -->
</div>
```

### Cards（ビジュアルデザイン）

```html
<div class="cards">
  <div class="card" data-choice="design1" onclick="toggleSelect(this)">
    <div class="card-image"><!-- mockup content --></div>
    <div class="card-body">
      <h3>Name</h3>
      <p>Description</p>
    </div>
  </div>
</div>
```

### Mockup container（モックアップ枠）

```html
<div class="mockup">
  <div class="mockup-header">Preview: Dashboard Layout</div>
  <div class="mockup-body"><!-- モックアップのHTML --></div>
</div>
```

### Split view（左右並列表示）

```html
<div class="split">
  <div class="mockup"><!-- 左 --></div>
  <div class="mockup"><!-- 右 --></div>
</div>
```

### Pros/Cons（長所/短所）

```html
<div class="pros-cons">
  <div class="pros"><h4>Pros</h4><ul><li>Benefit</li></ul></div>
  <div class="cons"><h4>Cons</h4><ul><li>Drawback</li></ul></div>
</div>
```

### Mock elements（ワイヤーフレームの構成要素）

```html
<div class="mock-nav">Logo | Home | About | Contact</div>
<div style="display: flex;">
  <div class="mock-sidebar">Navigation</div>
  <div class="mock-content">Main content area</div>
</div>
<button class="mock-button">Action Button</button>
<input class="mock-input" placeholder="Input field">
<div class="placeholder">Placeholder area</div>
```

### タイポグラフィとセクション

- `h2` — ページタイトル
- `h3` — セクション見出し
- `.subtitle` — タイトル下のサブテキスト
- `.section` — 下マージン付きのコンテンツブロック
- `.label` — 小さな大文字ラベルテキスト

## ブラウザイベントのフォーマット

ユーザーがブラウザで選択肢をクリックすると、その操作は `$STATE_DIR/events` に記録される（1行1JSONオブジェクト）。新しい画面をpushすると、このファイルは自動的にクリアされる。

```jsonl
{"type":"click","choice":"a","text":"Option A - Simple Layout","timestamp":1706000101}
{"type":"click","choice":"c","text":"Option C - Complex Grid","timestamp":1706000108}
{"type":"click","choice":"b","text":"Option B - Hybrid","timestamp":1706000115}
```

イベントストリーム全体からユーザーの検討経路が分かる — 決めるまでに複数の選択肢をクリックすることがある。最後の `choice` イベントが通常は最終選択だが、クリックのパターンから迷いや、確認する価値のある好みが見えてくることもある。

`$STATE_DIR/events` が存在しない場合、ユーザーはブラウザを操作しなかったということなので、ターミナルのテキストのみを使う。

## デザインのコツ

- **質問に応じて忠実度をスケールする** — レイアウトの質問ならワイヤーフレーム、磨き込みの質問なら仕上げた見た目で
- **各ページで質問内容を説明する** — 単に「選んでください」ではなく「どちらのレイアウトの方がプロフェッショナルに感じますか？」のように
- **前進する前に反復する** — フィードバックが現在の画面を変えるものなら、新しいバージョンを書く
- 1画面あたり**最大2〜4個**の選択肢まで
- **重要な場面では実際のコンテンツを使う** — 写真ポートフォリオなら実際の画像（Unsplashなど）を使う。プレースホルダーはデザイン上の問題を覆い隠してしまう
- **モックアップはシンプルに保つ** — ピクセルパーフェクトなデザインではなく、レイアウトと構造に焦点を当てる

## ファイルの命名

- 意味の分かる名前を使う: `platform.html`, `visual-style.html`, `layout.html`
- ファイル名を使い回さない — 画面ごとに新しいファイルにする
- 反復の場合: `layout-v2.html`, `layout-v3.html` のようにバージョンサフィックスを付ける
- サーバーは更新日時が最新のファイルを配信する

## クリーンアップ

```bash
scripts/stop-server.sh $SESSION_DIR
```

セッションが `--project-dir` を使っていた場合、モックアップファイルは後で参照できるよう `.superpowers/brainstorm/` に残る。`/tmp` セッションのみ停止時に削除される。

## 参考

- フレームテンプレート（CSSリファレンス）: `scripts/frame-template.html`
- ヘルパースクリプト（クライアント側）: `scripts/helper.js`

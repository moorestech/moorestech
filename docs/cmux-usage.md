# cmux 使い方メモ

cmux は `tmux` 風に、`window` / `workspace` / `pane` / `surface` を CLI から操作できる環境です。cmux の terminal では `CMUX_WORKSPACE_ID` や `CMUX_SURFACE_ID` が自動設定されるため、多くのコマンドは対象を省略すると現在の workspace / surface に対して実行されます。

## 基本構造
- `window`: cmux のトップレベルウィンドウ
- `workspace`: 作業単位。タブやプロジェクトに近い
- `pane`: 分割された表示領域
- `surface`: pane の中身。`terminal` / `browser` / `agent-session` など

対象指定には短い ref を使えます。UUID も使えますが、通常の確認や操作では短い ref で十分です。
```bash
window:1
workspace:8
pane:55
surface:129
```

## 状況確認
現在フォーカスされている `window` / `workspace` / `pane` / `surface` を確認します。
```bash
cmux identify --no-caller
```

現在の workspace を表示します。
```bash
cmux current-workspace
```

window / workspace / pane / surface の階層を一覧します。ペイン位置や tty を確認する時に一番便利です。

```bash
cmux tree
cmux tree --all
```

pane や surface を個別に確認します。

```bash
cmux list-panes
cmux list-panels
cmux list-pane-surfaces --pane pane:55
```

cmux 内のプロセス、CPU、メモリ、surface 対応を確認します。

```bash
cmux top --workspace workspace:8 --processes --flat --format tsv
```

## 画面を読む
指定 surface の現在画面を読みます。対象ペインに入力しないため、安全に状態確認できます。
```bash
cmux read-screen --surface surface:129 --lines 80
cmux read-screen --surface surface:129 --scrollback --lines 200
```

tmux 互換名でも読めます。

```bash
cmux capture-pane --surface surface:129 --lines 80
```

## 入力を送る
通常テキストや Enter などのキーを送ります。誤操作を避けるため、入力前に `read-screen` で対象が入力待ちか確認してください。
```bash
cmux send --surface surface:129 "echo hello"
cmux send-key --surface surface:129 Enter
```

panel 指定で送る場合は `send-panel` / `send-key-panel` を使います。

```bash
cmux send-panel --panel pane:55 "pwd"
cmux send-key-panel --panel pane:55 Enter
```

## フォーカス操作

指定 pane / panel / window にフォーカスします。

```bash
cmux focus-pane --pane pane:55
cmux focus-panel --panel pane:55
cmux focus-window --window window:1
```

## 分割と作成

現在 workspace に右分割の terminal pane を作ります。

```bash
cmux new-pane --type terminal --direction right
```

tmux 風に右分割します。

```bash
cmux new-split right
```

指定 pane に terminal surface や browser surface を追加します。

```bash
cmux new-surface --type terminal --pane pane:55
cmux new-surface --type browser --pane pane:55 --url https://example.com
```

## ワークスペース操作

workspace 一覧と現在 workspace を確認します。

```bash
cmux list-workspaces
cmux current-workspace
```

workspace を作成、選択、リネームします。

```bash
cmux new-workspace --name "作業名" --cwd /Users/katsumi/moorestech
cmux select-workspace --workspace workspace:8
cmux rename-workspace --workspace workspace:8 "新しい名前"
```

## ブラウザ操作

ブラウザ split を開き、状態確認やスクリーンショット取得を行います。

```bash
cmux browser open https://example.com
cmux browser snapshot
cmux browser get url
cmux browser get title
cmux browser screenshot --out /tmp/cmux-shot.png
```

## diff / Markdown 表示

未ステージ差分、patch、Markdown を cmux 上で表示します。

```bash
cmux diff --source unstaged --cwd /Users/katsumi/moorestech
cmux diff patch.diff
cmux markdown open README.md
```

## 設定とドキュメント

公式ドキュメントや raw resource の場所を表示します。

```bash
cmux docs api
cmux docs agents
cmux docs shortcuts
cmux docs settings
```

設定ファイルの場所を確認します。

```bash
cmux settings path
cmux config paths
```

Ghostty config と cmux config を再読み込みします。アプリ再起動は不要です。

```bash
cmux reload-config
```

## 右隣ペインを確認する手順

右隣ペインのような「周辺 pane を読むだけ」の用途では、次の流れが安全です。

1. `pwd` で現在の worktree を確認する。
2. `cmux identify --no-caller` で自分の `pane` / `surface` を確認する。
3. `cmux tree` で同じ workspace 内の隣接 pane と surface を特定する。
4. `cmux read-screen --surface surface:<id> --lines 80` で対象画面を読む。
5. 入力が必要な場合だけ、対象を再確認してから `cmux send` / `cmux send-key` を使う。

実際に使った確認コマンド例です。

```bash
pwd
cmux identify --no-caller
cmux current-workspace
cmux list-panes
cmux list-panels
cmux tree
cmux read-screen --surface surface:129 --lines 80
cmux read-screen --surface surface:135 --lines 80
cmux read-screen --surface surface:139 --lines 80
cmux top --workspace workspace:8 --processes --flat --format tsv
```

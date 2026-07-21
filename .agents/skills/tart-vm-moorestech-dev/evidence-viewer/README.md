# Evidence Viewer

任意ディレクトリ配下の画像・動画（evidence）をブラウザで一覧・再生するための、小さく再利用可能な Vite + React ビューア。

## 使い方

初回のみビルド:

```bash
pnpm install && pnpm build
```

サーバー起動（`--dir` は evidence を置いた絶対パス、`--port` は任意）:

```bash
node server.mjs --dir /absolute/path/to/evidence --port 4970
```

外部公開用トンネル（必要な場合）:

```bash
cloudflared tunnel --url http://localhost:4970
```

`/api/list` が `--dir` 配下（サブディレクトリ再帰）の画像・動画一覧を返し、`/media/<相対パス>` が Range 対応でファイルをストリーム配信する。動画の途中シークに対応している。

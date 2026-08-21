# vendored assets

`digest.html` は `file://` で開ける自己完結HTMLであり、CDNを参照しない（ADR 0018 決定3）。
そのため highlight.js とそのテーマをここへ固定バージョンで置き、ビルド時に digest.html へインライン展開する。

| ファイル | 取得元 |
|---|---|
| `highlight.min.js` | https://cdn.jsdelivr.net/npm/@highlightjs/cdn-assets@11.11.1/highlight.min.js |
| `github.min.css` | https://cdn.jsdelivr.net/npm/@highlightjs/cdn-assets@11.11.1/styles/github.min.css |
| `github-dark.min.css` | https://cdn.jsdelivr.net/npm/@highlightjs/cdn-assets@11.11.1/styles/github-dark.min.css |

バージョンを上げるときは3ファイルを同じバージョンで差し替え、`python3 -m pytest tests/` を通してから
`tests/golden/pr-1155-digest.expected.html` を再生成する。

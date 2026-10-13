# PR報告ページ（review.moores.tech/pr/<N>/report/）

PRの成果物（画像・動画・検証結果）は、PRごとに1枚の報告ページへまとめ、PR本文の先頭行にそのURLを1本だけh1見出しで張る（`# https://review.moores.tech/pr/<N>/report/`。リンク文字列で包まない）。
ページの書式は `scripts/pr-report/report_html.py` だけが決める。PRごとに独自のHTMLを書かない。

## 置き場と見え方

- 公開URL: `https://review.moores.tech/pr/<PR番号>/report/`（裁定サイトと同じ Cloudflare Access 配下。公開PRから張っても外部の人には見えない）
- 実体: mac-mini の `~/hermes-agent/data/services/pr-review/reports/pr-<PR番号>/`。git には入れない
- 公開は mac-mini で実行する。同じPRへ再実行すると丸ごと差し替わる（push で成果物が変わったら再公開する）

## 載せるもの

- タスク中に撮った画像・動画は**全部**載せる。取捨選択で落とさない
  - unityプレイ録画テストを実行したら `recording.mp4` は必須
  - Web UI・ゲーム内UIなど見た目が変わる変更で素材が無い・古い場合は、撮ってから載せる
- 素材が本当に無い変更（ロジックのみ・ドキュメントのみ等）だけ `no_media_reason` に理由を書く
- 秘密情報・一時的なデバッグ表示が映ったものは撮り直す

## manifest.json

PR作業のscratch（コミットしない場所）に置く。`file` は絶対パスか manifest からの相対パス。

```json
{
  "head": "<PRブランチのコミットSHA先頭10桁>",
  "summary": ["何を変えたか（1行ずつ）"],
  "media": [
    {"file": "/abs/path/recording.mp4", "caption": "設置→Escで終了する一連", "group": "unityプレイ録画テスト"},
    {"file": "shots/japanese-PlaceBlock.png", "caption": "日本語 / 設置モード", "group": "スクリーンショット"}
  ],
  "verification": ["Unityコンパイル: エラー0", "unityプレイ録画テスト: 73/73アサーション成功"],
  "notes": ["任意。未解決・裁定事項など"]
}
```

- `summary`・`verification` は空でない文字列配列（必須）
- `media` の拡張子は png / jpg / jpeg / gif / webp / mp4 / webm。0 byte は拒否される
- `group` はページ内の見出し。manifest に書いた順で並ぶ
- `media` が空なら `no_media_reason` が必須

## 公開

```bash
python3 scripts/pr-report/publish_pr_report.py <PR番号> <manifest.json>
```

成功すると標準出力に公開URL、標準エラーに「画像 N 枚 / 動画 M 本」が出る。公開後に裁定サイトから全ファイルを取り直して確かめ、取れないものがあれば失敗で終わる。

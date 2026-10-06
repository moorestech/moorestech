# プレイテストダッシュボード

`moorestech_logs/harness/playtest/`（プレイ報告・進行記録・日次ダイジェスト）と `harness/bug-report/runs/`（自動修正ラン）を読み、
ブラウザで一覧・絞り込み・詳細確認できるようにする読み取り専用の Web 画面。標準ライブラリのみ（venv 不要）。

公開先は `https://review.moores.tech/playtest/`。裁定サイトと同じ named tunnel の ingress に `path: ^/playtest` の行を足して
`127.0.0.1:8932` へ通し、Cloudflare Access（review.moores.tech のアプリ）で本人以外を弾く。
テスターの SteamID・説明文・録画を出すため、Access の無い経路で公開しない（ADR 0061「閲覧は本人とエージェントのみ」）。

## 画面

| 画面 | 内容 |
|---|---|
| 概要 | 未投入のバグ報告・報告数・テスター数・セッション数、日別の報告数（種別の積み上げ）とプレイ時間、最新の未投入バグと感想、離脱時のUI状態 |
| 報告 | 全報告を新しい順に。種別・投入状態・テスター・ビルド・文字列で絞り込み（条件は URL に残る） |
| 報告詳細 | 説明全文・動画（シーク可）・スクショ・unity.log・manifest.json・ビルド/コミット・遠隔実行の状態・修正ランの結果。未投入なら `enqueue-autofix.sh` のコピー |
| テスター | 1人1行で最終活動・セッション数・プレイ時間・到達チャレンジ（最奥の名前）・研究数・報告数 |
| 進行 | チャレンジ到達ファネル（マスタの定義順・到達人数）とセッション一覧。テスターで絞り込み |
| ダイジェスト | `digests/<日付>.md` を Discord の 1800 文字切り詰め無しで表示 |

## 判定規則（ダイジェストと揃える）

- 投入状態はダイジェストの投入候補と同じ順で決める: バグ以外 → 遠隔実行あり/不明（除外）→ `AUTOFIX_QUEUED` あり（投入済み）→ 未投入
- 進行記録の集計は `remoteExec === false` のセッションだけ。キーの無い旧版の記録（不明）も有効と同じく外す
- 読めない箱は一覧から消さず「読めない箱」として理由付きで出す（無音で落とさない）
- **投入はしない。** 投入の判断は人が持つ（ADR 0061）ので、画面は貼れるコマンドを出すだけ

## 構成

```
server.py            HTTP サーバー（127.0.0.1 待受・GET のみ・CSP で自オリジンのスクリプトに限定）
collect_reports.py   プレイ報告と自動修正ランの読み取り（digest_schema の型検証を再利用）
collect_progress.py  進行記録の読み取り
master_names.py      チャレンジ・研究の GUID → 表示名（moorestech_master の v8 mod）
media.py             スクショ・動画・ログの許可リスト配信（Range 対応・safe_segment で検証）
static/              index.html・CSS・ES modules（views/ が画面ごと）
```

## 起動

```bash
python3 scripts/playtest/dashboard/server.py            # 既定: --port 8932、--logs と --master は repo の兄弟から導出
python3 scripts/playtest/tests/test_dashboard.py        # テスト
```

Mac mini では always-on supervisor の `playtest-dashboard`（longrun）がメインクローンから起動する。
repo-auto-pull で master が進むと静的ファイルは即反映、Python 側はプロセス再起動で反映される。

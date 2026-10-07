# プレイテストダッシュボード

`moorestech_logs/harness/playtest/`（プレイ報告・進行記録・日次ダイジェスト）と `harness/bug-report/runs/`（自動修正ラン）を読み、
ブラウザで一覧・絞り込み・詳細確認できるようにする Web 画面。標準ライブラリのみ（venv 不要）。
報告データは読むだけで、書き込むのは人が付ける状態（報告の既読と関連チケットのリンク）だけ。

公開先は `https://review.moores.tech/playtest/`。裁定サイトと同じ named tunnel の ingress に `path: ^/playtest` の行を足して
`127.0.0.1:8932` へ通し、Cloudflare Access（review.moores.tech のアプリ）で本人以外を弾く。
テスターの SteamID・説明文・録画を出すため、Access の無い経路で公開しない（ADR 0061「閲覧は本人とエージェントのみ」）。

## 画面

| 画面 | 内容 |
|---|---|
| 概要 | 未投入のバグ報告・報告数・テスター数・セッション数、日別の報告数（種別の積み上げ）とプレイ時間、最新の未投入バグと感想、離脱時のUI状態 |
| 報告 | 全報告を新しい順に。種別・投入状態・テスター・ビルド・文字列で絞り込み（条件は URL に残り、最後の条件はブラウザに記憶して「報告」タブで戻る） |
| 報告詳細 | 一覧の条件を引き継ぎ、前後・一覧へ戻るも同じ条件の中で辿る。説明全文・動画（シーク可）・スクショ・unity.log・manifest.json・ビルド/コミット・遠隔実行の状態・修正ランの結果。未投入なら `enqueue-autofix.sh` のコピー |
| テスター | 1人1行で最終活動・セッション数・プレイ時間・到達チャレンジ（最奥の名前）・研究数・報告数 |
| 進行 | チャレンジ到達ファネル（マスタの定義順・到達人数）とセッション一覧。テスターで絞り込み |
| ダイジェスト | `digests/<日付>.md` を Discord の 1800 文字切り詰め無しで表示。感想全文が混ざるので Markdown として解釈せず素の文章で出し、コピー用ボタンも出さない（感想本文から本物そっくりのコマンドを偽装できるため） |

## 既読と関連チケット

- 既読は報告ごとに**手動で**付け外しする（開いただけでは既読にしない）。一覧の行頭の丸、詳細の「既読にする」、一覧の「表示中の未読N件を既読にする」
- 関連チケット（Notion・GitHub 等の https URL）は報告詳細で紐付けるか、Mac mini で次を叩く。成功するとチケット側へ貼るビューワー URL を出す

```bash
scripts/playtest/link-ticket.sh <steamId> <reportId> <チケットURL> <チケット名>   # 紐付け（同じURLは題名だけ更新）
scripts/playtest/link-ticket.sh --remove <steamId> <reportId> <チケットURL>        # 解除
```

- エージェント（Hermes 等）がチケットを起票するときは、関連する報告ごとに (1) チケット本文へビューワー URL `https://review.moores.tech/playtest/#/report/<steamId>/<reportId>` を書き、(2) `link-ticket.sh` で紐付ける。関連する報告は `curl -s -H 'Host: 127.0.0.1:8932' http://127.0.0.1:8932/playtest/api/data` の `reports[]`（`description`・`boxSteamId`・`boxId`）から探す
- 保存先は `moorestech_logs/.state/playtest-dashboard-state.json`（git 管理外）。報告箱の中に置くとテスターが同名ファイルを送って既読を偽装でき、git 管理下に置くとログ同期の rebase 中に記録が消えるため。書き手はダッシュボードのサーバー1プロセスだけで、`link-ticket.sh` もサーバー経由で書く。git の履歴には残らない（ディスクが壊れたら既読とリンクは失われる）
- 書き込み API（`POST /playtest/api/read` は `{"items":[{"steamId","id"}...],"read":bool}` で複数件を全件か0件で、`/api/links/add`・`/api/links/remove` は1件ずつ。紐付いていない URL の解除は 404）は JSON 本文と `X-Playtest-Dashboard: 1` ヘッダを必須にし、Origin があれば許可ホストと照合する（Access の内側でも別サイトからの送信を弾くため）。壊れた状態ファイルは上書きせず 500 を返す

## 判定規則（ダイジェストと揃える）

- 投入状態はダイジェストの投入候補と同じ順で決める: バグ以外 → 遠隔実行あり/不明（除外）→ `AUTOFIX_QUEUED` あり（投入済み）→ 未投入
- 進行記録の集計は `remoteExec === false` のセッションだけ。キーの無い旧版の記録（不明）も有効と同じく外す
- 読めない箱（ingest.json・manifest.json が壊れている・無い）は一覧から消さず「読めない箱」として理由付きで出し、投入対象にしない
- 判定は digest_schema の契約どおりに行い、表示だけに使う項目（tick・commit 等）は `display_fields.py` で1項目ずつ緩く読む（表示用の1項目の型違いで箱ごと落とさない）
- 日別のプレイ時間はプレイした日（`sessionStart` の JST 日付）で切る。ダイジェストは受信日（`readyAt`）で切るので、日ごとの数字は一致しない
- 公開ホスト名・`127.0.0.1:<port>`・`localhost:<port>` 以外の Host は 421 で拒否する（ローカルのブラウザ経由の DNS リバインディングで Access を迂回させない）
- **投入はしない。** 投入の判断は人が持つ（ADR 0061）ので、画面は貼れるコマンドを出すだけ

## 構成

```
server.py            HTTP サーバー（127.0.0.1 待受・GET のみ・CSP で自オリジンのスクリプトに限定）
collect_reports.py   プレイ報告と自動修正ランの読み取り（digest_schema の型検証を再利用）
collect_progress.py  進行記録の読み取り
master_names.py      チャレンジ・研究の GUID → 表示名（moorestech_master の v8 mod）
media.py             スクショ・動画・ログの許可リスト配信（Range 対応・safe_segment で検証）
display_fields.py    表示用項目の緩い読み取り（型違いはログを出して空表示）
security_headers.py  全応答に付ける CSP・nosniff
dashboard_state.py   既読・チケットリンクの保存（1ファイル・プロセス内ロック・一時ファイルから置き換え）
write_api.py         既読・チケットリンクを書き換える POST の受け口（CSRF 対策・報告キー検証）
static/              index.html・CSS・ES modules（views/ が画面ごと）
```

## 起動

```bash
python3 scripts/playtest/dashboard/server.py            # 既定: --port 8932、--logs と --master は repo の兄弟から導出
python3 scripts/playtest/tests/test_dashboard.py        # テスト（読み取り）
python3 scripts/playtest/tests/test_dashboard_state.py  # テスト（既読・チケットリンクの書き込み）
```

Mac mini では always-on supervisor の `playtest-dashboard`（longrun）がメインクローンから起動する。
repo-auto-pull で master が進むと静的ファイルは即反映、Python 側はプロセス再起動で反映される。

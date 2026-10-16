## Requirements

1. 受け口は機材名つきの開発者トークンを発行・一覧・失効できる。発行・一覧・失効は管理キーでだけ通る。
   - 受け入れ: vitest で、発行→交換→アップロード→失効後の交換401 が通る。管理キーが無いと401。
2. 開発者トークンを交換すると、Steam のセッションと同じ形の応答（steamId=`dev-<機材名>`, allowed, token, expiresAt）が返る。その token で上げた箱は `reports/dev-<機材名>/<id>/` に置かれる。
   - 受け入れ: vitest。
3. 受け口は `dev-` の sub からの進行記録（kind=progress）の prepare・complete を 403 で拒否し、理由を warn に出す。また `dev-` の sub の prepare・complete では毎回台帳を確かめ、失効済みの機材なら 401 `developer-token-revoked` にする（失効を即時に効かせる）。
   - 受け入れ: vitest（失効直後に、発行済みセッションでの prepare が 401）。
4. Mac mini から `scripts/playtest/dev-token.sh issue|list|revoke <機材名>` で開発者トークンを扱える。取り込みは `dev-*` の Steam 表示名を Steam に問い合わせず「開発機 <機材名>」とし、ダイジェストにはそれが出る。
   - 受け入れ: bash テスト。
5. 開発者トークンは Unity のエディタ拡張（メニュー `moorestech/Playtest/Developer Token`）で貼り付け・保存・削除・接続確認ができる。保存先は機材ローカルのファイル `<GameSystemDirectory>/PlaytestReceiver/developer-token.txt` で、Editor と開発ビルドが同じファイルを読む。
   - 受け入れ: EditMode テストで保存・読込・削除の往復。接続確認は実機で確かめる。
6. 送信経路は次のとおり1か所で決める。
   - 配布版 → Steam 認証で全 outbox。
   - Editor・開発ビルドでトークンが読めた → 開発者トークン認証で、報告用 outbox（バグ報告・感想・異常終了）だけ。進行記録は送らない。
   - それ以外 → 送らず「ローカル保存のみ」とし、理由を状態に載せる。
   - 受け入れ: EditMode テストで3経路と、進行記録が送られないこと。
7. 溜まっている箱は、トークン設定後の MainGame 起動時と報告送信時に送られる。再生停止で中断した送信は、次の再生で再送される。
   - 受け入れ: EditMode テストで起動時の押し出し1回。実機で、溜まっていた箱が届くこと。
8. バグ報告画面に送信状況を1行出す。表示は「ローカル保存のみ（理由）＋件数」「送信待ち N 件」「受け口に拒否された（トークン失効の可能性）＋件数」「すべて送信済み」のどれか。4言語の文言をそろえる。
   - 受け入れ: webui vitest、C# の DTO テスト、unityプレイ録画テストで目視。
9. Editor で報告を取るとき、moorestech と moorestech_master の未push コミットを `repo/commits.bundle`・`repo/master-commits.bundle` にし、状態を `repo/bundle-status.txt` へ書く。書式は従来の運搬スクリプトと同じ `<label>: <state>`。失敗は manifest の missing にも残す。
   - 受け入れ: EditMode テスト（一時 git repo で created / not-needed / skipped-no-commit）。prepare-run.sh の既存テストが通る。
10. 報告の確保時に、今回の再生中に書かれた列車同期の停滞診断（`stall-*.json`、新しい順に最大8件）を `logs/train-sync/` へ入れる。
    - 受け入れ: EditMode テスト。
11. `scripts/bugreport/triage.py <箱>` が Unity 無しで次を出す: manifest 要約、Error と Warning の行、`[TrainSynchronization]` の行、列車ごとの tick 時系列（自動運転・速度・時刻表・位置）、レールの有向辺一覧、動画のコンタクトシート。
    - 受け入れ: python テスト（合成した箱）と、実際の箱での手動実行。
12. `scripts/playtest/find-reports.sh` が、受け口の未取り込み・取り込み済みの報告（新しい順）・自動修正の inbox とランを1回で並べる。
    - 受け入れ: bash テスト（偽の受け口と偽のディレクトリ）。
13. 運搬スクリプト一式（`ship-outbox.sh`・launchd plist・そのテスト）を削除し、それを前提にした記述（README・コメント・bug-report-auto-fix スキル・ADR 0057/0061 の状態行）を新経路に合わせる。
    - 受け入れ: `git grep -n "ship-outbox\|bugreport-shipper\|rsync path"` が履歴文書（docs/superpowers/plans/ 配下の旧plan）以外で0件。
14. 実機で確かめる。
    - Mac mini の作業 worktree の Editor と、winbuild（Windows）の Editor の両方で、トークン設定→報告送信→Mac mini の取り込み（`reports/dev-<機材名>/<id>/`）まで通す。
    - Mac では bundle-status.txt と commits.bundle の中身も確かめる。
    - 受け入れ: 取り込み済みディレクトリのパスとファイル一覧を報告に残す。

**やらないこと（スコープ境界）:**
- 配布版の Steam 認証経路の変更。
- タイトルゲート（同意・前回異常終了）の挙動変更。開発者経路の押し場は、MainGame 起動と報告送信の2つにする。
- 開発者報告の自動修正への自動投入（裁定: 人が投入する）。
- Steam 表示名キャッシュの掃除。

## Global Constraints

- 受け口の送り先は本番 `https://playtest.moores.tech`。staging は使わない。
- 開発者トークン経路の選択（`PlaytestDeliveryRouteResolver.Resolve` の DeveloperMode 分岐）は `#if UNITY_EDITOR || DEVELOPMENT_BUILD` で閉じる。配布ビルドではこの分岐が消え、開発者トークンを読まない。経路の型そのものは #if で囲まない（テストから使えるように）（前例: `PlaytestReceiverConfig.BaseUrl` の環境変数上書き）。
- 開発者トークンの形は `mdt_` + 小文字16進64桁。機材名は `^[a-z0-9][a-z0-9-]{0,31}$`。セッションの sub は `dev-<機材名>`。
- R2 の台帳キー:
  - `devtokens/by-hash/<sha256hex>` → `{"machine": "...", "createdAt": "<ISO>"}`
  - `devtokens/by-machine/<machine>` → `{"hash": "<sha256hex>", "createdAt": "<ISO>"}`
  - トークンの生値は R2 に置かない。
- 1ファイル200行未満。1ディレクトリ10ファイルまで。partial・`Func<>`・デフォルト引数・単純 getter/setter は禁止。主要処理に日英2行コメントを付ける。
- try-catch は外部境界（プロセス起動・ネットワーク・外部入力パース・ディスクIO）だけ。catch した失敗はログと manifest の Missing の両方に残す。
- 無音の縮退は禁止。送らない・拒否する経路は理由をログに出し、バグ報告画面の送信状況にも出す。
- `.meta` を手で作らない。Prefab やシーンを手で編集しない。
- .cs を変えたら `uloop compile --project-path ./moorestech_client` を通す。
- 常駐サービス・wrangler・launchd を触る前に `~/hermes-agent/data/HERMES-OPS.md` を読む。秘密値の env ファイルは `>` で作り直さない。

## 判断記録（ADR）

- 設計 ADR: `docs/adr/0081-developer-bug-reports-via-receiver-with-per-machine-tokens.md`。裁定は `.decisions/2026-10-16-開発者*.md` の5本。
- 出所: ユーザー裁定 2026-10-16「CFへ一本化しrsync廃止」「開発者トークンを機材ごとに発行」＋原文「エディタ拡張でセットできるようにして」「テスター報告と同じく人が選ぶ」、過去引用「それで」（提示7点の逐語原文は未保全のため引用単独の含意検査不能。現在の採用根拠は `.decisions/2026-10-16-開発者報告の実装planを共通制約として無人実行する.md`）、「Mac・Windows両方」「次回再生時に再送」。
- 出所: シミュレーター予測（確信高・拒否権つき前提宣言）2026-10-16。異常終了箱も開発者トークンで送る（Requirements 6）。
- 出所: agent前提（winbuild に Unity 6000.3.8f1 と `C:\dev\moorestech` が実在。判事が ssh で実測）。Windows の確認は winbuild の Editor のバッチモードで、[Explicit] テストを使って行う。GUI 操作は ssh 越しに難しいので、画面の確認は Mac の録画で行う。
- 出所: agent前提（ADR 0070 で manifest の steamId はローカルの値）。開発機の manifest の steamId は空のままにし、識別は受け口の置き場が担う。
- 出所: agent前提（git の実測: 生 SHA の `git bundle create` は「Refusing to create empty bundle」）。一時 ref `refs/bugreport/<id>` を使う。`RepositoryStateProbe` の「書き換えない」規則の例外として明記する。
- 出所: agent前提（前例 `Client.RemoteExec/Access/RemoteExecAccessFile.cs`）。トークンは EditorPrefs ではなく GameSystemPaths 配下のファイルに置く。開発ビルドも同じファイルを読める。
- 出所: agent前提。開発機の押し場は「MainGame 起動」と「報告送信」の2つ。タイトルゲートの `uploadsEnabled` は配布版の同意ゲートなので変えない。
- 出所: agent前提。送信状況は既存の `bugReport`（送信可否）とは別のフィールド・別の行にする。送信可否と送信状況は独立に変わるため。
- 出所: ユーザー裁定 2026-10-16 選択「失効を即時に効かせる」。`dev-` の prepare・complete ごとに台帳を head し、無ければ 401 にする。
- 出所: ユーザー裁定 2026-10-16 選択「置いたままにする」。Mac mini の開発者トークン（機材名 macmini）は検証後も常設する。
- 出所: agent前提。`BugReport/` 直下が10ファイルを超えているので、新設の bundle 処理は `BugReport/Repository/` に置き、既存の repository 系2ファイルも移す。
- unityプレイ録画テスト: 含める（Task 10 Step 3）。バグ報告画面の表示変化（ローカル保存のみ→送信待ち→すべて送信済み）はランタイムの UI なので録画で確かめる。
- 出所: シミュレーター予測（確信高・拒否権つき前提宣言）2026-10-16。Phase 2.6 強: `PlaytestCredentialPost`・`PlaytestDeveloperTokenRead`・`PlaytestDeliveryRoute`・`PlaytestDeliveryStatus` を sealed 派生の判別 union にする／`Resolve` は評価済みの `bool isDistribution` を受ける（NotEvaluated の例外を消す）／受け口の `refusesDeveloperProgress` は `PlaytestKind` 型を受ける。
- 出所: 検査7（plan と裁定の食い違い）。裁定「送信待ちと送信完了を区別して見せる」が優先なので、`allSent` も「すべての報告を送信しました」を1行出す（質問せず plan を裁定へ合わせた）。
- 出所: ユーザー裁定 2026-10-16 質問「送信状況の置き場は読む口と書く口を別の interface に分けるか」→ 選択「このままでよい」（書く口は internal で限定）。
- 出所: ユーザー裁定 2026-10-16 質問「弱い指摘11件のうち推奨4件だけ反映でよいか」→ 選択「推奨の4件だけ拾う」。①走行結果を `PlaytestUploadRunEnd` の種類で返す ②トークン一覧を readonly で返す ③16進変換と R2 の JSON 読みを1関数ずつにする ④制約文を「経路の選択を #if で閉じる」に直す。残り7件（`dev-` 接頭辞の文字列判定、`Pending(0)` 初期値、主スレッド前提、押し場3口、契約型の置き場、受け口の dev 分岐、bash の `dev-*`）はこのまま。


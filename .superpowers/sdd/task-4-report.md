# Task 4 報告: 管理API（inbox 列挙・取得・ack、許可リスト GET/PUT）と README

## 実装したこと

- `src/routes/admin.ts`（新規、154行）: 管理API（`/v1/inbox`・`/v1/inbox/{kind}/{steamId}/{id}/...`・`/v1/inbox/{kind}/{steamId}/{id}/ack`・`/v1/allowlist`）の経路一致・ディスパッチを担う `routeAdmin(request, env, segments)` と、5つのハンドラ `getInbox`・`getInboxObject`・`postAck`・`getAllowlist`・`putAllowlist` をエクスポート。
  - ブリーフは `src/admin.ts` を指定していたが、派遣プロンプトのコンテキスト（上書き事項1）に従い `src/routes/admin.ts` に配置し、既存の `routeSession`（`routes/session.ts`）・`routeUploads`（`routes/uploads.ts`）と同型（`routeXxx(request, env, segments) -> Response | null`）にした。
  - Task 1〜3 の `requireAdmin`・`bundlePrefix`・`pendingIndexKey`・`parsePendingIndexKey`・`isKind`・`isSafeSegment`・`joinSafePath`・`READY_MARKER`（未使用）・`ACKED_MARKER`・`readAllowlist`・`writeAllowlist` をそのまま再利用。新規実装なし。
- `src/index.ts`: `import { routeAdmin } from "./routes/admin";` を追加し、`routeUploads` の直後に
  ```ts
  const admin = await routeAdmin(request, env, segments);
  if (admin !== null) return admin;
  ```
  の2行を足しただけ。`handle()` は薄いディスパッチャのまま。
- `test/admin.test.ts`（新規、158行）: ブリーフ Step 1 のテストをベースに、9ケース。
  - ブリーフの `upload()` ヘルパーには `content-length` ヘッダが無く、実際の `putUpload` は `content-length` 必須（411で拒否）のためそのままでは全アップロードが失敗する。ヘルパーに `"content-length": String(body.length)` を追加して修正済み（ブリーフのコード欠陥。既存の `test/support/uploadsFixture.ts` の `clean`・`noNetwork`・`workerEnv` を再利用し重複させないようにした）。
  - 派遣プロンプトのコンテキスト（上書き事項4）に従い、拒否経路4種（admin key欠落401・admin key不一致401・不正kind 400・inbox個別ファイル不在404・allowlist PUT不正body 400×2パターン）すべてに `vi.spyOn(console, "warn")` を追加し発火を検証。
- `README.md`（新規）: ブリーフ Step 5 の内容をベースに、派遣プロンプトの上書き事項6に従い「デプロイ前に `compatibility_date` を Cloudflare の最新へ見直す」旨を1行足し、`STEAM_WEB_API_KEY` が Steamworks パートナーサイトの publisher key である旨を明記。コマンドはすべて実在の `package.json` scripts（`pnpm test`・`pnpm typecheck`・`pnpm run deploy`・`pnpm dev`）・`pnpm exec wrangler ...` と一致（`pnpm exec wrangler --version` で検算 → `4.131.2`）。

## fail-closed経路のwarn（一覧、全経路で確認済み）

- admin key未設定/不一致 → `requireAdmin`（`src/http.ts`、既存）がwarn
- inboxの不正kind・不正path segment・ack以外の不明パス404 → `routeAdmin`/`routeInbox`内でwarn
- `getInboxObject` の不正相対パス400・不在404 → warn
- `putAllowlist` のJSON非パース400・`steamIds`が配列/文字列でない400 → warn

## テスト結果

```
cd tools/playtest-receiver && pnpm test
 Test Files  10 passed (10)
      Tests  68 passed (68)
```

```
cd tools/playtest-receiver && pnpm typecheck
（エラーなし）
```

```
pnpm exec wrangler --version
4.131.2
```

### RED確認（Step 2）

実装完了後、`src/routes/admin.ts` と `src/index.ts` の変更を一時退避し（`index.ts` はコミット済みHEAD版に戻す）、`pnpm test` を実行して再現:
```
 Test Files  1 failed | 9 passed (10)
      Tests  9 failed | 59 passed (68)
```
`admin.test.ts` の9ケース全てが失敗（未知パスのため `/v1/inbox`・`/v1/allowlist` が404を返す。例: `許可リストPUTの本文がsteamIds配列でなければ400でwarnする` は `expected 404 to be 400`）することを確認。その後ファイルを復元し、`pnpm test`・`pnpm typecheck` が全PASSに戻ることを確認済み（GREEN、上記結果）。

### テスト出力のノイズについて

`pnpm test` の出力に以下の1行が毎回出る:
```
console warning; message = Called .text() on an HTTP body which does not appear to be text. The body's Content-Type is "application/octet-stream". ...
```
これは `test/admin.test.ts` の「inboxの個別ファイルを取れる」テストが `getInboxObject` の返す `content-type: application/octet-stream` のレスポンスへ `.text()` を呼ぶことで、workerd/undici本体が出す診断メッセージ（`console.warn` ではなくランタイムの内部警告）。ブリーフ Step 1 のテストコード自体がこの呼び出し方を指定しており、`content-type` をテキストへ変えるのは設計（プレイテスト添付ファイルは任意バイナリ）に反するため、この1行はテストの構造上避けられないノイズとして許容した。

## 変更ファイル

- 新規: `tools/playtest-receiver/src/routes/admin.ts`
- 新規: `tools/playtest-receiver/test/admin.test.ts`
- 新規: `tools/playtest-receiver/README.md`
- 変更: `tools/playtest-receiver/src/index.ts`（import 1行 + 分岐2行）

## 自己レビュー所見

- ブリーフのProduces署名（`getInbox`・`getInboxObject`・`postAck`・`getAllowlist`・`putAllowlist`）はすべて実装、シグネチャもブリーフどおり。
- ブリーフのコード例に無かった `console.warn` を fail-closed 経路へ追加（上書き事項4の明示指示）。
- ディレクトリファイル数: `src/routes/`3・`src/`直下7・`test/`直下10（`support/`はサブディレクトリでカウント外）— いずれも10ファイル以内。ファイル行数はいずれも200行未満（`admin.ts`154行・`admin.test.ts`158行）。
- README のコマンドはすべて `package.json` の実在scriptと一致することを実行で確認済み。

## 懸念事項

- 特になし。ブロッカーなし。

## Fix報告（レビュー所見対応）

対象: `/Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/playtest-receiver/.superpowers/sdd/task-4-review.md`

### Important 1 — 管理API認証を経路検証より前・1箇所に統一

`src/routes/admin.ts` の `routeAdmin` 冒頭（`/v1/allowlist` または `/v1/inbox` に一致した直後）で `requireAdmin` を1回だけ呼ぶよう変更。5つのハンドラ（`getInbox`・`getInboxObject`・`postAck`・`getAllowlist`・`putAllowlist`）内の重複した `requireAdmin` 呼び出しは削除した。
これにより、鍵なし・不一致は経路の形やメソッドに関わらず必ず `401 {"reason":"unauthorized"}` になる（以前は `POST /v1/allowlist` が405、`GET /v1/inbox/badkind/...` が400、`GET /v1/inbox/report/<id>/<id>` が404、`ack` への不正メソッドが405を認証前に返していた）。
`test/admin/auth.test.ts` に2件追加（不正kindのinboxパス・allowlistへの不正メソッド。いずれも鍵なしで401）し、既存の「adminキーが無ければ401」系と合わせて経路形状に関わらず401になることを確認した。

### Important 2 — README にPLAYTEST_ADMIN_KEYの出所を明記

README:46-56 に「Mac mini 側の env ファイルを作る」手順を追加（`~/hermes-agent/data/services/playtest/env.sh` に `PLAYTEST_RECEIVER_BASE`・`PLAYTEST_ADMIN_KEY` の2変数を書き `chmod 600`、値は手順2で `ADMIN_KEY` に入れたものと同じ、`PLAYTEST_ENV_FILE` で既定パスを上書き可能な旨）。動作確認ブロック（README:58-66）は `. env.sh` してから `$PLAYTEST_RECEIVER_BASE`・`$PLAYTEST_ADMIN_KEY` を使う形に書き換え、未定義の値が残らないようにした。

### Important 3 — README のPUT行にContent-Length必須を明記

README:11（エンドポイント表 `PUT /v1/uploads/...` 行）に「`Content-Length` 必須（欠落411・非数値400・100MiB超413）」を追記。

### Cloudflare認証

README の初回セットアップに手順0として `wrangler login`（または `CLOUDFLARE_API_TOKEN`）と、複数アカウント環境向けに `wrangler whoami` を見て `CLOUDFLARE_ACCOUNT_ID` を設定する旨を追加。`wrangler.toml` に `account_id` は追加していない（指示どおり）。

### Minor対応

- **4（tryの境界コメント）**: `putAllowlist` の日英コメントを「管理者が手で送るJSON本文のパースは外部入力境界」という根拠を明示する形に書き換え、`src/allowlist.ts:11-12` の前例に揃えた。
- **5（未使用export）**: 対応せず。ブリーフのProduces欄が明示的に `export` を要求しており（plan-mandated）、前例（`routes/uploads.ts` も同様に未使用exportを持つ）とも矛盾しないため現状維持。
- **6（cursorページングのテスト欠落）**: `test/admin/inbox.test.ts` に「101件あれば2ページ目にcursorで続きが取れる」を追加。R2へ直接101件のpending索引を書き、1ページ目100件+cursor非null、2ページ目1件+cursor null を検証。
- **7（テスト出力のworkerd診断ノイズ）**: `ok.text()` を `new TextDecoder().decode(await ok.arrayBuffer())` に置き換え、警告行が出なくなったことを確認（`pnpm test` 出力にwarning行が無い）。
- **8（STEAM_ID重複定義）**: `test/support/uploadsFixture.ts` の `STEAM_ID` を import する形にし、admin側の再定義を削除。
- **9（test/直下ファイル数上限）**: このFixで `test/admin.test.ts`（219行、200行規約に抵触）を分割する必要が生じたため、併せて `test/admin/` サブディレクトリを新設し `auth.test.ts`・`inbox.test.ts`・`allowlist.test.ts` の3ファイルに分割した。これにより `test/` 直下のコードファイルは9個（`support/`・`admin/` はサブディレクトリでカウント外）に減り、Minor #9 の指摘（10ファイルちょうどで次のタスクが超過する）も同時に解消した。
- **10（README動作確認3本目のHTTPコード不一致）**: 3本目のcurlに `-s -o /dev/null -w '%{http_code}\n'` を付け、コメントを「401 を期待（無効チケット）」に統一した。
- **11（成功経路のconsole.warn）**: `putAllowlist` の成功時ログを `console.warn` → `console.log` に変更（拒否理由ではないため）。
- **12（postAckが対象の実在を確認しない）**: `postAck` で `pendingIndexKey` の実在を `BUCKET.head` で確認し、無ければ404 + warnで拒否するよう変更（ACKEDだけのゴミオブジェクトを作らない）。`test/admin/inbox.test.ts` に「pendingでないidをackすると404でwarnする」を追加して検証した。

### 変更ファイル（Fix分）

- 変更: `tools/playtest-receiver/src/routes/admin.ts`
- 変更: `tools/playtest-receiver/README.md`
- 削除: `tools/playtest-receiver/test/admin.test.ts`（200行超過のため分割・移設）
- 新規: `tools/playtest-receiver/test/admin/auth.test.ts`
- 新規: `tools/playtest-receiver/test/admin/inbox.test.ts`
- 新規: `tools/playtest-receiver/test/admin/allowlist.test.ts`

### テスト結果（Fix後の再実行）

```
cd tools/playtest-receiver && pnpm test
 Test Files  12 passed (12)
      Tests  72 passed (72)
```
（workerd診断ノイズなし、出力クリーン）

```
cd tools/playtest-receiver && pnpm typecheck
（エラーなし）
```

### 自己レビュー（Fix分）

- Important 3件すべて対応済み。Minor 9件中8件対応、1件（#5 未使用export）は理由を明記して見送り。
- `postAck` の404化は既存テスト（成功ack、pendingあり）に影響しないことを確認済み（全72件PASS）。
- ファイル/ディレクトリ規約（200行・10ファイル）を全ファイルで再確認: `admin.ts`159行、`test/admin/inbox.test.ts`129行・`auth.test.ts`55行・`allowlist.test.ts`57行、いずれも200行未満。`test/`直下9ファイル、`src/routes/`3ファイル。

### 懸念事項（Fix分）

- 特になし。ブロッカーなし。

## Fix報告2（再レビュー対応）

対象: `/Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/playtest-receiver/.superpowers/sdd/task-4-review.md` の「# 再レビュー」節

### Important A — `postAck` を冪等にする（コントローラー裁定に従い (a) を採用）

`src/routes/admin.ts` の `postAck`: 未ACK索引が無い場合、即404にせず `{bundlePrefix}/ACKED` の実在を `BUCKET.head` で確認するよう変更。ACKED が既にあれば `200 {acked:true}`（冪等な再送として成功扱い）、ACKED も索引も無ければ従来どおり404+warn。
`test/admin/inbox.test.ts` に「ackを2回呼んでも200でACKEDは1つのまま」を追加: 同一ackを2回呼び双方200、`BUCKET.list` で `ACKED` オブジェクトが1つだけであることを確認。既存の「pendingでないidをackすると404でwarnする」テストは索引もACKEDも無いidを使っているため、この変更後も404のまま成立することを確認済み（全73件PASS）。

### 新規Minor 6件の対応

1. **`/v1/allowlist/*` 未知パスの扱い**: レビューの推奨どおり、`routeAdmin` 末尾の `return null` の直前に、鍵なし401／鍵ありR1逐語404という意図的な非対称の理由を日英コメントで明記した（`src/routes/admin.ts:30-33`）。挙動自体は変更していない（実害が無く、既存挙動を変えると別の後方互換懸念を生むため）。
2. **コメント誤記**: `getInbox` 直上のコメントを「認証はrouteAdmin/routeInboxが済ませている」→「認証はrouteAdminが済ませている」に修正（`routeInbox` は認証していない事実に合わせた）。
3. **未使用export（前回Minor#5）の見送り**: 今回も見送り。理由は下記「見送り理由」を参照。
4. **未使用`request`引数**: `getInboxObject`・`postAck`・`getAllowlist` から `request` パラメータを削除し、呼び出し元（`routeInbox`/`routeAdmin`）のシグネチャも合わせて更新。ブリーフのProduces署名とは変わるが、コントローラー指示に従い実際に使われない引数を落とした。
5. **README手順7のパス不整合**: `tools/playtest-receiver` にcdした状態のままだと `scripts/playtest/allowlist.sh`（リポジトリルート基準）が見つからない問題を修正。`(cd ../.. && scripts/playtest/allowlist.sh add <steamId>)` の形にし、その旨を手順文にも明記。
6. **ヒアドキュメントの字下げ問題**: 手順6の `cat > ... <<'EOF'` をヒアドキュメントではなく `{ echo '...'; echo '...'; } > env.sh` の形に置き換え、Markdownリストの字下げに関係なくraw README から直接コピーしても動くようにした。

### 未使用export（前回Minor#5）の見送り理由（再確認）

`getInbox`・`getInboxObject`・`postAck`・`getAllowlist`・`putAllowlist` は現在も `export` のまま。理由:
- ブリーフのProduces欄がこれらの `export` を明示的に要求しており（plan-mandated）、今回のFixでも本文の実装意図（各ハンドラの単体テスト可能性）を変える指示は無かった。
- レビューが指摘した「`uploads.ts` は自前で `authorize` するのに対しこちらは何もしない」という差は事実だが、認証をrouteAdmin側へ一元化したこと自体がコントローラー裁定によるImportant #1の是正であり、「ハンドラが認証しない」ことは意図した設計（重複認証の排除）である。ハンドラを非exportにしても呼び出し経路は変わらず、挙動上のリスク低減にはならない（`routeAdmin`経由以外に呼び出し口が無いことは変わらないため）。
- 今回のコーディネーター指示は「未使用exportの見送りは理由を報告に書く」であり、exportを外す指示ではなかったため、見送りを維持した。

### 変更ファイル（Fix2分）

- 変更: `tools/playtest-receiver/src/routes/admin.ts`
- 変更: `tools/playtest-receiver/README.md`
- 変更: `tools/playtest-receiver/test/admin/inbox.test.ts`

### テスト結果（Fix2後）

```
cd tools/playtest-receiver && pnpm test
 Test Files  12 passed (12)
      Tests  73 passed (73)
```

```
cd tools/playtest-receiver && pnpm typecheck
（エラーなし）
```

### 自己レビュー（Fix2分）

- Important A対応済み、既存の「pendingでないid」404テストとの整合を確認済み。
- Minor 6件中5件を実コード/README変更で対応、1件（未使用export見送り）は理由を明記。
- ファイル行数: `admin.ts`168行、`test/admin/inbox.test.ts`152行。いずれも200行未満。

### 懸念事項（Fix2分）

- 特になし。ブロッカーなし。

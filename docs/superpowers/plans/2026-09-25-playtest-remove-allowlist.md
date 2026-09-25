# プレイテスト許可リスト撤去と報告者名の取り込み時解決 Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** 許可リストと起動時照合（オンライン必須）を撤去して、誰でも配布ビルドを起動・報告できるようにする。そのうえで、Mac mini の取り込み時に報告者の Steam 表示名を解決してダイジェストに出す。

**Architecture:** 3系統を独立に直す。(1) Worker（`tools/playtest-receiver`）は許可リストを消し、`/v1/session` をチケット検証だけにする。(2) クライアントは `PlaytestLaunchGate`（照合・ReactiveProperty）を、同期で配布版かどうかを判定する `PlaytestLaunchProfile` に置き換える。配布版なら起動時にローカル Steam の SteamID を識別へ差し込み、アップロード走行は自分で `PlaytestSession` を持ってトークンを送信時に取りに行く。(3) Mac mini の `ingest.sh` は取り込み時に `GetPlayerSummaries` を引き、`ingest.json` に名前とプロフィールURLを残す。`digest*.py` がそれを表示する。

**Tech Stack:** TypeScript（Cloudflare Workers・vitest）、Unity C#（UniRx・UniTask・Steamworks.NET・NUnit）、bash＋python3（stdlib）

## Requirements

1. Worker の許可リストをまるごと消す：`src/allowlist.ts`・`/v1/allowlist`（GET/PUT）・`session.ts` の照合・関連テストが対象。`/v1/session` は Steam チケットが有効なら誰にでも 200＋トークンを返す（受入: 空の R2 で有効チケット → 200、`/v1/allowlist` → 404）。
2. 応答の `allowed: true` は残す。配布済みの旧ビルドがこれを必須にしているため（受入: session.test の応答キーは `["allowed","expiresAt","steamId","token"]` のまま）。
3. `scripts/playtest/allowlist.sh`・`tests/test-allowlist.sh`・`lib/receiver-api.sh` の allowlist 関数を消す（受入: `git grep -n allowlist -- scripts/playtest tools/playtest-receiver` の該当は、無関係な release 系のラベル検証だけになる）。
4. クライアントの起動時照合を撤去する。オフラインでも Worker 不達でも、タイトルで止まらず開始できる（受入: `PlaytestLaunchGate`・`PlaytestGateDecision`・`PlaytestGateResult`・`PlaytestGateStatus`・`ReceiverVerifiedSessionIdentity` が存在しない。Play locally は照合を待たない）。
5. 配布版かどうかの判定（`build-info.json` があり、かつ Steam が起動している）は残す。配布版だけがアップロードする（受入: 開発者モードで `RequestUpload` → "developer mode" ログで送らない。配布版では送信時にセッションを作り送る）。
6. トークンは送信時に取りに行く。取れなければ箱は outbox に残る（受入: 既存の `PlaytestUploader*Test` がそのまま緑）。
7. 報告・進行記録・異常終了箱の `steamId` は、配布版の起動時に `SteamUser.GetSteamID()` で読む。読めなければ空にし、理由を識別へ載せる（受入: 配布版で読めたら `PlaytestSessionIdentityProvider.Current.SteamId` がその値。開発者モードでは `DeveloperModeReason`）。
8. 出展モード（`EventModeAutoStart`）と検証機 smoke は照合を待たない。smoke の前提検査は「配布版であること」に変える（受入: `EventModeAutoStartVerdictTest` と smoke の前提テストを新しい判定で書き直して緑）。
9. 照合専用の Localization キー（`ui.playtest.checking/notAllowed/unreachable/ticketFailed/malformedResponse`）を消す。`ui.playtest.gate.notStarted` は照合に触れない文言へ直す。webui の `localizationKeys.ts` を再生成する（受入: `localizationKeysFreshness.test.ts` 緑）。
10. 同意ポップアップ本文 `ui.playtest.consent.body` の Source/english/japanese/german に、「報告はSteamアカウント（SteamIDと公開表示名）に紐づけて記録される」旨の1文を「これ以外はPCから送信されません」の直前へ足す。表示規則（保存先ごとに初回1回）は変えず、既読者に再表示しない。
11. `ingest.sh` は新しく取り込む箱ごとに Steam Web API `ISteamUser/GetPlayerSummaries/v2` を引き、`ingest.json` に `steamPersonaName`・`steamProfileUrl` を書く。解決できなければ空文字にして `steamPersonaMissing` に理由を書き、ログにも出す。取り込みは止めない。同じ走行内の同じ SteamID は1回だけ引く（受入: test-ingest で、スタブ成功 → 名前入り、キー未設定 → 空＋理由）。
12. 対象は報告・進行記録の両方。既に取り込み済みの箱は書き直さない。閲覧時に現在名を引き直さない。連絡先は集めない。
13. 日次ダイジェストの報告行・自動修正候補行に `名前（SteamID・プロフィールURL）` を出す。未解決なら `名前未解決（SteamID …）`。進行記録の節に、期間内のテスター一覧を `名前（SteamID）` で出す。名前は Discord マークアップ無害化を通し、貼り付け用コマンドには入れない。
14. Worker を moorestech プロファイルでデプロイし、R2 の `config/allowlist.json` を消す。旧ビルドでも「not on the playtest list」が出なくなることを確かめる。
15. やらないこと：拒否リスト・個別停止の代替、同意の版上げ・再表示、閲覧時の名前再取得、連絡先欄、新しい配布ビルドのリリース（`release-playtest.sh` は人が別途叩く）。

## Global Constraints

- AGENTS.md の規約（1ファイル200行未満・1ディレクトリ10ファイルまで・partial禁止・`Func<>`禁止・try-catch は外部境界だけ・日英2行コメント・fail-closed はログ必須・デフォルト引数禁止・`.meta` を手で作らない・Prefab/Scene の手編集禁止で `uloop execute-dynamic-code` 経由）。
- .cs を変更したら `uloop compile --project-path ./moorestech_client` が ErrorCount 0 であること。
- クラスの改名は `git mv X.cs Y.cs && git mv X.cs.meta Y.cs.meta` で GUID を保つ（シーン参照を切らない）。`.meta` を新しく作らない。新規 .cs の `.meta` は Unity の自動生成を待ってからコミットする。
- Localization CSV を変えたら `moorestech_client/Assets/Scripts/Client.Localization/_CompileRequester.cs` の変化も同じコミットに入れる。キーを消したら `pnpm --dir moorestech_web/webui gen:i18n`。
- Worker のデプロイは `cd tools/playtest-receiver && npx wrangler deploy --profile moorestech`（`CLOUDFLARE_API_TOKEN` をシェルに置かない）。
- `STEAM_WEB_API_KEY` の値をログ・出力・コミットに出さない。
- 出所: ADR 0070（`docs/adr/0070-playtest-drops-allowlist-and-resolves-reporter-names-at-ingest.md`）と `.decisions/2026-09-25-*.md`。

## File Structure

| 対象 | 変更 | 責務 |
|---|---|---|
| `tools/playtest-receiver/src/allowlist.ts` | 削除 | — |
| `tools/playtest-receiver/src/routes/session.ts` | 変更 | チケット検証 → トークン発行だけ |
| `tools/playtest-receiver/src/routes/admin.ts` | 変更 | inbox のみ |
| `tools/playtest-receiver/test/{allowlist.test.ts,admin/allowlist.test.ts}` | 削除 | — |
| `tools/playtest-receiver/test/session.test.ts`・`test/admin/auth.test.ts` | 変更 | allowlist 前提を外す |
| `scripts/playtest/allowlist.sh`・`tests/test-allowlist.sh` | 削除 | — |
| `scripts/playtest/lib/receiver-api.sh` | 変更 | allowlist 関数を削除 |
| `scripts/playtest/lib/steam-persona.sh` | 新規 | GetPlayerSummaries を引き、走行内でキャッシュする（`receiver_curl` とは鍵もホストも違うため別の口。`STEAM_CURL_CMD` で差し替える） |
| `scripts/playtest/lib/steam_persona.py` | 新規 | 応答 JSON から name/url を取り出す（`safe_segment.py` と同じ「lib の python 小道具」の型） |
| `scripts/playtest/ingest.sh` | 変更 | ingest.json に名前欄を足す |
| `scripts/playtest/digest_schema.py`・`digest_collect.py`・`digest.py`・`digest_candidates.py` | 変更 | 名前欄の読み取りと表示 |
| `scripts/playtest/tests/ingest-fixture.sh`・`test-ingest.sh`・`digest_fixture.py`・`test_digest.py` | 変更 | 名前のテスト |
| `Client.PlaytestReceiver/Launch/PlaytestLaunchProfile.cs` | 新規（`Gate/PlaytestLaunchGate.cs` を置換） | 配布版判定と識別の差し込み（プロセスで1回、同期） |
| `Client.PlaytestReceiver/Launch/PlaytestLaunchKind.cs` | 新規 | `NotEvaluated/DeveloperMode/Distribution` |
| `Client.PlaytestReceiver/Launch/LocalSteamSessionIdentity.cs` | `Gate/ReceiverVerifiedSessionIdentity.cs` を git mv して改名 | ローカルSteamIDの識別 |
| `Client.PlaytestReceiver/Steam/PlaytestLocalSteamIdReader.cs` | `Client.Starter/PlaytestSmoke/StandalonePlaytestSmokeSteamIdReader.cs` を git mv して移設 | `SteamUser.GetSteamID()` の外部境界（smoke も呼ぶ。写しを作らない） |
| `Client.PlaytestReceiver/Gate/*` の残り | 削除 | — |
| `Client.PlaytestReceiver/PlaytestSession.cs`・`PlaytestSessionResult.cs`・`Http/Responses/PlaytestSessionResponse.cs`・`Upload/Failure/PlaytestUploadFailurePolicy.cs` | 変更 | `NotAllowed` と `allowed` の読み取りを消す |
| `Client.PlaytestReceiver/Upload/PlaytestUploadRunner.cs` | 変更 | 配布版判定と自前セッション |
| `Client.Starter/Playtest/TitleGates/PlaytestTitleGates.cs` | 変更 | 照合段を消し、`TryBegin(PlaytestLaunchKind, …)` |
| `Client.MainMenu/Playtest/PlaytestLaunchGateView.cs` → `PlaytestTitleGateView.cs` | git mv して改名・縮小 | タイトルの合成ルート（同意・異常終了の確認だけ） |
| `Assets/Scenes/Game/MainMenu.unity` | uloop 経由 | GameObject 名 `PlaytestLaunchGate` → `PlaytestTitleGates`、`messagePopup` 参照を外して保存 |
| `Client.Starter/EventMode/EventModeAutoStart.cs` | 変更 | 照合待ちを消す |
| `Client.Starter/PlaytestSmoke/StandalonePlaytestSmoke{Bootstrap,Preconditions}.cs` | 変更 | 配布版判定で前提検査 |
| `scripts/playtest/windows/run-smoke.ps1` | 変更 | launch gate 180s の予算を外す |
| `Localization/localization.csv` | 変更 | キー削除・文言修正・同意文追記 |
| README 類・`CONTEXT.md` | 変更 | allowlist と照合の記述を消し、名前解決を書く |

前例と再利用：
| 新規/変更 | 既存の同役割 | 判断 |
|---|---|---|
| `PlaytestLaunchProfile` | `PlaytestLaunchGate.RequiresCheck`（同じ判定）・static 保持（MainMenu に DI が無い） | 判定を移して呼ぶ。ReactiveProperty は同期判定なので不要 |
| `PlaytestLocalSteamIdReader` | `StandalonePlaytestSmokeSteamIdReader` | 移設して両方から呼ぶ |
| `steam-persona.sh` | `receiver-api.sh` の `receiver_curl` | 同形の別関数（X-Admin-Key を Steam へ送らないため共通化しない） |
| `steam_persona.py` | `lib/safe_segment.py`（lib の python 小道具） | 新規 |

## ユーザー操作が失敗する経路

| 状態 | いつ・誰が起こすか | ユーザーに見えるもの | 操作なしで解消するか |
|---|---|---|---|
| Worker 不達・Steam 不達 | テスターがオフライン、または Worker 障害時 | 何も見えない（遊べる）。箱は outbox に残る | 次の起動・次の報告送信で再送される（`RequestUpload` は起動直後と報告直後に呼ばれる） |
| `SteamUser.GetSteamID()` 失敗 | Steam は起動しているが未ログインなど | 何も見えない。記録の steamId が空になる | R2 の置き場所は送信時のトークンで決まるので、追跡は失われない |
| Web API キー未設定・Steam API 不達 | Mac mini の env 欠落や Steam 障害 | ダイジェストに「名前未解決（SteamID …）」 | その箱は解決しない（Requirements 12）。以後に取り込む箱で解決する |

恒久失敗になる行はない。

---

### Task 1: Worker から許可リストを撤去する

**Files:**
- Delete: `tools/playtest-receiver/src/allowlist.ts`, `tools/playtest-receiver/test/allowlist.test.ts`, `tools/playtest-receiver/test/admin/allowlist.test.ts`
- Modify: `tools/playtest-receiver/src/routes/session.ts:1,31-43`, `tools/playtest-receiver/src/routes/admin.ts:1,22,29-33,146-178`
- Modify: `tools/playtest-receiver/test/session.test.ts`, `tools/playtest-receiver/test/admin/auth.test.ts:44-50`
- Modify: `tools/playtest-receiver/README.md:10,17,91,101-105,113`

**Interfaces:**
- Produces: `POST /v1/session` → 200 `{ steamId, allowed: true, token, expiresAt }`（有効チケットなら誰でも）。`/v1/allowlist` → 404（`routeAdmin` が null を返し、既存の未一致経路へ落ちる）。

- [ ] **Step 1: テストを書き換える（失敗させる）**

`test/session.test.ts`：`writeAllowlist` の import・`beforeEach` の delete・各 `writeAllowlist` 呼び出しを削除する。「corrupt→503」「403 not-allowed」「空リスト→403」のケースを削除し、代わりに次を足す。

```ts
it("issues a token to any Steam-verified ticket without consulting an allowlist", async () => {
  const response = await handle(sessionRequest("aa"), env, steamOk("76561198000000001"));
  expect(response.status).toBe(200);
  const body = await response.json<Record<string, unknown>>();
  expect(Object.keys(body).sort()).toEqual(["allowed", "expiresAt", "steamId", "token"]);
  expect(body.allowed).toBe(true);
  expect(body.steamId).toBe("76561198000000001");
});

it("no longer serves the allowlist admin route", async () => {
  const response = await handle(new Request("https://x/v1/allowlist", { headers: { "X-Admin-Key": env.ADMIN_KEY } }), env, fetch);
  expect(response.status).toBe(404);
});
```
（`sessionRequest`・`steamOk` はこのファイルの既存ヘルパ名に合わせる。無ければファイル冒頭の既存の組み立て方を関数へ切り出して使う。）
`test/admin/auth.test.ts:44-50` の `/v1/allowlist` POST を `/v1/inbox` への `DELETE`（405 より前に 401 になる経路）へ差し替える。期待は 401 のまま。

- [ ] **Step 2: 失敗を確認する**

Run: `cd tools/playtest-receiver && pnpm test`
Expected: FAIL（import 先の `allowlist.ts` がまだある/404 が 200 or 401 になる）

- [ ] **Step 3: 実装する**

`src/routes/session.ts` から `readAllowlist` の import と 31-43 行（コメント2行＋照合ブロック）を削除する。`src/routes/admin.ts` は import を削除し、22 行を `if (segments[1] !== "inbox") return null;` にし、29-33 行の allowlist 分岐と `getAllowlist`/`putAllowlist` を削除する。`src/allowlist.ts` と2つのテストファイルを `git rm` する。

- [ ] **Step 4: 通ることを確認する**

Run: `cd tools/playtest-receiver && pnpm test && pnpm typecheck`
Expected: PASS・型エラー0

- [ ] **Step 5: README を直す**

`README.md:10` の「チケット検証＋許可リスト照合＋…」を「チケット検証＋…」へ、`:17` の `/v1/allowlist` 行を削除、`:91` の手順6の説明から `allowlist.sh` の言及を外して「ingest がこの env を読む」とする、`:101-105` の手順7を削除する（後続の番号を詰める）、`:113` の curl 例を削除する。

- [ ] **Step 6: コミット**

```bash
git add -A tools/playtest-receiver
git commit -m "feat(playtest-receiver): 許可リスト照合と管理APIを撤去しチケット検証だけでトークンを出す (ADR 0070)"
```

### Task 2: Mac mini 側の allowlist 管理を撤去する

**Files:**
- Delete: `scripts/playtest/allowlist.sh`, `scripts/playtest/tests/test-allowlist.sh`
- Modify: `scripts/playtest/lib/receiver-api.sh:64-81`（`receiver_admin_body`・`receiver_allowlist_get`・`receiver_allowlist_put`）
- Modify: `scripts/playtest/README.md:16-28,107-108,177`, `scripts/playtest/README-macmini.md:30`, `scripts/playtest/verify-on-windows.sh:91-93`（コメントの「sibling allowlist.sh」を「sibling ingest.sh」へ）

- [ ] **Step 1:** `grep -n "receiver_admin_body\|receiver_allowlist" -r scripts/playtest` で allowlist.sh 以外に呼び手が無いことを確かめる（Expected: allowlist.sh と test-allowlist.sh と receiver-api.sh だけ）。
- [ ] **Step 2:** 3関数を削除し、2ファイルを `git rm` する。README の「許可リスト」節（16-28）を削除し、検証手順7（107-108）の「受け口の起動時照合が通ることを確認」を「タイトルで止まらず Play locally まで進むことを確認」へ、177 行のテスト一覧から `test-allowlist.sh` を消す。README-macmini:30 の列挙から `allowlist.sh` を消す。
- [ ] **Step 3:** Run: `for t in scripts/playtest/tests/test-*.sh; do bash "$t" || echo "FAIL $t"; done` → Expected: FAIL 行なし
- [ ] **Step 4: コミット** `git commit -m "chore(playtest): allowlist.sh と受け口ラッパの allowlist 関数を撤去 (ADR 0070)"`

### Task 3: 取り込み時に Steam 表示名を解決して ingest.json へ書く

**Files:**
- Create: `scripts/playtest/lib/steam-persona.sh`, `scripts/playtest/lib/steam_persona.py`
- Modify: `scripts/playtest/ingest.sh:24-31`（lib の source に steam-persona.sh を足す）, `:97-104`
- Modify: `scripts/playtest/tests/ingest-fixture.sh`, `scripts/playtest/tests/test-ingest.sh`
- Modify: `scripts/playtest/README.md:7-14,42`

**Interfaces:**
- Produces: `steam_persona_resolve <steamId> <outJson>` → 常に 0 を返し、`outJson` に `{"steamPersonaName": str, "steamProfileUrl": str, "steamPersonaMissing": str}` を書く（解決できたら missing は ""）。
- Produces: ingest.json のキー `kind, steamId, id, readyAt, ingestedAt, steamPersonaName, steamProfileUrl, steamPersonaMissing`

- [ ] **Step 1: テストを書く（失敗させる）**

`tests/ingest-fixture.sh` に Steam 用スタブを足す。

```bash
# Steam Web API のスタブ。STEAM_STUB_MODE=ok なら固定の名前、fail なら 503 を返す
# Stub for the Steam Web API: STEAM_STUB_MODE=ok returns a fixed persona, fail returns 503
steam_stub_curl() {
  local out="" url=""
  while [ $# -gt 0 ]; do
    case "$1" in
      -o) out="$2"; shift 2 ;;
      -w|--max-time|-H) shift 2 ;;
      -*) shift ;;
      *) url="$1"; shift ;;
    esac
  done
  echo "$url" >> "$FIXTURE_DIR/steam-calls.log"
  if [ "${STEAM_STUB_MODE:-ok}" = "ok" ]; then
    printf '{"response":{"players":[{"steamid":"76561198000000001","personaname":"Tester <One>","profileurl":"https://steamcommunity.com/profiles/76561198000000001/"}]}}' > "$out"
    printf '200'
  else
    printf 'unavailable' > "$out"; printf '503'
  fi
}
export -f steam_stub_curl
```
`run_ingest`（:78-81）の env に `STEAM_CURL_CMD=steam_stub_curl` を加える（`PLAYTEST_ENV_FILE=/dev/null` は維持）。`test-ingest.sh` に3ケースを足す。
1. `STEAM_WEB_API_KEY=dummy STEAM_STUB_MODE=ok` で1箱取り込み → `ingest.json` の `steamPersonaName == "Tester <One>"`、`steamProfileUrl` がプロフィールURL、`steamPersonaMissing == ""`。
2. 同じ steamId の箱2つ → `steam-calls.log` が1行（走行内キャッシュ）。
3. `STEAM_WEB_API_KEY` 未設定 → 取り込みは成功し、name/url が ""、`steamPersonaMissing` が `STEAM_WEB_API_KEY 未設定` を含む。ログに `[WARN]` 行が出る。
加えて `STEAM_STUB_MODE=fail` → missing に `status=503` を含む。

- [ ] **Step 2:** Run: `bash scripts/playtest/tests/test-ingest.sh` → Expected: FAIL（キーが無い）

- [ ] **Step 3: `lib/steam_persona.py` を書く**

```python
#!/usr/bin/env python3
"""GetPlayerSummaries の応答から表示名とプロフィールURLを取り出す。取れなければ missing に理由を書く
Extracts the persona name and profile URL from a GetPlayerSummaries response; on failure the reason goes to missing"""
import json
import sys


def extract(response_path: str, steam_id: str) -> dict:
    # Steam の応答は外部入力。形が違えば理由付きの未解決にする
    # The Steam response is external input; an unexpected shape becomes an unresolved entry with a reason
    try:
        with open(response_path, encoding="utf-8") as f:
            players = json.load(f)["response"]["players"]
    except (OSError, ValueError, KeyError, TypeError) as error:
        return unresolved(f"応答の解析に失敗: {type(error).__name__}")
    for player in players if isinstance(players, list) else []:
        if isinstance(player, dict) and player.get("steamid") == steam_id:
            name, url = player.get("personaname"), player.get("profileurl")
            if isinstance(name, str) and isinstance(url, str):
                return {"steamPersonaName": name, "steamProfileUrl": url, "steamPersonaMissing": ""}
    return unresolved("応答に該当 SteamID が無い")


def unresolved(reason: str) -> dict:
    return {"steamPersonaName": "", "steamProfileUrl": "", "steamPersonaMissing": reason}


if __name__ == "__main__":
    mode, out = sys.argv[1], sys.argv[-1]
    result = extract(sys.argv[2], sys.argv[3]) if mode == "extract" else unresolved(sys.argv[2])
    with open(out, "w", encoding="utf-8") as f:
        json.dump(result, f, ensure_ascii=False, separators=(",", ":"))
```

- [ ] **Step 4: `lib/steam-persona.sh` を書く**

```bash
#!/usr/bin/env bash
# 報告者の Steam 表示名を GetPlayerSummaries で引く（ADR 0070）。取り込みを止めないため常に 0 を返し、失敗は理由付きで書く
# Resolves the reporter's Steam persona via GetPlayerSummaries (ADR 0070); always returns 0 and records failures with a reason
STEAM_CURL_CMD="${STEAM_CURL_CMD:-curl}"
STEAM_API_MAX_TIME="${STEAM_API_MAX_TIME:-20}"
STEAM_PERSONA_CACHE_DIR="${STEAM_PERSONA_CACHE_DIR:-$(mktemp -d)}"

steam_persona_resolve() {
  local steam_id="$1" out="$2"
  local cached="$STEAM_PERSONA_CACHE_DIR/$steam_id.json"
  # 同じ走行で同じ SteamID は1回だけ引く
  # One lookup per SteamID per run
  if [ -f "$cached" ]; then cp "$cached" "$out"; return 0; fi
  if [ -z "${STEAM_WEB_API_KEY:-}" ]; then
    log "[WARN] STEAM_WEB_API_KEY 未設定のため表示名を解決しない: $steam_id"
    python3 "$HERE/lib/steam_persona.py" unresolved "STEAM_WEB_API_KEY 未設定" "$cached"
    cp "$cached" "$out"; return 0
  fi
  local body="$STEAM_PERSONA_CACHE_DIR/$steam_id.response" code
  # 鍵はクエリに載るため URL をログへ出さない
  # The key rides in the query, so the URL is never logged
  code="$("$STEAM_CURL_CMD" --silent --max-time "$STEAM_API_MAX_TIME" -w '%{http_code}' -o "$body" \
    "https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v2/?key=${STEAM_WEB_API_KEY}&steamids=${steam_id}")" || code="curl-failed"
  if [ "$code" = "200" ]; then
    python3 "$HERE/lib/steam_persona.py" extract "$body" "$steam_id" "$cached"
  else
    python3 "$HERE/lib/steam_persona.py" unresolved "GetPlayerSummaries status=${code}" "$cached"
  fi
  local missing; missing="$(python3 -c 'import json,sys;print(json.load(open(sys.argv[1]))["steamPersonaMissing"])' "$cached")"
  [ -z "$missing" ] || log "[WARN] 表示名を解決できない: $steam_id（$missing）"
  cp "$cached" "$out"; return 0
}
```
（`log` と `HERE` は ingest.sh の既存定義を使う。steamId は `ingest_one` で安全セグメント検証済みなので、ファイル名とクエリにそのまま使える。）

- [ ] **Step 5: `ingest.sh` へ組み込む**

lib の source 列（:24-31）に `. "$HERE/lib/steam-persona.sh"` を足す。:97 の python の直前に `steam_persona_resolve "$steam_id" "$partial/.persona.json"` を置き、python を次に差し替える。

```bash
    python3 -c '
import json,os,sys
kind, steam_id, idv, ready_at, ingested_at, persona_path, out = sys.argv[1:8]
with open(persona_path, encoding="utf-8") as f:
    persona = json.load(f)
os.remove(persona_path)
meta = {"kind": kind, "steamId": steam_id, "id": idv, "readyAt": ready_at, "ingestedAt": ingested_at}
meta.update(persona)
with open(out, "w", encoding="utf-8") as f:
    json.dump(meta, f, ensure_ascii=False, separators=(",", ":"))
' "$kind" "$steam_id" "$id" "$ready_at" "$(now_utc)" "$partial/.persona.json" "$partial/ingest.json" \
```
（後続の `|| { log "ERROR: ingest.json 書き込み失敗 …" …}` はそのまま。）`.persona.json` が予約名衝突チェック（safe_segment の exit 6）に当たらないことを確認する。当たる場合はファイル名を `$STEAM_PERSONA_CACHE_DIR` 側へ移す。

- [ ] **Step 6:** Run: `bash scripts/playtest/tests/test-ingest.sh && bash scripts/playtest/tests/test-ingest-guards.sh && bash scripts/playtest/tests/test-ingest-lock.sh` → Expected: PASS
- [ ] **Step 7:** README.md:7-14 の env 例に `export STEAM_WEB_API_KEY=…` があることを確かめ、無ければ足す。:42 を「publisher key は Worker のチケット検証と ingest の表示名解決（GetPlayerSummaries）の両方で使う」へ直す。
- [ ] **Step 8: コミット** `git commit -m "feat(playtest): 取り込み時に GetPlayerSummaries で報告者の表示名を ingest.json に残す (ADR 0070)"`

### Task 4: ダイジェストに名前を出す

**Files:**
- Modify: `scripts/playtest/digest_schema.py:28-30`, `digest_collect.py:72-115`, `digest.py:60` と進行記録節（:65-92）, `digest_candidates.py:42-47,65`
- Modify: `scripts/playtest/tests/digest_fixture.py:20-73`, `scripts/playtest/tests/test_digest.py`

**Interfaces:**
- Consumes: Task 3 の ingest.json キー
- Produces: `digest_collect.reporter_label(meta: dict) -> str`（`名前（SteamID x・url）` か `名前未解決（SteamID x）`）

- [ ] **Step 1: テスト（失敗させる）** `digest_fixture.py` の ingest.json 生成に `steamPersonaName`・`steamProfileUrl`・`steamPersonaMissing` を引数付きで足す（既定は名前入り）。`test_digest.py` に次を足す。(a) 感想の見出しに `Tester（SteamID 7656…・https://steamcommunity.com/profiles/…/）` が出る。(b) 名前欄の無い古い ingest.json では `名前未解決（SteamID …）` が出る。(c) 名前 `@everyone **x**` が無害化される（`neutralize_discord_markup` の既存テストと同じ判定）。(d) 候補行に名前が出て、`enqueue-autofix.sh` のコマンド行には名前が出ない。(e) 進行記録の節に `テスター: 名前（SteamID）` 一覧が出る。
- [ ] **Step 2:** Run: `python3 -m unittest discover -s scripts/playtest/tests` → FAIL
- [ ] **Step 3: 実装**
  - `digest_schema.py`: `INGEST_SCHEMA` に `"steamPersonaName": (STR, ""), "steamProfileUrl": (STR, ""), "steamPersonaMissing": (STR, "")` を足す（旧 ingest.json は既定 "" で読める）。
  - `digest_collect.py`: 次を足し、`load_reports` の辞書に `"reporter": reporter_label(box["meta"])` を、`flatten_progress_record` の戻りに同じく `"reporter"` を足す。
    ```python
    def reporter_label(meta: dict) -> str:
        """報告者の表示ラベル。名前は取り込み時点の値で、同一人物の判定は SteamID が担う（ADR 0070）
        The reporter label; the name is as of ingest and identity is carried by the SteamID (ADR 0070)"""
        name, url, steam_id = meta.get("steamPersonaName", ""), meta.get("steamProfileUrl", ""), meta.get("steamId", "")
        if not name:
            return f"名前未解決（SteamID {steam_id}）"
        return f"{name}（SteamID {steam_id}・{url}）" if url else f"{name}（SteamID {steam_id}）"
    ```
  - `digest.py:60`: `f"### {report['id']}（{report['reporter']} / build {report['buildLabel'] or '不明'}）"`。バグ報告側にも SteamID を出している行があれば同様に置き換える（`grep -n "SteamID" scripts/playtest/digest*.py`）。進行記録の節の末尾に、期間内の `reporter` を重複なしで `- テスター: a、b` の1行として足す。
  - `digest_candidates.py`: 候補辞書に `"reporter": meta … reporter_label(meta)` を足し、:65 の行を `- {id}（{date}・{reporter}）… {head}` にする。:71 のコマンド行は変えない。
- [ ] **Step 4:** Run: `python3 -m unittest discover -s scripts/playtest/tests` → PASS
- [ ] **Step 5: コミット** `git commit -m "feat(playtest): 日次ダイジェストに報告者の表示名とプロフィールURLを出す (ADR 0070)"`

### Task 5: クライアントの起動時照合を配布版判定へ置き換える

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.PlaytestReceiver/Launch/PlaytestLaunchKind.cs`, `.../Launch/PlaytestLaunchProfile.cs`
- git mv: `Client.PlaytestReceiver/Gate/ReceiverVerifiedSessionIdentity.cs(.meta)` → `Client.PlaytestReceiver/Launch/LocalSteamSessionIdentity.cs(.meta)`
- git mv: `Client.Starter/PlaytestSmoke/StandalonePlaytestSmokeSteamIdReader.cs(.meta)` → `Client.PlaytestReceiver/Steam/PlaytestLocalSteamIdReader.cs(.meta)`（asmdef が Steamworks を参照していることを確認。していなければ `Client.PlaytestReceiver.asmdef` へ `com.rlabrecque.steamworks.net` を足す。`PlaytestSteamTicketProvider` が同じ asmdef で Steamworks を使っているので参照済みのはず）
- Delete: `Client.PlaytestReceiver/Gate/{PlaytestLaunchGate,PlaytestGateDecision,PlaytestGateResult}.cs(.meta)`（Gate ディレクトリごと）
- Modify: `Client.PlaytestReceiver/PlaytestSession.cs:73-96`, `PlaytestSessionResult.cs`（`NotAllowed` を削除）, `Http/Responses/PlaytestSessionResponse.cs:42`, `Upload/Failure/PlaytestUploadFailurePolicy.cs:65-77`, `Upload/PlaytestUploadRunner.cs`
- Modify: `Client.Game/.../BugReport/Playtest/PlaytestSessionIdentityProvider.cs:11`（既定理由の文言）
- Modify: `Client.Starter/Playtest/TitleGates/PlaytestTitleGates.cs`, `PlaytestStartGateBypass.cs:24-25`, `PreviousSessionStartupTasks.cs:21-22`, `CleanExitMarkWriter.cs:9-10`（「前例: PlaytestLaunchGate」を `PlaytestLaunchProfile` へ）
- Tests: 後述

**Interfaces:**
- Produces:
  ```csharp
  public enum PlaytestLaunchKind { NotEvaluated, DeveloperMode, Distribution }
  public static class PlaytestLaunchProfile
  {
      public static PlaytestLaunchKind Resolve();                          // プロセスで1回だけ判定し、識別も差し込む
      internal static void SetForTest(PlaytestLaunchKind kind, string steamId); // テスト専用の差し込み（InternalsVisibleTo Client.Tests）
  }
  internal static class PlaytestLocalSteamIdReader { public static bool TryRead(out string steamId, out string failureReason); }
  ```
- `PlaytestTitleGates.TryBegin(PlaytestLaunchKind kind, IPlaytestUploadRequester uploadRequester, out PlaytestTitleGateSequence sequence)`。`BeginComposed(PreviousSessionArtifacts, bool distributionBuild, IPlaytestUploadRequester, string, CancellationToken)` は引数名だけ `receiverSessionAllowed` → `distributionBuild`。
- `PlaytestUploadRunner.RequestUpload()` は変えない（呼び手の変更なし）。

- [ ] **Step 1: テストを書く（失敗させる）**

新規 `Client.Tests/PlaytestReceiver/Launch/PlaytestLaunchProfileTest.cs`：
```csharp
[Test]
public void EditorWithoutBuildInfoResolvesToDeveloperModeAndLeavesTheIdentityEmpty()
{
    // Editor には build-info.json が無いので開発者モードに確定し、識別は開発者モードの理由を持つ
    // The Editor has no build-info.json, so it settles as developer mode and the identity carries the developer-mode reason
    PlaytestLaunchProfile.ResetForTest();
    Assert.AreEqual(PlaytestLaunchKind.DeveloperMode, PlaytestLaunchProfile.Resolve());
    Assert.AreEqual(EmptyPlaytestSessionIdentity.DeveloperModeReason, PlaytestSessionIdentityProvider.Current.SteamIdAbsenceReason);
}

[Test]
public void DistributionWithLocalSteamIdSetsTheIdentity()
{
    PlaytestLaunchProfile.SetForTest(PlaytestLaunchKind.Distribution, "76561198000000001");
    Assert.AreEqual(PlaytestLaunchKind.Distribution, PlaytestLaunchProfile.Resolve());
    Assert.AreEqual("76561198000000001", PlaytestSessionIdentityProvider.Current.SteamId);
}

[Test]
public void DistributionWithoutReadableSteamIdLeavesAReasonedEmptyIdentity()
{
    PlaytestLaunchProfile.SetForTest(PlaytestLaunchKind.Distribution, "");
    StringAssert.Contains("SteamUser.GetSteamID", PlaytestSessionIdentityProvider.Current.SteamIdAbsenceReason);
}
```
`PlaytestBuildInfoFileTest.cs` は `Client.Tests/PlaytestReceiver/Launch/` へ git mv して残す。
`Gate/PlaytestGateDecisionTest.cs`・`PlaytestLaunchGateBlockTest.cs`・`PlaytestLaunchGateCheckingTest.cs`・`PlaytestLaunchGateIdentityTest.cs`・`Playtest/TitleGates/PlaytestTitleGatesBlockedVerdictTest.cs` を削除する。
`PlaytestTitleGatesTest.cs`・`PlaytestTitleGateSequenceTest.cs`・`CrashReportGateTest.cs`・`PlaytestConsentGateTest.cs`・`PlaytestUploadRunnerTest.cs` の `PlaytestLaunchGate.SetCurrent(PlaytestGateResult.Allowed(…))` を `PlaytestLaunchProfile.SetForTest(PlaytestLaunchKind.Distribution, "76561198000000001")` へ、`SetCurrent(DeveloperMode)` を `SetForTest(PlaytestLaunchKind.DeveloperMode, "")` へ置き換える。`PlaytestTitleGatesTest.cs:167` の「照合を通っていない再訪」ケースは削除する。
`PlaytestUploadRunnerTest.cs` に「配布版なら `RequestUpload` が受け口の `/v1/session` を1回叩いてから prepare へ進む」ケースを足す（`PlaytestReceiverFakes` のセッション応答を使う）。
`PlaytestSessionTest.cs` の 403→NotAllowed ケースと「allowed が立っていない200では許可しない」ケースを削除し、「`allowed` 欄が無い 200 でも token があれば Allowed」を足す。`PlaytestReceiverFakes.cs:58,62` は `"allowed":true` を残す（Worker の実応答と同じ形）。

- [ ] **Step 2:** Run: `uloop compile --project-path ./moorestech_client` → Expected: コンパイルエラー（`PlaytestLaunchProfile` 未定義）

- [ ] **Step 3: `PlaytestLaunchKind.cs` と `PlaytestLaunchProfile.cs` を書く**

```csharp
namespace Client.PlaytestReceiver.Launch
{
    // 起動が配布版か開発者モードか。配布版だけが受け口へ送る（ADR 0070）
    // Whether this boot is a distribution build or developer mode; only a distribution build ships to the receiver (ADR 0070)
    public enum PlaytestLaunchKind
    {
        NotEvaluated,
        DeveloperMode,
        Distribution,
    }
}
```

```csharp
using System.IO;
using Client.Game.InGame.BugReport.Playtest;
using Client.PlaytestReceiver.Steam;
using Game.Paths;
using UnityEngine;

namespace Client.PlaytestReceiver.Launch
{
    // 配布版かどうかをプロセスで1回だけ判定し、配布版ならローカルSteamのSteamIDを識別へ差し込む（ADR 0070）
    // Decides once per process whether this is a distribution build, and pushes the local Steam SteamID into the identity if so (ADR 0070)
    public static class PlaytestLaunchProfile
    {
        // MainMenuにはDIコンテナが無く、開始経路・走行役・タイトルが別々に読むのでstaticで持つ
        // The MainMenu has no DI container and the start paths, runner and title read it separately, so it is held statically
        private static PlaytestLaunchKind _kind = PlaytestLaunchKind.NotEvaluated;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlayMode()
        {
            _kind = PlaytestLaunchKind.NotEvaluated;
        }

        public static PlaytestLaunchKind Resolve()
        {
            if (_kind != PlaytestLaunchKind.NotEvaluated) return _kind;
            if (!IsDistributionBuild())
            {
                Apply(PlaytestLaunchKind.DeveloperMode, "");
                return _kind;
            }
            // 読めなくても開始は止めない。追跡の正は送信時トークンが決めるR2の置き場所
            // A failed read never stops the boot; the R2 location set by the send-time token is the tracking authority
            if (!PlaytestLocalSteamIdReader.TryRead(out var steamId, out var failureReason)) Debug.LogWarning($"[PlaytestReceiver] 記録のSteamIDを空で続行します: {failureReason}");
            Apply(PlaytestLaunchKind.Distribution, steamId);
            return _kind;

            #region Internal

            bool IsDistributionBuild()
            {
                // 開発者モードへ倒す経路も理由をログへ残す。無音だと配布版で送信が止まっても気づけない
                // The developer-mode fallback is logged too; silently, a distribution build that stopped shipping would go unnoticed
                if (!File.Exists(GameSystemPaths.BuildInfoFilePath))
                {
                    Debug.Log("[PlaytestReceiver] developer mode (no build-info.json)");
                    return false;
                }
                if (!new PlaytestSteamTicketProvider().IsSteamRunning())
                {
                    Debug.Log("[PlaytestReceiver] developer mode (Steam is not running)");
                    return false;
                }
                return true;
            }

            #endregion
        }

        internal static void SetForTest(PlaytestLaunchKind kind, string steamId)
        {
            Apply(kind, steamId);
        }

        internal static void ResetForTest()
        {
            ResetOnPlayMode();
        }

        private static void Apply(PlaytestLaunchKind kind, string steamId)
        {
            _kind = kind;
            // 識別の設定は判定と同じ1箇所で行う。開発者モードと読めなかった配布版は理由付きの空にする
            // The identity is set in the same single place as the decision; developer mode and an unreadable distribution get a reasoned empty one
            if (kind == PlaytestLaunchKind.Distribution && !string.IsNullOrEmpty(steamId)) PlaytestSessionIdentityProvider.SetCurrent(new LocalSteamSessionIdentity(steamId));
            else if (kind == PlaytestLaunchKind.DeveloperMode) PlaytestSessionIdentityProvider.SetCurrent(new EmptyPlaytestSessionIdentity(EmptyPlaytestSessionIdentity.DeveloperModeReason));
            else PlaytestSessionIdentityProvider.SetCurrent(new EmptyPlaytestSessionIdentity("テスター識別（SteamID）が無い（SteamUser.GetSteamID で読めなかった）"));
        }
    }
}
```
（`SetForTest`・`ResetForTest` はテストからの差し込み口。AGENTS.md の「デバッグ/テスト専用publicを残さない」に従い internal にする。`Client.PlaytestReceiver/AssemblyInfo.cs` に `InternalsVisibleTo("Client.Tests")` があることを確認する。`PlaytestLaunchGate.SetCurrent` が internal だったのと同じ扱い。）

- [ ] **Step 4: 識別と Steam 読み取りを移設・改名する**

`git mv` 後、`LocalSteamSessionIdentity` の namespace を `Client.PlaytestReceiver.Launch` に、コメントを「配布版の起動時にローカル Steam から読んだ SteamID を記録の識別として差し込む実体（ADR 0070）」に直す。`PlaytestLocalSteamIdReader` の namespace を `Client.PlaytestReceiver.Steam` に、クラスを `internal static class PlaytestLocalSteamIdReader` に、ログ接頭辞を `[PlaytestReceiver]` にする。smoke 側の呼び出し（`grep -rn StandalonePlaytestSmokeSteamIdReader`）を `PlaytestLocalSteamIdReader.TryRead` へ置き換える（Client.Starter が Client.PlaytestReceiver の internal を見るには `InternalsVisibleTo("Client.Starter")` が要る。無ければクラスを public にする方を選ぶ）。

- [ ] **Step 5: セッションから `NotAllowed` と `allowed` の必須化を外す**

`PlaytestSession.cs:74` の 403 分岐を削除する（403 は他の非200と同じ `Unreachable` 側の既存分岐へ落ちる）。`:83-87` の `if (!parsed.Allowed)` ブロックを削除する。`PlaytestSessionResponse.cs:42` の `allowed` の読み取りとフィールドを削除する。`PlaytestSessionResult.cs` の `PlaytestSessionOutcome.NotAllowed` を削除し、`PlaytestUploadFailurePolicy.cs:68` の `case NotAllowed` を削除する（`TicketRejected` の SessionRefused は残す）。

- [ ] **Step 6: 走行役を自前セッションへ**

```csharp
public void RequestUpload()
{
    // 送るのは配布版だけ。送らない場合も理由をログへ出す
    // Only a distribution build ships, and not shipping is logged too
    if (PlaytestLaunchProfile.Resolve() != PlaytestLaunchKind.Distribution)
    {
        Debug.Log("[PlaytestReceiver] developer mode; outbox boxes are left for the rsync path");
        return;
    }
    if (_running) { /* 既存の再要求ブロックをそのまま残す */ }
    _running = true;
    RunAsync(_session).Forget();
}
```
コンストラクタで `_session = new PlaytestSession(api, new PlaytestSteamTicketProvider());` を持つ（トークンを走行を跨いで使い回すため）。`PlaytestSession` の実際のコンストラクタ引数を確認し、それに合わせる。上の `/* … */` は既存コードをそのまま残すという意味で、実装では既存の `_rerunRequested` ブロックを消さない。

- [ ] **Step 7: タイトルのゲートから照合段を外す**

`PlaytestTitleGates.TryBegin` の第1引数を `PlaytestLaunchKind kind` にし、`verdict.TryGetAllowedSession(out _)` を `kind == PlaytestLaunchKind.Distribution` に置き換え、`if (verdict.IsBlocked)` ブロックを削除する。`EvaluateStart` の `PlaytestLaunchGate.TryPassLaunchCheck` ブロックを `PlaytestLaunchProfile.Resolve();`（直接起動でも識別を差し込むため）に置き換える。`BeginComposed`・`Compose` の `receiverSessionAllowed` を `distributionBuild` に改名する。クラス冒頭の summary を「タイトルのゲート（同意・前回異常終了の確認）の順序をここに閉じる」へ直す。

- [ ] **Step 8:** Run: `uloop compile --project-path ./moorestech_client` → Expected: ErrorCount 0（Task 6 の呼び手が残っていればそのエラーだけ。その場合は Task 6 まで進めてからコンパイルする）
- [ ] **Step 9:** Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlaytestLaunchProfile|PlaytestSession|PlaytestUpload|PlaytestTitleGate|CrashReportGate|PlaytestConsentGate|PlaytestBuildInfo|PlaytestBoxFilePolicy"` → Expected: 全件 PASS
- [ ] **Step 10: コミット**（Unity が生成した新規 `.meta` を含める）`git commit -m "feat(playtest): 起動時照合を配布版判定へ置き換え識別はローカルSteamから読む (ADR 0070)"`

### Task 6: タイトル・出展モード・smoke の呼び手を直す

**Files:**
- git mv: `Client.MainMenu/Playtest/PlaytestLaunchGateView.cs(.meta)` → `Client.MainMenu/Playtest/PlaytestTitleGateView.cs(.meta)`
- Scene（uloop 経由）: `Assets/Scenes/Game/MainMenu.unity`
- Modify: `Client.Starter/EventMode/EventModeAutoStart.cs:95-140`, `Client.Starter/PlaytestSmoke/StandalonePlaytestSmokeBootstrap.cs:24,76-86`, `StandalonePlaytestSmokePreconditions.cs:16-24`, `scripts/playtest/windows/run-smoke.ps1:18`
- Tests: `Client.Tests/Playtest/TitleGates/PlaytestLaunchGateViewSceneWiringTest.cs` → `PlaytestTitleGateViewSceneWiringTest.cs`（git mv）、`Client.Tests/EventMode/EventModeAutoStartVerdictTest.cs`、`Client.Tests/PlaytestSmoke/StandalonePlaytestSmokeSettingsTest.cs`（前提検査を扱っていれば）

- [ ] **Step 1: テストを書き換える（失敗させる）** SceneWiringTest の型名を `"PlaytestTitleGateView"`、GameObject 名を `"PlaytestTitleGates"` にし、`messagePopup` の検査があれば削除する。`EventModeAutoStartVerdictTest` は `DecideAutoStart` を失うので削除し、`EventModeAutoStart` に照合待ちが無いこと（開始が同期で呼ばれること）を既存の起動テストがあればそこで確かめる。無ければ削除だけにする（照合が無いので断念の分岐が消え、テストすべき判定が残らない）。smoke の前提テストは `TryFindFailure(settings, PlaytestLaunchKind.DeveloperMode, out _)` が true で理由に `developer mode` を含むこと、`Distribution` と既読同意で false になることに書き換える。
- [ ] **Step 2: View を改名・縮小する** `PlaytestTitleGateView` から `messagePopup` フィールド・`PlaytestLaunchGate` の購読・`EvaluateAsync`・`Show` を削除し、`Start()` を次にする。
  ```csharp
  private void Start()
  {
      // MainMenuにはDIコンテナが無いので、ここを合成ルートとして受け口と走行役を組む
      // The MainMenu scene has no DI container, so this is the composition root for the receiver client and the runner
      var receiver = new PlaytestReceiverClient(PlaytestReceiverConfig.BaseUrl);
      var uploadRequester = new PlaytestUploadRunner(receiver, PlaytestOutboxDirectories.FromGameSystemPaths());
      // 配布版かの判定はここで確定させ、タイトルのゲートを始める。照合は無いので待たない（ADR 0070）
      // The distribution decision settles here and the title gates begin at once; there is no check to wait for (ADR 0070)
      if (!PlaytestTitleGates.TryBegin(PlaytestLaunchProfile.Resolve(), uploadRequester, out var sequence)) return;
      if (_boundSequence == sequence) return;
      _boundSequence = sequence;
      consentPopup.Initialize(sequence);
      crashReportPopup.Initialize(sequence);
      // 表示は段階を映すだけ。購読はこの常時有効な合成ルートが持つ（非アクティブのポップアップにAddToしない）
      // The display only mirrors the step; this always-active root owns the subscription (never AddTo an inactive popup)
      sequence.Step.Subscribe(step =>
      {
          consentPopup.SetVisible(step == PlaytestTitleGateStep.Consent);
          crashReportPopup.SetVisible(step == PlaytestTitleGateStep.CrashReport);
      }).AddTo(this);
  }
  ```
  `TryBegin` は照合を失って常に true になるので、戻り値を `void` にしてもよい。その場合は呼び手とテストを合わせる。`_uploadRequester` フィールドが不要になれば消す。
  `IsShowingConfirmation()` の呼び手が無くなったら `PlaytestTitleGateSequence` から削除する（`grep -rn IsShowingConfirmation`）。
- [ ] **Step 3: シーンを uloop で直す** `uloop compile` 後に `uloop execute-dynamic-code` で次を実行する。MainMenu.unity を開き、`GameObject.Find("PlaytestLaunchGate")` を `PlaytestTitleGates` に改名し、`EditorSceneManager.MarkSceneDirty` → `SaveScene` する。保存後に `git diff --stat Assets/Scenes/Game/MainMenu.unity` で、変化が GameObject 名と消えた `messagePopup` 行だけであることを確かめる。
- [ ] **Step 4: 出展モード** `EventModeAutoStart` の `StartWhenLaunchVerdictSettlesAsync`・`DecideAutoStart`・`LaunchVerdictTimeoutSeconds`・`EventModeAutoStartDecision`（他に呼び手が無ければ型ごと）を削除し、呼び手（:97）を次の同期呼び出しへ置き換える。
  ```csharp
  // 照合は無いので待たずに新規生成して開始する（ADR 0070）
  // There is no check to wait for, so the world is regenerated and started at once (ADR 0070)
  GameSystemPaths.DeleteDefaultWorldDirectory();
  LocalGameLauncher.StartLocalGame();
  ```
- [ ] **Step 5: smoke** Bootstrap の `LaunchGateTimeoutSeconds` と `WaitForSettledVerdictAsync` を削除し、`StandalonePlaytestSmokePreconditions.TryFindFailure(settings, PlaytestLaunchProfile.Resolve(), out var failureReason)` にする。Preconditions の第2引数を `PlaytestLaunchKind kind` にし、`if (kind != PlaytestLaunchKind.Distribution) { failureReason = $"launch kind is {kind} (Distribution required; developer mode means build-info.json is missing or Steam is not running)"; return true; }` にする。`StartWhenPreconditionsHoldAsync` に await が残らなければ同期メソッドへ直す。`run-smoke.ps1:18` の予算から `launch gate 180` を外し、合計秒数を 180 減らす（コメントも直す）。
- [ ] **Step 6:** Run: `uloop compile --project-path ./moorestech_client` → ErrorCount 0。続けて `git grep -n "PlaytestLaunchGate\b\|PlaytestGateResult\|PlaytestGateStatus\|TryPassLaunchCheck\|WaitForSettledVerdictAsync" -- '*.cs' '*.ps1'` → 0件
- [ ] **Step 7:** Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlaytestTitleGate|EventMode|PlaytestSmoke|ExceptionSceneLocalizedText"` → PASS
- [ ] **Step 8: コミット** `git commit -m "feat(playtest): タイトル・出展モード・smoke から起動時照合の待ちを外す (ADR 0070)"`

### Task 7: Localization（照合キーの撤去と同意文の追記）

**Files:**
- Modify: `Localization/localization.csv:284-288,298,304`
- Modify: `moorestech_client/Assets/Scripts/Client.Localization/_CompileRequester.cs`（Unity が書き換える）
- Modify: `moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts`（再生成）

- [ ] **Step 1:** `git grep -n "ui\.playtest\.\(checking\|notAllowed\|unreachable\|ticketFailed\|malformedResponse\)\|Playtest\.\(Checking\|NotAllowed\|Unreachable\|TicketFailed\|MalformedResponse\)"` が CSV と生成物以外で 0 件であることを確かめる（Task 5・6 の後）。
- [ ] **Step 2:** CSV から5キーの行を削除する。`ui.playtest.gate.notStarted`（:304）の各言語を、照合に触れない文言にする。
  - Source/english: `The title confirmations are still being prepared. Try again in a moment.`
  - japanese: `タイトルの確認を準備中です。少し待ってからもう一度お試しください。`
  - german: `Die Bestätigungen auf dem Titelbildschirm werden noch vorbereitet. Bitte versuche es gleich noch einmal.`
- [ ] **Step 3:** `ui.playtest.consent.body`（:298）の最後の文の直前に1文を足す。
  - Source/english: `Reports are recorded under your Steam account (your SteamID and public display name).`
  - japanese: `報告はあなたのSteamアカウント（SteamIDと公開表示名）に紐づけて記録されます。`
  - german: `Berichte werden deinem Steam-Konto (SteamID und öffentlicher Anzeigename) zugeordnet gespeichert.`
  CSV のクォートを壊さないよう、python の `csv` モジュールで読み書きして差し替える（`csv.writer` の `quoting` は元ファイルと同じ最小クォートにし、差分が当該行だけであることを `git diff` で確かめる）。
- [ ] **Step 4:** `uloop compile --project-path ./moorestech_client`（SchemaWatcher が `_CompileRequester.cs` を書き換える。キーが消えるので、エラーが出たら memory の「localization.csv は force-recompile が要る」に従い `uloop compile --force-recompile`）→ ErrorCount 0
- [ ] **Step 5:** `pnpm --dir moorestech_web/webui gen:i18n && pnpm --dir moorestech_web/webui vitest run src/shared/i18n` → PASS
- [ ] **Step 6:** Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Localiz"` → PASS
- [ ] **Step 7: コミット** `git commit -m "feat(playtest): 照合専用の文言を撤去し同意文にSteamアカウント紐づけを追記 (ADR 0070)"`

### Task 8: ドキュメントと用語集

**Files:** `CONTEXT.md`（プレイテスト節）, `scripts/playtest/README.md`（Task 2・3 の残り）

- [ ] **Step 1:** `CONTEXT.md` のプレイテスト節に用語を1つ足す。
  ```
  **報告者名**:
  取り込み時点でSteamから引いた報告者の公開表示名とプロフィールURL。同一人物の判定はSteamIDが担い、報告者名は読みやすさのためのラベル。
  _Avoid_: ユーザー名, アカウント名
  ```
  **日次ダイジェスト** の定義に「報告者名を添える」を足す。
- [ ] **Step 2:** `git grep -n -i "許可リスト\|allowlist\|起動時照合" -- CONTEXT.md scripts/playtest tools/playtest-receiver/README.md` の残りを確かめ、現行の説明として残っている箇所を直す（`docs/superpowers/plans/` の過去 plan と ADR 本文は履歴なので触らない）。
- [ ] **Step 3: コミット** `git commit -m "docs(playtest): 報告者名を用語集へ足し許可リストの記述を撤去 (ADR 0070)"`

### Task 9: デプロイと実機確認

- [ ] **Step 1: Worker をデプロイする** `cd tools/playtest-receiver && pnpm test && npx wrangler deploy --profile moorestech`。成功後に `. ~/hermes-agent/data/services/playtest/env.sh && curl -s -o /dev/null -w '%{http_code}\n' -H "X-Admin-Key: $PLAYTEST_ADMIN_KEY" "$PLAYTEST_RECEIVER_BASE/v1/allowlist"` → `404`。
- [ ] **Step 2: R2 の残骸を消す** `npx wrangler r2 object delete moorestech-playtest/config/allowlist.json --remote --profile moorestech`（先に `wrangler r2 object get … --pipe` で中身が SteamID 一覧だけであることを確かめる）。
- [ ] **Step 3: 旧ビルドの確認** 許可リストに無い Steam アカウントで、今配布中の旧ビルドを起動する。「not on the playtest list」が出ずに Play locally へ進めることを、ユーザーに確かめてもらう（Mac mini からは操作できないため、依頼文と期待結果を報告に書く）。
- [ ] **Step 4: 取り込みの実走** `bash scripts/playtest/ingest.sh` を env 込みで1回走らせる。新着の箱があれば `ingest.json` に `steamPersonaName` が入っていることを確かめる。無ければ `STEAM_WEB_API_KEY` を読んだうえで、`steam_persona_resolve 76561198217468291 /tmp/p.json` 相当を scratchpad で1回叩き、名前が返ることを確かめる（鍵を出力しない）。ログは期待語ではなく `grep -n "WARN\|ERROR\|status=" <ingest ログ>` で拾い、今回の走行分が 0 件であることを合格条件にする。
- [ ] **Step 5: ダイジェスト** `python3 scripts/playtest/digest.py --dry-run`（既存の dry-run 手段がある場合。無ければ tests の fixture 経由で出力を目視）で、名前付きの行が出ることを確かめる。

### Task 10: 全ブランチレビュー（省略不可）

- [ ] **Step 1:** 必ず最後にコードレビュースキルで全ブランチレビューを実行する（自動実行・ゴール文言による省略不可）。スキルは **moores-code-review**。
- [ ] **Step 2:** 指摘の反映がソース（判定経路・条件式・その評価時点）に触れたら、Task 9 の Step 1・4 を反映後のコードで再実施する。
- [ ] **Step 3:** 未検証・残課題（例: 新しい配布ビルドでの通し検証、既存テスターへの同意文の個別連絡）は1件ずつ `bd create` で起票し、完了報告には issue 番号を列挙する。
- [ ] **Step 4:** 実機確認の合否は、`[WARN]`・`ERROR`・`status=`・`refused`・`developer mode`（配布版での実行時）の警告語が確認区間で 0 件であることで判定する。

## 判断記録（ADR）

- 設計の裁定は ADR 0070（`docs/adr/0070-playtest-drops-allowlist-and-resolves-reporter-names-at-ingest.md`）と `.decisions/2026-09-25-*.md`（5件）が正本。出所欄はそちらのとおり。
- Worker の応答に `allowed: true` を残す。配布済みの旧ビルドが `allowed` を必須にしており、欠くと旧ビルド全員が NotAllowed で止まるため。出所: agent判断（`PlaytestSession.cs:83-87` の実装事実）
- 起動時照合の置き換えは、同期の配布版判定（`PlaytestLaunchProfile`）にし、ReactiveProperty を持たない。照合という非同期の結論が無くなり、判定はファイル有無と Steam 起動の同期読みだけになるため。出所: agent判断（置換対象 `PlaytestLaunchGate.RequiresCheck` の判定をそのまま移す）
- アップロード走行は自前で `PlaytestSession` を1つ持ち、送信時にトークンを取る。出所: agent判断（ADR 0070 裁定「トークンは送信時に取りに行く」の実装形。`PlaytestSession.EnsureTokenAsync` が既に遅延認証を持つ）
- `SteamUser.GetSteamID()` の読み取りは smoke の既存部品を `Client.PlaytestReceiver/Steam` へ移して共用する。出所: agent判断（写しの禁止）
- Steam Web API 呼び出しは `receiver_curl` と共通化せず別関数にする。`receiver_curl` は `X-Admin-Key` を必ず付けるため、Steam へ鍵を送らないよう分ける。テストスタブも `STEAM_CURL_CMD` で分ける（`CURL_CMD` スタブは全 URL を受け口として扱うため）。出所: agent判断
- 403 の専用分岐（NotAllowed）を消し、他の非200と同じ扱いにする。Worker は 403 を返さなくなるため。出所: agent判断
- `ui.playtest.gate.notStarted` は消さずに文言を直す。タイトルの列がまだ始まっていない開始要求は、照合が無くても（View の Start より前の開始要求で）起こり得るため。出所: agent判断
- 出展モードの `DecideAutoStart` とその判定テストは削除する。照合が無くなると断念の分岐が消え、残る判定が無いため。出所: agent判断
- 新しい配布ビルドのリリースは本 plan に含めない。Worker のデプロイだけで旧ビルドの「not on the playtest list」は解消するため、リリースは人が別途 `release-playtest.sh` を叩く。出所: agent判断

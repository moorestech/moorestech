# Task 12 レポート: 辞書ロードstatusのUI消費＋失敗リトライ＋uninitialized分離（D2案A・C8・C9）

コミット: `60ff19547 feat: 辞書ロードstatusをUIへ露出し失敗リトライとuninitialized分離を実装`
ブランチ: `feature/localization-foundation`（worktree: `/Users/katsumi/moorestech/.worktrees/localization-foundation`）

---

## 1. 実装したもの

### (C9) `uninitialized` の分離（`generation` 意味流用の解消）
- `I18nStatus` に `"uninitialized"` を追加。初期snapshotの `status` を `"loading"` → `"uninitialized"` へ。
- `createTranslator` の `if (current.generation === 0) return "";` を `if (current.status === "uninitialized") return "";` へ置換。リポジトリ内に `generation === 0` 判定は残っていない（grep 0件）。
- `setDictionaryLoading` は「初回辞書が届く前は `uninitialized` を維持し、表示できる辞書がある再取得だけ `loading`」という遷移規則へ変更。`uninitialized` は「辞書が一度も届いていない」ことのみを意味する。

### (C8) ロード失敗の分類とリトライ（`I18nProvider`）
- `fetchDictionary` が `response.status` を見て `DictionaryFetchError`（`failure: "unavailable" | "staleRevision" | "transient"`）で投げ分ける。
  - `400` / `404` → `unavailable`（再試行せず即 `error`）
  - `409` → `staleRevision`（再試行も失敗表示もしない。新revisionのpushが既存経路で取り直すため）
  - それ以外の `!ok` と、通信断・JSON破損などの非 `DictionaryFetchError` → `transient`
- `transient` のみ **`setTimeout` 5秒で1回だけ**再試行し、それでも失敗したら `setDictionaryLoadError`。無限リトライにはならない（`retriesLeft` が 1 → 0 で打ち切り）。
- effect の cleanup で `abort.abort()` に加え `clearTimeout(retryTimer)` を行い、unmount時にタイマーが漏れない。

### (D2案A) statusのUI消費 — すべて**辞書非依存リテラル**
- 新規 `src/shared/i18n/dictionaryIndependentText.ts` に `DictionaryIndependentText` を集約（`t()` を通さない理由をコメントで明記）。`@/shared/i18n` から再export。
- `App.tsx`: `const { status, t } = useI18n();` を追加し、`status === "error"` のとき既存reconnectオーバーレイと同型（`Portal` + `Overlay fixed center backgroundOpacity={0.6} blur={2}` + `--z-reconnect`）の失敗表示を1つ描画。文言は `"Failed to load language data. / 言語データの読み込みに失敗しました。"`（ブリーフ指定どおり）。`data-testid="dictionary-error-overlay"`。
- `AppErrorFallback`: `status !== "ready"` のとき3つの `t()`（uiErrorOccurred / renderFailed / reload）をリテラルへフォールバック。
- `LanguageSelect`: 状態を `{ status: "loading" | "error" | "ready"; entries: LanguageEntry[] }` へ変更。`!response.ok` を reject に変えて `catch` で `error` へ落とす（従来の「失敗を空一覧として黙殺」を廃止）。error時は `ModeSwitch` の代わりにリテラルのエラー文＋再試行ボタン（`reloadCount` を進めてeffectで再fetch）を描画。

**装飾のための画像アセット追加は無し**（既存Mantine部品とCSS/DOMのみ）。エラー表示はいずれも既存の前例（reconnectオーバーレイ / `AppErrorFallback` / `PauseMenuPanel` のMantine `Button`）に合わせた。

---

## 2. TDDの証拠

### RED（Step 1→2）
コマンド: `cd moorestech_web/webui && npx vitest run src/shared/i18n src/features/settings`

```
 Test Files  3 failed | 6 passed (9)
      Tests  7 failed | 91 passed (98)

 ❯ src/shared/i18n/useI18n.test.ts (15 tests | 2 failed)
   × starts uninitialized before any dictionary request
     → expected 'loading' to be 'uninitialized'
   × does not warn or show a missing marker before the first dictionary generation is ready
     → expected 'loading' to be 'uninitialized'
 ❯ src/shared/i18n/provider/dictionaryRetry.test.ts (5 tests | 3 failed)
   × retries a transient failure once after five seconds and then reports the error
     → expected 'error' not to be 'error'
   × leaves a stale-revision 409 to the next revision push without retrying or erroring
     → expected 'error' to be 'loading'
   × cancels the pending retry timer when the effect is cleaned up
     → expected 'error' not to be 'error'
 ❯ src/features/settings/LanguageSelect.test.ts (3 tests | 2 failed)
   × 一覧取得に失敗したら辞書非依存リテラルのエラーと再試行ボタンを描画する
     → expected [] to deeply equal [ "Failed to load the language list. / 言語一覧の読み込みに失敗しました。" ]
   × 再試行ボタンで再取得し、成功したらoptionsへ復帰する
     → expected [] to have a length of 1 but got +0
```

**なぜ想定どおりか**
- `useI18n` の2件: 実装前は初期status が `"loading"` で `uninitialized` 状態が型・値として存在しないため。
- retryの3件: 実装前は失敗＝即 `setDictionaryLoadError` で、再試行も409の除外も無いため（`error` になってしまう）。一方で 404/400 の2件は実装前から即errorなので **最初からGREEN**であり、これは「非再試行の挙動を壊していない」ことのリグレッション保護として意図的に残した。
- LanguageSelect の2件: 実装前は `!response.ok` を空一覧として黙殺しており、エラー文も再試行ボタンもDOMに存在しないため。

### GREEN（Step 3→4後）
```
$ npx vitest run src/shared/i18n src/features/settings
 ✓ src/shared/i18n/provider/dictionaryRetry.test.ts (5 tests) 8ms
 ✓ src/shared/i18n/useI18n.test.ts (15 tests) 3ms
 ✓ src/features/settings/LanguageSelect.test.ts (3 tests) 11ms
 ✓ src/shared/i18n/allScreensI18n.test.ts (6 tests) 266ms
 Test Files  9 passed (9)
      Tests  98 passed (98)
```

### Step 5 検証
```
$ npx tsc -b        → exit 0（出力なし）
$ npm run lint      → exit 0（eslint src 出力なし）
$ npm test          → Test Files 82 passed (82) / Tests 540 passed (540)
$ npx playwright test --config e2e/playwright.config.ts --grep "i18n"
  ✓ localization.current切替でlocale別辞書を再取得し表示を更新する (213ms)
  ✓ ポーズメニューの言語選択で英語と日本語を往復する (412ms)
  2 passed
```

**正常系の確認**: 上記 i18n e2e は言語一覧のクリック操作を伴うため、新オーバーレイ（pointerを捕捉する）が出ていれば必ず失敗する。2件passは「辞書が読める通常起動では新しいエラー表示が一切出ない」ことの実証。

**e2e全体の既存失敗について**: 全e2e（`npx playwright test`）は 119 passed / 11 failed。この11件が本変更由来でないことを確認するため、変更11ファイルを `git stash push -u`（パス明示）で退避したベースラインで同じspecを実行し、同一の失敗（modeHud 2件・recipe 3件・connection 1件）が再現することを確認済み。stashは `git stash pop` で復元済み（`git checkout --` はパス明示でのみ使用）。

---

## 3. 変更ファイル

| ファイル | 種別 |
|---|---|
| `moorestech_web/webui/src/shared/i18n/i18nStore.ts` | 変更（status型・初期値・遷移規則・translator判定） |
| `moorestech_web/webui/src/shared/i18n/I18nProvider.tsx` | 変更（失敗分類・5秒1回リトライ・timer cleanup） |
| `moorestech_web/webui/src/shared/i18n/dictionaryIndependentText.ts` | 新規（辞書非依存リテラル集約） |
| `moorestech_web/webui/src/shared/i18n/index.ts` | 変更（re-export追加） |
| `moorestech_web/webui/src/app/App.tsx` | 変更（status===error の全面オーバーレイ） |
| `moorestech_web/webui/src/app/AppErrorBoundary.tsx` | 変更（status!==ready のリテラルフォールバック） |
| `moorestech_web/webui/src/features/settings/LanguageSelect.tsx` | 変更（status付き状態・エラー表示＋再試行） |
| `moorestech_web/webui/src/shared/i18n/useI18n.test.ts` | テスト（uninitialized 3ケース） |
| `moorestech_web/webui/src/shared/i18n/provider/dictionaryRetry.test.ts` | テスト新規（リトライ5ケース） |
| `moorestech_web/webui/src/features/settings/LanguageSelect.test.ts` | テスト新規（3ケース） |
| `moorestech_web/webui/src/shared/i18n/allScreensI18n.test.ts` | テスト（既存失敗ケースを 500→404 へ。理由をコメント併記） |

行数はすべて200行以下（最大 `i18nStore.ts` 156行）。`src/shared/i18n` 直下の非テストファイルは6件（+3ディレクトリ）で10件制限内。テストファイル名は vitest の include 設定が `src/**/*.test.ts` のみのため `.test.ts`（既存の react-test-renderer 前例に一致）。

---

## 4. 自己レビュー所見

- **辞書非依存の証明**: `LanguageSelect.test.ts` はわざと `setDictionaries("english", {}, {}, {})`（全辞書空）で描画し、`t()` 経路なら `[!ui.settings.language]` になる状況でエラー文が期待どおりのリテラルで出ることを検証している。`t()` を通していない。
- **リトライは1回で止まる**: retryテストで `advanceTimersByTimeAsync(5000*4)` を追加し、fetch 呼び出しが6回（=初回3＋再試行3）から増えないことを確認。
- **タイマー漏れなし**: cleanup後に時間を進めても fetch 追加なし・status が `error` にならないことをテスト済み。
- **`generation === 0` 残存なし**: `grep -rn "generation === 0" src` は0件。`generation` は「欠落キー警告の世代リセット」という本来の用途としてのみ残る。
- **正常系**: 初期status が `uninitialized` のため通常起動でオーバーレイは出ない（i18n e2eで実証）。
- **前例一致**: オーバーレイは reconnect と同型・同トークン（`--z-reconnect`）、`Button`/`Text` は隣接の `PauseMenuPanel`・`AppErrorFallback` と同じMantine部品。webui-design §6「装飾の画像アセット化禁止」に抵触する追加は無い。

---

## 5. 懸念事項（レビュー時に見てほしい点）

1. **辞書エラーオーバーレイは操作をブロックし、自動復帰手段が無い**: reconnectと同型にした結果、`status === "error"` の間は `Overlay` がpointerを捕捉する。1回のリトライも尽きて `error` になると、次のrevision push（サーバ側再pushか言語切替）まで解除されない。ブリーフ指定（「reconnectオーバーレイと同型」「App側は失敗表示を1つ」）に忠実に実装したが、再読み込みボタンを添えるか非ブロッキング表示にする選択肢は残っている（裁定範囲外と判断し据え置き）。
2. **初回ロード失敗後の `t()` は空文字でなく `[!key]` マーカーになる**: `uninitialized` → `error` へ遷移すると `createTranslator` の空文字返しが外れるため。全面オーバーレイ（暗転＋ぼかし）の下に出るだけで診断性はむしろ上がると判断した。`error` から `loading` へ戻った場合も同様。
3. **`409` は `loading` のまま留まる**: 再試行も失敗表示もしないため、新revisionのpushが来なければ status は `loading` のまま（表示は直前の辞書が継続）。ブリーフの「409は既存のrevision再push経路が回す」という前提に依存している。
4. **`allScreensI18n.test.ts` の既存ケースを 500→404 に変更**した。500は今回から「5秒後に1回再試行」になり、`vi.waitFor`（既定1秒）では `error` に到達しなくなるため。再試行経路のカバレッジは新規 `provider/dictionaryRetry.test.ts` が持つ。
5. **e2e全体は既存11件が赤**（modeHud/recipe/research/skit/train/connection/commonHud）。ベースライン比較で本タスク由来でないことは確認済みだが、ブランチ全体としては Task 19 のレビューで扱う必要がある。

> **[レビュー後追記による訂正]** 上記 1〜4 のうち 1・2・4 はレビュー指摘（Important 1〜3）として第2コミットで解消済み。**3 の記述は誤り**だった。辞書未取得のまま 409 を受けた場合、当時の実装では `setDictionaryLoading` が `uninitialized` を維持するためオーバーレイも出ず、全 `t()` が空文字＝**無表示のまま停止**する（「表示は直前の辞書が継続」ではない）。現在は status が取得ライフサイクル専任になったため、409 後の status は `loading`、表示内容は `dictionaries` の有無で決まる（辞書があれば直前の辞書が継続、無ければ空文字）。詳細は §6。

---

## 6. レビュー指摘対応（追記・2026-08-03）

コミット: `fix: 辞書の有無をstatusから分離し失敗時の表示保護とリロード手段を回復`

### Important 1: `status` の2軸兼務を型で分離（採用: 判別共用体版）
「最小対応の `hasEverLoaded: boolean`」ではなく、レビュー第1案の**判別共用体**を採用した。

- `I18nSnapshot` の `dictionary` / `fallbackDictionary` / `sourceDictionary` の3フィールドを
  `dictionaries: { kind: "none" } | { kind: "loaded"; dictionary; fallbackDictionary; sourceDictionary }` の1フィールドへ置換。
- `createTranslator` の空文字ガードを `dictionaries.kind === "none"` へ。これで **uninitialized / loading / error のいずれでも「辞書ゼロなら空文字」**となり、旧 `generation === 0` の被覆範囲が完全に回復した（ガードの意味的縮小＝デグレを解消）。
- 副産物として `setDictionaryLoading` の分岐（`uninitialized` を維持するか否か）が不要になり、常に `loading` へ移す素直な実装に戻った。`status` は取得ライフサイクル専任、辞書の有無は `dictionaries` が持つ、と責務が1軸ずつになった。
- テスト追加: `useI18n.test.ts` に「初回ロード失敗後も `t()` は空文字・`console.warn` も出ない」を追加（`ui.mainMenu.playLocally` と `ui.settings.language` の2キーで検証）。既存の「初回取得中」ケースは status が `loading` になった点だけ更新し、検証内容（空文字・警告なし）は不変。
- 逆転検証: ガードを `current.status === "uninitialized"` に戻すと当該2件が落ちることを実測（`× keeps translations empty and silent after the very first load failed` / `× does not warn or show a missing marker...`）。
- 影響範囲の追随: `provider/dictionaryGeneration.test.ts` は `getI18nSnapshot().dictionary[...]` を `currentTranslation()` ヘルパ（`kind === "loaded"` を1箇所で剥がす）経由へ。`useI18n.test.ts` の各snapshot構築は `loadedDictionaries(...)` ヘルパへ機械的に置換し、入力値・アサーションは変更していない。

### Important 2: オーバーレイにリロード手段を追加
- `App.tsx` の辞書エラーオーバーレイに `AppErrorFallback` と同型の `location.reload()` ボタン（文言 `DictionaryIndependentText.reload`・`data-testid="dictionary-error-reload"`）を追加。ポインタを捕捉したまま解除手段が無い恒久ロックを解消した。
- 任意項目だった「辞書がある場合は非ブロッキング通知にする」は今回は入れていない（`dictionaries.kind` で判別可能になったので、必要なら次段で低コストに実施できる）。
- テスト追加: `App.architecture.test.ts` に「`dictionary-error-overlay` 以降のソースに `location.reload()` と `DictionaryIndependentText.reload` が含まれる」ガードを追加（同ファイル既存のソース検査様式に合わせた）。

### Important 3: 5xx を通るテストを回復
- `provider/dictionaryRetry.test.ts` に「500 → 5秒後に1回リトライ → `error`、かつ直前の ready 辞書と locale を保持」を追加。`classifyResponseStatus` の既定分岐（5xx→transient）と「失敗時に前回辞書を置き換えない」の両方を1ケースで押さえる。
- 逆転検証: `classifyResponseStatus` を `status >= 500` も `unavailable` にすると当該ケースが落ちることを実測。
- `allScreensI18n.test.ts` の 404 化はそのまま（理由コメント付き）。5xx経路は上記の新ケースが担保する。

### 再検証（Important 1〜3適用後）
```
$ npx tsc -b        → exit 0
$ npm run lint      → exit 0
$ npm test          → Test Files 82 passed (82) / Tests 543 passed (543)
$ npx playwright test --config e2e/playwright.config.ts --grep "i18n" → 2 passed
```
`i18nStore.ts` 159行 / `App.tsx` 155行で200行制限内。

# CEF Web UI 描画不能調査記録（2026-07-04, tree2 worktree）

## ステータス：解決済み ✅

真因は **`moorestech_web/webui/vite.config.js`（`vite.config.ts` のビルド残留成果物）が本来の `.ts` 設定より優先して読み込まれ、その中の古い `fs.allow` 制限が `@mantine/core/styles.css` の配信を 403 でブロックしていたこと**。CEF・Unity・git worktree はすべて無罪で、Web UI 側（Vite dev server）だけの問題だった。

## 目的

ゲーム画面で CEF（`Client.WebUiHost` が起動する Web UI）を実際に表示し、代わりに既存 uGUI を非表示にする。`Ctrl+I` で uGUI と CEF UI を排他的に切り替えられるようにする。

## 完了した作業

1. **CEF ネイティブバイナリの LFS 修復**
   - 当時の UPM git パッケージで Git LFS 設定が不足し、ネイティブバイナリが 131B の LFS ポインタのままだった。
   - 当時は PackageCache の手動修復で調査を継続したが、この手順は廃止済み。現在は `../design/cef-binary-integration.md` と `scripts/setup-cef.*` を正とする。

2. **Node/pnpm 環境のセットアップ**
   - `moorestech_web/setup.sh` を実行し、Node.js 20.18.1 + pnpm 9.15.0 を `moorestech_web/node/mac-arm64/` に配置。

3. **`moorestech_web/webui/pnpm-workspace.yaml` の設定不備を修正**
   - `packages:` フィールドが欠落しており、pnpm 9.15.0 で `pnpm exec`（`pnpm --version` すら）が `ERROR packages field missing or empty` で失敗していた。
   - `packages: [.]` を追記して解消。

4. **`WebUiCefToggle.cs` の新規実装**
   - `Assets/Scripts/Client.Game/InGame/UI/UIState/WebUiCefToggle.cs`
   - `MainGameUI` ルートに配置し、`CefUnity` 以外の直下の子（uGUI 群、約19個）を自動収集。
   - `Ctrl+I` で `CefUnity` の GameObject と uGUI 群の `SetActive` を排他的に切り替える。
   - 既存の `UIRoot.cs`（`Ctrl+U` で全 UI 一括非表示）と同じ実装パターン（生 `Input` ポーリング、TODO コメント含む）に準拠。

5. **`MainGameUI.prefab` の更新**
   - `CefUnityBrowserSample` コンポーネントを `m_Enabled: 1` に変更（従来は `0` で無効化されていた）。
   - `_url` を `https://google.com`（デフォルト値）から `http://localhost:5173/` に変更。
   - `WebUiCefToggle` コンポーネントを追加し、`cefUnityRoot` フィールドを `CefUnity` 子オブジェクトに配線。
   - `CefUnity` GameObject 自体は `m_IsActive: 0`（デフォルト非アクティブ）のまま維持。トグルは実行時に制御。

6. **`moorestech_web/webui/vite.config.js` / `vite.config.d.ts` の削除（今回の本質的な修正）**
   - 詳細は後述。

## 症状（解決前）

`CefUnityBrowserSample` を有効化すると：
- ブラウザプロセス（`cef-unity-server` とその Helper 群）は正常に起動する。
- Vite dev server（`http://127.0.0.1:5173/`）への TCP 接続は確立される。
- ネイティブ側ログで `on_accelerated_paint` イベントが実際に発火している。
- **しかし Unity 側で受け取るテクスチャの中身は常に全ゼロ（完全に透明/黒）** で、画面には何も表示されない。

一方、Chromium 内蔵のエラーページ（`ERR_CONNECTION_REFUSED`）は正しく描画される（非ゼロピクセル）。CEF の描画パイプライン自体は機能しており、**実際の Vite/React ページのコンテンツだけが描画バッファに反映されない**という再現性のある症状だった。

## 根本原因の特定に至った経緯

### 誤った方向への調査（否定された仮説）

worktree であることが原因ではないか、という仮説のもとで以下をすべて検証したが、いずれも否定された：

| # | 仮説 | 検証方法 | 結果 |
|---|---|---|---|
| 1 | セッション内の蓄積状態（プロセス残留・Mach port 汚染） | Unity・全プロセスを完全終了 → クリーンな状態から再試行 | ✗ 変わらず失敗 |
| 2 | Quality Level の違い（URP Asset 差異） | ランタイムで Low に統一して再試行 | ✗ 変わらず失敗 |
| 3 | `$TMPDIR` 共有の CEF キャッシュ汚染 | `cef_unity_cache` を完全削除して再試行 | ✗ 変わらず失敗 |
| 4 | GPU/グラフィックス API の違い | 比較 | 同一（Metal, Apple M5） |
| 5 | worktree のパス長・ネスト構造 | 短いパス・非ネストの新規 worktree（`/Users/katsumi/cef-path-test`）で再試行 | ✗ 変わらず失敗 |
| 6 | PluginImporter の `.meta` 設定差異 | メインリポジトリと diff | 完全に同一 |
| 7 | macOS 画面収録権限（TCC） | システム設定の画面収録一覧を確認 | 該当エントリなし、原因と考えにくい |

3点比較（メインリポジトリ＝成功、tree2 worktree＝失敗、新規 worktree＝失敗）まで確定させたが、真因には辿り着けなかった。

### ブレークスルー：Fable 5 subagent への相談

行き詰まった時点で `fable-5` モデルの subagent に調査内容一式を渡して助言を求めたところ、以下の重要な指摘を得た：

1. **「全ゼロ＝転送失敗」ではなく「透明な空ページが正しく描画されている」可能性がある。** CEF OSR の背景が transparent で、React アプリが空 DOM のままブートに失敗した場合、正常に全ゼロで描画される。エラーページは自前の白背景 CSS を持つ「中身のあるページ」だから描画された、という説明に矛盾がない。
2. **`ViteProcess.cs` の `--strictPort` + `KillAnyLingering()`（5173番ポートを握るプロセスを無差別に kill）** により、複数 worktree が同じポートを奪い合うレースコンディションが起きている可能性がある。
3. 次の一手として **実ブラウザで Vite dev server に直接アクセスしてコンソールを見る** ことを提案された。

### 実際の検証と特定

Fable 5 の提案通り、tree2 の Vite dev server（`http://127.0.0.1:5173/`）を **CEF を介さず通常の Chrome で直接開いた** ところ、**真っ白な空白ページ**（タブタイトルは "moorestech Web UI" で HTML 自体は読み込めている）だった。これは CEF・Unity・worktree と完全に無関係な、Web UI 側単体の不具合であることを意味する。

Chrome の DevTools コンソールを確認すると：
```
Failed to load resource: the server responded with a status of 403 (Forbidden)
  → /node_modules/@mantine/core/styl...
```

`src/main.tsx` で `import "@mantine/core/styles.css"` を直接 import しており、このリクエストが Vite の `server.fs.allow` 制限に阻まれて 403 になっていた。ESM のモジュール読み込みが失敗し、`ReactDOM.createRoot().render()` に到達する前に例外が発生 → React アプリがマウントされず空白ページ、という完全な説明がついた。

`vite.config.ts` の該当箇所：
```ts
fs: {
  // リポジトリ外や node_modules 階層への /@fs/ アクセスを封じる
  allow: ["./src", "./public", "./index.html"],
  strict: true,
},
```

この制限は 2026-04-22 の scaffold コミット（Tailwind 版）で追加されたもので、当時は node_modules から直接 CSS を import する必要がなかったため問題化しなかった。その後の Mantine 移行で `@mantine/core/styles.css` の直接 import が必要になったが、この `fs.allow` は更新されていなかった。

**メインリポジトリで「成功」していたのは、そちらが `feature/electric-wire-system` ブランチ（Mantine 移行前の Tailwind 版）を動かしており、そもそもこの import が存在せず、たまたま同じ制限に引っかからなかっただけ** だった。worktree かどうかは一切関係なかった。

### 修正の試行錯誤

1. `allow` に `"./node_modules"` を追加 → **効果なし**（Vite は `node_modules` を含むエントリを allow リストから暗黙的に除外する挙動があるらしく、エラーメッセージの許可リスト表示に反映されなかった）。
2. `allow` に `"./node_modules/@mantine"`（より具体的なパス）を追加 → **これも効果なし**（同様に無視された）。
3. `allow: ["."]`（プロジェクトルート全体）に変更 → **それでも効果なし**。エラーメッセージの許可リストが一切変化しなかったことから、設定変更そのものが反映されていないと判明。
4. 原因を調査したところ、**`moorestech_web/webui/vite.config.js` と `vite.config.d.ts` という別ファイルが存在**しており（`.gitignore` 登録済みの生成物、おそらく過去に `tsc -b` を実行した際の残留ビルド成果物）、Vite が `.ts` より **`.js` を優先して読み込んでいた**。この `.js` には編集前の古い `fs.allow` がそのまま残っていた。
5. `vite.config.js` / `vite.config.d.ts` を削除 → 以後は `vite.config.ts` が正しく読み込まれるようになった。
6. `allow: ["."]`（プロジェクトルートを許可）で再検証 → `@mantine/core/styles.css` が 200 で配信され、リポジトリ外（`/@fs/etc/passwd` 等）は引き続き 403 でブロックされることを確認（セキュリティ要件も維持）。

### 最終確認

- 実ブラウザ（Chrome）で `http://127.0.0.1:5173/` をリロード → **実際に "moorestech Web UI" の画面（Inventory・Recipe・Items パネル）が正常に描画される**ことを確認。
- Unity の CEF（`CefUnityBrowserSample`）で `GetRawTextureData`/`Graphics.Blit` 経由のピクセル読み取り → **2304/2304 サンプル全て非ゼロ**（描画成功）。
- ゲーム画面のスクリーンショットで、**背景の3Dシーンが透過して見えつつ、CEF の Web UI（ヘッダー・インベントリグリッド・アイテムアイコン）が正しくオーバーレイ表示される**ことを最終確認。

## 教訓

- 「全ゼロ＝転送失敗」と決めつけず、「正しく描画された結果として全ゼロ（透明ページ）」の可能性を早期に疑うべきだった。CEF を経由せず**実ブラウザで直接 Vite dev server にアクセスして確認する**のが、最初に行うべき最も安価で情報量の高いデバッグ手順だった。
- `.gitignore` されたビルド生成物（`vite.config.js` 等）が `.ts` ソースより優先して読み込まれるケースがあることに留意する。設定変更が反映されない場合は、同名の生成物ファイルの有無を疑う。
- worktree 間の環境比較は有用な切り分け手法だが、比較対象が実際に「同じもの」を指しているか（今回はブランチが異なり Web UI の中身自体が違った）を先に確認すべきだった。

## 関連ファイル

- `moorestech_web/webui/vite.config.ts`（`fs.allow` 修正）
- `moorestech_web/webui/src/main.tsx`（`@mantine/core/styles.css` の import 元）
- `moorestech_client/Assets/Scripts/Client.WebUiHost/Boot/ViteProcess.cs`（Vite 起動・`KillAnyLingering`）
- `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/WebUiCefToggle.cs`（新規実装）
- `moorestech_client/Assets/Asset/UI/Prefab/MainGameUI.prefab`（CEF 有効化・URL設定）

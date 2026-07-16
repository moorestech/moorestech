# Web UI 動的ポート化設計（複数Unityプロセス同時起動対応）

日付: 2026-07-16
状態: 承認済み

## 背景と目的

Web UI ホストのポートが全て固定値（Vite 5173 / Kestrel 5050）でハードコードされており、
複数の Unity Editor（worktree 併用）を同時起動すると 2 つ目がポート衝突する。さらに
`ViteProcessKiller.KillAnyLingering()` が「5173 を握る pid」を無条件 kill するため、
2 つ目の起動が 1 つ目の Vite を巻き添えにする。

本設計は以下を実現する:

1. ベースポートを被りにくい値へ変更（Vite 25173 / Kestrel 25050）
2. ポートの自動衝突回避（インクリメント探索）で複数インスタンス共存
3. CEF 表示・CORS 検査・残留プロセス掃除を実ポート追従に変更

## 現状の固定ポート依存箇所（調査結果）

| 箇所 | 内容 |
|---|---|
| `ViteProcess.cs:107` | `vite --port 5173 --strictPort` |
| `KestrelServer.cs:16` | `const int Port = 5050` |
| `WebUiEndpoints.cs:147` | CORS/WS オリジン検査が `localhost:5173` 固定 |
| `ViteProcessKiller.cs` | 5173 の LISTEN pid を無条件 kill |
| `WebUiHost.cs:88` | ready ログの URL が 5173 固定 |
| `moorestech_web/webui/vite.config.ts` | port 5173 / proxy target 5050 直書き |
| `MainGameUI.prefab` | `CefUnityBrowserSample._url: http://localhost:5173/` |

`e2e/playwright.config.ts` は独自 PORT + mock-host で自己完結しており影響なし。
ゲームサーバーポート 11564 の衝突は本設計のスコープ外（既知の別問題）。

## 設計

### ベースポート値

- Vite: **25173**、Kestrel: **25050**
- 根拠: 元の値 +20000 で覚えやすく、Linux/macOS/Windows の ephemeral レンジ
  （32768〜 / 49152〜）より下、既知の常用ツールと非衝突（25565=Minecraft は回避）。

### 起動シーケンス

```
1. Kestrel: 25050 から +1 ずつ最大 20 回 bind 試行 → 実ポート確定
2. Vite: --strictPort を外し --port 25173 で spawn（Vite 標準の自動インクリメント）
   - 環境変数 MOORESTECH_BACKEND_PORT で Kestrel 実ポートを注入
   - stdout の "Local: http://127.0.0.1:{port}/" から Vite 実ポートをパース
3. 実ポート確定後: CORS 検査値・WebUiHost.ViteUrl をセット、ready ログに実 URL を出力
4. CEF: 新規コンポーネントが WebUiHost 起動完了を待って LoadUrl(実URL)
```

### コンポーネント別の変更

**KestrelServer**
- ベース 25050 から最大 20 ポートを順に bind 試行。bind 失敗（AddressInUse）は
  OS ネットワーク境界のため try-catch を許容（境界根拠コメントを明記）。
- 確定した実ポートを公開する。

**ViteProcess**
- `--strictPort` を削除し、ベース 25173 を `--port` に指定。衝突時は Vite が自動で
  次のポートへ逃げる。
- `ProcessStartInfo.Environment` に `MOORESTECH_BACKEND_PORT`（Kestrel 実ポート）を設定。
- stdout の `Local:` 行から実ポートを正規表現でパースし、ready 判定と同時に確定する。
  パースできない場合は起動失敗（false）として扱う。

**vite.config.ts**
- `port: Number(process.env.MOORESTECH_VITE_PORT ?? 25173)`（Unity からは --port 引数優先）
- proxy target: `http://127.0.0.1:${Number(process.env.MOORESTECH_BACKEND_PORT ?? 25050)}`
  （`pnpm dev` 単体起動時のフォールバックとして 25050 を維持）
- `strictPort: true` を削除。

**WebUiEndpoints（CORS/WS オリジン検査）**
- 固定文字列比較をやめ、起動時に確定した Vite 実ポートと突き合わせる遅延バインドにする。
- 実ポート保持は Client.WebUiHost 内の実行時ポート保持クラス（static）に集約する。

**ViteProcessKiller（残留プロセス掃除）**
- 自インスタンスの Vite (pid, port) を `SessionState` に記録し、cleanup 時はそれを kill
  （ドメインリロード越しに追跡、他 worktree 不干渉）。pid 再利用誤爆を防ぐため、
  kill 前に「記録した port を当該 pid が LISTEN しているか」を lsof で照合する。
- クラッシュ孤児対策: 起動時にポート範囲（ベース〜ベース+N）の LISTEN pid を列挙し、
  **cwd が自 worktree の webuiRoot に一致するもののみ** kill する
  （lsof の cwd 照合。他 worktree の Vite には触れない）。

**CEF 表示（MainGameUI.prefab + 新規コンポーネント）**
- prefab の `_url` を `about:blank` に変更（uloop execute-dynamic-code 経由。手編集禁止）。
- 新規コンポーネント（Client.Game 側、CefUnityBrowserSample と同じ GameObject）が
  WebUiHost の起動完了を待ち、`CefUnityBrowserSample.LoadUrl(WebUiHost.ViteUrl)` を呼ぶ。
- WebUiHost が起動失敗（node 欠如等）の場合は about:blank のまま（現状の無 UI と同等）。

### エラーハンドリング

- Kestrel: 20 ポート全滅なら起動失敗として既存のロールバック経路（false 返却）に乗せる。
- Vite: ready タイムアウト（既存 30 秒）と実ポートパース失敗はどちらも false。
- try-catch は bind 試行（OS 境界）のみ。他は既存方針どおり条件分岐で処理。

### テスト・検証

- コンパイル: `uloop compile`
- 動作確認: PlayMode 起動で ready ログの実 URL・CEF 表示・WS 接続を確認。
  ポート占有状態（ダミーで 25173/25050 を LISTEN）を作り、インクリメント回避と
  CEF が実ポートへ到達することを確認する。
- 掃除ロジック: 別 worktree の Vite を模したプロセスが kill されないこと（cwd 照合）を確認。

## 不採用案

- **完全動的（port 0 / OS 割当）**: 衝突回避は最強だが `pnpm dev` 単体起動が Kestrel へ
  到達不能になり、URL が毎回変わって外部ブラウザ確認が不便。
- **worktree パスハッシュでポート導出**: worktree ごとに安定 URL になるが、衝突時の
  インクリメントは結局必要で、複雑さに見合わない（YAGNI）。

## 自己反証

- 2 Editor 同時起動で同ベースポートを取り合う → bind は OS がアトミックに裁定するため、
  負けた側がインクリメントで次を取るだけで破綻しない。
- Editor クラッシュで孤児 Vite が残る → 旧設計の「固定ポート kill」が持っていた掃除機能は
  cwd 照合付き範囲スイープで自 worktree 分のみ引き継ぐ。
- 検証済みの範囲: 本 spec 時点ではコード調査のみ。Vite stdout の `Local:` 行形式は
  現行 ViteProcess.cs の ready 判定（`Local:` 検知）で使用実績があるが、
  ポート番号パース自体は実装時に実出力で確認する。

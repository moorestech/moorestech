# Phase A 実行計画: インフラ3本柱

親: `../MIGRATION.md` / 進捗: `../TODO.md`
全 Phase と並行可能。**A1 が最優先**（実機検証が全 Phase でこれ待ちになっている）。

---

## A1: CEF バイナリ恒久統合（INFRA-1）

### 現状の問題
`moorestech_client/Packages/manifest.json` は `jp.juha.cefunitysample` を git URL 参照しており、
libcef 等の LFS 実体が UPM 解決時にポインタのまま落ちてくる。真因は環境の `git lfs install`
未実行だが、環境依存の手動回避（lfs install + UPM 再解決）を繰り返しているのが現状。

### 作業
1. パッケージを **embedded package 化**（`Packages/` 配下へ実体を取り込む）するか、LFS 非依存の
   配布形態（tarball / レジストリ / バイナリ別取得スクリプト）にするかを比較して決定
   - 判断基準: リポジトリサイズ影響（libcef は巨大）/ worktree 頻用運用との相性 / Windows 対応（A3）との整合
   - リポジトリ直コミットが重すぎる場合は「初回セットアップスクリプトでバイナリ取得 + gitignore」が有力
2. 決定した方式を実装し、**クリーンな worktree からの再現手順**（clone → 起動まで）で検証
3. `CLAUDE.md`/`AGENTS.md` 等に残る手動回避手順の記述を削除

### 完了条件
- 新規 worktree で手動介入なしに CEF が起動する
- 実機 web↔host 連携検証（これまで mock-host e2e 止まりだった検証）が可能になる

---

## A2: 入力・IME・フォーカス排他（INFRA-2）

### 現状の問題
- **入力二重配送**: Web パネルへのクリックが uGUI / 3D ワールドにも届く
- CEF へのマウス/キーボード/IME パススルーが未検証。フォーカス往復後の入力復活と IME 入力
  （セーブ名・ブループリント名等の InputField 系）が未保証

### 作業
1. CEF 側のヒットテスト結果（DOM 要素上か透明部か）を Unity 側へ返す経路を実装し、
   DOM 上のイベントは世界/uGUI へ流さない入力排他を一元化する
2. キーボードフォーカスの主権規則を決める（テキスト入力中は Unity キーバインド停止、Esc で返却等）
3. IME 動作を実機確認（日本語入力でセーブ名・BP 名が入力できる）
4. Web 側は `src/shared/uiState/activeLayer.ts` の排他レイヤーと整合させる

### 完了条件
- Web ボタンのクリックが背後の 3D 操作を誘発しない
- テキスト入力（IME 含む）と Unity キーバインドが衝突しない
- 検証は実機（A1 完了後）。注意: CEF パネルへは `InputSystem.QueueStateEvent` 注入が効かない
  （Web は Unity 入力系を経由しない）ため、実機ジェスチャ検証はブラウザ/E2E 側で行う

---

## A3: 本番静的配信 + Vite 死活 + Windows（INFRA-9/8）

### 現状の問題
- フロント配信は「Unity が Node.js ごと Vite dev サーバを spawn」する開発形態のみ。
  出荷形態（ビルド済み静的アセット）が存在しない
- Vite dev サーバ停止時の検知・復旧なし
- `ViteProcess.cs:246` に Windows の pid 特定 TODO が残る（Windows/Linux 未対応）

### 作業
1. `pnpm build` の成果物を Kestrel の静的ファイル配信で返すモードを `KestrelServer.cs`/
   `WebUiEndpoints.cs` に追加（dev=Vite / prod=静的 の切替。ビルド成果物の同梱方法も決める）
2. dev モードに Vite 死活検知（ヘルスチェック + 失敗時のトースト/ログ + 再起動）
3. Windows での Vite spawn / pid 管理 / CEF 動作を確認（A1 の配布方式と合わせて検証）

### 完了条件
- Vite を起動せずにビルド済み UI でゲームが動く
- dev で Vite が死んでも無言で固まらない

---

## 検証ゲート（共通）

`.cs` 変更は `uloop compile` / 起動系の変更は PlayMode スモーク
（`unity-playmode-recorded-playtest`）。A1・A2 完了時に実機 web↔host 連携の統合検証を 1 回実施し、
結果を `../TODO.md` に記録する。

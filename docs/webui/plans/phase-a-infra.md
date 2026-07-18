# Phase A 実行計画: インフラ（A1〜A5）

親: `../MIGRATION.md` / 進捗: `../TODO.md`
全 Phase と並行可能。**A1 が最優先**（実機検証が全 Phase でこれ待ちになっている）。
A5 の Web 側実装のみ WU1〜9 完了待ち（規約策定・C# 側は先行可）。

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

### 完了条件（証跡ベース）
- クリーンな worktree から clone/checkout → 起動で、手動介入なしに **CEF 描画 → host 接続 →
  Topic snapshot 受信 → Action 往復**まで成功した記録（録画 or ログ）を残す
- 上記をもって実機 web↔host 連携検証（これまで mock-host e2e 止まり）を解禁する

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
  （Web は Unity 入力系を経由しない）。**ブラウザ E2E だけでは二重配送・ネイティブフォーカス・IME を
  観測できない**ため、実機検証は (a) OS レベル入力での CEF+Unity 統合確認と、(b) Unity 側に
  「ワールド操作が発火しなかったこと」を記録する probe（ログ）を仕込んだ二面で行う

---

## A3: 本番静的配信 + アセット配信 + Windows（INFRA-9/8/5）

### 現状の問題
- フロント配信は「Unity が Node.js ごと Vite dev サーバを spawn」する開発形態のみ。
  出荷形態（ビルド済み静的アセット）が存在しない
- Vite dev サーバ停止時の検知・復旧なし
- `ViteProcess.cs:246` に Windows の pid 特定 TODO が残る（Windows/Linux 未対応）

### 作業
1. `pnpm build` の成果物を Kestrel の静的ファイル配信で返すモードを `KestrelServer.cs`/
   `WebUiEndpoints.cs` に追加（dev=Vite / prod=静的 の切替。ビルド成果物の同梱方法も決める）
   - **成果物整合性**: UI 成果物に manifest/version/hash を持たせホストと照合。SPA fallback・
     hashed asset・MIME を確認。**不一致・ロード失敗時は web モード成立前にゲートを uGUI へ戻す**
2. dev モードに Vite 死活検知（ヘルスチェック + 失敗時のトースト/ログ + 再起動）
3. **汎用アセット配信（INFRA-5）**: アイコン配信（`/api/icons/`）を汎用画像（スキット立ち絵等）へ
   拡張する配信規約を定める（実利用は C4。ここでは経路とキャッシュ方針まで）
4. 動的ポート・多重起動: ポート競合時の割当と、複数 worktree/多重起動時の分離を確認
   （既存計画 `docs/superpowers/plans/2026-07-16-webui-dynamic-ports.md` があれば整合させる）
5. Windows での Vite spawn / pid 管理 / CEF 動作を確認（A1 の配布方式と合わせて検証）

### 完了条件
- Vite を起動せずにビルド済み UI でゲームが動く（Windows 実機での確認を含む）
- 成果物不整合・ロード失敗で uGUI フォールバックが働く
- dev で Vite が死んでも無言で固まらない

---

## A4: 接続堅牢性 + Topic 横断規約（INFRA-13/7）

### 現状の問題
- WS 切断・CEF リロード・Kestrel クラッシュ後の復帰と状態復元が未設計（再接続オーバーレイはあるが
  復元保証がない）
- Topic の snapshot と event が競合すると、遅れて届いた snapshot が新しい event を上書きし得る。
  デバウンス・配信頻度・再接続時の再 snapshot 取得の規約が定義されていない

### 作業
1. **Topic 横断規約の策定・文書化**（`MIGRATION.md` §5 に反映済みの要件を実装に落とす）:
   状態 Topic は単調増加 revision を持つ / 購読確立と snapshot 取得を原子的に扱う /
   Web 側は古い revision を破棄する / 連続変動値は固定間隔サンプリング
2. 既存 Topic（inventory/blockInventory/research 等）を規約準拠へ改修（Web 側は WU 完了後）
3. 再接続時の全購読 Topic 再 snapshot と画面状態復元。CEF リロード・Kestrel 再起動からの復帰
4. 死活監視: WS ハートビート・CEF プロセス監視・復帰時のユーザー通知

### 完了条件
- fault-injection スモーク（WS 切断→再接続 / CEF リロード / フォーカス往復）で各画面の表示と
  操作が復元される
- 規約が文書化され、以降の新規 Topic のテンプレートになっている

---

## A5: i18n 基盤 + 要素 ID 規約の策定（INFRA-11/12 前倒し）

> 外部監査指摘により Phase D / C4 から前倒し。全画面実装後に後付けすると横断手戻りになるため、
> **C 系着手前に基盤と規約を敷き、各画面は実装時に準拠する**。

### i18n 基盤（INFRA-11 の基盤部分）
1. ローカライズ辞書の配信（HTTP or Topic）+ 言語切替イベント。uGUI 側の正:
   `Client.Localization/TextMeshProLocalize.cs` と同じ辞書ソースを使う
2. Web 側 `t(key)` フック + 言語切替時の再描画
3. **新規ハードコード禁止 lint**（ESLint ルール。WU4 の ESLint 導入と統合）
4. 既存10画面の文字列変換は Phase D（ここではやらない）

### 要素 ID 規約（INFRA-12）
1. `data-tutorial-anchor` 属性の命名規約を策定（`data-testid` とは**分離** — チュートリアル ID は
   ゲーム契約で長期安定、test ID はテスト都合で変更可能）
2. anchor registry の設計: Web 側で mount/unmount・可視性を追跡し、Unity へ
   `ready / not-found / hidden` を ack できる宣言的な仕組み（実装は C4。ここでは規約と型まで）
3. 以降の全画面 Phase（B/C）は実装時に anchor を付与する（`MIGRATION.md` §5 手順5）

### 完了条件
- i18n フックと lint が導入され、新規実装がハードコードできない状態
- anchor 規約が文書化され、C1 以降の実装がそれに従える

---

## 検証ゲート（共通）

`.cs` 変更は `uloop compile` / 起動系の変更は PlayMode スモーク
（`unity-playmode-recorded-playtest`）。A1・A2 完了時に実機 web↔host 連携の統合検証を 1 回実施し、
結果を `../TODO.md` に記録する。

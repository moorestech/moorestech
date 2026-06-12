# Web UI 段階移行 設計仕様

**作成日:** 2026-06-12
**目的:** ゲーム内UI（uGUI）をWeb UI（React + Tailwind + WS購読）へ移行する全体計画
**参照:**
- 全体計画: `docs/cef-webui-plan.md`
- 基盤検証手順: `docs/web-ui-verification.md`
- 基盤設計（削除済み・git参照）: `git show e21fb4fdc:docs/superpowers/specs/2026-04-22-web-ui-foundation-design.md`

---

## 1. 確定方針

| 項目 | 決定 |
|---|---|
| 切り替え方式 | **一括切り替え**。uGUIとWeb UIの共存期間は設けない |
| ブランチ運用 | `feature/web-ui` 長期ブランチで開発。masterを**週次目安で逆マージ**して鮮度維持。切り替え完了時にmasterへマージ |
| 対象範囲 | **ゲーム内UIのみ**（MainGameUI / UIState配下）。メインメニュー・Debug系UIは対象外 |
| 検証手段 | CEF統合は最終フェーズ。それまでブラウザ（`http://localhost:5173/`）で実プレイ検証 |
| TS型定義 | 手書きで開始し、2〜3UI移植後にC#→TS SourceGeneratorを導入して置換 |
| 双方向API | 既存WS接続上の `action` op + `requestId` 相関（後述） |
| レイヤリング | WebはC#クライアント側コントローラ（`LocalPlayerInventoryController` 等）を呼ぶだけ。サーバープロトコル（moorestech_server）には一切触れない |

### 移行対象UI一覧（UIStateEnumベース）

GameScreen（HUD）/ PlayerInventory / SubInventory（機械・チェスト等ブロックUI）/ PauseMenu / DeleteBar / Story（スキット）/ PlaceBlock / ChallengeList / ResearchTree / TrainHUDScreen。
Debug は対象外。

---

## 2. 双方向APIプロトコル

既存のtopic購読プロトコル（`subscribe` / `snapshot` / `event`）に以下を追加する。

```jsonc
// Web → Unity
{ "op": "action",
  "type": "inventory.move_item",        // ドット区切りの名前空間
  "requestId": "a1",                    // クライアント採番、応答相関用
  "payload": { "from": {"area":"main","slot":3},
               "to":   {"area":"hotbar","slot":0},
               "count": 5 } }

// Unity → Web（応答）
{ "op": "result", "requestId": "a1", "ok": true }
{ "op": "result", "requestId": "a1", "ok": false, "error": "invalid_slot" }

// 状態反映は従来どおりtopic eventで全購読者に配信
{ "op": "event", "topic": "local_player.inventory", "data": { ... } }
```

設計上の要点:

- **1本のWS接続に統一**することで、action発行とevent受信の順序が保証される（HTTP併用案は順序保証がなく不採用。fire-and-forget案はエラーが闇に消えるため不採用）
- **メインスレッドdispatch必須**: WS受信はスレッドプール上で行われるため、actionハンドラの実行前に必ず `UniTask.SwitchToMainThread()` で切り替える。応答送信はバックグラウンドに戻してよい
- actionの成否は `result` で返すが、UI状態の更新は `result` ではなく後続のtopic eventで反映する（resultにデータを載せない）

### C#側の構成

```
Client.WebUiHost/
├── Boot/
│   └── WebSocketHub.cs      action/result opのルーティングを追加
└── Game/
    ├── WebUiGameBinder.cs   topicと同様にactionハンドラも登録
    └── Actions/
        ├── IActionHandler.cs       // type名 + UniTask<ActionResult> Execute(payload)
        └── InventoryActions.cs     // 既存コントローラを呼ぶ薄いハンドラ群
```

### JSONシリアライザの置換

現行 `WebSocketHub` の自作軽量JSONパース（既知キーのstring抽出のみ、エスケープ非対応）はactionのpayloadを扱えないため、**Phase 0でNewtonsoft.Jsonに全面置換**する。topicのsnapshot/event生成側も文字列手組みをやめ、DTOクラス＋シリアライズに統一する。

---

## 3. フェーズ構成

### Phase 0: 通信基盤の双方向化

インベントリ移植の前提となる基盤。完了条件は「ダミーactionをブラウザから発行し、resultとevent反映が返ること」。

- WebSocketHubのJSON処理をNewtonsoft.Jsonへ置換
- `action` / `result` opとIActionHandlerレジストリの実装（メインスレッドdispatch込み）
- アイコン配信: Kestrelに `GET /api/icons/{itemId}.png` を追加。ItemImageContainerのSprite→PNGエンコード＋メモリキャッシュ。`Cache-Control` 付与
- React側: `sendAction(type, payload): Promise<Result>`（requestId採番・タイムアウト・WS切断時reject）とエラートースト表示

### Phase 1: プレイヤーインベントリ移植（パイロット）

双方向API・アイコン配信を初めて実戦で通し、**以降のUI移植テンプレートを確立する**フェーズ。

- topic再設計: `local_player.inventory` をmain / hotbar / grab（掴み中アイテム）を含む形に拡張
- action実装: `inventory.move_item` / `inventory.split` / `inventory.collect`（同種一括回収）/ `craft.execute`
- Web側: インベントリ＋クラフト画面をフル機能実装（ドラッグ&ドロップ、ツールチップ、アイコン表示）
- 完了時に一度だけCefUnityをオンにして「ゲーム内テクスチャに表示されるか」のスモーク確認を行う（統合の未知をPhase 3に溜めない）

確立するテンプレート: **topic追加 → action追加 → 手書きTS型 → React実装 → uloop自動E2E**。

### Phase 2: 残りゲーム内UIの順次移植

推奨順序（依存と難度ベース）:

1. **GameScreen HUD**（ホットバー表示・プログレスバー等。表示主体で軽く、テンプレートの反復確認に最適）
2. **SubInventory**（機械・チェスト等のブロックUI。**ブロック種別→Reactコンポーネントの対応表設計が本フェーズの本丸**。汎用スロットグリッド＋ブロック種別ごとの差分という構成を想定）
3. **PlaceBlock / DeleteBar**（HUD系の操作UI）
4. **ResearchTree / ChallengeList**（大型ツリーUI。topicが大きくなるならここで差分配信を検討）
5. **TrainHUD / Story / PauseMenu**

並行作業:

- **3UI目あたりでC#→TS SourceGenerator導入**。DTOクラス（topic payload / action payload / result）からTS型を自動生成し、手書き型を置換。Mooresmaster SourceGeneratorと同系統の方式
- 各UIの完了定義: 「ブラウザ上でその機能をuGUIなしで完結操作できる」＋「uloop自動E2Eが通る」

### Phase 3: 統合・一括切り替え

- **UIステート管理の移管**: キー入力（E / Esc等）はUnityが拾い続け、`ui.state` topicでWebへ通知。Web側は表示切替のみ行う。Web内ボタンからの画面遷移は `ui.set_state` actionで逆方向に流す。UIStateControlの遷移ロジックはUnity側に残す
- **CEF統合**: MainGameUI内のCefUnityオブジェクトを有効化し、URLをlocalhostへ。CEF上にUIがある間はゲーム入力（カメラ・ホットキー）を停止するフォーカス制御を実装
- **本番化**: `vite build` 成果物のKestrel静的配信（Node/Vite無しで起動できる経路）、ビルド同梱、Windows動作確認（`ViteProcess.KillAnyLingering` のWindows未実装解消を含む）
- **一括切り替え**: uGUIゲーム内UIを無効化（コードは残す）→ 全UI通し検証 → masterへマージ

---

## 4. リスクと手当て

| リスク | 手当て |
|---|---|
| 長期ブランチの腐敗 | master逆マージを週次目安で実施。uGUI側ファイルは原則「追加のみ」でコンフリクト面を最小化 |
| topic全量再送の肥大化 | インベントリ規模は問題なし。ResearchTree等で重くなった時点で該当topicにのみ差分opを追加（最初から作り込まない） |
| CEF統合の未知（フォーカス・性能） | Phase 1完了時のスモーク確認で早期に顕在化させる。Mac側に既知の7ms同期待ち問題あり（`docs/cef-webui-plan.md` §5） |
| 自作JSONパースの脆さ | Phase 0で全面置換し、以降は触らない |
| アイコンのPNGエンコード負荷 | 初回エンコード後メモリキャッシュ。起動時一括ではなく遅延生成 |

---

## 5. テスト戦略

- **C#ユニットテスト**: 各actionハンドラ・topic snapshotロジックをNUnitで（既存のサーバーテスト実行経路で回す）
- **uloop自動E2E**: UI移植ごとに `web-ui-verification.md` 方式（Play mode起動 → WS購読 → action発行 → result/event検証）のレシピを追加
- **Playwright**: Phase 2中盤から導入検討。ブラウザDOMまで含めた検証（クリック→action→event→DOM更新）

---

## 6. スコープ外

- メインメニュー・Debug系UIのWeb化
- モバイル / コンソール対応
- CEF以外のレンダラー差し替え抽象化（計画docのタスクボードA「CEFリファクタ」は本計画と独立）
- セキュリティハードニング（CSRFトークン等。localhostバインド＋Origin検証の現状維持）
- ポート衝突時のフォールバック（5050 / 5173固定の現状維持）

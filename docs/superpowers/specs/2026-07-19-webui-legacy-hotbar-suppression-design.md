# WebUIモード中の旧uGUIホットバー抑止設計

## 背景

WebUI版ホットバーは `moorestech_web/webui/src/features/inventory/HotbarPanel` に実装済みだが、PlayModeでは旧uGUI版 `HotBarView` も同時に表示される。

既存のゲート監査は常駐ホットバーを `PlayerInventoryViewController` の配下として扱っている。しかし実際の `HotBarView` は `GameStateController` が独立して `SetActive` を呼ぶHUDであり、`PlayerInventoryViewController` の表示ゲートでは抑止されない。これが二重表示の原因である。

## 要件

- WebUIの実効モード中は旧uGUIホットバーを表示しない。
- WebUI版ホットバー、ホットバー選択状態、手持ちアイテム処理は維持する。
- WebUIホストが起動できない場合は、既存仕様どおり旧uGUIへフォールバックする。
- 起動後にCEFトグルまたはWebUIホスト稼働状態が変わった場合も、旧ホットバー表示を即時更新する。
- SkitやCutSceneによる既存のホットバー表示制御を維持する。

## 採用設計

`WebUiScreenGate` をWebUI実効モードの単一情報源として維持し、実効値の変化をUniRxで購読可能にする。

`HotBarView` は次の2状態を合成して最終表示を決める。

- `GameStateController` から渡されるゲーム状態上の表示要求
- `WebUiScreenGate.IsWebUiMode`

最終表示条件は「ゲーム状態上で表示対象、かつWebUI実効モードではない」とする。`HotBarView.SetActive` はゲーム状態上の要求を保存して再評価し、WebUI実効モードの購読でも同じ再評価処理を呼ぶ。

これにより、WebUI中にuGUIの描画だけを止めつつ、WebUIホストから参照される `HotBarView` の選択状態と外部選択APIは残す。

## 配置と前例

| 項目 | 配置先 | 役割・前例 |
|---|---|---|
| WebUI実効モード変化通知 | `Client.Game/InGame/UI/UIState/WebUiScreenGate.cs` | 既存ゲート値の通知面。UniRxのprivate `Subject<T>` + public `IObservable<T>`は `GameStateController.OnStateChanged` と同形 |
| 旧ホットバー表示再評価 | `Client.Game/InGame/UI/Inventory/HotBarView.cs` | 表示専用の読み手。`CurrentChallengeHudView` 等と同様にWebUIモード中だけ旧ビューを抑止 |
| ゲート漏れ監査 | `Client.Tests/WebUi/Gate` | 既存の `WebUiGateAuditTest` と分類台帳を拡張 |

データフローは `WebUiCefToggle/WebUiHost → WebUiScreenGate → HotBarView（読み手）→ uGUI表示` の一方向とする。ホットバー側からゲートやホスト状態を書き戻す経路は追加しない。

既存機構を無傷で残して表示子だけを差し替える案も比較した。しかし `HotBarView` は描画と旧入力処理を同じGameObjectで所有しており、描画子だけを止めるにはPrefab変更または旧入力との二重駆動が必要になる。採用案は既存のWebUIゲート済みビューと同様にGameObjectを抑止する一方、WebUIが利用する `SetSelectIndex` と選択状態はコンポーネント参照経由で引き続き利用する。

## 操作の死活表

| 操作・状態 | 計画後 | 根拠 |
|---|---|---|
| WebUIホットバー表示 | 維持 | React側 `HotbarPanel` は変更しない |
| WebUIの1〜9キー選択 | 維持 | Web actionから非アクティブな `HotBarView.SetSelectIndex` を引き続き呼べる |
| WebUIのホイール選択 | 維持 | 同じ `inventory.select_hotbar` action経路を維持 |
| WebUIのスロットクリック | 維持 | Reactのslot action経路は変更しない |
| 手持ち3Dモデル更新 | 維持 | Web actionの `SetSelectIndex` が既存 `ApplySelection` を通る |
| WebUIホスト失敗時のuGUI | 維持 | 実効モードfalse通知で保存済みゲーム状態要求を再適用 |
| Skit/CutScene中のホットバー退避 | 維持 | `GameStateController.SetActive(false)` の要求を保存し、WebUI状態より優先して非表示 |
| `InventoryTopic` の状態配信 | 維持 | `HotBarView` のインスタンスと公開状態は破棄しない |

## 代替案と不採用理由

### `HotBarView.SetActive` に条件を追加するだけ

変更量は最小だが、InGame中にCEFトグルやホスト状態が変わっても `GameStateController` が再度 `SetActive` を呼ぶ保証がない。起動後切替で旧ホットバーが残るため不採用とする。

### `WebUiCefToggle` から `HotBarView` を直接操作する

表示結果は得られるが、CEFトグルとホットバーという具体ビューを直接結合する。また、ホスト稼働状態の変化を別経路で扱う必要があり、実効モードの単一情報源が崩れるため不採用とする。

## 監査とテスト

`WebUiGateClassification` で `HotBarView.cs` を独立した `GatedRoot` に分類し直す。これにより、将来ゲート参照が消えた場合は `WebUiGateAuditTest.GatedRootsContainGateToken` が失敗する。

自動検証は以下を行う。

1. ゲート監査テストが `HotBarView` の直接ゲートを要求すること。
2. CEFトグルとホスト状態のANDで実効モードが変化し、購読へ通知されること。
3. Unityコンパイルが成功すること。
4. 関連する限定EditModeテストが成功すること。
5. PlayMode録画でWebUI版ホットバーが表示され、旧uGUIホットバーが表示されないこと。
6. PlayMode中のスクリーンショットとUnity Errorログを確認すること。

## 反例

起動時はWebUIホストが未準備で旧uGUIが表示され、その後ホスト起動成功によりWebUI実効モードへ遷移する場合を考える。`SetActive` 呼び出し時だけ判定する実装では旧uGUIが残る。本設計は実効モード変化を購読して同じ表示再評価を行うため、この遷移でも旧ホットバーを停止できる。

逆に、WebUIホストが停止して実効モードがfalseへ戻った場合は、保存していたゲーム状態上の表示要求を使って旧uGUIを再表示し、操作不能な状態を避ける。

## 変更範囲

- `WebUiScreenGate`: 実効モード変化通知
- `HotBarView`: ゲーム状態要求とWebUI実効モードを合成した表示制御
- `WebUiGateClassification`: 独立ゲートルートへの分類訂正
- 関連テスト

Prefab、Scene、ScriptableObjectは変更しない。

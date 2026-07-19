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

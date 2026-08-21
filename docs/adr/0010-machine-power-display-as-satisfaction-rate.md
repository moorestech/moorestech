# 0010: 機械の電力表示は実効要求電力に対する充足率と稼働状態で表す

日付: 2026-08-17
状態: 採択

## 背景

機械UI（Web UI `moorestech_web/webui`）の電力表示は `電力 {rate}% ({current}/{required})` で、rate はクライアントが `CurrentPower / RequestPower` から再計算している（`detailLogic.ts`。サーバー側 `CommonMachineBlockStateDetail.PowerRate` は `[IgnoreMember]` でワイヤに乗らない）。

サーバーには待機電力の概念が既にある。`VanillaMachineProcessorComponent.EffectiveRequestPower` は待機中に `RequestPower × idlePowerRate`（マスタ既定0.2）、稼働中に `RequestPower × モジュール倍率` を返し、電力ネットワークにはこの実効値で要求している。しかし state に詰める `RequestPower` は基礎要求値のままなので、待機中で電力が完全に足りていても表示は「20%」になり、電力不足の20%と見分けが付かない。同型の idlePowerRate 持ち消費者は電気ポンプ・歯車機械・歯車ベルトコンベア等にも存在する。

## 決定

1. **電力パーセンテージは「実効要求電力に対する充足率」のみを意味する。** 待機中でも足りていれば100%、100%未満は常に電力不足。あわせて稼働状態ラベル（待機中/稼働中/停止中、`CurrentStateType` 由来）を併記する。
   出所: ユーザー裁定 2026-08-17（[[2026-08-17-電力表示は充足率と稼働状態ラベルに分離する]]）
2. **`CommonMachineBlockStateDetail.RequestPower` の意味を実効要求電力に変更する（案A）。** サーバーが state 生成時に `EffectiveRequestPower` を詰める。充足率のクライアント再計算・ワイヤ2値（実消費/実効要求）の構造は変えない。同じ意味論を idlePowerRate を持つ全消費者に適用する。
   出所: ユーザー裁定 2026-08-17（[[2026-08-17-stateのRequestPowerは実効要求電力を送る]]。「power rateはクライアント側で計算し、要求と実消費の2値をいれる」確認への ok）
3. 停止中（Halted、クリーンルーム条件未達）は実効要求0のため充足率は需要なし扱い（100%）となるが、稼働状態ラベルが文脈を与えるため専用表示は設けない。
   出所: agent前提（`computePowerRate` の request==0 → 1 既存挙動と `CleanRoomMachineProcessorComponent` の Halted=0f に基づく）

## 検討した代替案

- **案B: `EffectiveRequestPower` を state の別フィールドとして追加**。既存フィールドの意味を保てるが、意味の違う2つの要求値がワイヤに並び、表示側が分母を選ぶ判断を持ち続ける。却下（出所: ユーザー裁定 2026-08-17、案A採択の裏）。
- **待機中は専用文言のみでパーセンテージ非表示**。状態と充足率が直交する情報である利点（待機中でも電力不足は起こり得る）を失う。却下（出所: agent前提）。

## 影響

- サーバー: state 生成箇所で基礎要求値→実効要求値への差し替え。派生 `PowerRate`（アニメーション速度用）は自然に充足率へ追従する。
- Web UI: 充足率計算は無変更。稼働状態ラベルの追加とローカライズ文言の追加のみ。
- 既存テスト: state の RequestPower を基礎値で検証しているテストは実効値前提へ更新する。

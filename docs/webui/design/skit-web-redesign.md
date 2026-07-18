# スキット Web 向け再設計

策定日: 2026-07-18  
対象: Phase C4 / FEAT-SKIT-1, FEAT-SKIT-2, INFRA-10  
親文書: `docs/webui/MIGRATION.md`, `docs/webui/plans/phase-c4-skit-tutorial.md`

## 1. 目的と境界

スキットの実行主権を Unity に保ったまま、スクリーンスペースの会話 UI だけを Web に置き換える。WebSocket 切断、CEF リロード、入力の重複があっても、Unity のストーリー進行を壊さず最新表示へ復元できることを最優先とする。

対象は本文、話者名、選択肢、スキップ、オート、UI 非表示である。コマンド解釈、分岐、待機、カメラ、3D キャラクター、モーション、表情、背景、ゲーム内オブジェクト、ボイス再生は Unity に残す。カットシーンは同じ Phase C4 だが別 Topic とし、本書では扱わない。

## 2. 現状分析

### 2.1 実体は UI ではなく Unity 上のコマンドインタプリタ

`SkitManager` は JSON をコマンド列へ変換し、同じ `StoryContext` に対して順番に実行する。

```csharp
// moorestech_client/Assets/Scripts/Client.Game/Skit/SkitManager.cs:63
var commandsToken = (JToken)JsonConvert.DeserializeObject(skitJson.text);
var commands = CommandForgeLoader.LoadCommands(commandsToken);
using var storyContext = await PreProcess();
CameraManager.RegisterCamera(skitCamera);

foreach (var command in commands)
{
    await command.ExecuteAsync(storyContext);
}
```

同ファイルの前処理（98-139行）は全キャラクターの Addressable prefab を生成し、`SkitUI`、`ISkitCamera`、`VoiceDefine`、キャラクターコンテナ、環境・ブロック制御を DI する。したがってコマンド列を Web に移すと、描画だけでなく Unity オブジェクトの生存期間とゲーム状態まで二重管理になる。

コマンドの責務も Unity 固有である。

- `CameraworkCommand.cs:13-25`: skip 状態を見てカメラを tween または終点へ移動
- `CharacterTransformCommand.cs:11-12`: 3D キャラクターの transform を変更
- `MotionCommand.cs:10-11`: Addressable の AnimationClip を再生
- `EmoteCommand.cs:12-13`: SkinnedMeshRenderer の blend shape を tween
- `ControlSkitBackgroundCommand.cs:17-28`: Unity 環境 prefab を追加・撤去
- `InGameObjectControlCommand.cs:18-22`: ゲーム背景とブロック群を有効・無効化
- `WaitCommand.cs:10-16`, `TransitionCommand.cs:11-22`: skip を含む時間進行

`SkitState` はスキット中の入力・HUD・ゲーム状態も所有する。

```csharp
// moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/SkitState.cs:20
InputManager.MouseCursorVisible(true);
GameStateController.ChangeState(GameStateType.Skit);
KeyControlDescription.Instance.SetText("");
```

### 2.2 `SkitUI` は薄いが、入力待ちがコマンド内に埋まっている

`SkitUI.cs:23-27` は話者名と本文を Label に設定し、`84-137` は選択ボタンを構築してクリックを待つ。`SkitUITools.cs:17-27` は非表示、skip、auto を `ISkitActionController` へ渡す。描画ガワは交換可能だが、現行 `TextCommand` は文字送りと入力待ちを直接扱う。

```csharp
// moorestech_client/Assets/Scripts/Client.Skit/Commands/TextCommand.cs:35
for (var i = 0; i < Body.Length; i++)
{
    skitUi.SetText(characterName, Body.Substring(0, i + 1));
    await UniTask.Delay(TimeSpan.FromSeconds(TextDuration), cancellationToken: token);
}
// クリック、auto、skip のいずれかまで次コマンドへ進まない
```

選択肢は `SelectionCommand.cs:14-36` で最大3件を組み立てるが、現状は選択結果を jump target に反映せず `return null; // TODO` で終わる。Web 化でこの既存欠陥を隠さず、選択分岐の修復を通常スキット移行の前提テストに含める。

### 2.3 ボイスは既に Unity の `AudioSource` から再生される

`VoiceDefine.cs:12-18` がキャラクターIDと本文から `AudioClip` を引き、通常スキットは `SkitCharacter.cs:34-42` のキャラクター別 `AudioSource` で再生・停止する。背景スキットも `BackgroundSkitUI.cs:18-31` の `AudioSource` で再生し、clip 長だけ待つ。Web/CEF に音声データを渡す経路は現状存在しない。

### 2.4 バックグラウンドスキットは小さな実証対象になる

`BackgroundSkitManager.cs:24-45` は `GameScreen` まで待ち、Text コマンドだけを逐次実行する。`BackgroundSkitTextCommand.cs:9-20` の表示状態は話者名、本文、ボイスだけで、カメラ・選択肢・専用 `SkitState` がない。完全 snapshot と Unity 音声の検証を通常スキットより低リスクで行える。

## 3. 設計決定

### 3.1 責務分割

| 選択肢 | 長所 | 短所 | 判定 |
|---|---|---|---|
| インタプリタも Web へ移す | DOM 側だけで演出を完結できる | Unity コマンド、3D オブジェクト、Addressable、GameState を再実装し二重主権になる | 不採用 |
| コマンドイベントを逐次 Web へ流す | 差分が小さい | 切断中イベントを失うと復元不能。順序・再送処理が必要 | 不採用 |
| Unity 主権＋Web薄表示層 | 現行の実行モデルを保ち、表示だけ交換できる | Unity 側に表示状態ストアを抽出する必要がある | **採用** |

採用案では、Unity に `SkitPresentationStateStore` を置き、コマンドは UI Toolkit を直接操作せずこのポートへ状態を push する。store は private `Subject<SkitPresentationState>` と read-only `IObservable<SkitPresentationState>` で変化を公開する。WebUiHost の Topic はストアを読み、Action は `ISkitActionController` と現在の待機点へ intent を渡す。汎用 WebSocket 基盤にスキット語彙を持ち込まず、スキット固有型は C4 の Game/Topics・Game/Actions 配下に置く。

### 3.2 同期は完全 snapshot、タイプライターは Web

`skit.presentation` はイベント差分ではなく、常にその時点の完全な表示状態を配信する。再購読時の `snapshot` と変化時の `event` は同じ payload 型を使う。`sceneRevision` は session 内で単調増加し、本文、選択肢、操作許可、auto/skip/hidden のいずれかが変わるたびに増やす。

タイプライターは Web の表示演出とする。Unity は全文を一度に snapshot へ載せ、Web は新しい `event` で `textReveal.mode === "typewriter"` のときだけ表示文字数を進める。再接続の `snapshot` は全文を即時表示する。これにより、接続断中も Unity の logical wait point は変わらず、復元のために文字単位イベントや時計同期を追加しない。

本文表示中の最初のクリックは Web 内で reveal を完了し、Unity Action を送らない。全文表示後のクリックだけ `skit.advance` を送る。キーボードによる advance も同じ規則に集約する。auto の待機時間と skip によるコマンド進行は Unity が所有し、Web のアニメーション完了を進行条件にはしない。

### 3.3 Action は session・revision・選択IDで冪等化

Unity は Action を実行する直前に以下を全件照合する。

1. `sessionId` が現在のセッションと一致する。
2. `sceneRevision` が現在の表示 revision と一致する。
3. intent が現在の `allowedIntents` に含まれる。
4. 選択時は `choiceId` が現在 snapshot の選択肢に存在する。

不一致は副作用なしで `stale_session`、`stale_revision`、`intent_not_allowed`、`unknown_choice` を返す。成功時は待機点を一度だけ解放し、直ちに revision を進めるため、同じ Action の再送は stale になる。`requestId` は通信応答の相関だけに使い、ゲーム冪等性キーには使わない。

### 3.4 ボイスは Unity 再生に統一する

| 選択肢 | 長所 | 短所 | 判定 |
|---|---|---|---|
| CEF/Web Audio で再生 | DOM 演出と同期しやすい | CEF の音声デバイス専有問題を直撃し、AudioClip 配信も新設が必要 | 不採用 |
| 通常は Unity、背景だけ Web | 個別実装は可能 | 音量設定、停止、デバイス、障害モードが二系統になる | 不採用 |
| 全ボイスを Unity AudioSource で再生 | 現行 asset・mixer・停止処理を維持し、CEF は無音にできる | Web 表示と音声開始のフレーム差を測る必要がある | **採用** |

INFRA-10 の解決方針は「CEF から音を出さない。ゲーム音とボイスを Unity の音声経路へ統一する」とする。ただし CEF パッケージ自体が無音ページでもデバイスを専有する可能性は実機試験で否定する必要がある。

検証計画:

1. Web UI 未起動、CEF 起動＋無音ページ、CEF 起動＋スキット表示の3条件を用意する。
2. Windows の既定出力デバイスと Bluetooth/USB 出力で、BGM・SE・通常ボイス・背景ボイスを同時再生する。
3. 再生中に CEF リロード、WS 再接続、アプリのフォーカス往復、出力デバイス切替を行う。
4. Unity AudioMixer の master/voice 音量と mute がボイスへ反映され、Web 側には audio/video 要素や Web Audio context が生成されないことを確認する。
5. 音声開始と新 revision の会話表示を録画し、目視で不自然な先行・遅延がないことを受け入れ確認する。

INFRA-10 の DoD は、対象 Windows 実機で CEF 表示中も BGM/SE と Unity ボイスが同時に聞こえ、上記障害操作後も復帰し、CEF が音声を生成しないこと。無音 CEF だけで専有が再現した場合は本案を完了扱いせず、CEF の audio sandbox/device 設定を A2/A3 と共同調査する。

### 3.5 立ち絵・キャラクター描画は Unity に残す

現行は独立した2D立ち絵ではなく、`SkitManager.cs:100-121` でロードする3Dキャラクターモデルと `SkitCharacterAnimator`、表情、カメラの組である。A3 の汎用アセット配信で Web へ画像を渡す案は、新しい2D素材、キャラクター状態同期、重なり順の定義を必要とし、現行内容の移行にならない。

したがってキャラクター、表情、モーション、背景、トランジション映像は Unity 描画に残し、Web は透明オーバーレイの会話 UI のみを描く。A3 の汎用画像配信は本機能では使用しない。将来2D立ち絵という新しい演出要件が決まった場合だけ別設計にする。

## 4. Topic・Action 契約案

TypeScript の型レベル案を正とし、WU5 後の zod schema と C# DTO/WireFixture を同形にする。

```ts
type SkitMode = "none" | "background" | "blocking";
type SkitIntent = "advance" | "select" | "set-auto" | "skip" | "set-ui-hidden";

type SkitChoice = {
  choiceId: string;       // command内で安定したID。表示indexをIDにしない
  labelKey?: string;
  label: string;          // i18n移行中も完全snapshot単体で表示可能
};

type SkitPresentationState = {
  mode: SkitMode;
  speakerName: string;
  body: string;
  choices: SkitChoice[];
  textAreaVisible: boolean;
  transitionVisible: boolean;
  autoEnabled: boolean;
  skipActive: boolean;
  uiHidden: boolean;
  textReveal: { mode: "instant" | "typewriter"; intervalMs: number };
};

type SkitPresentationData = {
  sessionId: string;
  sceneRevision: number;
  presentationState: SkitPresentationState;
  allowedIntents: SkitIntent[];
};

type SkitActionBase = { sessionId: string; sceneRevision: number };
type SkitActionPayloads = {
  "skit.advance": SkitActionBase;
  "skit.select": SkitActionBase & { choiceId: string };
  "skit.set_auto": SkitActionBase & { enabled: boolean };
  "skit.skip": SkitActionBase;
  "skit.set_ui_hidden": SkitActionBase & { hidden: boolean };
};
```

Topic 名は `skit.presentation` の1本とし、`mode` で background/blocking/none を表す。通常と背景を別 Topic にすると排他制御と復元順序が二重になるため分けない。セッション開始時に新しい opaque UUID を発行し、終了 snapshot は同じ session の `mode: "none"` を1回 publish する。次回開始では sessionId を再利用しない。

背景スキットは `allowedIntents: []` から始め、Unity が voice clip 長または現行3秒待機で次へ進める。これが完全 snapshot、再接続、Unity音声経路の最初の実証になる。

## 5. 配置と既存前例

| 項目 | 所有層 | 使用機構・前例 |
|---|---|---|
| スキット現在表示状態 | `Client.Skit` / `Client.Game.Skit` | `SkitPresentationStateStore`。コマンドが pushし、UniRxで通知する。`SkitActionContext` と同じスキット所有 |
| Topic DTO/handler | `Client.WebUiHost/Game/Topics/Skit` | `UiStateTopic.cs` の snapshot + publish 方式 |
| Action handler | `Client.WebUiHost/Game/Actions/Skit` | `IActionHandler` と `ActionResult`。主権はスキット側へ委譲 |
| Web store/view | `webui/src/features/skit` | `protocol.ts` の Topic/Action registry と feature 単位配置 |
| ボイス | Unity `AudioSource` | `SkitCharacter.PlayVoice`, `BackgroundSkitUI.SetText` |

既存 `WebSocketHub` は snapshot と event を配信できるため新しい通信経路は作らない。新設が必要なのは、現在 `SkitUI` へ直接書かれている表示状態を保持するスキットドメイン内の store だけである。既存 `SkitActionContext` は auto/skip しか保持せず本文・選択肢・待機 revision を復元できないため、そのままの拡張だけでは完全 snapshot を構成できない。

## 6. 実装フェーズ

### Phase S0: 契約と実行境界の固定

- 選択肢分岐の現行 TODO を再現するテストを追加し、期待する jump semantics を固定する。
- presentation store、session/revision、allowed intent の状態遷移テストを書く。
- zod/C# DTO/WireFixture へ上記契約を追加する。

### Phase S1: バックグラウンドスキット実証

- `BackgroundSkitTextCommand` の表示先を presentation store に切り替える。
- `skit.presentation` Topic と Web の背景会話ボックスを実装する。
- Unity `AudioSource` の再生を維持し、切断・再接続・CEF リロードで最新全文が復元されることを試験する。
- 実証で session/revision または音声方式に欠陥が出た場合、通常スキットへ進まず契約を修正する。

### Phase S2: 通常本文と操作

- `TextCommand` の文字列 push と入力ポーリングを presentation port / intent wait へ置換する。
- Web タイプライター、advance、auto、skip、hidden と冪等 Action を実装する。
- UI Toolkit 表示を Web モード時だけ抑止する。

### Phase S3: 選択肢とトランジション表示

- choiceId と jump target の対応を Unity に閉じ、Web は choiceId だけ返す。
- `ShowTextCommand`、`TransitionCommand` のスクリーン表示状態を snapshot 化する。
- カメラ、3Dキャラ、背景、モーションが従来どおり Unity で進む統合試験を行う。

### Phase S4: INFRA-10 実機ゲートと全体スモーク

- ボイス検証計画を Windows 実機で実施する。
- 通常/背景、auto/skip/選択、切断復帰、連打、キーリピート、セッション境界を録画付き PlayMode で確認する。

## 7. Definition of Done

- 通常/背景スキットの話者名と本文、通常スキットの選択肢・auto・skip・非表示が Web に表示される。
- Unity がコマンド進行、分岐、カメラ、3Dキャラ、背景、モーション、表情の唯一の主権を持つ。
- 再接続・CEF リロード後、最新完全 snapshot だけで表示が復元し、過去イベントの replay を必要としない。
- stale session/revision、未知 choice、許可されない intent は副作用なしで拒否される。連打・再送で二重進行しない。
- 新規 event ではタイプライター、再購読 snapshot では全文即時表示となり、どちらも Unity の進行をブロックしない。
- 選択結果が正しい jump target に反映され、現行 `SelectionCommand` の TODO が解消されている。
- CEF は音声を生成せず、Windows 実機で BGM/SE と Unity ボイスが同時再生され、障害操作後も復帰する。
- 3Dキャラクターは Unity 描画のままで、A3 の画像配信に依存しない。
- TypeScript/zod、C# DTO、WireFixture の契約テスト、Unity 状態遷移テスト、Web component test、Playwright、録画付き PlayMode が通る。

## 8. 未決事項（ユーザー判断が必要）

1. **タイプライター速度のプロダクト値**: 現行の固定50ms/文字を維持するか、アクセシビリティ設定として可変にするか。同期方式には影響せず、初回実装は50msを既定値にできる。
2. **背景スキットの手動送り**: 現行どおりボイス長/3秒で自動進行のみとするか、クリック送りを新機能として追加するか。後者は `advance` intent とUI affordanceを背景 modeにも許可する必要がある。
3. **既存選択肢の期待仕様**: 現行コードは選択結果を捨てているため、既存ストーリーデータが本来どの分岐を期待しているかをコンテンツ責任者が承認する必要がある。

立ち絵方式、音声方式、同期方式、責務分割は技術的に決定済みであり未決事項にはしない。

# チュートリアル Web 向け再設計

策定日: 2026-07-18  
対象: Phase C4 / FEAT-TUT-1  
親文書: `docs/webui/MIGRATION.md`, `docs/webui/plans/phase-c4-skit-tutorial.md`

## 1. 目的と境界

チャレンジ進行とチュートリアル開始・終了の主権を Unity に保ち、DOM 化された平面 UI のハイライトとキーガイドだけを Web 表示へ移す。SPA の mount/unmount、Portal、仮想化、スクロール、リサイズに追従し、Unity が「指示を出した」ことと Web が「実際に対象を表示できた」ことを ack で区別できる設計にする。

MapObjectPin、HudArrow、BlockPlacePreview はワールド座標・Camera・GameObject に依存するため Unity に残す。本書はそれらを DOM に投影せず、同じチュートリアル lifecycle で発火・終了する整合だけを扱う。（§9で改訂: MapObjectPin / HudArrow の表示は Web へ移行済み。BlockPlacePreview のみ残置）

## 2. 現状分析

### 2.1 `TutorialManager` がチャレンジ単位の主権を持つ

`TutorialManager` は tutorial type から各 manager を引き、チャレンジ開始時に view を生成し、完了時に同じ view を終了する。

```csharp
// moorestech_client/Assets/Scripts/Client.Game/InGame/Tutorial/TutorialManager.cs:30
foreach (var tutorial in challenge.Tutorials)
{
    var tutorialView = _tutorialViewManagers[tutorial.TutorialType]
        .ApplyTutorial(tutorial.TutorialParam);
    if (tutorialView != null) tutorialViews.Add(tutorialView);
}
```

```csharp
// 同:47
if (!_tutorialViews.TryGetValue(challengeId, out var tutorialViews)) return;
foreach (var tutorialView in tutorialViews)
{
    tutorialView.CompleteTutorial();
}
```

この lifecycle はチャレンジマスタとゲーム進行に結び付いており、Web が再構築してはならない。Web は Unity が宣言した現在状態だけを表示する。

### 2.2 現行 UI ハイライトは Unity 階層探索と毎フレーム追従

`UIHighlightTutorialManager.cs:20-40` は `FindObjectsOfType<UIHighlightTutorialTargetObject>(true)` で ID 一致する対象を探す。Item 用 manager は対象がまだ存在しない前提で `forceCreate = true` を渡す（`ItemViewHighLightTutorialManager.cs:16-22`）。

`UIHighlightTutorialView` は対象がない間、毎フレーム再探索し、見つかれば RectTransform の全プロパティを複写する。

```csharp
// moorestech_client/Assets/Scripts/Client.Game/InGame/Tutorial/UIHighlight/UIHighlightTutorialView.cs:22
private void Update()
{
    SyncRectTransform();
}

// 同:37
var highlightTargetObjects = FindObjectsOfType<UIHighlightTutorialTargetObject>(true);
```

```csharp
// 同:48-70
highlightObject.SetActive(_highlightTutorialTargetObject.ActiveSelf);
highlightImage.position = targetRect.position;
highlightImage.rotation = targetRect.rotation;
highlightImage.sizeDelta = targetRect.sizeDelta;
```

DOM 化後は Unity の階層探索では対象を発見できない。単に ID を query するだけでも、SPA 再描画、Portal、仮想化されたリスト、hidden、スクロール後の位置変化を扱えない。

### 2.3 キーガイドは UIState を毎フレーム比較する独立表示

`KeyControlTutorialManager.cs:17-27` は `Update()` で `UIStateControl.CurrentState.ToString()` と tutorial param の `UiState` を比較し、TMP オブジェクトを出し入れする。本文は `ControlText` を直接設定する（30-40行）。Phase C2 は既に `KeyControlDescription` を現在 state のキーヒント Topic として Web 化する計画であり、チュートリアル専用の第2描画基盤を作る理由はない。

### 2.4 ワールド空間系は Unity 固有データを直接使う

- `MapObjectPin.cs:38-62`: プレイヤー位置から最寄り MapObject を探し、毎フレーム world transform を更新
- `HudArrowManager.cs:55-141`: `Camera.WorldToViewportPoint` から画面内外と矢印位置・回転を計算
- `BlockPlacePreviewTutorialManager.cs:68-79`: block prefab と material をロードし、world position/rotation を設定
- 同 `88-94`: `OnBlockPlaced` を購読し、対象ブロック設置で tutorial を完了

これらは表示だけでなく Unity world の探索・判定・3D preview を含む。Web へ移すと座標ストリームとGameObject生存監視が増えるため、マスタープランのワールド空間 UI 除外方針どおり残置する。

## 3. 設計決定

### 3.1 DOM ハイライトは宣言的 Topic＋anchor registry

| 選択肢 | 長所 | 短所 | 判定 |
|---|---|---|---|
| Unity で DOM 座標を管理 | Unity側で完結して見える | DOM lifecycle をUnityが観測できず、座標通信も高頻度 | 不採用 |
| Action で「今ハイライトせよ」を一度だけ送る | 実装が小さい | 未mount時に失い、再接続復元できない | 不採用 |
| Unityが状態を宣言し、Web registryが対象を追跡してack | SPA lifecycleと再接続に耐え、責務が分離する | observerの統合と状態機械が必要 | **採用** |

Unity は `tutorial.presentation` に現在有効な平面チュートリアルを完全 snapshot として載せる。Web は各 DOM 要素の `data-tutorial-anchor="<anchorId>"` を registry に登録し、宣言された highlight を overlay portal に描く。`data-testid` はテスト都合、`data-tutorial-anchor` はゲーム契約なので兼用しない。

registry は次を一つの再評価キューへ集約する。

- `MutationObserver`: anchor の mount/unmount、属性変更、Portal/リスト再構築
- `ResizeObserver`: anchor と viewport のサイズ変更
- `IntersectionObserver`: viewport との交差状態
- document の capture phase `scroll`: 任意のスクロールコンテナ移動
- `window.resize` と `visualViewport.resize/scroll`: CEF viewport とズーム変化

observer callback 内で即座に layout を反復読取せず、dirty anchor ID を集めて次の `requestAnimationFrame` に1回だけ `getBoundingClientRect()` を読む。overlay は `position: fixed` で描き、pointer event は原則 `none` とする。複数一致は契約違反で、最初の要素を曖昧採用せず `not-found` 相当の `duplicate-anchor` 診断をログ・テストで失敗させる。

### 3.2 ready / not-found / hidden の意味

各 highlight command は次の Web resolution を持つ。

- `ready`: anchor が一意に mount され、要素と祖先が表示状態で、面積があり、viewport と交差して矩形を描ける。
- `not-found`: 一意な anchor が DOM に存在しない。未mount・仮想化範囲外・重複もこの ack の reason で区別する。
- `hidden`: anchor は一意に存在するが、`display:none`、`visibility:hidden`、`hidden`/`aria-hidden`、ゼロ面積、非交差のいずれかで描画できない。

Web は command を受けた直後と resolution が変わった時だけ `tutorial.anchor_ack` を送る。Unity は ack を診断・進行ゲートに利用できるが、ack 未着だけでチャレンジを完了・失敗させない。ack には session/revision/highlightId/anchorId を全て含め、古い DOM 状態の ack を Unity が破棄できるようにする。

`not-found` は SPA の一時状態として正常に起こり得る。既定では Topic を維持して registry が mount を待つ。自動スクロールや画面遷移はハイライト基盤の責務に含めない。

### 3.3 キーガイドは C2 共通基盤へ統合する

C2 の共通 Topic を `ui.key_hints` として提案し、これを唯一のキーヒント表示経路とする。Tutorial manager は TMP を直接操作せず、共通 key-hint state の `tutorial` source を set/clear する。共通 state は `UIState` に合う source だけを完全 snapshot 配信し、Web は C2 の同じ `KeyHintBar` を描く。C2 の詳細計画が別名を既に採用していた場合は、C4で別名を増やさずC2側の名前へ機械的に読み替える。

優先順位は `tutorial > mode/screen default` とする。tutorial が非対象 UIState の間は source を保持したまま出力から除外し、対象 state に戻れば再表示する。チュートリアル独自の文字列 DOM、Topic、キーアイコン体系は作らない。文言は A5/D の i18n key を基本とし、移行期間のみ displayText を併記する。

### 3.4 ワールド空間系は Unity 残置、lifecycle だけ統一

MapObjectPin、HudArrow、BlockPlacePreview の manager と view は Unity のままとする。`TutorialManager.ApplyTutorial` / `CompleteChallenge` の同じ呼び出しで、DOM highlight、key hint、world view を同時に開始・終了する。

整合ルール:

1. `tutorialSessionId` は challenge の適用1回ごとに発行する。
2. 同じ session の平面状態と world view を同じ Unity transaction 内で set する。
3. complete では Topic を `highlights: []` にし、key-hint tutorial source を clear してから world view を `CompleteTutorial()` する。
4. Web ack は表示能力の報告であり、`BlockPlacePreview` の `OnBlockPlaced` 等、ゲーム進行判定を代替しない。
5. Web 切断中も world view とチャレンジ進行は継続し、再接続 snapshot はその時点で有効な平面状態だけを返す。

## 4. Topic・Action 契約案

### 4.1 TypeScript 型

```ts
type TutorialHighlightKind = "outline" | "spotlight" | "callout";
type TutorialAnchorStatus = "ready" | "not-found" | "hidden";
type TutorialAnchorReason =
  | "mounted"
  | "missing"
  | "duplicate-anchor"
  | "display-none"
  | "visibility-hidden"
  | "aria-hidden"
  | "zero-area"
  | "outside-viewport";

type TutorialHighlight = {
  highlightId: string;       // session内で安定。anchorIdとは別
  anchorId: string;          // data-tutorial-anchor の値
  kind: TutorialHighlightKind;
  messageKey?: string;
  message: string;
  paddingPx: number;
  blocksPointerInput: boolean;
};

type TutorialPresentationData = {
  tutorialSessionId: string;
  revision: number;
  challengeId: string;
  highlights: TutorialHighlight[];
};

type TutorialAnchorAckPayload = {
  tutorialSessionId: string;
  revision: number;
  highlightId: string;
  anchorId: string;
  status: TutorialAnchorStatus;
  reason: TutorialAnchorReason;
};

type TutorialKeyHintSource = {
  source: "tutorial";
  tutorialSessionId: string;
  targetUiState: string;
  entries: Array<{
    bindingId: string;       // C2の入力binding契約を再利用
    labelKey?: string;
    label: string;
  }>;
};
```

Topic は `tutorial.presentation`。Action は `tutorial.anchor_ack`。ack はゲーム操作ではないが、既存 Web→Unity RPC の `IActionHandler` を再利用し、新しい transport は作らない。Action result は stale session/revision をエラーにせず `ok: true` で無視してもよい設計にはせず、テスト可能な `stale_session` / `stale_revision` を返す。

key hint の wire payload は C2 の共通 Topic 型へ `source` と優先順位を追加して表現し、`tutorial.presentation` に複製しない。

### 4.2 Registry の内部型

```ts
type AnchorRegistration = {
  anchorId: string;
  element: HTMLElement;
  resizeObserver: ResizeObserver;
};

type ResolvedAnchor =
  | { status: "ready"; reason: "mounted"; rect: DOMRectReadOnly }
  | { status: "not-found"; reason: "missing" | "duplicate-anchor" }
  | {
      status: "hidden";
      reason: "display-none" | "visibility-hidden" | "aria-hidden" |
              "zero-area" | "outside-viewport";
    };

interface TutorialAnchorRegistry {
  resolve(anchorId: string): ResolvedAnchor;
  subscribe(anchorId: string, listener: (value: ResolvedAnchor) => void): () => void;
  dispose(): void;
}
```

React の各 feature は共通 helper で `data-tutorial-anchor` を付けるだけとし、個々の component が observer や ack を送らない。registry と overlay controller が一元管理する。

## 5. 配置と既存前例

| 項目 | 所有層 | 使用機構・前例 |
|---|---|---|
| tutorial lifecycle/session | `Client.Game/InGame/Tutorial` | 既存 `TutorialManager` の Apply/Complete |
| DOM表示状態 store | 同 tutorial domain | `TutorialPresentationStateStore`。各 manager から現在値を pushし、private `Subject<T>` / public `IObservable<T>` で通知。静的 Master へ置かない |
| Topic/ack handler | `Client.WebUiHost/Game/Topics/Tutorial`, `Actions/Tutorial` | `UiStateTopic` / `IActionHandler` の既存 transport |
| anchor registry/overlay | `webui/src/features/tutorial` または共通 tutorial package | React feature、DOM Observer API |
| key hint | C2 共通 key-hint state/component | `KeyControlDescription` Web 化計画を拡張 |
| world view | 既存 Tutorial manager/view | Camera、UniRx `OnBlockPlaced`、GameObject の既存機構 |

既存 WebSocket Topic/Action と C2 key-hint 基盤候補を拡張できるため、新しい通信経路やチュートリアル専用キーヒント renderer は作らない。DOM lifecycle を扱う既存機構はないため anchor registry は新設する。

## 6. 実装フェーズ

### Phase T0: anchor 契約棚卸し

- A5 の命名規約に従い、既存10画面とC1〜C3の対象を一覧化する。
- anchor ID の重複、Portal、仮想化、未mountになり得る対象をテストケースへ分類する。
- tutorial master の既存 `HighLightUIObjectId` / item GUID から新 anchor ID への全件対応表を作る。欠損補完や optional fallback は使わず一括移行する。

### Phase T1: registry と overlay の単体実装

- Mutation/Resize/Intersection/capture scroll/viewport observer を統合する。
- ready/not-found/hidden 判定、重複検知、rAF batching、cleanup を Vitest（happy-domだけで不足する箇所は Playwright component/e2e）で検証する。
- spotlight/outline/callout の最小 overlay と pointer input 方針を実装する。

### Phase T2: Unity presentation と ack 契約

- `TutorialManager` 配下に session/revision 付き平面 presentation state を置く。
- Topic、ack Action、zod/C# DTO/WireFixture を追加する。
- 再接続 snapshot、stale ack、mount遅延、unmount→remount の統合テストを行う。

### Phase T3: UIHighlight / ItemHighlight 移行

- `UIHighlightTutorialManager` と `ItemViewHighLightTutorialManager` の Web モード表示先を presentation state に切り替える。
- Unity の `FindObjectsOfType` / RectTransform 追従ビューを Web モード時に抑止する。
- 既存 tutorial param と anchor 対応を全件移行する。

### Phase T4: C2 key hint 統合

- `KeyControlTutorialManager` を C2 共通 key-hint source の set/clear へ切り替える。
- UIState の変化通知で表示を切り替え、毎フレーム同値判定を廃止する。
- tutorial source の優先順位、i18n、対象 state 復帰を検証する。

### Phase T5: world view 発火整合と実地試験

- MapObjectPin、HudArrow、BlockPlacePreview が DOM系と同一 session lifecycle で開始・終了することをテストする。
- 該当チャレンジを実際に進行し、画面遷移、スクロール、Portal、リスト仮想化、CEFリロード、WS再接続を録画付きで確認する。

## 7. Definition of Done

- DOM 対象は `data-tutorial-anchor` で一意に識別され、`data-testid` と分離される。
- Unity は `tutorial.presentation` の完全 snapshot を主権として配信し、Web は再接続時に最新状態だけで復元する。
- MutationObserver、ResizeObserver、IntersectionObserver、capture scroll、viewport変化で overlay が追従し、observer と listener が unmount 時に解放される。
- Web は ready/not-found/hidden と具体 reason を session/revision/highlight/anchor 照合付きで ack し、stale ack は副作用なしで拒否される。
- 未mount、hidden、画面外、Portal、仮想化、重複 anchor、remount の自動テストが通る。
- キーガイドは C2 共通 Topic/component の tutorial source として表示され、専用 renderer や毎フレーム UIState 比較を持たない。
- MapObjectPin、HudArrow、BlockPlacePreview は Unity に残り、従来の world 追従・設置完了判定が維持される。
- challenge 完了時に DOM highlight、key hint、world view が同じ session から全て消え、次 challenge の表示と混ざらない。
- zod/C# DTO/WireFixture、Unity lifecycle test、Vitest、Playwright、録画付き PlayMode が通る。

## 8. 未決事項（ユーザー判断が必要）

1. **対象が画面外のときの製品挙動**: `hidden/outside-viewport` のまま待つか、画面端へ誘導表示を出すか。自動スクロールは操作を奪うため採用しない前提だが、誘導表示はUI仕様の判断が要る。
2. **ハイライト中の入力遮断**: `blocksPointerInput` を tutorial param ごとに許可するか、全ハイライトを説明専用（常にクリック可能）にするか。ゲームデザイン上の強制導線に関わる。
3. **not-found のユーザー向けフォールバック**: 開発ログ/ack のみとするか、「対象画面を開いてください」という一般案内を表示するか。技術的にはどちらでも registry 契約は同じである。
4. **callout の文言配置**: anchor の上下左右を自動選択するだけでよいか、コンテンツ側が preferred placement を指定する必要があるか。初回は自動配置だけで実装可能。

DOM方式、C2統合、ワールド空間系のUnity残置は技術的に決定済みであり未決事項にはしない。

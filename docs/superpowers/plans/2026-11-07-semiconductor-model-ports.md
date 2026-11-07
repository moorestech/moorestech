# 半導体機械の占有・接続表示整合 Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行はmoores-subagent-driven-developmentが担う。モデル実装と検証・レビュー・PRを完遂する。

**Goal:** 半導体設備を、見える口へ接続して搬入・生産・搬出でき、占有からはみ出さないモデルにする。

**Architecture:** 現行の占有・論理接続を基準にPrefabをUnity Editorで修正する。レシピ量や供給ロジックを変えて表示の欠落を吸収しない。マスタは実物照合で必要な修正を確定し、変更時は別PRとピンを揃える。

**Tech Stack:** Unity 6000.3.8f1、PrefabUtility、uloop、v8 JSON master、Playtest DSL。

## Requirements

- 専用4機の本体・装飾・表示・有効Colliderを3×2×3の占有内へ収める。共用ベルトの地下脚は接続模型の前例と比較して判断する。
- 専用4機の模型は入力セル(1,0,0)、出力セル(1,0,2)、液体入口セル(0,0,1)と一致し、余分な出口・入口らしい模型がない。
- 空気清浄機を占有1×2×1へ収め、入力セル(0,0,0)の水平4方向入力を示し、出力に見える模型を残さない。
- 化学プラントは占有6×16×11と、item入力(3,0,0)、fluid入力(3,1,0)/(2,1,0)、fluid出力(2,1,10)を部位別に照合し、欠落表示と突出を直す。
- 回転4方向で模型と接続位置の一致を測定し、実ベルト・実パイプによる供給・加工・出力を録画で確かめる。
- 生成アイコンは対象全体が画像内に収まり、口の表示を撮影対象レイヤーへ揃える。
- 今回は模型とマスタの整合修正。ゲームバランス、工程、セーブ形式、共用ベルト資産そのものは変更しない。

## Global Constraints

- Unity資産はUnity Editor経由で保存する。YAML直接編集、.meta手動作成は禁止。
- 隔離worktree以外で実装しない。既存本体のdirty pinとprivate repoの未追跡素材を巻き込まない。
- 別repoに変更する場合はpush・PR作成と、そのpush済みcommitへの本体ピン更新を揃える。
- C#変更を行った場合はuloop compileを実行する。アセット変更だけで新規ユニットテストを作らない。

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反0件・修正0件 / 既存機械PrefabとJSONの責務内で修正、機能喪失なし。
- Phase 2.6（型閉包・重複・ADR矛盾）: 実施済み / 強0・弱0・第3バケツ0 / fresh-context独立確認、プロダクトAPI新設なし。

## 配置と前例

| 対象 | 変更の所有者 | 前例 |
|---|---|---|
| 本体・口・Collider | 各設備Prefab | ElectricPrecisionAssembler.prefab（占有内の外形とセル中央のポート表示） |
| 接続セル・寸法 | blocks.jsonの各設備 | 現行CleanRoomMachine定義とBlockConnectorConnectPositionCalculator |
| 観測と供給操作 | PlaytestResults下の使い捨てシナリオ | scenarios/tier9/tier9-1a-chip-line.cs、building/belt-segment-flow.cs |

精密組立機は自分のポート表示をPrefabで保持する。今回も各機械Prefabが自分のポートを保持し、共用ロジックに機械名の分岐を追加しない。

## Task 1: 対象設備のモデルとマスタを整合させる

**Files:**
- Modify: `moorestech_client/Assets/AddressableResources/Block/CleanRoomMachine/WaferSlicer.prefab`
- Modify: `moorestech_client/Assets/AddressableResources/Block/CleanRoomMachine/EuvLithographyTool.prefab`
- Modify: `moorestech_client/Assets/AddressableResources/Block/CleanRoomMachine/DicingSaw.prefab`
- Modify: `moorestech_client/Assets/AddressableResources/Block/CleanRoomMachine/PackagingMachine.prefab`
- Modify: `moorestech_client/Assets/AddressableResources/Block/CleanRoomAirFilter.prefab`
- Modify: `moorestech_client/Assets/AddressableResources/Block/Chemical Plants.prefab`
- Inspect and modify if required: `../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/blocks.json`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Block/IconCapture/BlockIconCaptureShot.cs`
- Modify when external commit changes: `.moorestech-external-revisions.json`

**Interfaces:**
- Consumes: 現行blocks.jsonのblockSize、inventoryConnectors、fluidInventoryConnectors。
- Produces: 各設備の占有と接続位置に一致するPrefab。新設プロダクトAPIはない。

- [ ] **Step 1: 専用4機の本体を局所調整する。** 本体の目標中心(1.5,0.85,1.5)、scale(2.8,1.7,2.8)。識別Accentは高さ2以内、文字も本体表面へ個別に調整する。root scale=1を維持する。計測で本体・装飾・Colliderが占有内であることを確かめる。
- [ ] **Step 2: ベルト模型を2本へ整理する。** 入力(1,0,0)を維持し、既存模型1本を出力(1,0,2)へ移し、余剰を削除。両方とも流れは＋Z。誤解を招くベルト由来のPreviewOnlyやClickColliderは実物のクリック範囲と照合し、各機械を覆う本体Colliderを残す。
- [ ] **Step 3: 液体入力を表示する。** 左面x=0、セル中心y=0.5/z=1.5にパイプ断面と合うソケットを置く。ソケットは占有内へ伸ばす。入力識別を付け、実パイプを隣接セル(-1,0,1)へ置いた状態で継ぎ目を確かめる。
- [ ] **Step 4: 空気清浄機を修正する。** 本体・装飾を[0,1]×[0,2]×[0,1]へ収める。2本の誤ったベルト模型を除き、水平4側面のセル中央にフィルター入力を示す。出力模型は付けない。既存論理入力4方向を削らない。
- [ ] **Step 5: 化学プラントを修正する。** scout.mdとUnityの本体/ポート計測を照合し、占有内へ本体を補正する。前面item1口、前面fluid2口、背面fluid1口をマスタ座標に表示する。論理定義が正しければ不用なmaster編集はしない。真の不整合があれば当該設備の定義だけをモデルと同時に更新し、レシピ量は変更しない。
- [ ] **Step 6: 変更した全PrefabをUnityで再読込・再計測し、変更前後を3方向からレンダーする。** 数値は報告へ、画像は.superpowers/sdd/screenshots配下へ3枚以内で保存。
- [ ] **Step 7: Task 1:をsubjectに含むコミットを作る。** マスタ変更時は別repoにもコミットし、報告に両SHAを書く。実装スクリプトは使い捨てEDCテキストとしてgitignore内へ置く。

### Task 1 のQA追従: アイコンの全体表示

- [ ] **Step 8: 共通アイコン撮影のフレームを外接球で保証する。** 空気清浄機の頂点は変更前画像内y93.1から変更後y-47.8へ出たことを独立検査で確認。Renderer中心の平均を使わず、union AABBの中心を使う。private AimCameraの中心引数と呼び出し側の平均計算を除く。camera角30/45は維持する。

```csharp
var center = (minPos + maxPos) * 0.5f;
var radius = Vector3.Distance(minPos, maxPos) * 0.5f;
var verticalHalfFov = blockImageCamera.fieldOfView * Mathf.Deg2Rad * 0.5f;
var horizontalHalfFov = Mathf.Atan(Mathf.Tan(verticalHalfFov) * blockImageCamera.aspect);
var distance = Mathf.Max(radius / Mathf.Sin(Mathf.Min(verticalHalfFov, horizontalHalfFov)) * 1.05f, radius + blockImageCamera.nearClipPlane);
blockImageCamera.transform.position = center - blockImageCamera.transform.forward * distance;
```

- [ ] **Step 9: compileと6アイコンと未変更の形状別代表（ベルト・石窯・電柱・電気精密組立機）の実撮影を行う。** 画像端の切断がなく、入力面・液体口・専用4機の識別性が保たれることを確認。カメラのnear=.3/far=1000は対象寸法で足りる。全体を入れるため以前より小さめに写ることはagent前提のトレードオフで、共通撮影の責務内で直す。新しいプロダクトAPIや型は作らない。
- [ ] **Step 10: Task 1:を含む修正コミットを作る。** レイヤー修正と撮影計算修正を区別して記録する。

## Task 2: unityプレイ録画テストで供給と占有を検証する

**Files:**
- Use scratch: `moorestech_client/PlaytestResults/scratch/semiconductor-model-ports.cs`
- Reference: `.agents/skills/unity-playmode-recorded-playtest/scenarios/tier9/tier9-1a-chip-line.cs`
- Report: `.superpowers/sdd/verification.md`

- [ ] scout.mdで確認した実在APIだけを使い、供給ベルトと配管を含むシナリオを作る。必要素材を機械へ直挿入して供給検証を代替しない。電力・クリーンルームは既存シナリオの構築法を使う。
- [ ] North/East/South/Westでポートの接続相手セルと模型を確認。隣接壁・機械の占有、照準・削除範囲、通常/プレビュー表示も確認する。
- [ ] 各機械について入力搬送、IPAタンク増減、加工進行、出力搬送を観測。液体なしで進行しないことと供給後の再開を確認。空気清浄機は4方向からの入力を確認する。
- [ ] 調査中Record=false、最終提出時Record=trueでシナリオ実行。Success、Asserts全PASS、非空動画、実プレイ視点の画像を確認し、観測値をverification.mdへ記録。
- [ ] C#変更時はcompile、必要な既存焦点テストを実行。テストワールドはlist-work-saves.shで特定して自分の新規分を片付ける。

## Task 3: 全ブランチレビュー

- [ ] 必ずmoores-code-reviewスキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。
- [ ] 目的だけを渡した独立評価役2体に実物の数値・モデル画像・供給の検証結果を判定させる。再現した不具合は修正し、影響する経路を再検証する。

## Task 4: PRを作り終了可能状態にする

- [ ] pr-createスキルでPRを作成し、masterとのコンフリクトがあればmasterをマージして解消・コンパイル確認のうえpushする。
- [ ] マスタに変更がある場合は別repoにもPRを作り、push済みcommitへ本体のピンを更新する。全作業をcommit・push済みでPRがマージ可能な状態にする。
- [ ] scripts/sdd-archiveで記録を退避してから、作業終了を報告する。

## 判断記録（ADR）

- ユーザー裁定: 2026-11-07「じゃあそれでモデルとマスタを変更してほしい」。元の調査提案を実装する。詳細はdocs/adr/0097-semiconductor-model-footprints-and-connectors.md。
- agent前提: 現行の占有・論理接続を基準に模型を修正し、正しいマスタ値へ見かけだけの変更は加えない。
- agent前提: ランタイムの模型表示・接続体験に触れるためunityプレイ録画テストを必須にする。セーブ形式は変更しない。

- agent前提（QA追従）: 新しい空気清浄機形状で露呈したアイコンの見切れは、既存BlockIconCaptureShotのフレーム責務で修正する。独立再現と型閉包検査0件。ソケットのレイヤー修正は26Rendererと親7件をBlockへ揃える既存契約への追従。

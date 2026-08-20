# 初期チュートリアル（生きる基盤）マスタ書き換え要望の実現可否調査

- Date: 2026-08-20
- 対象: `../moorestech_master` origin/master (`4e07ed0`) の `server_v8/mods/moorestechAlphaMod_8/master/challenges.json` / `items.json` / `localization/localization.csv`
- 方法: クライアント/Web UI/サーバー/SourceGeneratorのコード読解。Unity実行は未実施（下記「検証手順」参照）

## 0. 着手前に知っておくべき3つの前提

1. **ローカルの `../moorestech_master` は `56dbd35` で止まっており origin/master より古い。** origin/master には既に「原始研究1〜4を完了する」4件、`challenge.current-hud`（左上HUD枠線）、`recipe.craft-button`、`research.node-<guid>`、`build-menu.entry-block-<guid>`→`hotbar.hud` のD&D誘導が入っている（ADR 0016）。要望の多くはこの版を土台にする。ローカルには `map.json`/`generation.json` の未コミット変更もあるので、更新時は stash か別worktreeで。
2. **`tools/tutorial_v3_port/generate_challenges.py` は committed JSON より古い。** origin/master版でも `highLightUIObjectId` / `fromUIObjectId` / `researchNode:<guid>` / `buildMenuBlock:<guid>` / `hotbar` という廃止済み語彙を出力する（scratchpadで再生成→diffで確認済み）。そのまま再実行すると JSON が退行し、`highLightAnchorId` 欠落で `MooresmasterLoaderException` → **ゲーム起動不能**になる（`LoaderGenerator.cs:143-171` の必須キーnullチェック。`default:` はエディタ用メタで、ローダーは `optional: true` しか見ない）。
3. **Web UI で文言が表示される経路は「チャレンジtitle（左上HUD）・summary（Qのチャレンジ画面のみ）・ワールドピン（mapObjectPin/veinPin）・スキット」だけ。** `uiHighLight`/`itemViewHighLight` は枠線のみで `highLightText` は描画されない（`presentation.ts:4-9` 「文言を持つkindは廃止済み」）。`keyControl` は Web UI モードでは無表示（`KeyControlTutorialManager.cs:78` / `KeyControlDescription.cs:46`・Web配信topic無し）。これは裁定 `.decisions/2026-08-18-チュートリアル提示はWebUI経路に統一しD&Dは矢印ループで示す.md`（棄却案③「uiHighLight文言吹き出しの新設」）と `.decisions/2026-08-19-keyControlチュートリアルは将来使うので残す.md` の帰結。**文言付きハイライト・キー操作提示を出す要望は裁定の更新が要る。**

## 1. 要望別の可否マトリクス

| # | 要望 | マスタのみで可 | 必要なコード変更 | 備考 |
|---|---|---|---|---|
| 1 | 小石3個: HUDハイライトに「左上で現在の目標を確認する」文言 | △ 枠線は既に有（`challenge.current-hud`）。文言は出ない | Web: highlight にラベル描画を追加（kind拡張 or `challengeTutorial.<guid>.text` をHUD近傍に描く） | 開始スキット100_start_game 139行目に同趣旨の台詞は追加済み。裁定2026-08-18の棄却③と衝突 |
| 2 | 石器を作る: Tabを促す keyControl | ✗（書けるが表示されない） | Web配信topic＋描画（keyControl復活）。schema `uiState` enum が実UIStateEnumとズレ（`BlockInventory`→実体は`SubInventory`、`ResearchTree`/`BuildMenu`無し）ので schema も直す | keyControl は裁定で「後で使う」前提の休眠機構 |
| 3 | 石器を作る: ①石器を選択 ②クラフト長押し の手順 | △ 枠線2つ（`recipe.item-<itemId>`・`recipe.craft-button`）は origin/master に既にある。`recipe.craft-button` は選択中アイテムの先頭レシピにだけ付くので「選択→ボタン」の順で自然に出る | 番号・文言を出すには上記#1と同じ描画拡張 | 「長押し」はテキストでしか伝えられない |
| 4 | 木を伐採の前に「石器を装備する」: Tab → 装備スロットへドラッグ誘導 | ✗ | (a) 新チャレンジにするなら server: `taskCompletionType` に `equipItem` 追加（現行は createItem/inInventoryItem/blockPlace/completeResearch の4種のみ）。(b) Web: インベントリ側・装備側のアンカーが無い（`anchorIds.ts` 静的14＋動的4に `inventory.*`/`equipment.*` は無い）→ `inventory.item-<itemId>`（そのアイテムが入った先頭スロット）と `equipment.slot-<index>` or `equipment.hud` を新設。(c) Tab提示は#2 | 木は `miningTools` に石器/石の斧が要るので装備誘導自体は妥当。チャレンジを増やさず「木を伐採」の tutorials に付ける案ならserver変更不要（(b)(c)は要る） |
| 5 | 木の板5枚: Tabでインベントリを開く | ✗ | #2と同じ | |
| 6 | 木の棒5本: 木の棒を itemViewHighLight | ✅ | なし | 木の棒は initialUnlocked・レシピも初期解放 |
| 7 | 原始研究1: 「Rを押して研究タブを開く」文言 | △ summary には既に「Rキーで研究画面を開き…」がある（Q画面のみ表示）。左上HUDに出すなら title に含める（例「Rで研究画面を開き原始研究1を完了する」）のがマスタのみの唯一の手 | HUD常設文言にするなら #2 | `research.node-<guid>` 枠線は研究画面を開いたときだけ出る |
| 8 | 「石を採掘する」→「石を5個採掘する」 | ✅（JSON title＋CSV 2列） | なし。ただし generator は `challengeGuid = uuid5(title)` なので **generatorを直さず再生成すると guid が変わり** CSV行孤児化・セーブの完了状態消失・`prevChallengeGuids` 付け替えが起きる | 生成器に title と独立な安定キー列を足すのが筋（2026-08-19 tutorialGuid 裁定と同じ思想） |
| 9 | 砕いた石材5個: itemViewHighLight | ✅ | なし | 砕いた石材は研究1の `unlockItemRecipeView` で表示解放。⑥研究1→⑧砕石 の順なので表示条件を満たす |
| 10 | 石の斧を作る: itemViewHighLight／craft系チャレンジは目標アイテムを常時ハイライト | ✅ | なし | 石の斧は研究2で表示解放、⑨研究2→⑩石の斧 の順でOK。**表示されるのはレシピビューアのアイテム一覧（右列）**で、一覧に出ない（未解放）アイテムは枠線も出ない。拾う/採掘系（小石・原木・石・粘土・青銅鉱石）に付けるとレシピ一覧を指すことになり誤誘導なので craft/inInventory(クラフト由来) に限定を推奨 |
| 11 | 斧・石器の装備モデル表示 | 石器 ✅ ／ 石の斧 ✗ | 石の斧: Unity側で Addressable 登録が必要 | 詳細 §3 |

## 2. マスタのみで出来る分の具体的な書き換え

### 2.1 前提作業
```bash
cd ../moorestech_master
git stash            # ローカル未コミット(map/generation)がある場合
git checkout master && git pull --ff-only   # 4e07ed0 以降へ
```
（本repo側 `.moorestech-external-revisions.json` のピンも更新対象。現状 origin/master のピン `c35f10ab` は存在しないコミットで CI が止まっている = bd moorestech-hvwb）

### 2.2 generator を現行語彙へ追従させる（先にやらないと再生成で退行）
`tools/tutorial_v3_port/generate_challenges.py`:
- `ui()` → `{'highLightAnchorId': anchor_id, 'highLightText': text}`（`'challengeHud'`→`'challenge.current-hud'`、`'craftButton'`→`'recipe.craft-button'`）
- `research_node_ui()` → `f'research.node-{guid}'`
- `drag()` → `{'fromAnchorId': f'build-menu.entry-block-{guid}', 'toAnchorId': 'hotbar.hud'}`
- `guid_for(title)` の title 依存を切る: CHALLENGES の各行に安定キー（例 `'mine-stone'`）を足し `uuid5(NS, 'tutorial-v8:' + key)` にする。**既存guidを維持するため、キーは現タイトル文字列そのものを初期値にする**（= rename してもキーは旧タイトルのまま → guid不変）。`tutorial_guid_for` も同じキーで導出。
- 再生成後 `diff` で「意図した差分のみ」を確認（anchorId系キー名が `*AnchorId` であること）。

### 2.3 追加・変更する tutorials（origin/master 基準）
```python
# 木の棒を5本作る
[iv('木の棒', '木の板から木の棒を作る')]
# 砕いた石材を5個作る
[iv('砕いた石材', '石から砕いた石材を作る')]
# 石の斧を作る
[iv('石の斧', '木の棒と砕いた石材で石の斧を作る')]
# 「石を採掘する」→ title を '石を5個採掘する'（キーは旧タイトル固定で guid 不変）
```
`iv()` の text は表示されない（`highLightText` は収集されるだけ）が、`challengeTutorial.<guid>.text` の原文として CSV に載る。

### 2.4 localization.csv
- `challenge.ba99109e-fbd4-….title` 行の Source/japanese を `石を5個採掘する`、english を `Mine 5 Stone` に。
- 新規 tutorial 3件分の `challengeTutorial.<tutorialGuid>.text` 行を追加（CSV 未登録でも JSON 原文にフォールバックするが、英語列のため追加が筋）。tutorialGuid は再生成後の JSON から拾う。

### 2.5 items.json（装備モデル・石器）
```json
"addressablePaths": { "handGrabModel": "Vanilla/Item/StoneTool", "entityModel": "" }
```
を 石器(`76174235-…`) に設定し、旧キー `"handGrabModelAddressablePath": ""` を削除。`Vanilla/Item/StoneTool` は `Vanilla Asset Group.asset` に登録済み（`AddressableResources/Item/StoneTool.prefab`、手持ちオフセット/スケール焼き込み済み）。

### 2.6 検証手順
1. 再生成 → `python3 -m json.tool` で構文・`diff` で差分確認。
2. 本repoのテスト（要worktree＋Editor）: `uloop run-tests --test-mode EditMode --filter-type regex --filter-value "TutorialAnchorContractTest|MasterSourceTextCollectorTest|ChallengeMaster"`。`TutorialAnchorContractTest.AllModAnchorIdsResolveToWebVocabulary` が anchorId の唯一の機械検査（sibling repo の `server*/mods/*/master/challenges.json` を走査）。
3. unityプレイ録画テスト（playtest DSL）で ①石器選択→枠線 ②木の棒/砕石/石の斧の枠線 ③石器装備でモデル表示 を目視。

## 3. 装備モデル（石器・石の斧）

- 読み手は `EquipmentHeldItemModel.cs:75-76` のみ。`AddressablePaths?.HandGrabModel` が空なら**無言で return**（ログも出ない）。
- items.json は 80件中 57件が `addressablePaths{}`（中身は全部空）、23件が旧フラットキー `handGrabModelAddressablePath`（ローダー未参照＝デッドデータ）。**handGrabModel が設定されているアイテムは現状ゼロ**。
- 石器: 上記2.5で解決。
- 石の斧: Addressable に斧モデルが無い（`Assets/Dependencies/Sketchfab/StoneAxe/StoneAxe.prefab` は存在するが未登録）。正攻法は `AddressableResources/Item/StoneAxe.prefab` を作り手持ちオフセットを焼き込んで `Vanilla/Item/StoneAxe` で登録 → master に設定。暫定なら `Vanilla/Item/StoneHammer` 等の既存アドレスを仮置き。
- ついで: 旧フラットキーの残り21件（原木・木の板・鉄系など）も `addressablePaths` 形へ揃えるのが設計原則（欠損補完で吸収しない）に沿う。

## 4. コード変更が要る分の最小設計案（grill対象）

A. **文言付きハイライト / キー操作提示の Web 表示**（#1,#2,#3,#5,#7）
   - 裁定 2026-08-18 棄却③・2026-08-19 keyControl休眠 の更新が前提。
   - 案: `TutorialHighlightSchema` に `labelTutorialGuid?`（文言は `challengeTutorial.<guid>.text` で Web 解決・WorldPinOverlay と同方式）を足し、outline の横にラベルを描く。keyControl は `TutorialPresentationStateStore` に `kind:"keyControl"` を足して HUD 固定位置に描く（`uiState` 一致時のみ）。schema の `uiState` enum を実 `UIStateEnum` に揃える。
B. **装備誘導のアンカー**（#4）: Web に `inventory.item-<itemId>`（該当アイテムの先頭スロット。`recipe.item-<itemId>` と同じ Unity `FromItemId` 経由か、Web 側で itemId から DOM を引く）と `equipment.hud`（または `equipment.slot-<index>`）を追加し `uiDragGuide` で結ぶ。新チャレンジ化するなら server に `equipItem` taskCompletionType（`LocalPlayerEquipment` 相当のサーバー側装備状態の購読が要る）。
C. **generator の安定キー化**（#8）: §2.2。

## 5. 参照
- `.decisions/2026-08-18-チュートリアル提示はWebUI経路に統一しD&Dは矢印ループで示す.md`
- `.decisions/2026-08-19-チュートリアルUI指定はWebアンカーIDを直書きし変換も検証も持たない.md`
- `.decisions/2026-08-19-tutorialGuidは文言非依存の安定キーから導出する.md`
- `.decisions/2026-08-19-keyControlチュートリアルは将来使うので残す.md`
- `docs/adr/0016-tutorial-challenge-lineup-research-sync.md`
- anchor正本: `moorestech_web/webui/src/shared/tutorialAnchor/anchorIds.ts`
- 描画: `moorestech_web/webui/src/features/tutorial/TutorialOverlay.tsx`, `WorldPinOverlay.tsx`
- 装備モデル: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/Equipment/EquipmentHeldItemModel.cs`

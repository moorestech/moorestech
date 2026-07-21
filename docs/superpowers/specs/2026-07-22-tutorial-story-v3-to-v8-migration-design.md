# チュートリアル/ストーリー v3→v8 移植 設計spec

日付: 2026-07-22
ステータス: ユーザー承認済み設計（実装プラン未作成）

## 背景と目的

v3 mod（`../moorestech_master/server/mods/moorestechAlphaMod_3`）にはストーリー（スキット）とチュートリアル（チャレンジ25個）が入っているが、v8 mod（`../moorestech_master/server_v8/mods/moorestechAlphaMod_8`）の challenges.json は空。v3のチュートリアル/ストーリー体験をv8に移植する。

## 調査で確定した事実

- v3チャレンジ: 1カテゴリ「生きる基盤」に25個。taskCompletionType は inInventoryItem×12 / createItem×11 / blockPlace×2。tutorials は mapObjectPin×4 / keyControl×1 / uiHighLight×1 / itemViewHighLight×3。actions は unlockItemRecipeView×13 / playSkit×2。
- スキットは2本のみ（`Vanilla/Skit/skits/100_start_game`、`200_star_background`）。クライアントの `moorestech_client/Assets/AddressableResources/Skit/skits/` に実在し、パス流用可能。
- **v3とv8でアイテムGUIDの共通は0件**（v8は全GUID再発行済み）。名前一致もv3の153品中23品のみ。
- v3チュートリアルの背骨（木の風車・ふるい・亜鉛・遠心分離機）はv8に存在しない。v8の進行は「石器→青銅→鉄→電気→クリーンルーム/半導体」で、research.json（47ノード）が解放を駆動する。
- 現行 challenges.yml スキーマはv3のJSON構造とほぼ同系譜（taskParam/tutorials/startedActions/clearedActions/displayListParam）。スキーマ変換自体は軽い。
- `skit.json` スキーマは廃止済みで `characters.yml` に置換。v3の characters.json（Yori/Eleno/Kurua の3キャラ）は現行スキーマ形式と一致しており、v8側は空配列。

## 決定事項

1. **移植方針: v8進行に合わせた再構成**（ユーザー決定）。v3は「構成の雛形」（拾う→作る→設置するのリズム、チュートリアル種の使い所）として使い、チャレンジ列はv8の実進行に沿って書き直す。
2. **カバー範囲: v3同等の序盤のみ**（ユーザー決定）。小石→石器→原木→木材加工→粘土/レンガ→青銅鉱石→青銅インゴット→青銅シート入口。20〜25個・1カテゴリ。
3. **GUIDは全てv8実GUIDへ名前/概念ベースで張り替え**。機械マッピング不可のため対応表を作成し人手確認。スクリプト生成時はv8 masterから名前引きで解決（GUID手打ち禁止）。
4. **解放責務のSSOT**: v8はresearchが解放（unlockBlock等）を担うため、チャレンジ側の `unlockItemRecipeView` は原則使わない。チャレンジは「ガイド＋ストーリー再生」専任。例外はresearch解放前の序盤アイテムのみ実装時に個別検討。
5. **スキット2本は開始演出のため進行非依存、パス流用でそのまま移植**（startedActions の playSkit で結線）。新規スキットは作らない（YAGNI）。
6. **characters.json はv3の3キャラをそのまま移植**。

## 成果物

すべて `../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/`:

- `challenges.json` — 1カテゴリ・20〜25チャレンジの新規作成
- `characters.json` — v3の3キャラ移植

クライアント側（moorestech本体リポジトリ）は変更なし。

## 作業手順

1. **進行列の抽出**: v8の research.json と craftRecipes.json から序盤の必須クラフト経路を機械抽出し、v3の25個との対応表（踏襲/読み替え/廃止/新規）を作る。ここが設計の本体。
2. **Web UIチュートリアル対応の棚卸し**（リスク対策・対応表作成前に実施）: 4種のtutorialType（mapObjectPin/keyControl/uiHighLight/itemViewHighLight）のうちWeb UIで機能するものを実装から確認。未対応タイプは使わず、対応済みのピン系に寄せる。
3. **JSON生成**: 対応表からスクリプトで challenges.json を生成。GUIDはv8 masterから名前引き。
4. **チュートリアル張り替え**: v3の使い所リズムを踏襲し、参照先だけv8のGUID（mapObjects/items/blocks）とWeb UI IDへ。

## 検証（QA）

- uloopでMasterHolderロード確認（スキーマ不整合は MooresmasterLoaderException で初期化が無言死する既知の罠）
- プレイテストDSLで最初の数チャレンジの完了フロー（拾う→クラフト→スキット再生→次チャレンジ解放）を実走
- webuiでHUDピン/ハイライトの表示確認

## リスク

- **uGUI時代の識別子**: `uiHighLight` の `highLightUIObjectId` と `keyControl` の `uiState` はuGUI前提。UIはWeb移行済み＆HUDピンはWeb移行中（feature/webui-hud-pin）のため、v3のID流用は効かない可能性が高い。→ 手順2の棚卸しで対応済みタイプのみ採用。
- **research.json との整合**: チャレンジ列がresearch解放順と矛盾すると「作れと言われたが解放されていない」導線切れが起きる。→ 手順1で research 依存を対応表に併記し、各チャレンジの前提researchを明示する。
- **プレイテストDSLとmasterピンの互換**: プレイテストは master リポジトリの互換コミットにピン留めした worktree を使う運用のため、v8 master変更後のシナリオ実行はピン更新が必要になる場合がある。

## 検証済み範囲の注記

- スキット2本の「再生可能」はAddressablesにJSONが存在することの確認まで。実再生はプレイテストで検証する（未実施）。
- characters.yml とv3 characters.json の形式一致はフィールド名の目視一致まで。ロード検証はQAで行う。

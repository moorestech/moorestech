# mapObject HPバーの最前面描画 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: subagent-driven-development スキルを使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** mapObjectのHPバー（World Space Canvas）を全ての3Dジオメトリの手前に描画し、樹冠や岩に沈んで見えない状態を解消する。

**Architecture:** HPバーの構成要素（Image3枚・TMP_Text）のマテリアルを `ZTest Always` のものへ差し替える。Imageは自作UIシェーダ `UI/Overlay`（UI/Default相当＋ZTest Always）＋マテリアル1枚、TMP_Textは同梱の `TextMeshPro/Mobile/Distance Field Overlay` を使うフォントマテリアルプリセット1枚。C#の変更は無い。

**Tech Stack:** Unity 6 / URP 17 / uGUI 2.0 / TextMeshPro（`Assets/Dependencies/TextMesh Pro`）

## Requirements

- HPバーのImage3枚（Background / Fill / Handle）とTMP_Textが、フォーカス中のmapObject自身のメッシュに隠れずに見えること。受け入れ基準: PlayModeで木にフォーカスしたとき、樹冠と重なる位置でもバーとHP数値が完全に視認できる
- 貫通は全ジオメトリに対して行う（手前の別オブジェクト・プレイヤーキャラも貫通してよい）。受け入れ基準: ZTestがAlwaysであり、条件分岐やレイヤー別処理を持ち込まないこと
- 変更は `MapObjectHpBar.prefab` の正本1件で全参照元（203prefab）に伝播すること。受け入れ基準: 個別のwrapper prefab側にマテリアルのオーバーライドを作らない
- C#コードを変更しないこと。受け入れ基準: `git diff --stat` に `.cs` が現れない
- **やらないこと**: HPバーの高さ（現状: 見た目の外接頂部 +0.5m）の変更。これは bd moorestech-ditv の別PRで扱う
- **やらないこと**: 常時表示化。HPバーは従来どおり `MapObjectGameObject` のフォーカス時のみ表示

## Global Constraints

- Prefab・マテリアル等のUnity固有YAMLアセットをテキストエディタで直接編集することは禁止。`uloop execute-dynamic-code` によるUnity Editor経由の変更のみ許可（AGENTS.md）
- `.meta` ファイルは手動作成しない。Unityの自動生成のみ
- シェーダは1ファイル200行以下（AGENTS.md）
- コメントは日本語・英語の2行セット、各1行に収める（AGENTS.md）

---

### Task 1: ZTest AlwaysのUIシェーダを追加する

**Files:**
- Create: `moorestech_client/Assets/Asset/Common/Shader/UI/UIOverlay.shader`

**Interfaces:**
- Produces: シェーダ名 `UI/Overlay`（Task 2 が `Shader.Find("UI/Overlay")` で参照する）

- [x] **Step 1: UI/Default相当のシェーダを `ZTest Always` で作成する**

`Shader "UI/Overlay"` として、UI/Defaultのプロパティ（`_MainTex` / `_Color` / Stencil一式 / `_ColorMask` / `_UseUIAlphaClip`）をそのまま持ち、`ZTest [unity_GUIZTestMode]` を `ZTest Always` に置き換える。

- [x] **Step 2: コンパイルを確認する**

Run: `uloop get-logs ./moorestech_client --log-type Error`
Expected: シェーダのコンパイルエラーが出ていないこと

- [x] **Step 3: コミットする**

```bash
git add moorestech_client/Assets/Asset/Common/Shader/UI/
git commit -m "feat(ui): ZTest AlwaysのUIシェーダ UI/Overlay を追加"
```

### Task 2: HPバーのマテリアルを作成し prefab へ割り当てる

**Files:**
- Create: `moorestech_client/Assets/Asset/Common/Shader/UI/UIOverlay.mat`
- Create: `moorestech_client/Assets/Asset/Environment/Prefab/MapObjectHpBarText.mat`
- Modify: `moorestech_client/Assets/Asset/Environment/Prefab/MapObjectHpBar.prefab`

**Interfaces:**
- Consumes: `UI/Overlay` シェーダ（Task 1）
- Produces: 最前面描画される `MapObjectHpBar.prefab`

- [x] **Step 1: `uloop execute-dynamic-code` でマテリアル2枚を作成する**

`UIOverlay.mat` は `Shader.Find("UI/Overlay")`、`MapObjectHpBarText.mat` は `Shader.Find("TextMeshPro/Mobile/Distance Field Overlay")` を使い、TMP側はprefabが参照している元フォントマテリアルのプロパティ（フォントアトラス・面色・アウトライン設定）をコピーしてからシェーダだけ差し替える。

- [x] **Step 2: prefabのImage3枚とTMP_Textへ割り当てる**

`MapObjectHpBar.prefab` をロードし、`GetComponentsInChildren<Image>(true)` の全件へ `UIOverlay.mat` を、`TMP_Text.fontSharedMaterial` へ `MapObjectHpBarText.mat` を設定して `SaveAsPrefabAsset` する。

- [x] **Step 3: 割り当て結果を検証する**

`uloop execute-dynamic-code` でprefabを再ロードし、Image3枚の `material.shader.name == "UI/Overlay"`、TMP_Textの `fontSharedMaterial.shader.name == "TextMeshPro/Mobile/Distance Field Overlay"`、および両シェーダのZTestがAlways（`GetTag`/`renderQueue` ではなく `Shader.Find` 経由の実体確認）であることをログ出力する。
Expected: 4件すべて期待どおり

- [x] **Step 4: PlayModeで見た目を確認する**

unity-playmode-recorded-playtest で木にフォーカスし、HPバーが樹冠に隠れず見えることをスクリーンショットで確認する。

- [x] **Step 5: コミットする**

```bash
git add moorestech_client/Assets/Asset/
git commit -m "feat(mapobject): HPバーを3Dジオメトリの手前に描画する"
```

### Task 3: 全ブランチレビュー（省略不可）

- [ ] **Step 1: moores-code-review スキルでブランチ全体をレビューする**

ゴール文言による省略は不可。指摘を反映してからPRを作成する。

## 判断記録（ADR）

- [docs/adr/0031-mapobject-hpbar-renders-in-front-of-geometry.md](../../adr/0031-mapobject-hpbar-renders-in-front-of-geometry.md)
- [.decisions/2026-08-23-HPバーは全ジオメトリ貫通で最前面に描く.md](../../../.decisions/2026-08-23-HPバーは全ジオメトリ貫通で最前面に描く.md)
- TMPのマテリアルはprefabの元マテリアルからプロパティをコピーしてシェーダのみ差し替える。出所: agent前提（フォントアトラス参照を失うと文字が消えるため）

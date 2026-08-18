# digest.md フォーマット仕様

`pr-independent-review` のStep 7で生成する `digest.md` の正本フォーマット。
`scripts/digest_build.py` がこの仕様どおりに書かれた `digest.md` を読み、
`digest.html` と `findings.json` を決定論的に生成する。生成subagentはHTMLを
直接書かず、この節に従って `digest.md` だけを書く。

## digest.md フォーマット仕様（全タスク共通の契約）

実装者はこの節を仕様の正本として扱う。

### 文書ヘッダ（必須・ファイル先頭）

    # PR #1155 機械UI改修: 進捗矢印の共通化・タブ入替/未選択時レシピ表示・電力の充足率化（ADR 0010）

    ```yaml
    pr: 1155
    head: 33e39a1f0c2b4d5e6f708192a3b4c5d6e7f80912
    verdict: reject
    verdict_line: Critical 8件 / 設計判断 3件 / 新形 0件 / suppressed 0件
    date: 2026-08-18
    generated_at: 2026-08-18T02:44:00+09:00
    ```

- `verdict` は `auto` / `ruling` / `reject` / `stub` の4語のみ。`data-verdict` 属性へそのまま入る。
- `verdict` の表示文言は固定表: `auto`=自動マージ可 / `ruling`=新形につき裁定行き / `reject`=Critical差し戻し / `stub`=未測定（スタブ）。
- コンバータは先頭見出しの `PR #<番号>` をそのまま使う。**番号を二重に付けない**（`独立レビュー: PR #1155 PR #1155 ...` のような重複を避けるため、見出しが `PR #` で始まっていればそのまま流用する）。

### finding ブロック（`## ` 見出しで始まる）

    ## 歯車機械の要求トルク率に上限なしの電力倍率を流すが供給側は1でクランプする

    ```yaml
    slug: gear-torque-rate
    category: design-decision
    severity: medium
    must_read: true
    summary: 需要だけ1.5倍に膨らみ、供給も速度も増えない。
    index_label: 歯車機械に倍率を効かせるか（案A/案Bは排他）
    files: [moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/VanillaGearMachineComponent.cs:40]
    options:
      - 供給側に requestRate を通し、要求と供給を同じ式に揃える
      - Processing ? 1f : idleRate へ戻す
    ```

    ```code-card
     36|        private void UpdateTorqueRequestRate()
     37|        {
    -38|            _gearEnergyTransformer.SetTorqueRequestRate(idleRate);
    +38|            // 表示の分母・加工速度と同じ導出をそのまま歯車網への要求へ反映する
    *+40|            _gearEnergyTransformer.SetTorqueRequestRate(...);
     41|        }
    ```

    **PR側の主張:** 歯車機械の要求トルクにもモジュール倍率を反映する `[agent前提]`

    **独立レビューの実測:** `GearConsumptionCalculator.cs:41-52` の required は rate を見ない。

YAMLキー:

| キー | 必須 | 意味 |
|---|---|---|
| `slug` | 必須 | 本文からの相互参照に使う安定キー。文書内で一意 |
| `category` | 必須 | `critical` / `design-decision` / `novelty` のいずれか |
| `severity` | 必須 | `critical` / `high` / `medium` / `low` のいずれか |
| `must_read` | `design-decision` のみ必須 | true なら「必読の設計判断」ゾーンへ入る |
| `summary` | 必須 | 一言サマリ1行（`p.summary-line` になる） |
| `index_label` | 任意 | 「あなたが判断すること」の短ラベル。省略時は `summary` |
| `files` | 必須 | `path:line` の配列。1件目が主。**id採番のキー** |
| `options` | 非suppressedで必須 | 案の要約の配列。**先頭が推奨**。コンバータがカード本文へ案A/案B…として描き、先頭へ推奨マークを付ける |
| `suppressed` | 任意（既定 false） | true なら suppressed ゾーンへ |
| `suppress_reason` | `suppressed: true` のとき必須 | 免責の出所要約 |
| `recommendation` | **書けない** | findings.json の `recommendation` は `options` 先頭から自動で入る。書くとエラーになる |
| `label` | 任意 | `data-label`。省略時は `{title}のカード（実コード抜粋つき）` |

**`recommended` を書く欄は存在しない**（R4）。コンバータが先頭optionに付ける。

**案の正本は `options:` 1箇所である。** カード本文に `代替案` を書くとコンバータがエラーで落とす
（同じ案が2箇所に出て片方だけ古くなる事故を防ぐため）。案の並び順がそのまま案A/案B…のキーになり、
先頭が推奨として描かれる。案どうしが排他である等の関係は `summary` か `index_label` へ書く。

**`[F:slug]` 参照が解決されるのはfindingの自由本文（`**PR側の主張:**` 等の段落）・
`summary` フィールド・`suppress_reason` フィールド（`suppressed: true` のカードのみ表示）・
`# 注記` の各ゾーン導入文・`# 判断台帳`・`# 折りたたみ参考` のMarkdown本文**（`render.py` が
`summary`・`suppress_reason` を含めこれらすべてに `inline_html()` を適用するため）。
`index_label` と `options` の各要素の2フィールドのみ、[F:slug]を
解決しない生文字列としてそのまま出力される（`render.py` はこの2フィールドに `escape()` のみを
適用する）。これらの
フィールドに `[F:slug]` を書いても素通しの `[F:slug]` という文字列がそのまま画面に出るので、
**必ずID非依存の文言で書く**（例: `index_label: 歯車機械に倍率を効かせるか` はOK、
`index_label: [F:gear-torque-rate] の是非` はNG）。既知の残課題としてbdに積み済み。

### コードフェンス `code-card`

各行は `[フラグ]<行番号>|<コード>`。フラグは次の3種で、`*` は `+` / `-` と併用できる。

| フラグ | 意味 | 行番号 | 見た目 |
|---|---|---|---|
| `+` | 追加行 | 新ファイルの行番号 | 緑帯・`+` グリフ |
| `-` | 削除行 | **旧ファイルの行番号** | 赤帯・`-` グリフ |
| （無し） | 文脈行 | 新ファイルの行番号 | 帯なし・空白グリフ |
| `*` | 注目行（他フラグと併用） | — | 左端のアンバー縦バー＋太字 |

`+` と `-` を同じ行に付けることはできない（エラー）。`|` の最初の1個だけが区切り。
コードは**エスケープせず生のまま**書く（コンバータがエスケープする）。

**置換を扱うカードには必ず `-` 行を書く。** コンバータは同じ `$RUNDIR` の `patch.diff` を読み、
`+` 行が属するhunkが行を削除しているのにカードへ `-<旧行番号>|<コード>` が無い場合、**エラーで非0終了する**。
「変更前が見えないと何を何に変えたか読めない」ためで、規約ではなく機械が担保する。

構文着色の言語は **`files` 先頭の拡張子** から自動で決まる（`cs`→csharp、`ts`/`tsx`→typescript、
`css`→css、`json`/`asmdef`→json、`yml`/`yaml`→yaml、`md`→markdown、未知拡張子は無着色）。
フェンスに言語を書く欄は無い。1カードには**単一ファイルの抜粋だけ**を入れること
（複数言語を混ぜると後半が誤着色される）。

### 相互参照

本文中で他の finding を指すときは `[F:gear-torque-rate]` と書く。コンバータが `<a href="#f03">F03</a>` へ解決する。未定義 slug はエラー。解決範囲は前節を参照（`index_label`/`options` の2フィールドでは解決されない）。

### 予約見出し（`# ` 見出し・すべて必須）

- `# 注記` — 直下に `## must-read` / `## other-rulings` / `## suppressed` / `## new-shape` / `## criticals` の5つ（各ゾーンの導入段落。0件ゾーンでは「該当なし（0件）。…」を書く）
- `# 判断台帳` — 自由Markdown（`<section id="ledger">` の中身）
- `# 折りたたみ参考` — 直下の `## ` 見出しがそれぞれ `<details><summary>` になる

### 対応する記法（これ以外はエラー）

段落 / `- ` 箇条書き / `### ` 見出し（h3） / コードフェンス（`code-card` と無印） / `**強調**` / `` `コード` `` / `[F:slug]` 参照。生のHTMLタグは書かない。

### CSSは生成側で足す必要はない

テンプレート（`assets/digest-template.html`）の `<style>` には、カードの枠線・
左色帯・`summary-line`・`details` 用のCSSが最初から入っている。旧フローでAIが
digest.html生成のたびに手でCSSを足していた分はテンプレ側へ移した。
digest.mdはCSSを一切気にせず内容だけを書けばよい。

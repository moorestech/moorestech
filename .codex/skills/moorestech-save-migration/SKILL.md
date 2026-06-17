---
name: moorestech-save-migration
description: moorestech のセーブ(JSON)を、コード変更で変わった新シリアライズ形式へ安全に移行する。揮発int→GUID解決、MessagePack/flat→JSON変換、実ロード検証まで行う。Use When — 「セーブが旧形式でロードできない」「ブランチでセーブ形式が変わった」「save_N.json をマイグレーションして」「ロード時にNRE/JSONパースエラーが出る」「保存周りを変えたから既存セーブを直して」と言われた場合。
---

# moorestech Save Migration

## 概要
moorestech は開発フェーズのため、コード変更でセーブのシリアライズ形式を頻繁に破壊的変更する（AGENTS方針: 旧セーブ互換不要）。本スキルは、形式変更で読めなくなった既存セーブファイルを新形式へ一括移行する手順。**コード側に互換コードを足すのではなく、セーブファイルを変換する**方針。

セーブ実体は `~/Library/Application Support/moorestech/saves/save_N.json`（mac、`GameSystemPaths.SaveFileDirectory`）。

## 前提条件
- Unity エディタが起動中で `uloop` が通ること（`uloop execute-dynamic-code --project-path ./moorestech_client`）。
- 変換対象セーブを作成したのと**同じ mod**（通常 `../../moorestech_master/server_v8`、`ServerDirectory.GetDirectory()` が返す）。揮発 int id はこの mod のロード順で決まるため、別 mod だと id がズレて別アイテムに化ける。
- Python3（標準ライブラリのみ。base64/struct/json）。
- 変換前後の比較用に git の merge-base（ブランチ分岐点）が分かること。

## 手順

### Step 1: 全形式変更を diff で「網羅列挙」する（最重要・最初にやる）
**クラッシュを1つずつ潰す方法は禁止。** ロードエラーは1ブロックで止まるので、直しても次が出るだけ。最初に変更された全セーブ形式を列挙する。

```bash
mb=$(git merge-base HEAD origin/master)
git diff --name-only $mb..HEAD -- '***.cs' | while read f; do
  git diff $mb..HEAD -- "$f" | grep -qiE "GetSaveState|LoadRailDirection|DeserializeObject|MessagePackSerializer|StateDetail|SaveJsonObject|componentStates|fluidId|itemId" && echo "$f"
done
```
ヒットした各ファイルで「旧形式 → 新形式」を確定する。旧形式は **merge-base のコード**（`git show $mb:path`）と**実セーブの中身**の両方で、新形式は HEAD のセーブオブジェクトクラスで確認する。

### Step 2: セーブ内の影響ブロック/状態を棚卸し
対象セーブを Python で読み、各 `world[].state{}` のキー別件数と、`trainUnits` を集計。どの形式が何件あるか把握する（0件なら無視してよい）。旧形式の典型マーカー: 状態値内の `\"fluidId\":` `\"FluidId\":` `\"itemId\":`、base64文字列（`k`等で始まりJSONでない）。

### Step 3: 揮発 id → GUID マップを「独立マスタ」から取得（Unity）
**グローバル `MasterHolder` を使うな。** 実行中エディタには別の（テスト用）マスタが載っていることがあり、その場合 `ExistItemId(58)` が false を返し解決できない。`ServerDirectory.GetDirectory()` から v8 マスタを**独立にロード**して引く（グローバルに触れない）。`references/dump_id_maps.cs` を `uloop` で実行し、全 item/fluid の id→GUID と `DefaultContainerTypeConst` 定数を取得して `/tmp/id_maps.json` に保存する。

### Step 4: 各新形式を実クラスから確認
変換先は**ゲームの実シリアライザ出力を正確に模倣**する。新セーブオブジェクトクラスを Read し、`[JsonProperty]` のキー名を厳密に合わせる（順序は不問、キー名は厳密）。代表例:
- `FluidContainerSaveJsonObject` → `{"fluidGuid":"<g>","amount":<double>}`（capacity は持たない＝ロード時に master から取得）
- `ItemStackSaveJsonObject` → `{"itemGuid":"<g>","count":<int>}`（空は Guid.Empty / count 0）
- 揮発 id 0（空）→ GUID は `00000000-0000-0000-0000-000000000000`

### Step 5: Python で実変換（backup → 変換 → 安全スキャン → 書き戻し）
`references/migrate_save_template.py` をベースに、Step 1 で列挙した形式ごとの変換関数を実装。必ず:
1. 変換前にタイムスタンプ付きバックアップディレクトリへコピー。
2. 解決できない id があれば**中断**（`ExistItemId` 相当: マップに無ければ abort）。mod ズレの早期検出。
3. **安全スキャン**: 変換後、全 `world[].state` 値が valid JSON か検査。非JSONが残れば未対応の base64/MessagePack 形式 → Step 1 の見落とし。
4. 書き戻しは全体 re-dump（compact JSON）で可。C# は型でパースするため float 整形差は無害。

### Step 6: 実ロードで検証（必須・「変換成功」≠「ロード可能」）
**デシリアライズ単体の確認では不十分。** ゲーム起動と同じ経路でフルロードする。`references/load_test.cs` を `uloop` で実行（`MoorestechServerDIContainerGenerator.Create` → `IWorldSaveDataLoader.LoadOrInitialize()`）。`LOAD OK | blocks=N` が出れば成功。失敗時は例外メッセージで次の未対応形式が分かる → Step 1 へ戻り列挙を補強。
さらに重要データ（列車インベントリ等）を `references/verify_loaded.cs` で実値確認し、サイレントなデータ欠損が無いか見る。

### Step 7: 完了後
ユーザーがゲームを起動してロードすると、以降は**純正の新形式で再セーブ**されるので移行は一度きり。逆に、検証ロード後にゲームが再セーブすると切り詰め等が確定するので注意（下記 Gotchas）。検証ロードはエディタの `ServerContext` にワールドを載せるため、**検証後は Unity 再起動を推奨**。

## Gotchas
- **クラッシュ駆動で直すな。** ロードは最初の失敗ブロックで止まる。Step 1 の diff 網羅列挙を先にやらないと、FuelGearGenerator を直したら次は Rail、と無限に出る（実際にそうなった）。
- **「移行した」と言う前に必ず実ロード（Step 6）。** デシリアライズ単体テストだけで「完了」と報告するのは誤り。形式は他にも変わっている可能性がある。
- **グローバル MasterHolder ≠ 対象 mod。** `loaded=false`（既ロード）で `missing:[58,79]`（解決不能）が出たら、別マスタが載っている。独立マスタ構築（Step 3）が必須。
- **揮発 int は mod ロード順依存。** save 作成時と同じ mod でないと id が別アイテムに化ける。マップに無い id があれば中断する（黙って続行しない）。
- **`uloop execute-dynamic-code` は `System.IO` 全面禁止**（File/Directory/**Path も**）。ファイル入出力は Python/bash 側で行う。スニペット内のパス連結は `Path.Combine` でなく文字列連結（`dir.EndsWith("/") ? dir+"mods" : dir+"/mods"`）。
- **C# の char リテラル `'/'` はシェルの single quote と衝突**して壊れる。スニペットは一時 `.cs` ファイルに書き、`--code "$(cat file.cs)"` で渡す（ダブルクォート内コマンド置換は内容を再解釈しない）。大きな入力は base64 で埋め込む。
- **base64-MessagePack 状態がある**（例 RailComponentStateDetail = `kZP...`）。`json.loads` が失敗する state は MessagePack。`[[x,y,z]]` 等を手デコードして JSON 化する。安全スキャン（Step 5-3）で取りこぼしを検出。
- **コンテナのスロット数 > master.InventorySlots だとロード時に切り詰められデータ欠損**（旧 MessagePack は切り詰めず復元していた＝ブランチの新挙動）。変換ファイル自体は全スロット保持できるが、ロードで落ちる。事前検出してユーザーに警告し、master のスロット数修正で回避可能か確認する。
- **マスタ参照値（capacity/スロット数）は新形式に含めない**。新シリアライザがそれらを出力しないので、模倣する変換も出力しない。ロード時に master から解決される。
- **id 0（空）の GUID は `Guid.Empty`**（`GetItemGuid/GetFluidGuid(Empty*) == Guid.Empty`）。マップには 0 が無いので明示的に空 GUID を割り当てる。

## Available scripts (references/)
呼び出し時に Read して用途に合わせ調整する（対象 save 番号・形式関数は都度書き換え）。
- `references/dump_id_maps.cs` — 独立 v8 マスタから item/fluid の id→GUID 全マップ＋定数を JSON で返す（Step 3）。`uloop execute-dynamic-code --project-path ./moorestech_client --code "$(cat ...)"`。
- `references/migrate_save_template.py` — backup→形式別変換→安全スキャン→書き戻しの雛形（Step 5）。形式ごとの `t_*` 関数を実装して使う。
- `references/load_test.cs` — DI フルロードで `LOAD OK | blocks=N` を確認（Step 6）。
- `references/verify_loaded.cs` — ロード後の列車インベントリ等を実値ダンプし欠損検査（Step 6）。

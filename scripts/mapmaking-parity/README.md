# mapmaking-parity 抽出スクリプト

MapMaking プロジェクト（`TmpUnityPjt/MapMaking`）のバイオームプリセットから、樹種・岩・小物のインベントリと
`treePlacement`・`objectConfig` 設定を抽出し `species-inventory.json` を生成する。この JSON は後続タスク
（map.json 生成・ラッパープレハブ生成・generation.json 同期）の唯一の入力。

後続スクリプト: `gen_map_master.py`（map.json へ mapObject 追記）、`gen_generation_treeplacement.py`
（treePlacement 同期）、`gen_generation_objectconfig.py`（objectConfig 同期＋generateObject 有効化）。
ラッパープレハブは Unity メニュー `moorestech/MapObjectWrapper/Generate All`。

`terrainSurroundEffectType` は kind と `bareGround` から機械決定する。移植元は objectConfig 配置のうち
名前に Boulder/Cliff を含む岩だけ裸地化するため、それ以外の岩・小物は `rockNoBareGround`（距離場のみ）になる。

This directory extracts the tree/rock inventory and `treePlacement` settings from the MapMaking biome
presets into `species-inventory.json`, the sole input for the follow-up map.json, wrapper prefab, and
generation.json work.

## 依存

`pyyaml` のみ。未導入の python で実行すると劣化せず即座に終了する。

```bash
python3 -m venv .venv
.venv/bin/pip install pyyaml
```

## 実行

リポジトリルートから、上記 venv の python で実行する。

```bash
.venv/bin/python scripts/mapmaking-parity/extract_mapmaking_species.py
```

出力先は常に `scripts/mapmaking-parity/species-inventory.json`（引数なし・固定パス）。

## 再実行しても出力が変わらないことの確認

抽出は入力プリセットに対して決定的なので、コミット済みの JSON と一致するはず。

```bash
.venv/bin/python scripts/mapmaking-parity/extract_mapmaking_species.py
git diff --exit-code scripts/mapmaking-parity/species-inventory.json
```

`git diff --exit-code` が終了コード 0（差分なし）であれば、MapMaking 側のプリセットも
スキーマも動いていない。差分が出たら入力が変わった証拠なので、内容を確認してからコミットする。

## fail-fast の方針

想定外の入力を黙って落とさず、必ず例外で止める。主な検算は以下。

- `REMOVED_UNITY_FIELDS` … 削除済み扱いのフィールドが現行スキーマに復活していたら例外
- `STALE_MISSING_FIELDS` … 未再保存プリセット（Mesa）で既定値補完を許すフィールドの宣言。
  宣言外のフィールドが欠けていれば `KeyError`、宣言したのに実際は欠けていなければ不一致として例外
- AnimationCurve … Unity 側のキー集合・スキーマ側の keyframe キー集合の双方を照合し、
  未知キー・欠落キー・スキーマ変化のいずれでも例外
- `SPECIES_ONLY_BIOMES` … 樹種のみ抽出するプリセットに有効プロトタイプが現れたら例外

つまり MapMaking 側かスキーマ側が動いたときは、静かに古い出力を出し続けるのではなく落ちる。

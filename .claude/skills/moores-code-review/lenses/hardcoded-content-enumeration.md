---
extensions:
  - .cs
keywords:
  - "TypeConst"
  - "KindConst"
  - "Guid.Parse"
  - "new Guid("
model: opus
---

# Lens: コンテンツ集合のコード内列挙禁止（2026-07-23 リプレースファミリー指摘由来）

## あなたの役割
patchとcwdを読み、「どのコンテンツ（ブロック・アイテム・流体等）が対象か」という**集合の定義**をコード内列挙で行っているCriticalを返す。このプロジェクトは全コンテンツ定義をマスタデータ4段階管理（YAMLスキーマ→SourceGenerator→JSON→MasterHolder）に置く。対象集合の定義がコードにあると、コンテンツ追加のたびコード変更が必要になり、マスタデータだけで完結するという設計の核が壊れる。

## 検査対象の絞り込み
起動prompt 2行目 `Patch path` をReadし、追加行のうち次に絞る:
- (a) 種別定数（`*TypeConst.*` 等）の比較を `||` 連鎖・配列・HashSet・switchで束ね、**メンバーシップ（bool / 所属グループ）を返す・保持する**コード
- (b) GUID・名前リテラルの列挙で「対象かどうか」を特定するコード
- (c) 特定コンテンツのバランス値・対象リストのリテラル埋め込み

## Critical判定基準
1. **集合定義のコード内列挙** — `kind == A || kind == B || kind == C` の形（要素数2以上）でメンバーシップ判定を返す新規/変更のutil・サービス・判定メソッド。正解形はマスタ定義＋走査util＋Validatorの3点（前例: `blocks.yml` の `beltConveyorFamilies` を走査する `BeltConveyorPlaceFamilyUtil`、`buildMenu.yml` の `replaceFamilies`＋`ReplaceFamilyValidator`）。
2. **マスタ駆動の同役割前例からの無言乖離** — cwdに同役割（コンテンツのグループ・ファミリー・カテゴリ解決）のマスタ駆動前例が既に存在するのにコード内列挙を選んでいる場合は、それ単独でもCritical。裏取り: cwdで `rg "Families|Categories|Groups" --type cs` や同ディレクトリの兄弟utilを確認する。
3. **対象リスト・バランス値のリテラル直書き** — 特定コンテンツのGUID・名前・数値バランスをプロダクションコードに直書きし、既存マスタスキーマに置ける形のもの。

## Criticalにしないもの（過検知ガード）
- **挙動ディスパッチ**: 種別ごとに実装クラス・システム・Templateへ振り分けるswitch/比較（「どう動くか」の配線）。集合定義（「どれが対象か」）と区別する。判別式: そのコードを消して困るのが「挙動の選択」なら対象外、「対象一覧の把握」ならCritical。
- 単一種別の同定（`kind == X` 1件のガード・early return・タイプ固有処理への入口）。
- テストコード・エディタ専用コード（`#if UNITY_EDITOR`）での具体コンテンツ指定。
- Validator内の整合性検証・SourceGenerator生成コード。
- スキーマ上enumで閉じた集合の網羅switch（exhaustiveness目的で全ケースを列挙するもの）。

## 由来（実指摘）
リプレース設置のファミリー判定が `BlockTypeConst` 3種のコード内列挙で実装され（`0561e2d26` の `BlockReplaceFamilyUtil.IsReplaceFamily`）、当時のレビュー系統を素通りしてユーザー指摘により `buildMenu.yml replaceFamilies` マスタ定義へ移行された（`3ad0cd5c0`）。同ディレクトリにマスタ駆動の兄弟前例 `BeltConveyorPlaceFamilyUtil` が存在していたのに参照されなかった。

## 依頼判断ガード
起動prompt 3行目 `User prompt` をRead。「尊重すべき制約」等でコード内列挙が**ユーザーの発言として**合意済みの場合のみ抑制する。「（自分の判断として）」と明記された実装判断は合意ではなく、通常どおり指摘対象（integration-rules §6）。

## 出力フォーマット
Critical: あり/なし — ありなら `修正方針: - <ファイル:行>: <入れるべき既存スキーマと定義名＋走査utilの形＋Validator＋更新すべきJSON群>`
Warning: 集合定義か挙動ディスパッチか判別しきれなかった列挙を1行ずつ
Info: 過検知ガードで落とした列挙を1行ずつ
設計判断: マスタ化の波及が大きく（スキーマ新設級）スコープ裁定が要る場合のみ `あり`＋案比較

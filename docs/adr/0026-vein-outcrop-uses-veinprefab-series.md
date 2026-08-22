# 鉱脈露頭のビジュアルをVeinPrefab_*シリーズへ切り替える

## Context

自動生成される鉱脈の露頭は、`OutcropGameObjectDatastore.ResolveOutcropPrefab` が `mapVeins` マスタの
`outcropAddressablePath` を `AddressableLoader.LoadDefault<GameObject>` に渡して解決している。
現在 v8 マスタが指しているのは `AddressableResources/Environment/Vein/Item/{Stone,Copper,Iron,Clay,Coal,Bronze,Tree,Tungsten}.prefab`
で、これらは板状メッシュ（`OutcropMesh` を `scale (3, 0.28, 3)`）に `OutcropGameObject` と `BoxCollider` を直接持たせた
仮ビジュアルである。

一方 `0c55254c0` / `f6a1eec5f` で `VeinPrefabBase.prefab`（PersonalAssets の PureNature Rock08/Rock11 由来。
子に MeshCollider を持つ）とその Variant `VeinPrefab_{Bronz,Clay,Coal,Copper,Iron,Stone,Tree}` が追加された。
Variant はマテリアルのみを差し替える構成で、`OutcropGameObject` は持たない。

`OutcropGameObjectDatastore.InstantiateOutcrop` は `GetComponent<OutcropGameObject>()` が null なら `AddComponent` し、
`OutcropGameObject.Initialize` は子の全 Collider に `OutcropRayTarget` を後付けする。
よって**プレハブ側にスクリプトを仕込む必要はなく、切り替えは master データの差し替えだけで成立する**。

## Decision

v8 マスタ（`../moorestech_master/server_v8/.../map.json`）の `outcropAddressablePath` を、Item 系鉱脈について
`Vanilla/Environment/Vein/Item/VeinPrefab_*` へ切り替える。Fluid 系（Water/Oil）は対象外。

- **Tungsten だけは旧 `Vanilla/Environment/Vein/Item/Tungsten` を据え置く。** 対応する Variant が存在せず、
  タングステン用マテリアルも未作成のため
- **`VeinPrefab_Bronz` を `VeinPrefab_Bronze` へリネーム**し、マスタはリネーム後の名前を指す
- **旧プレハブは Tungsten.prefab のみ残し、他7種（Stone/Copper/Iron/Clay/Coal/Bronze/Tree）を削除**する。
  合わせてテストマスタ2件（`Client.Tests/.../EditModeInPlayingTestMod/master/map.json`、
  `Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/map.json`）の参照も新シリーズへ更新する
- **`VeinPrefab_Stone` にマテリアル上書きが無い（= `VeinPrefabBase` の素の岩肌）のは意図どおり**とし、本件では触らない

出所: ユーザー裁定 2026-08-21 原文「自動生成されるveinのオブジェクトを
/Users/katsumi/moorestech/moorestech_client/Assets/AddressableResources/Environment/Vein/Item/VeinPrefab　〜〜シリーズにしたい」
→ 選択「Tungstenだけ旧プレハブ据え置き」「意図通り（石＝ベースの岩肌）」「テストmasterも新シリーズへ更新し旧を削除」
「VeinPrefab_Bronze へリネーム」

## Considered Options

- **Tungsten（採択）**: 旧 `Tungsten.prefab` 据え置き。旧シリーズ全廃はできなくなるが、未作成マテリアルの色を
  agent が独断で決めずに済む
- **Tungsten（棄却）**: `VeinPrefab_Tungsten` を新規作成。タングステン用マテリアルの指定が別途必要になる
- **Tungsten（棄却）**: 暫定で `VeinPrefab_Stone` を流用。Stone と見分けがつかなくなる
- **旧プレハブ（棄却）**: 残置し v8 マスタだけ切り替える。テストは PersonalAssets 非依存のまま保てるが、
  参照先が新旧に割れたまま残る
- **旧プレハブ（棄却）**: テストマスタのみ更新しファイルは残す
- **Bronz 綴り（棄却）**: `VeinPrefab_Bronz` のまま使う。綴り不統一が恒久的に残る
- **Stone マテリアル（棄却）**: 他6種と同じく7スロットへ石用マテリアルを割り当てる
- **Stone マテリアル（棄却）**: 漏れと認めたうえで別タスクへ送る

出所: ユーザー裁定 2026-08-21（AskUserQuestion 4問の選択）

## Consequences

- 露頭の**見た目**が PersonalAssets（`moorestech-client-private` の PureNature）依存になる。
  ただし `VeinPrefabBase.prefab:3-19` はルート GameObject・Transform・LODGroup を main repo 内に自前で持ち、
  PersonalAssets から来るのは子の Rock08/Rock11 PrefabInstance だけである。したがって PersonalAssets の無い
  環境でもプレハブのロードと `Instantiate` は成功し、子の岩が missing prefab になるだけで露頭オブジェクト自体は立つ。
  個数と座標しか見ない `MapVeinOutcropAndRangeViewTest` は CI で通り続ける
- PersonalAssets の無い環境では岩メッシュが出ないため MeshCollider も存在せず、`OutcropGameObject.Initialize` が
  `OutcropRayTarget` を1つも付けない。露頭への手掘りレイを実際に飛ばす検証は本体 worktree でのみ有効になる
- 露頭の当たり判定が `BoxCollider`（板状 3×0.28×3）から `MeshCollider`（岩メッシュ）へ変わる。
  鉱脈AABBは点中心の1辺3セル固定（[ADR-0023](0023-vein-aabb-is-per-point-fixed-2x2x2.md)）なので、
  岩メッシュの実寸が AABB に対して過大／過小でないかは実プレイでの目視確認が要る
- Tungsten だけ板状の旧ビジュアルが残るため、見た目が1種だけ浮く

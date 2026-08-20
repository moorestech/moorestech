"""
MapMakingのtreePlacementプロトタイプを、treePlacementConfigスキーマ準拠のdictへ変換する。
Converts a MapMaking treePlacement prototype into a dict conforming to the treePlacementConfig schema.
"""

from __future__ import annotations

import schema_spec
from unity_asset_yaml import is_null_reference, reference_guid

# スキーマキーとUnityフィールド名が異なる箇所だけを明示する
# Only the places where the schema key and the Unity field name diverge
UNITY_FIELD_OVERRIDES = {
    "mapObjects": "prefabs",
    "surroundLayerAddressablePath": "surroundLayer",
    "texturePngPath": "texture",
}

# Unity参照からアドレッサブルパス文字列へ置換されたキー（参照は未設定であることを要求する）
# Keys replaced by an addressable path string; the underlying Unity reference must be unset
REFERENCE_TO_PATH_KEYS = frozenset({"surroundLayerAddressablePath", "texturePngPath"})

# プレハブ参照配列を {mapObjectGuid} 配列へ写すスキーマキー（treePlacement / objectConfig）
# Schema keys whose prefab reference arrays become {mapObjectGuid} arrays (treePlacement / objectConfig)
GUID_ARRAY_KEYS = frozenset({"mapObjects", "primary", "prefabs"})

# UnityのAnimationCurveラッパが持つキー全集合。ここに無いキーは未知の情報として弾く
# Every key Unity's AnimationCurve wrapper carries; anything else is unknown information and rejected
ANIMATION_CURVE_KEYS = frozenset({
    "serializedVersion", "m_Curve", "m_PreInfinity", "m_PostInfinity", "m_RotationOrder",
})

# スキーマに写し先が無いラッパキー（wrap mode等）。キーフレームが空なら挙動に効かないので黙認できる
# Wrapper keys with no schema counterpart (wrap modes); inert only while the curve holds no keyframe
CURVE_KEYS_WITHOUT_SCHEMA_COUNTERPART = ANIMATION_CURVE_KEYS - {"m_Curve", "serializedVersion"}

# スキーマ側のkeyframe定義。Unityのキーフレームから写せる集合と一致することを毎回検算する
# The schema keyframe definition, re-verified each call against what a Unity keyframe can supply
KEYFRAME_SCHEMA_KEYS = frozenset({"time", "value", "inTangent", "outTangent"})


class PrototypeConverter:
    """1プリセット分の変換器。旧フィールドの扱いはプリセット単位で明示的に許可する。
    Converter for one preset; legacy field handling is opted into per preset."""

    def __init__(self, map_object_guid_by_prefab_guid: dict, stale_serialization: bool,
                 removed_unity_fields: frozenset, missing_unity_fields: frozenset,
                 null_surround_layer_address: str):
        self._map_object_guid_by_prefab_guid = map_object_guid_by_prefab_guid
        # surroundLayer未設定時に書くアドレス。treePlacementは空文字（重み0で不使用）、objectConfigは移植元のMudフォールバック
        # The address written for an unset surroundLayer: "" for treePlacement (unused at weight 0), the source's Mud fallback for objectConfig
        self._null_surround_layer_address = null_surround_layer_address
        # 現行C#クラスへの改修後にプリセットが再保存されていない場合のみTrue
        # True only when the preset has not been re-saved since the current C# class landed
        self._stale_serialization = stale_serialization
        self._removed_unity_fields = removed_unity_fields
        self._missing_unity_fields = missing_unity_fields
        self._missing_fields_seen: set[str] = set()

    def convert(self, node: schema_spec.SchemaNode, unity_value: dict, location: str) -> dict:
        """プロトタイプ1件をスキーマ順のdictへ写し取る。
        Transcribes one prototype into a dict ordered by the schema."""
        if not isinstance(unity_value, dict):
            raise ValueError(f"{location}: オブジェクトを期待したが {type(unity_value).__name__}")

        converted: dict = {}
        consumed: set[str] = set()
        for key, child in node.properties:
            unity_field = UNITY_FIELD_OVERRIDES.get(key, key)
            if unity_field not in unity_value:
                converted[key] = self._missing_field_value(key, child, f"{location}.{key}")
                continue

            consumed.add(unity_field)
            converted[key] = self._convert_value(key, child, unity_value[unity_field],
                                                 f"{location}.{key}")

        self._reject_unmapped(set(unity_value) - consumed, location)
        return converted

    def _missing_field_value(self, key: str, node: schema_spec.SchemaNode, location: str):
        """宣言済みの欠落フィールドに限り、Unityが与えるのと同じ既定値で補う。
        Only for declared missing fields, supplies the same default Unity itself would apply."""
        if not self._stale_serialization or key not in self._missing_unity_fields:
            raise KeyError(f"{location}: MapMakingアセットに該当フィールドが無い")
        self._missing_fields_seen.add(key)
        return schema_spec.default_value(
            node, location, {"surroundLayerAddressablePath": self._null_surround_layer_address})

    def reject_missing_field_mismatch(self, location: str) -> None:
        """欠落を宣言したフィールドが実際に全て欠落していたことを検算する。
        Verifies that every field declared as missing was in fact missing."""
        if self._missing_fields_seen != set(self._missing_unity_fields):
            raise KeyError(f"{location}: 欠落フィールドの実測 {sorted(self._missing_fields_seen)} が "
                           f"宣言 {sorted(self._missing_unity_fields)} と一致しない")

    def _reject_unmapped(self, unmapped: set, location: str) -> None:
        surplus = sorted(unmapped - self._removed_unity_fields) if self._stale_serialization \
            else sorted(unmapped)
        if surplus:
            raise KeyError(f"{location}: スキーマに存在しないMapMakingフィールド {surplus}")

    def _convert_value(self, key: str, node: schema_spec.SchemaNode, unity_value, location: str):
        if key in REFERENCE_TO_PATH_KEYS:
            _reject_set_reference(unity_value, location)
            return self._null_surround_layer_address if key == "surroundLayerAddressablePath" else ""

        if node.kind == schema_spec.OBJECT:
            return self.convert(node, unity_value, location)

        if node.kind == schema_spec.ARRAY:
            return self._convert_array(key, node, unity_value, location)

        if node.kind == schema_spec.ENUM:
            return _convert_enum(node, unity_value, location)

        return _convert_scalar(node, unity_value, location)

    def _convert_array(self, key: str, node: schema_spec.SchemaNode, unity_value,
                       location: str) -> list:
        if key in GUID_ARRAY_KEYS:
            return [{"mapObjectGuid": self._map_object_guid(reference, f"{location}[{index}]")}
                    for index, reference in enumerate(unity_value)]

        if key == "curve":
            return _convert_animation_curve(node, unity_value, location)

        # 構造体配列（clusterEntries/secondaries/entries）は要素ごとに同じ規則で再帰変換する
        # Struct arrays (clusterEntries/secondaries/entries) recurse element by element under the same rules
        if node.item.kind == schema_spec.OBJECT:
            return [self.convert(node.item, item, f"{location}[{index}]")
                    for index, item in enumerate(unity_value)]

        raise NotImplementedError(f"{location}: 配列キー {key} の変換規則が未定義")

    def _map_object_guid(self, reference: dict, location: str) -> str:
        prefab_guid = reference_guid(reference, location)
        if prefab_guid not in self._map_object_guid_by_prefab_guid:
            raise KeyError(f"{location}: プレハブguid {prefab_guid} に対応する樹種が未登録")
        return self._map_object_guid_by_prefab_guid[prefab_guid]


def _convert_animation_curve(node: schema_spec.SchemaNode, unity_value: dict, location: str) -> list:
    """AnimationCurveをkeyframe配列へ写す。写し先の無い情報を持つカーブは変換せず弾く。
    Maps an AnimationCurve into a keyframe array, rejecting curves carrying untranslatable data."""
    unknown = sorted(set(unity_value) - ANIMATION_CURVE_KEYS)
    if unknown:
        raise KeyError(f"{location}: AnimationCurveに未知のキー {unknown}")

    missing = sorted(ANIMATION_CURVE_KEYS - set(unity_value))
    if missing:
        raise KeyError(f"{location}: AnimationCurveにキーが無い {missing}")

    schema_keys = {key for key, _ in node.item.properties}
    if schema_keys != KEYFRAME_SCHEMA_KEYS:
        raise KeyError(f"{location}: keyframeスキーマが想定と異なる {sorted(schema_keys)}")

    # wrap modeはスキーマに写せないため、キーフレームが付いて挙動に効き始めたら変換を止める
    # Wrap modes cannot be transcribed, so stop converting once keyframes make them take effect
    if unity_value["m_Curve"]:
        raise NotImplementedError(
            f"{location}: キーフレームを持つカーブは "
            f"{sorted(CURVE_KEYS_WITHOUT_SCHEMA_COUNTERPART)} の写し先が無く変換できない")
    return []


def _reject_set_reference(unity_value: dict, location: str) -> None:
    """参照が設定されたTerrainLayer/Textureはアドレッサブルパスへ機械変換できないので止める。
    A set TerrainLayer/Texture reference cannot be mechanically mapped to an addressable path, so stop."""
    if not is_null_reference(unity_value, location):
        raise ValueError(
            f"{location}: アドレッサブルパスへの置換対象だが参照が設定されている ({unity_value!r})")


def _convert_enum(node: schema_spec.SchemaNode, unity_value, location: str) -> str:
    if not isinstance(unity_value, int) or not 0 <= unity_value < len(node.options):
        raise ValueError(f"{location}: enum添字が範囲外 ({unity_value!r} / {node.options})")
    return node.options[unity_value]


def _convert_scalar(node: schema_spec.SchemaNode, unity_value, location: str):
    scalar_type = node.scalar_type
    if scalar_type == "vector2":
        if not isinstance(unity_value, dict) or set(unity_value) != {"x", "y"}:
            raise ValueError(f"{location}: Vector2として解釈できない ({unity_value!r})")
        return [unity_value["x"], unity_value["y"]]

    if scalar_type == "boolean":
        if unity_value not in (0, 1):
            raise ValueError(f"{location}: booleanは0/1のみ許容 ({unity_value!r})")
        return bool(unity_value)

    if scalar_type == "integer":
        if not isinstance(unity_value, int):
            raise ValueError(f"{location}: integerを期待したが {unity_value!r}")
        return unity_value

    if scalar_type == "number":
        if isinstance(unity_value, bool) or not isinstance(unity_value, (int, float)):
            raise ValueError(f"{location}: numberを期待したが {unity_value!r}")
        return unity_value

    raise NotImplementedError(f"{location}: スカラー型 {scalar_type} の変換規則が未定義")

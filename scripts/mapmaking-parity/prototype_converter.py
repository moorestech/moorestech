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


class PrototypeConverter:
    """1プリセット分の変換器。旧フィールドの扱いはプリセット単位で明示的に許可する。"""
    """Converter for one preset; legacy field handling is opted into per preset."""

    def __init__(self, map_object_guid_by_prefab_guid: dict, stale_serialization: bool,
                 removed_unity_fields: frozenset):
        self._map_object_guid_by_prefab_guid = map_object_guid_by_prefab_guid
        # 現行C#クラスへの改修後にプリセットが再保存されていない場合のみTrue
        # True only when the preset has not been re-saved since the current C# class landed
        self._stale_serialization = stale_serialization
        self._removed_unity_fields = removed_unity_fields

    def convert(self, node: schema_spec.SchemaNode, unity_value: dict, location: str) -> dict:
        """プロトタイプ1件をスキーマ順のdictへ写し取る。"""
        """Transcribes one prototype into a dict ordered by the schema."""
        if not isinstance(unity_value, dict):
            raise ValueError(f"{location}: オブジェクトを期待したが {type(unity_value).__name__}")

        converted: dict = {}
        consumed: set[str] = set()
        for key, child in node.properties:
            unity_field = UNITY_FIELD_OVERRIDES.get(key, key)
            if unity_field not in unity_value:
                converted[key] = self._missing_field_value(child, f"{location}.{key}")
                continue

            consumed.add(unity_field)
            converted[key] = self._convert_value(key, child, unity_value[unity_field],
                                                 f"{location}.{key}")

        self._reject_unmapped(set(unity_value) - consumed, location)
        return converted

    def _missing_field_value(self, node: schema_spec.SchemaNode, location: str):
        """未再保存プリセットに限り、Unityが与えるのと同じ既定値で補う。"""
        """Only for stale presets, supplies the same default Unity itself would apply."""
        if not self._stale_serialization:
            raise KeyError(f"{location}: MapMakingアセットに該当フィールドが無い")
        return schema_spec.default_value(node, location)

    def _reject_unmapped(self, unmapped: set, location: str) -> None:
        surplus = sorted(unmapped - self._removed_unity_fields) if self._stale_serialization \
            else sorted(unmapped)
        if surplus:
            raise KeyError(f"{location}: スキーマに存在しないMapMakingフィールド {surplus}")

    def _convert_value(self, key: str, node: schema_spec.SchemaNode, unity_value, location: str):
        if key in REFERENCE_TO_PATH_KEYS:
            return _convert_reference_to_path(unity_value, location)

        if node.kind == schema_spec.OBJECT:
            return self.convert(node, unity_value, location)

        if node.kind == schema_spec.ARRAY:
            return self._convert_array(key, unity_value, location)

        if node.kind == schema_spec.ENUM:
            return _convert_enum(node, unity_value, location)

        return _convert_scalar(node, unity_value, location)

    def _convert_array(self, key: str, unity_value, location: str) -> list:
        if key == "mapObjects":
            return [{"mapObjectGuid": self._map_object_guid(reference, f"{location}[{index}]")}
                    for index, reference in enumerate(unity_value)]

        if key == "curve":
            return _convert_animation_curve(unity_value, location)

        raise NotImplementedError(f"{location}: 配列キー {key} の変換規則が未定義")

    def _map_object_guid(self, reference: dict, location: str) -> str:
        prefab_guid = reference_guid(reference, location)
        if prefab_guid not in self._map_object_guid_by_prefab_guid:
            raise KeyError(f"{location}: プレハブguid {prefab_guid} に対応する樹種が未登録")
        return self._map_object_guid_by_prefab_guid[prefab_guid]


def _convert_animation_curve(unity_value: dict, location: str) -> list:
    """AnimationCurveのm_Curveをkeyframe配列へ写す。"""
    """Maps an AnimationCurve's m_Curve into a keyframe array."""
    if "m_Curve" not in unity_value:
        raise KeyError(f"{location}: AnimationCurveにm_Curveが無い")

    return [
        {
            "time": keyframe["time"],
            "value": keyframe["value"],
            "inTangent": keyframe["inSlope"],
            "outTangent": keyframe["outSlope"],
        }
        for keyframe in unity_value["m_Curve"]
    ]


def _convert_reference_to_path(unity_value: dict, location: str) -> str:
    if not is_null_reference(unity_value, location):
        raise ValueError(
            f"{location}: アドレッサブルパスへの置換対象だが参照が設定されている ({unity_value!r})")
    return ""


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

"""
VanillaSchemaのYAMLからキー構造を起こし、変換器が参照するスキーマノード木を組む。
Builds the schema node tree the converter walks, sourced from the VanillaSchema YAML itself.
"""

from __future__ import annotations

from pathlib import Path

from unity_asset_yaml import yaml

OBJECT = "object"
ARRAY = "array"
ENUM = "enum"
SCALAR = "scalar"


class SchemaNode:
    """スキーマ1要素。kindに応じてproperties/item/options/scalar_typeのいずれかを持つ。
    One schema element; holds properties, item, options, or scalar_type depending on kind."""

    def __init__(self, kind: str, *, properties=None, item=None, options=None, scalar_type=None,
                 declared_default=None):
        self.kind = kind
        self.properties = properties
        self.item = item
        self.options = options
        self.scalar_type = scalar_type
        self.declared_default = declared_default


def default_value(node: SchemaNode, location: str):
    """スキーマ既定値（＝MapMakingのC#フィールド初期値）を組み立てる。
    Builds the schema default, which mirrors the MapMaking C# field initializer."""
    if node.declared_default is not None:
        return node.declared_default
    if node.kind == OBJECT:
        return {key: default_value(child, f"{location}.{key}") for key, child in node.properties}
    raise ValueError(f"{location}: 既定値が定義されていない")


def load_schema(schema_dir: Path, schema_id: str) -> SchemaNode:
    """スキーマidを起点にrefを解決しながらノード木を読み込む。
    Loads the node tree from a schema id, resolving refs along the way."""
    return _SchemaLoader(schema_dir).load(schema_id)


class _SchemaLoader:
    def __init__(self, schema_dir: Path):
        self._schema_dir = schema_dir
        self._resolving: list[str] = []

    def load(self, schema_id: str) -> SchemaNode:
        if schema_id in self._resolving:
            raise ValueError(f"スキーマrefが循環している: {' -> '.join(self._resolving)} -> {schema_id}")

        schema_path = self._schema_dir / f"{schema_id}.yml"
        if not schema_path.exists():
            raise FileNotFoundError(f"スキーマが見つからない: {schema_path}")

        self._resolving.append(schema_id)
        node = self._build(yaml.safe_load(schema_path.read_text(encoding="utf-8")), schema_id)
        self._resolving.pop()
        return node

    def _build(self, definition: dict, location: str) -> SchemaNode:
        if "ref" in definition:
            return self.load(definition["ref"])

        schema_type = definition.get("type")
        if schema_type is None:
            raise ValueError(f"{location}: typeもrefも無い定義 {definition!r}")

        if schema_type == OBJECT:
            properties = [
                (entry["key"], self._build(entry, f"{location}.{entry['key']}"))
                for entry in definition["properties"]
            ]
            return SchemaNode(OBJECT, properties=properties,
                              declared_default=definition.get("default"))

        if schema_type == ARRAY:
            return SchemaNode(ARRAY, item=self._build(definition["items"], f"{location}[]"))

        if schema_type == ENUM:
            return SchemaNode(ENUM, options=list(definition["options"]),
                              declared_default=definition.get("default"))

        return SchemaNode(SCALAR, scalar_type=schema_type,
                          declared_default=definition.get("default"))

"""外部JSONの必須形を検査する。
Validate required shapes at the external JSON boundary.
"""


def required(value, key, kind, path, prefix):
    if key not in value or not isinstance(value[key], kind) or (kind is int and isinstance(value[key], bool)):
        raise ValueError(f'{path}: {prefix}{key}: {kind.__name__} が必要')
    return value[key]


def objects(values, path, field):
    for index, value in enumerate(values):
        if not isinstance(value, dict):
            raise ValueError(f'{path}: {field}[{index}]: object が必要')

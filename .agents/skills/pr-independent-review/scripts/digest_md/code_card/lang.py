# finding が指すファイルの拡張子から構文着色の言語を決める。書き忘れが起きない唯一の情報源
# Decide the highlight language from the finding's file extension: the one source that cannot be forgotten
from __future__ import annotations

# 拡張子 → highlight.js の言語名。未収録の拡張子は無着色にフォールバックする
# Extension to highlight.js language name; unlisted extensions fall back to no highlighting
LANGUAGE_BY_EXTENSION = {
    "cs": "csharp", "ts": "typescript", "tsx": "typescript", "js": "javascript",
    "jsx": "javascript", "css": "css", "scss": "scss", "json": "json",
    "asmdef": "json", "yml": "yaml", "yaml": "yaml", "md": "markdown",
    "html": "xml", "xml": "xml", "py": "python", "sh": "bash",
}


def language_of(files: list) -> str:
    # files先頭の `path:line` からパスを取り、拡張子を言語名へ写す
    # Take the path from the first `path:line` entry and map its extension to a language name
    path = files[0].split(":")[0]
    name = path.rsplit("/", 1)[-1]
    if "." not in name:
        return ""
    return LANGUAGE_BY_EXTENSION.get(name.rsplit(".", 1)[-1].lower(), "")

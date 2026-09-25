#!/usr/bin/env python3
"""Reject test code that reads private client assets or a sibling master checkout."""

import re
import subprocess
import sys
from pathlib import Path


TEST_ROOTS = (
    "moorestech_client/Assets/Scripts/Client.Tests/",
    "moorestech_server/Assets/Scripts/Tests/",
    "moorestech_server/Assets/Scripts/Tests.Module/",
    "moorestech_web/webui/e2e/",
)
FORBIDDEN = (
    (re.compile(r"\bPinnedMasterRepository\b"), "pinned master repository helper"),
    (re.compile(r"\bServerDirectory\s*\.\s*GetDirectory\s*\("), "sibling master data directory"),
    (re.compile(r"moorestech-client-private", re.IGNORECASE), "private client asset repository"),
    (re.compile(r"(?:\.\./)+moorestech_master(?:/|\b)"), "sibling master repository path"),
    (re.compile(r"Path\.Combine\s*\([^;\r\n]*[\"']\.\.[\"'][^;\r\n]*[\"']moorestech_master[\"']"), "constructed sibling master repository path"),
)
GUID = re.compile(r"^guid: ([0-9a-f]{32})$", re.MULTILINE)
ADDRESS = re.compile(r"m_GUID: ([0-9a-f]{32})\s+m_Address: ([^\r\n]+)")
SOURCE_PREFAB = re.compile(r"m_SourcePrefab: \{[^}]*guid: ([0-9a-f]{32})")
STRING_LITERAL = re.compile(r'"([^"\r\n]+)"')


def tracked_test_sources(root: Path) -> list[Path]:
    result = subprocess.run(
        ["git", "ls-files", "--cached", "--", *TEST_ROOTS],
        cwd=root,
        check=True,
        capture_output=True,
        text=True,
    )
    return [Path(line) for line in result.stdout.splitlines() if line.endswith((".cs", ".ts", ".tsx"))]


def tracked_assets(root: Path) -> tuple[dict[str, Path], dict[str, Path]]:
    result = subprocess.run(
        ["git", "ls-files", "--cached", "--", "moorestech_client/Assets/"],
        cwd=root,
        check=True,
        capture_output=True,
        text=True,
    )
    paths = [Path(line) for line in result.stdout.splitlines()]
    by_guid = {}
    for meta in (path for path in paths if path.suffix == ".meta"):
        match = GUID.search((root / meta).read_text(encoding="utf-8-sig", errors="replace"))
        if match:
            by_guid[match.group(1)] = Path(str(meta)[:-5])
    by_address = {}
    for group in (path for path in paths if "AddressableAssetsData/AssetGroups/" in str(path) and path.suffix == ".asset"):
        for guid, address in ADDRESS.findall((root / group).read_text(encoding="utf-8-sig", errors="replace")):
            if guid in by_guid:
                by_address[address] = by_guid[guid]
    return by_guid, by_address


def violations(root: Path) -> list[tuple[Path, int, str]]:
    found = []
    by_guid, by_address = tracked_assets(root)
    by_unity_path = {str(path.relative_to("moorestech_client")): path for path in by_guid.values() if path.suffix == ".prefab"}
    private_prefabs = {}

    def missing_source_prefab(asset: Path) -> bool:
        if asset.suffix != ".prefab":
            return False
        if asset in private_prefabs:
            return private_prefabs[asset]
        private_prefabs[asset] = False
        for guid in SOURCE_PREFAB.findall((root / asset).read_text(encoding="utf-8-sig", errors="replace")):
            if guid not in by_guid or missing_source_prefab(by_guid[guid]):
                private_prefabs[asset] = True
        return private_prefabs[asset]

    for source in tracked_test_sources(root):
        content = (root / source).read_text(encoding="utf-8-sig")
        for number, line in enumerate(content.splitlines(), 1):
            if line.lstrip().startswith(("//", "*", "///")):
                continue
            for pattern, description in FORBIDDEN:
                if pattern.search(line):
                    found.append((source, number, description))
            for literal in STRING_LITERAL.findall(line):
                asset = by_address.get(literal)
                if asset is not None and missing_source_prefab(asset):
                    found.append((source, number, f"Addressable prefab {literal} has an untracked source prefab"))
                asset = by_unity_path.get(literal)
                if asset is not None and missing_source_prefab(asset):
                    found.append((source, number, f"Prefab {asset} has an untracked source prefab"))
    return found


def main() -> int:
    root = Path(__file__).resolve().parents[2]
    found = violations(root)
    if not found:
        print("Test external dependency check passed: no private client or sibling master references.")
        return 0
    print("Tests must use tracked fixtures. External repository references were found:", file=sys.stderr)
    for source, line, description in found:
        print(f"{source}:{line}: {description}", file=sys.stderr)
        print(f"::error file={source},line={line}::Test depends on {description}; use a tracked fixture.")
    return 1


if __name__ == "__main__":
    raise SystemExit(main())

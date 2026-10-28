"""Task 32 one-shot static evidence; does not start Unity or alter working trees."""
import hashlib
import json
from pathlib import Path
import re
import subprocess

body = Path(__file__).resolve().parents[3]
master = Path("/Users/sakastudio/hermes-agent/data/worktrees/moorestech_master/tier9-integrated")
keys = {"inputTankCount", "outputTankCount", "innerTankCapacity", "fluidInventoryConnectors"}
checked_gear = 0
for root in (body, master):
    for name in subprocess.check_output(["git", "ls-files", "*.json"], cwd=root, text=True).splitlines():
        path = root / name
        if "blocks" not in path.name:
            continue
        document = json.loads(path.read_text())
        if not isinstance(document, dict) or not isinstance(document.get("data"), list):
            continue
        for block in document["data"]:
            if block.get("blockType") == "GearMachine":
                assert not keys.intersection(block["blockParam"]), (path, block.get("name"))
                checked_gear += 1
print(f"GearMachine JSON sweep: {checked_gear} blocks, no removed keys")
for name in ("VanillaSchema/blocks.yml", "mooresmaster/mooresmaster.SandBox/schema/blocks.yml"):
    text = (body / name).read_text()
    gear = re.search(r"(?ms)^( +)- when: GearMachine\n(.*?)(?=^\1- when:)", text).group(2)
    assert not keys.intersection(re.findall(r"^ +- key: (\w+)", gear, re.MULTILINE))
    assert "interfacePropertyConstants:" in gear
    for key in ("inputTankCount", "outputTankCount", "innerTankCapacity"):
        assert re.search(r"^ +" + key + r": 0$", gear, re.MULTILINE)
print("GearMachine schema constants: PASS")

files = subprocess.check_output(["git", "diff", "--name-only", "b0624f3549"], cwd=body, text=True).splitlines()
new_files = subprocess.check_output(["git", "diff", "--name-only", "--diff-filter=A", "b0624f3549"], cwd=body, text=True).splitlines()
code_count = 0
for name in files:
    path = body / name
    if path.suffix == ".cs":
        assert len(path.read_text().splitlines()) < 200, name
        code_count += 1
for directory in {Path(name).parent for name in new_files if name.endswith(".cs")}:
    assert len(list((body / directory).glob("*.cs"))) <= 10, directory
print(f"C# length and added-file directory limits: PASS ({code_count} files)")
patch = subprocess.check_output(["git", "diff", "b0624f3549", "--", "*.cs"], cwd=body, text=True)
added = "\n".join(line[1:] for line in patch.splitlines() if line.startswith("+") and not line.startswith("+++"))
assert not re.search(r"\bFunc<|\bpartial\s+(class|struct)", added)
for root in (body / "moorestech_client/Assets/Scripts", body / ".agents/skills/unity-playmode-recorded-playtest/scenarios"):
    assert all("ServerService<" not in path.read_text() for path in root.rglob("*.cs"))
assert "CurrentVersion = 8" in next((body / "moorestech_server/Assets/Scripts/Game.SaveLoad/Json/WorldVersions").rglob("WorldSaveAllInfo.cs")).read_text()
print("No added Func/partial, no ServerService, CurrentVersion=8: PASS")

# Check the named assembly graph, including the newly added dependency edges.
assemblies = {}
for path in (body / "moorestech_server/Assets/Scripts").rglob("*.asmdef"):
    doc = json.loads(path.read_text(encoding="utf-8-sig"))
    assemblies[doc["name"]] = doc.get("references", [])
for path in (body / "moorestech_client/Assets/Scripts").rglob("*.asmdef"):
    doc = json.loads(path.read_text(encoding="utf-8-sig"))
    assemblies[doc["name"]] = doc.get("references", [])
visited = set()
def visit(name, ancestors):
    assert name not in ancestors, f"assembly cycle: {ancestors + [name]}"
    if name in visited:
        return
    for target in assemblies.get(name, []):
        if target in assemblies:
            visit(target, ancestors + [name])
    visited.add(name)
visit("Game.World", [])
visit("Client.Playtest", [])
print("Assembly dependency graph: PASS")
for name in ("moorestech_client", "moorestech_server"):
    path = body / name / "Assets/Plugins/mooresmaster.Generator.dll"
    print(f"Generator DLL {name}: {hashlib.sha256(path.read_bytes()).hexdigest()}")

# A second application must preserve every tracked master JSON and localization byte.
tracked = subprocess.check_output(["git", "ls-files"], cwd=master, text=True).splitlines()
paths = [master / name for name in tracked if name.startswith("server_v8/") or name == "design/tier9-balance/guids.json"]
before = {str(path): hashlib.sha256(path.read_bytes()).hexdigest() for path in paths}
subprocess.run(["python3", "design/tier9-balance/apply/apply_spec.py", "--mod-dir", "server_v8/mods/moorestechAlphaMod_8", "--spec-dir", "design/tier9-balance/spec", "--ledger", "design/tier9-balance/guids.json"], cwd=master, check=True)
assert before == {str(path): hashlib.sha256(path.read_bytes()).hexdigest() for path in paths}
print(f"Master reapply byte idempotency: PASS ({len(paths)} tracked files)")

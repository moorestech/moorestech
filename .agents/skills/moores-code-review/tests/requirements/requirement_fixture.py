import argparse
import subprocess


def requirement_fixture(root):
    source = root / "source"
    source.mkdir()
    subprocess.run(["git", "init", "-q", str(source)], check=True)
    subprocess.run(["git", "-C", str(source), "config", "user.email", "test@example.com"], check=True)
    subprocess.run(["git", "-C", str(source), "config", "user.name", "Test"], check=True)
    (source / "tracked").write_text("base", encoding="utf-8")
    subprocess.run(["git", "-C", str(source), "add", "tracked"], check=True)
    subprocess.run(["git", "-C", str(source), "commit", "-qm", "initial"], check=True)
    context, patch = root / "context.md", root / "patch.diff"
    context.write_text("request", encoding="utf-8")
    patch.write_text("patch", encoding="utf-8")
    tooling = root / "tool" / "scripts" / "requirements"
    tooling.mkdir(parents=True)
    reference = tooling.parents[1] / "references" / "requirement-proof.md"
    reference.parent.mkdir()
    reference.write_text("procedure", encoding="utf-8")
    args = argparse.Namespace(repo_root=str(source), context=str(context), patch=str(patch),
                              run_dir=str(root / "run"), model="sonnet")
    return args, tooling / "main.py"

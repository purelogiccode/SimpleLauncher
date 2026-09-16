#!/usr/bin/env python3
"""Sync the SimpleLauncher docs folder to the GitHub wiki.

The GitHub wiki is a separate git repository (SimpleLauncher.wiki.git). The default
GITHUB_TOKEN cannot push to it, so this script is run locally (or in CI with a PAT).

Mappings applied:
  docs/README.md            -> Home.md
  docs/NN-*.md (01..18)     -> same filename (wiki page name = filename without .md)
  docs/parameters.md        -> parameters.md   (PROTECTED: the app opens this page,
                               https://github.com/purelogiccode/SimpleLauncher/wiki/parameters)
  docs/manual-tests.md      -> manual-tests.md
  skipped: docs/index.html, docs/_sidebar.md, docs/.nojekyll, DocsPlan.md

Link rewriting for the wiki: markdown links to published pages drop the ".md"
extension (GitHub wiki resolves plain page names); links to https:// URLs pass
through unchanged. Stale wiki pages are deleted, except the protected
parameters.md page.

Usage:
  python scripts/sync-wiki.py             # full sync: clone/pull, rewrite, commit, push
  python scripts/sync-wiki.py --dry-run   # show the plan without changing anything
  python scripts/sync-wiki.py --wiki-dir <dir> [--repo-dir <dir>]
"""

from __future__ import annotations

import argparse
import os
import shutil
import subprocess
import sys
import tempfile

WIKI_REPO_URL = "https://github.com/purelogiccode/SimpleLauncher.wiki.git"

# Files in the wiki that are never deleted, even if not in the publish set.
PROTECTED_WIKI_FILES = {"parameters.md"}

# Docs-folder files that are not published to the wiki.
WIKI_EXCLUDED = {"index.html", "_sidebar.md", ".nojekyll", "DocsPlan.md"}


def run_git(cwd: str, *args: str, check: bool = True) -> subprocess.CompletedProcess[str]:
    """Run a git command in the given directory."""
    result = subprocess.run(
        ["git", "-C", cwd, *args],
        capture_output=True,
        text=True,
        encoding="utf-8",
        errors="replace",
    )
    if check and result.returncode != 0:
        raise RuntimeError(f"git {' '.join(args)} failed: {result.stderr.strip()}")
    return result


def ensure_wiki_clone(wiki_dir: str) -> str:
    """Clone (or reset) the wiki repository into wiki_dir."""
    git_dir = os.path.join(wiki_dir, ".git")
    if os.path.isdir(git_dir):
        run_git(wiki_dir, "fetch", "origin")
        run_git(wiki_dir, "reset", "--hard", "origin/master")
    else:
        os.makedirs(wiki_dir, exist_ok=True)
        run_git(wiki_dir, "clone", WIKI_REPO_URL, ".", check=False)
        if not os.path.isdir(git_dir):
            raise RuntimeError(
                f"Could not clone {WIKI_REPO_URL}. Check network access and that the wiki is enabled."
            )
    return wiki_dir


def build_page_map(repo_dir: str) -> dict[str, str]:
    """Return {wiki_filename: source_file_path} for everything to publish."""
    docs_dir = os.path.join(repo_dir, "docs")
    pages: dict[str, str] = {}

    readme = os.path.join(docs_dir, "README.md")
    if os.path.isfile(readme):
        pages["Home.md"] = readme

    for name in sorted(os.listdir(docs_dir)):
        if name in WIKI_EXCLUDED or not name.endswith(".md"):
            continue
        if name == "README.md":
            continue
        pages[name] = os.path.join(docs_dir, name)

    return pages


def rewrite_wiki_links(text: str, page_names: set[str]) -> str:
    """Rewrite markdown links so they resolve inside the flat wiki namespace."""
    import re

    def replace(match: re.Match[str]) -> str:
        label, target = match.group(1), match.group(2)
        if target.startswith(("http://", "https://", "#", "mailto:")):
            return match.group(0)
        page = target.split("#")[0].removesuffix(".md")
        if page == "README":
            # docs/README.md is published as the wiki Home page, so links to it must
            # resolve to Home instead of degrading to plain text.
            page = "Home"
        anchor = target.split("#", 1)[1] if "#" in target else ""
        if page in page_names:
            return f"[{label}]({page}{('#' + anchor) if anchor else ''})"
        # Not a published page: drop the link, keep the label as plain text.
        return label

    return re.sub(r"\[([^\]]*)\]\(([^)]+)\)", replace, text)


def generate_sidebar(page_names: list[str]) -> str:
    """Generate a wiki _Sidebar.md with the docs in order."""
    lines = []
    if "Home" in page_names:
        lines.append("- [Home](Home)")
    for page in page_names:
        if page == "Home":
            continue
        label = page.replace("-", " ").replace("_", " ").title()
        if page.startswith("manual"):
            label = "Manual Tests"
        elif page == "parameters":
            label = "Parameters"
        lines.append(f"- [{label}]({page})")
    return "\n".join(lines) + "\n"


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dry-run", action="store_true", help="show the plan, change nothing")
    parser.add_argument("--repo-dir", default=os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
    parser.add_argument("--wiki-dir", default=os.path.join(tempfile.gettempdir(), "sl_wiki_sync"))
    args = parser.parse_args()

    repo_dir = os.path.abspath(args.repo_dir)
    docs_dir = os.path.join(repo_dir, "docs")
    if not os.path.isdir(docs_dir):
        print(f"ERROR: no docs folder at {docs_dir}", file=sys.stderr)
        return 2

    print(f"Using repo: {repo_dir}")
    print(f"Using wiki: {args.wiki_dir}")
    wiki_dir = ensure_wiki_clone(args.wiki_dir)
    print(f"Wiki clone ready at {wiki_dir}")

    page_map = build_page_map(repo_dir)
    page_names = {os.path.splitext(name)[0] for name in page_map}

    # 1. Rewrite + stage new/updated pages
    plan: list[tuple[str, str]] = []  # (action, file)
    for wiki_name, source_path in sorted(page_map.items()):
        text = open(source_path, encoding="utf-8").read()
        text = rewrite_wiki_links(text, page_names)
        target = os.path.join(wiki_dir, wiki_name)
        existing = open(target, encoding="utf-8").read() if os.path.isfile(target) else None
        if existing != text:
            if not args.dry_run:
                with open(target, "w", encoding="utf-8", newline="\n") as fh:
                    fh.write(text)
            plan.append(("update" if existing is not None else "add", wiki_name))

    # 2. Sidebar
    sidebar = generate_sidebar(sorted(page_names))
    sidebar_target = os.path.join(wiki_dir, "_Sidebar.md")
    if not os.path.isfile(sidebar_target) or open(sidebar_target, encoding="utf-8").read() != sidebar:
        if not args.dry_run:
            with open(sidebar_target, "w", encoding="utf-8", newline="\n") as fh:
                fh.write(sidebar)
        plan.append(("update", "_Sidebar.md"))

    # 3. Delete stale pages (never the protected parameters page)
    for name in sorted(os.listdir(wiki_dir)):
        if not name.endswith(".md") or name.startswith("_"):
            continue
        if name in page_map or name in PROTECTED_WIKI_FILES:
            continue
        if not args.dry_run:
            os.remove(os.path.join(wiki_dir, name))
        plan.append(("delete", name))

    if not plan:
        print("No changes - wiki is up to date.")
        return 0

    print("\nPlanned changes:")
    for action, name in plan:
        print(f"  {action:6s} {name}")

    if args.dry_run:
        print("\nDry run - nothing was changed.")
        return 0

    run_git(wiki_dir, "add", "-A")
    run_git(wiki_dir, "commit", "-m", "docs: sync wiki from docs/")
    print("\nPushing to the wiki...")
    try:
        run_git(wiki_dir, "push", "origin", "master")
    except RuntimeError as ex:
        print(f"PUSH FAILED: {ex}", file=sys.stderr)
        print(
            "The changes are committed locally in the wiki clone at:\n"
            f"  {wiki_dir}\n"
            "Run 'git -C <that dir> push origin master' after authenticating.",
            file=sys.stderr,
        )
        return 1

    print("Wiki updated.")
    return 0


if __name__ == "__main__":
    sys.exit(main())

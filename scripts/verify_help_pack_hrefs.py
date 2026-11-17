"""Verify the generated user-guide sites are safe for the offline file:// help pack.

The help/<lang>/ trees ship inside the release zip and are opened via file://,
where a root-absolute href ("/en/") resolves against the drive root of the
opened file instead of the pack root. So every generated page must:

- contain no href starting with "/" (404.html is exempt: it is server-only
  and never ships in the pack),
- contain the JS language switcher markup (data-gv-lang anchors plus the
  __gvLangSwitcher handler emitted by the alternate.html override).

For every page the script also resolves the switch target the same way the
switcher does at runtime (replace the /en/ or /de/ segment of the pathname)
and checks that the target file exists in the sibling site tree. It prints
one line per distinct page depth.

Run from the repo root — after `python -m zensical build` in each of
docs/userguide/de and docs/userguide/en locally, and automatically in the
release workflow's "Build user guide (en, de)" step:

    python scripts/verify_help_pack_hrefs.py
"""
from __future__ import annotations

import re
import sys
from html.parser import HTMLParser
from pathlib import Path

REPO = Path(__file__).resolve().parent.parent
LANGS = ("de", "en")
SITE_DIRS = {lang: REPO / "docs" / "userguide" / lang / "site" for lang in LANGS}
EXEMPT = {"404.html"}
LANG_SEGMENT = re.compile(r"/(de|en)(?=/|$)")


class HrefCollector(HTMLParser):
    def __init__(self) -> None:
        super().__init__()
        self.hrefs: list[str] = []
        self.switcher_entries = 0

    def handle_starttag(
        self, tag: str, attrs: list[tuple[str, str | None]]
    ) -> None:
        attr = {k: v for k, v in attrs if v is not None}
        href = attr.get("href")
        if href is not None:
            self.hrefs.append(href)
        if "data-gv-lang" in attr:
            self.switcher_entries += 1


def main() -> int:
    failures: list[str] = []
    depth_lines: list[str] = []
    seen_depths: dict[int, tuple[str, str, str]] = {}
    page_count = 0

    for lang in LANGS:
        site = SITE_DIRS[lang]
        if not site.is_dir():
            failures.append(f"{lang}: missing site tree at {site}")
            continue
        for html_path in sorted(site.rglob("*.html")):
            rel = html_path.relative_to(site).as_posix()
            if rel in EXEMPT:
                continue
            page_count += 1
            parser = HrefCollector()
            parser.feed(html_path.read_text(encoding="utf-8"))

            absolute = [h for h in parser.hrefs if h.startswith("/")]
            if absolute:
                failures.append(f"{lang}/{rel}: root-absolute hrefs {absolute}")
            if parser.switcher_entries != len(LANGS):
                failures.append(
                    f"{lang}/{rel}: expected {len(LANGS)} switcher entries, "
                    f"found {parser.switcher_entries}"
                )
            if "__gvLangSwitcher" not in html_path.read_text(encoding="utf-8"):
                failures.append(f"{lang}/{rel}: switcher script missing")

            # Resolve the switch target the way the runtime handler does:
            # rewrite the language segment of the page URL and check that the
            # sibling site contains that file. In the shipped pack each site
            # is published under publish/help/<lang>/, so the URL carries the
            # language segment even though each build's site/ tree roots at
            # its own language.
            url_path = f"/help/{lang}/" + rel
            other = "en" if lang == "de" else "de"
            target = LANG_SEGMENT.sub(f"/{other}", url_path)
            target_file = SITE_DIRS[other] / Path(target).relative_to(f"/help/{other}")
            ok = target_file.is_file()
            if not ok:
                failures.append(f"{lang}/{rel}: switch target {target} missing in {other}")
            depth = url_path.count("/") - 1
            if depth not in seen_depths:
                seen_depths[depth] = (rel, target, "ok" if ok else "MISSING")

    for depth in sorted(seen_depths):
        page, target, status = seen_depths[depth]
        depth_lines.append(f"depth {depth}: {page} -> {target} [{status}]")

    print(f"checked {page_count} pages across {len(LANGS)} site trees")
    for line in depth_lines:
        print(line)
    if failures:
        print(f"\nFAIL ({len(failures)} problems):")
        for failure in failures:
            print(f"  {failure}")
        return 1
    print("OK: no root-absolute hrefs, switcher present and resolvable everywhere")
    return 0


if __name__ == "__main__":
    sys.exit(main())

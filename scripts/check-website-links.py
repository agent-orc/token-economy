#!/usr/bin/env python3
"""Check local website links and fragments without making network requests."""
from __future__ import annotations

from html.parser import HTMLParser
from pathlib import Path
from urllib.parse import unquote, urlsplit


ROOT = Path(__file__).resolve().parents[1]
WEBSITE = ROOT / "website"
SITE_PREFIX = "/token-economy"


class PageParser(HTMLParser):
    def __init__(self) -> None:
        super().__init__()
        self.ids: set[str] = set()
        self.references: list[tuple[str, str]] = []

    def handle_starttag(self, tag: str, attrs: list[tuple[str, str | None]]) -> None:
        values = dict(attrs)
        if values.get("id"):
            self.ids.add(values["id"] or "")
        for attribute in ("href", "src"):
            if values.get(attribute):
                self.references.append((attribute, values[attribute] or ""))


def parse(path: Path) -> PageParser:
    parser = PageParser()
    parser.feed(path.read_text(encoding="utf-8"))
    return parser


def target_for(page: Path, reference: str) -> tuple[Path, str] | None:
    split = urlsplit(reference)
    if split.scheme or split.netloc or reference.startswith(("mailto:", "data:")):
        return None
    raw_path = unquote(split.path)
    if raw_path.startswith(SITE_PREFIX):
        raw_path = raw_path[len(SITE_PREFIX):]
        target = WEBSITE / raw_path.lstrip("/")
    elif raw_path.startswith("/"):
        return None
    else:
        target = page.parent / raw_path
    if not raw_path or raw_path.endswith("/"):
        target /= "index.html"
    return target.resolve(), unquote(split.fragment)


def main() -> None:
    pages = sorted(path for path in WEBSITE.rglob("*.html") if "_includes" not in path.parts)
    parsed = {path.resolve(): parse(path) for path in pages}
    failures: list[str] = []
    for page in pages:
        for attribute, reference in parsed[page.resolve()].references:
            target = target_for(page, reference)
            if target is None:
                continue
            target_path, fragment = target
            if not target_path.exists():
                failures.append(f"{page.relative_to(ROOT)}: {attribute}=\"{reference}\" has no target")
                continue
            if fragment and target_path.suffix.lower() == ".html":
                target_page = parsed.get(target_path)
                if target_page is None:
                    target_page = parse(target_path)
                if fragment not in target_page.ids:
                    failures.append(f"{page.relative_to(ROOT)}: {reference} has no #{fragment}")
    if failures:
        raise SystemExit("Website link check failed:\n" + "\n".join(failures))
    print(f"Website link check passed for {len(pages)} HTML pages.")


if __name__ == "__main__":
    main()

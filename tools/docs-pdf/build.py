"""Builds the PDF editions of the project documents.

The Markdown files stay the source of truth: they are what gets edited and reviewed. Each PDF is generated
from its Markdown file with a cover page, a document control table, a table of contents with page numbers
and a running header and footer.

Usage:
    py tools/docs-pdf/build.py                 every document
    py tools/docs-pdf/build.py GAME_DESIGN     only the named ones

Requires pandoc, Google Chrome or Microsoft Edge, and the pypdf package.
"""

from __future__ import annotations

import argparse
import datetime
import html
import os
import re
import shutil
import subprocess
import sys
from dataclasses import dataclass
from pathlib import Path

from pypdf import PdfReader, PdfWriter

ASSETS = Path(__file__).resolve().parent
REPO = ASSETS.parents[1]
OUTPUT_DIR = REPO / "docs" / "pdf"
WORK_DIR = REPO / "out" / "docs-pdf"

PROJECT = "Puzzle (çalışma adı)"
OWNER = "SaturnWave"
BROWSER_OVERRIDE = "PUZZLE_PDF_BROWSER"
BROWSER_TIMEOUT_SECONDS = 180

# The table of contents is printed without page numbers first, then again with the measured ones.
# Its page column has a fixed width, so the second print normally lands on the same pages.
MAX_LAYOUT_PASSES = 3

FRAGMENT_SEPARATOR = "<!--BODY-->"
TITLE_LINE = re.compile(r"^# (?P<title>.+?)(?: — v(?P<version>[\w.]+))?\s*$")
HEADING_ID = re.compile(r'<h[1-6] id="([^"]+)"')
TOC_LINK = re.compile(r'<a href="#(?P<anchor>s\d+)"[^>]*>(?P<text>.*?)</a>', re.DOTALL)
RELATIVE_LINK = re.compile(r'<a href="(?!https?://|mailto:|#)[^"]*"[^>]*>(?P<text>.*?)</a>', re.DOTALL)
MONTHS = ("Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran",
          "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık")


class BuildError(Exception):
    """A document could not be built; the message says what to fix."""


@dataclass(frozen=True)
class Document:
    source: Path
    """Markdown file, relative to the repository root."""

    kind: str
    """What sort of document this is; shown on the cover."""

    @property
    def name(self) -> str:
        return self.source.stem


DOCUMENTS = (
    Document(Path("docs/OVERVIEW.md"), "Genel Belge"),
    Document(Path("docs/GAME_DESIGN.md"), "Tasarım Belgesi"),
    Document(Path("docs/DEVLOG.md"), "Proje Günlüğü"),
    Document(Path("docs/PUBLIC_TEST.md"), "Kullanım Kılavuzu"),
    Document(Path("docs/PHONE_TESTS.md"), "Test Planı"),
    Document(Path("docs/IOS_BUILD.md"), "Derleme Planı"),
    Document(Path("docs/research/in-level-boosters.md"), "Araştırma Notu"),
    Document(Path("docs/next-game/BLOCK_PUZZLE_PROMPT.md"), "Başlangıç Metni"),
    Document(Path("docs/music/MUSIC_BRIEF.md"), "Müzik İstekleri"),
    Document(Path("docs/research/spawn-control-and-solvability.md"), "Araştırma Raporu"),
    Document(Path("docs/research/monetization-and-retention.md"), "Araştırma Raporu"),
    Document(Path("docs/research/iap-and-analytics.md"), "Araştırma Notu"),
    Document(Path("docs/research/mini-games.md"), "Araştırma Raporu"),
    Document(Path("research.md"), "Araştırma Özeti"),
)


@dataclass(frozen=True)
class SourceText:
    title: str
    version: str | None
    """Taken from a "— v1.2" suffix on the title line; absent when the document is not versioned."""

    body: str
    """The Markdown below the title line."""


@dataclass(frozen=True)
class Revision:
    changed_on: datetime.date
    commit: str | None
    """Short hash of the last commit that touched the file; absent when it was never committed."""

    has_uncommitted_changes: bool


@dataclass(frozen=True)
class Fragments:
    contents: str
    """The table of contents as nested lists; empty when the document has no headings."""

    body: str


def main() -> int:
    arguments = argparse.ArgumentParser(description="Build the PDF editions of the project documents.")
    arguments.add_argument("names", nargs="*", help="document names (file name without .md); default: all")
    try:
        documents = select_documents(arguments.parse_args().names)
        browser = find_browser()
        OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
        WORK_DIR.mkdir(parents=True, exist_ok=True)
        for document in documents:
            pdf = build(document, browser)
            print(f"{pdf.relative_to(REPO).as_posix()}: {len(PdfReader(pdf).pages)} pages, {pdf.stat().st_size // 1024} KB")
    except BuildError as error:
        print(f"docs-pdf: {error}", file=sys.stderr)
        return 1
    return 0


def select_documents(names: list[str]) -> tuple[Document, ...]:
    if not names:
        return DOCUMENTS
    known = {document.name: document for document in DOCUMENTS}
    unknown = [name for name in names if name not in known]
    if unknown:
        raise BuildError(f"unknown document {', '.join(unknown)}; choose from {', '.join(known)}")
    return tuple(known[name] for name in names)


def find_browser() -> Path:
    override = os.environ.get(BROWSER_OVERRIDE)
    candidates = [
        override,
        "C:/Program Files/Google/Chrome/Application/chrome.exe",
        "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
        shutil.which("google-chrome"),
        shutil.which("chromium"),
        shutil.which("msedge"),
    ]
    for candidate in candidates:
        if candidate and Path(candidate).is_file():
            return Path(candidate)
    raise BuildError(f"no Chrome or Edge found; set {BROWSER_OVERRIDE} to the browser executable")


def build(document: Document, browser: Path) -> Path:
    source = read_source(document)
    revision = read_revision(document.source)
    fragments = without_relative_links(with_short_anchors(render_fragments(source.body)))
    printed = typeset(document, source, revision, fragments, browser)
    target = OUTPUT_DIR / f"{document.name}.pdf"
    write_with_metadata(printed, target, title=source.title, subject=document.kind)
    return target


def read_source(document: Document) -> SourceText:
    markdown = (REPO / document.source).read_text(encoding="utf-8")
    title_line, _, body = markdown.partition("\n")
    match = TITLE_LINE.match(title_line)
    if match is None:
        raise BuildError(f"{document.source.as_posix()} must start with a '# Title' line")
    return SourceText(match["title"], match["version"], body)


def read_revision(source: Path) -> Revision:
    last_commit = git("log", "-1", "--format=%cs %h", "--", source.as_posix())
    has_uncommitted_changes = bool(git("status", "--porcelain", "--", source.as_posix()))
    if last_commit and not has_uncommitted_changes:
        committed_on, commit = last_commit.split()
        return Revision(datetime.date.fromisoformat(committed_on), commit, False)
    edited_on = datetime.date.fromtimestamp((REPO / source).stat().st_mtime)
    return Revision(edited_on, last_commit.split()[1] if last_commit else None, has_uncommitted_changes)


def git(*arguments: str) -> str:
    return run(["git", "-C", str(REPO), *arguments]).strip()


def render_fragments(markdown: str) -> Fragments:
    """Converts the Markdown; its second-level headings become the PDF's top-level sections."""
    rendered = run(
        ["pandoc", "--from=gfm", "--to=html5", "--toc", "--toc-depth=2", "--shift-heading-level-by=-1",
         "--wrap=none", "--syntax-highlighting=none", f"--template={ASSETS / 'fragment.html'}"],
        stdin=markdown)
    contents, separator, body = rendered.partition(FRAGMENT_SEPARATOR)
    if not separator:
        raise BuildError("pandoc output is missing the body marker; check tools/docs-pdf/fragment.html")
    return Fragments(contents.strip(), body.strip())


def with_short_anchors(fragments: Fragments) -> Fragments:
    """Renames heading anchors to s1, s2, ...: PDF destinations must not depend on non-ASCII heading text."""
    contents, body = fragments.contents, fragments.body
    for index, original in enumerate(HEADING_ID.findall(body), start=1):
        body = body.replace(f'id="{original}"', f'id="s{index}"').replace(f'href="#{original}"', f'href="#s{index}"')
        contents = contents.replace(f'href="#{original}"', f'href="#s{index}"')
    return Fragments(contents, body)


def without_relative_links(fragments: Fragments) -> Fragments:
    """Keeps the text of links to other repository files: in a PDF they would point at local paths."""
    return Fragments(fragments.contents, RELATIVE_LINK.sub(r'<span class="docref">\g<text></span>', fragments.body))


def typeset(document: Document, source: SourceText, revision: Revision, fragments: Fragments, browser: Path) -> Path:
    page_file = WORK_DIR / f"{document.name}.html"
    printed = WORK_DIR / f"{document.name}.pdf"
    heading_pages: dict[str, int] = {}
    for _ in range(MAX_LAYOUT_PASSES):
        page_file.write_text(compose_page(document, source, revision, fragments, heading_pages), encoding="utf-8")
        print_to_pdf(browser, page_file, printed)
        measured = read_heading_pages(printed)
        if measured == heading_pages:
            return printed
        heading_pages = measured
    raise BuildError(f"{document.name}: page numbers did not settle after {MAX_LAYOUT_PASSES} passes")


def compose_page(document: Document, source: SourceText, revision: Revision, fragments: Fragments,
                 heading_pages: dict[str, int]) -> str:
    changed_on = turkish_date(revision.changed_on)
    edition = f"v{source.version} · {changed_on}" if source.version else changed_on
    values = {
        "title": html.escape(source.title),
        "kind": html.escape(document.kind),
        "project": html.escape(PROJECT),
        "stylesheet": (ASSETS / "print.css").read_text(encoding="utf-8"),
        "running_title": css_string(source.title),
        "running_edition": css_string(edition),
        "running_project": css_string(PROJECT),
        "control_rows": control_rows(document, source, revision),
        "contents": contents_section(fragments.contents, heading_pages),
        "body": fragments.body,
    }
    page = (ASSETS / "page.html").read_text(encoding="utf-8")
    for name, value in values.items():
        page = page.replace("{{" + name + "}}", value)
    return page


def control_rows(document: Document, source: SourceText, revision: Revision) -> str:
    if revision.commit is None:
        repository_record = "Henüz depoya kaydedilmedi"
    elif revision.has_uncommitted_changes:
        repository_record = f"{revision.commit} + kaydedilmemiş değişiklikler"
    else:
        repository_record = revision.commit
    rows = [
        ("Sürüm", f"v{source.version}" if source.version else None),
        ("Son değişiklik", turkish_date(revision.changed_on)),
        ("Kaynak dosya", document.source.as_posix()),
        ("Depo kaydı", repository_record),
        ("Proje sahibi", OWNER),
    ]
    return "\n".join(
        f"<tr><th>{html.escape(label)}</th><td>{html.escape(value)}</td></tr>" for label, value in rows if value)


def contents_section(contents: str, heading_pages: dict[str, int]) -> str:
    if not contents:
        return ""

    def entry(link: re.Match[str]) -> str:
        page = heading_pages.get(link["anchor"])
        return (f'<a href="#{link["anchor"]}"><span class="toc-text">{link["text"]}</span>'
                f'<span class="toc-dots"></span><span class="toc-page">{page or ""}</span></a>')

    return f'<nav class="contents"><h1>İçindekiler</h1>{TOC_LINK.sub(entry, contents)}</nav>'


def print_to_pdf(browser: Path, page_file: Path, pdf: Path) -> None:
    pdf.unlink(missing_ok=True)
    run([
        str(browser), "--headless=new", "--disable-gpu", "--no-first-run", "--disable-extensions",
        f"--user-data-dir={WORK_DIR / 'browser-profile'}",
        "--no-pdf-header-footer", "--generate-pdf-document-outline",
        f"--print-to-pdf={pdf}", page_file.as_uri(),
    ], timeout=BROWSER_TIMEOUT_SECONDS)
    if not pdf.is_file():
        raise BuildError(f"{browser.name} did not produce {pdf.name}")


def read_heading_pages(pdf: Path) -> dict[str, int]:
    """Page number of every heading the table of contents links to, keyed by anchor."""
    reader = PdfReader(pdf)
    return {
        anchor.lstrip("/"): reader.get_destination_page_number(destination) + 1
        for anchor, destination in reader.named_destinations.items()
    }


def write_with_metadata(printed: Path, target: Path, *, title: str, subject: str) -> None:
    writer = PdfWriter(clone_from=printed)
    writer.add_metadata({"/Title": title, "/Subject": subject, "/Author": OWNER, "/Creator": "tools/docs-pdf"})
    with target.open("wb") as output:
        writer.write(output)


def run(command: list[str], *, stdin: str | None = None, timeout: int | None = None) -> str:
    tool = Path(command[0]).name
    try:
        completed = subprocess.run(
            command, input=stdin, capture_output=True, text=True, encoding="utf-8", timeout=timeout, check=False)
    except FileNotFoundError as error:
        raise BuildError(f"{tool} is not installed or not on PATH") from error
    except subprocess.TimeoutExpired as error:
        raise BuildError(f"{tool} did not finish within {timeout} seconds") from error
    if completed.returncode != 0:
        raise BuildError(f"{tool} failed ({completed.returncode}): {completed.stderr.strip()}")
    return completed.stdout


def turkish_date(date: datetime.date) -> str:
    return f"{date.day} {MONTHS[date.month - 1]} {date.year}"


def css_string(text: str) -> str:
    escaped = text.replace("\\", "\\\\").replace('"', '\\"')
    return f'"{escaped}"'


if __name__ == "__main__":
    sys.exit(main())

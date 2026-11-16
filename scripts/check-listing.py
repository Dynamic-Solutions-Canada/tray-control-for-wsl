#!/usr/bin/env python3
"""Check the Microsoft Store listing source and the release notes for the current version.

Listing (store-assets/microsoft/listings.json, LIST-3, LIST-5, LIST-12):
- schemaVersion 1 and the nine locale identities, in order; en-CA and fr-CA are copyFrom aliases of a
  full record, and the full records are exactly the app's wording catalogs (LOC-11).
- Every full record has every field and no other (no releaseNotes, LIST-10), and every field is within
  the Microsoft Store's limits. Over-length text fails; nothing is truncated.
- Every locale has the same number of screenshot captions, and no text contains an em dash.
- The title is the product name, and the README's header tagline is the first sentence of the en-US
  shortDescription, word for word (LIST-12).

Release notes (release-notes/, REL-6, LIST-10), for the <Version> in Directory.Build.props:
- <version>.md (en-US) and one <version>.<identity>.md per other wording catalog exist, are not empty,
  have no heading, fit the Microsoft Store's 1,500-character limit, and contain no em dash. The
  Canadian identities have no files of their own: the publisher maps them to en-US and fr-FR.

Not checked yet: screenshot files and their manifest (LIST-8, none captured yet) and the live listing
(LIST-9 check mode, which waits for the publisher).

Run from the repo root: python3 scripts/check-listing.py
"""
import json
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent
LISTING = ROOT / "store-assets" / "microsoft" / "listings.json"
CATALOGS = ROOT / "WslMonitor" / "Localization" / "Catalogs"
NOTES = ROOT / "release-notes"
PROPS = ROOT / "Directory.Build.props"
README = ROOT / "README.md"

PRODUCT_NAME = "WSL Monitor"

# The nine-locale set, in the order locales.md gives, and the two aliases that reuse a catalog.
LOCALES = ["en-US", "en-CA", "es-ES", "fr-FR", "fr-CA", "de-DE", "zh-CN", "pt-BR", "ja-JP"]
ALIASES = {"en-CA": "en-US", "fr-CA": "fr-FR"}

# Microsoft Store limits (LIST-5).
FIELDS = ["title", "shortDescription", "fullDescription", "features", "keywords", "screenshotCaptions"]
SHORT_MAX = 270
FULL_MAX = 4000
FEATURES_MAX, FEATURE_MAX = 20, 200
KEYWORDS_MAX, KEYWORD_MAX, KEYWORD_WORDS_MAX = 7, 40, 21
CAPTION_MAX = 200
NOTES_MAX = 1500

EM_DASH = "—"


def check_text(problems, where, text, limit):
    """Record a problem when text is empty, over its limit, or contains an em dash."""
    if not isinstance(text, str) or not text.strip():
        problems.append(f"{where}: empty")
        return
    if len(text) > limit:
        problems.append(f"{where}: {len(text)} characters, over the {limit} limit")
    if EM_DASH in text:
        problems.append(f"{where}: contains an em dash")


def check_list(problems, where, items, count_max, item_max):
    """Record a problem when a list field is not a list, too long, or has a bad item."""
    if not isinstance(items, list) or not items:
        problems.append(f"{where}: must be a non-empty list")
        return
    if len(items) > count_max:
        problems.append(f"{where}: {len(items)} items, over the {count_max} limit")
    for index, item in enumerate(items, 1):
        check_text(problems, f"{where} {index}", item, item_max)


def check_record(problems, locale, record):
    """Check one full locale record against the field list and the store limits."""
    missing = [f for f in FIELDS if f not in record]
    extra = [f for f in record if f not in FIELDS]
    if missing:
        problems.append(f"{locale}: missing {', '.join(missing)}")
    if extra:
        problems.append(f"{locale}: unexpected {', '.join(extra)} (release notes live in release-notes/)")

    if record.get("title") != PRODUCT_NAME:
        problems.append(f"{locale}.title: must be {PRODUCT_NAME!r}")
    check_text(problems, f"{locale}.shortDescription", record.get("shortDescription"), SHORT_MAX)
    check_text(problems, f"{locale}.fullDescription", record.get("fullDescription"), FULL_MAX)
    check_list(problems, f"{locale}.features", record.get("features"), FEATURES_MAX, FEATURE_MAX)
    check_list(problems, f"{locale}.keywords", record.get("keywords"), KEYWORDS_MAX, KEYWORD_MAX)
    check_list(problems, f"{locale}.screenshotCaptions", record.get("screenshotCaptions"), 10, CAPTION_MAX)

    keywords = record.get("keywords")
    if isinstance(keywords, list):
        words = sum(len(str(k).split()) for k in keywords)
        if words > KEYWORD_WORDS_MAX:
            problems.append(f"{locale}.keywords: {words} words combined, over the {KEYWORD_WORDS_MAX} limit")


def check_listing(problems):
    """Check listings.json. Returns the wording-catalog locales (the full records) for the notes check."""
    data = json.loads(LISTING.read_text(encoding="utf-8"))
    if data.get("schemaVersion") != 1:
        problems.append("listings.json: schemaVersion must be 1")

    locales = data.get("locales", {})
    if list(locales) != LOCALES:
        problems.append(f"listings.json: locales must be {LOCALES} in that order, found {list(locales)}")

    full = [locale for locale, record in locales.items() if "copyFrom" not in record]
    for alias, target in ALIASES.items():
        record = locales.get(alias, {})
        if record != {"copyFrom": target}:
            problems.append(f"{alias}: must be {{\"copyFrom\": \"{target}\"}}")
    for locale in full:
        check_record(problems, locale, locales[locale])

    # LOC-11: the listing's wording records are exactly the app's catalogs.
    catalogs = sorted(p.stem for p in CATALOGS.glob("*.json"))
    if sorted(full) != catalogs:
        problems.append(f"listings.json: full records {sorted(full)} differ from the catalogs {catalogs}")

    # Every locale shows the same screenshots, so it needs the same number of captions.
    counts = {locale: len(locales[locale].get("screenshotCaptions") or []) for locale in full}
    if len(set(counts.values())) > 1:
        problems.append(f"screenshotCaptions: counts differ between locales {counts}")

    check_tagline(problems, locales.get("en-US", {}).get("shortDescription", ""))
    return full


def check_tagline(problems, short_description):
    """LIST-12: the README header tagline is the first sentence of the en-US shortDescription."""
    first = re.match(r".*?[.!?](?=\s|$)", short_description)
    expected = first.group(0) if first else short_description
    readme = README.read_text(encoding="utf-8")
    tagline = re.search(r'<p align="center">\s*(.*?)\s*</p>', readme, re.S)
    if not tagline or tagline.group(1).strip() != expected:
        found = tagline.group(1).strip() if tagline else "(none)"
        problems.append(f"README tagline {found!r} must be the first en-US shortDescription sentence {expected!r}")


def check_notes(problems, wording_locales):
    """REL-6: the notes for the current version exist for every wording catalog and fit the store."""
    match = re.search(r"<Version>([^<]+)</Version>", PROPS.read_text(encoding="utf-8"))
    if not match:
        problems.append("Directory.Build.props: no <Version>")
        return
    version = match.group(1).strip()

    expected = {f"{version}.md"} | {f"{version}.{locale}.md" for locale in wording_locales if locale != "en-US"}
    for name in sorted(expected):
        path = NOTES / name
        if not path.is_file():
            problems.append(f"release-notes/{name}: missing (notes for {version})")
            continue
        text = path.read_text(encoding="utf-8").strip()
        check_text(problems, f"release-notes/{name}", text, NOTES_MAX)
        if any(line.lstrip().startswith("#") for line in text.splitlines()):
            problems.append(f"release-notes/{name}: has a heading; CI adds it")

    for alias in ALIASES:
        stray = NOTES / f"{version}.{alias}.md"
        if stray.exists():
            problems.append(f"release-notes/{stray.name}: {alias} reuses {ALIASES[alias]}; remove this file")


def main():
    problems = []
    wording_locales = check_listing(problems)
    check_notes(problems, wording_locales)

    for problem in problems:
        print(problem)
    print(f"Listing and release notes: {len(problems)} problem(s)")
    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main())

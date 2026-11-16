#!/usr/bin/env python3
"""Check WSL Monitor's interface text catalogs (WslMonitor/Localization/Catalogs/*.json).

Fails when:
  - a catalog is missing a key that en-US has, or has a key en-US doesn't;
  - a translation's {{placeholders}} differ from the English ones;
  - any text contains an em dash (house style);
  - code or XAML uses a key that en-US doesn't define;
  - the package manifest's <Resource Language> list differs from the supported identities.

Keys defined but never referenced in code are reported as warnings. Run from anywhere:
`npm run check:l10n`.
"""
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
STRINGS = ROOT / "WslMonitor" / "Localization" / "Catalogs"
SOURCE = ROOT / "WslMonitor"
MANIFEST = ROOT / "WslMonitorMSIX" / "Package.appxmanifest"
LANGUAGES_CS = SOURCE / "Classes" / "AppLanguages.cs"

PLACEHOLDER = re.compile(r"\{\{\s*(\w+)\s*\}\}")
# Keys appear in code as UiText.Get("k") / UiText.Format("k", ...), Localize.SetX(target, "k"),
# l10n:Localize.X="k" in XAML, and as bare "group.name" literals (state keys, AddDetail labels).
KEY_IN_CODE = re.compile(r'"((?:[a-z]+\.[a-z0-9.-]+)|AppDescription)"')


def load(path: Path) -> dict[str, str]:
    with path.open(encoding="utf-8") as f:
        return json.load(f)


def main() -> int:
    errors: list[str] = []
    warnings: list[str] = []

    catalogs = {p.stem: load(p) for p in sorted(STRINGS.glob("*.json"))}
    english = catalogs.get("en-US")
    if english is None:
        print("en-US.json is missing", file=sys.stderr)
        return 1

    # 1. Same keys and placeholders everywhere; no em dashes.
    for lang, catalog in catalogs.items():
        for key in sorted(english.keys() - catalog.keys()):
            errors.append(f"{lang}: missing {key}")
        for key in sorted(catalog.keys() - english.keys()):
            errors.append(f"{lang}: extra key {key}")
        for key, text in catalog.items():
            if "\u2014" in text:
                errors.append(f"{lang}: {key} contains an em dash")
            if not text.strip():
                errors.append(f"{lang}: {key} is empty")
            if key in english and set(PLACEHOLDER.findall(text)) != set(PLACEHOLDER.findall(english[key])):
                errors.append(f"{lang}: {key} placeholders differ from en-US")

    # 2. Every key the app uses exists.
    used: set[str] = set()
    for path in list(SOURCE.rglob("*.cs")) + list(SOURCE.rglob("*.xaml")):
        if any(part in ("bin", "obj") for part in path.parts):
            continue
        used.update(KEY_IN_CODE.findall(path.read_text(encoding="utf-8")))
    # File names look like keys ("wsl.exe", "settings.json"); they aren't.
    used = {key for key in used if not re.search(r"\.(exe|json|txt|ico|png|config)$", key)}
    for key in sorted(used - english.keys()):
        errors.append(f"code uses undefined key {key}")
    for key in sorted(english.keys() - used):
        warnings.append(f"en-US key not referenced in code: {key}")

    # 3. Manifest languages match AppLanguages.
    identities = re.findall(r'\("([a-z]{2}-[A-Z]{2})", "[a-z]{2}-[A-Z]{2}"', LANGUAGES_CS.read_text(encoding="utf-8"))
    declared = re.findall(r'<Resource Language="([^"]+)"', MANIFEST.read_text(encoding="utf-8"))
    if sorted(identities) != sorted(declared):
        errors.append(f"manifest languages {declared} differ from AppLanguages {identities}")
    catalog_names = set(re.findall(r'\("[a-z]{2}-[A-Z]{2}", "([a-z]{2}-[A-Z]{2})"', LANGUAGES_CS.read_text(encoding="utf-8")))
    if catalog_names != set(catalogs):
        errors.append(f"catalog files {sorted(catalogs)} differ from AppLanguages catalogs {sorted(catalog_names)}")

    for line in warnings:
        print("warning:", line)
    for line in errors:
        print("error:", line, file=sys.stderr)
    print(f"{len(catalogs)} catalogs, {len(english)} keys, {len(errors)} errors, {len(warnings)} warnings")
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())

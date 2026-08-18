"""Generate reviewed narration clips from the runtime dialogue catalog.

By default this generates only System entries. Use --all only after the
historical entries have been editorially reviewed; it overwrites matching MP3s.
"""
import argparse
import asyncio
import json
from pathlib import Path

import edge_tts

CATALOG_PATH = Path("Assets/Resources/audio_runtime_catalog.json")
OUTPUT_DIR = Path("Assets/Resources/GuidanceAudio")
VOICES = {"EN": "en-US-SteffanNeural", "DE": "de-DE-ConradNeural"}


def language_for(clip_id: str) -> str:
    return "DE" if clip_id.upper().endswith("_DE") else "EN"


async def generate_clip(clip_id: str, text: str) -> None:
    language = language_for(clip_id)
    output_path = OUTPUT_DIR / f"{clip_id}.mp3"
    communicate = edge_tts.Communicate(text.strip(), VOICES[language])
    await communicate.save(str(output_path))
    print(f"Generated {clip_id} ({VOICES[language]})")
    await asyncio.sleep(0.05)


async def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--all", action="store_true", help="Generate every source-safe catalog entry.")
    parser.add_argument("--missing", action="store_true", help="Generate only missing or empty source-safe clips.")
    parser.add_argument("--limit", type=int, default=0, help="Maximum clips to generate (use with --missing for resumable batches).")
    parser.add_argument("--id", action="append", default=[], help="Generate one exact catalog ID; repeatable.")
    args = parser.parse_args()

    catalog = json.loads(CATALOG_PATH.read_text(encoding="utf-8"))
    entries = catalog.get("dialogues", [])
    selected = [
        entry for entry in entries
        if entry.get("id") and entry.get("text") and (
            args.all or args.missing or entry["id"] in args.id or (
                not args.id and entry.get("category") == "system"
            )
        ) and not entry.get("script_status", "").startswith("preserved_legacy")
    ]

    if args.missing:
        selected = [entry for entry in selected if not (OUTPUT_DIR / f"{entry['id']}.mp3").exists() or (OUTPUT_DIR / f"{entry['id']}.mp3").stat().st_size == 0]
    if args.limit > 0:
        selected = selected[:args.limit]

    if not selected:
        raise SystemExit("No matching catalog entries.")

    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    print(f"Generating {len(selected)} reviewed catalog entries.")
    for entry in selected:
        await generate_clip(entry["id"], entry["text"])


if __name__ == "__main__":
    asyncio.run(main())

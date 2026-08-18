"""Build the source-traceable subtitle and synthesis catalog used at runtime.

This deliberately preserves existing clips when no safe source text is
available. It never deletes, renames, or infers a memorial narration.
"""
import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
RUNTIME = ROOT / "Assets/Resources/dialogues_config.json"
OUTPUT = ROOT / "Assets/Resources/audio_runtime_catalog.json"
CANONICAL = ROOT.parent / ".agents/wiki/audio_canonical_draft.json"
INDIVIDUAL = ROOT.parent / ".agents/wiki/individual_audio_scripts.json"
SAFE_FALLBACKS = {
    # The legacy MG12 sequence consisted only of a literal TODO. This neutral
    # accessibility cue is preferable to narrating an unsupported death count.
    "MG12_EN": "This is a mass grave. The information panel and memorial inscription provide the available recorded details.",
    "MG12_DE": "Dies ist ein Massengrab. Die Informationstafel und die Inschrift nennen die verfügbaren dokumentierten Angaben.",
}
REVIEWED_OVERRIDES = {
    "Walking_RegistryDestruction_EN": ("The number of people who died at Bergen-Belsen is estimated at around 52,000. Many victims cannot be identified by name because records are incomplete or were destroyed. Memorials and graves therefore do not provide a complete record of every death.", "source_checked", "Schiffer/Seybold transcription: death estimates and memorial landscape."),
    "AUDIO_11_EN": ("The number of people who died at Bergen-Belsen is estimated at around 52,000. Many victims cannot be identified by name because records are incomplete or were destroyed. Memorials and graves therefore do not provide a complete record of every death.", "source_checked", "Schiffer/Seybold transcription: death estimates and memorial landscape."),
    "AUDIO_11_DE": ("Die Zahl der in Bergen-Belsen Verstorbenen wird auf rund 52.000 geschätzt. Viele Opfer können nicht namentlich identifiziert werden, weil Unterlagen unvollständig sind oder zerstört wurden. Die Gedenkzeichen und Gräber bilden daher nicht jeden Todesfall vollständig ab.", "needs_de_review", "Schiffer/Seybold transcription: death estimates and memorial landscape."),
    "AUDIO_21_EN": ("On 21 May 1945, the British Army burned the former prisoner barracks after liberation in an effort to contain disease. The action changed the physical landscape of the former camp; the memorial landscape was shaped in the years that followed.", "source_checked", "Schiffer/Seybold transcription: end phase and memorial landscape."),
    "AUDIO_21_DE": ("Am 21. Mai 1945 verbrannte die britische Armee nach der Befreiung die ehemaligen Häftlingsbaracken, um die Ausbreitung von Krankheiten einzudämmen. Dadurch veränderte sich die bauliche Landschaft des ehemaligen Lagers; die Gedenkstätte entstand in den folgenden Jahren.", "needs_de_review", "Schiffer/Seybold transcription: end phase and memorial landscape."),
    "AUDIO_23_EN": ("You may pause the route and examine this memorial at your own pace. Continue the tour when you are ready.", "source_checked", "App interaction wording."),
    "AUDIO_23_DE": ("Sie können die Route anhalten und dieses Gedenkzeichen in Ihrem eigenen Tempo betrachten. Setzen Sie die Tour fort, wenn Sie bereit sind.", "needs_de_review", "App interaction wording."),
    "AUDIO_24_EN": ("You may pause here to look more closely at this memorial. Continue when you are ready.", "source_checked", "App interaction wording."),
    "AUDIO_24_DE": ("Sie können hier anhalten, um dieses Gedenkzeichen genauer zu betrachten. Setzen Sie fort, wenn Sie bereit sind.", "needs_de_review", "App interaction wording."),
    "AUDIO_26_EN": ("Some evidence about Bergen-Belsen has survived in diaries and other personal records. These sources preserve individual perspectives, alongside official documents and later research.", "source_checked", "Conservative source-literacy wording; no unsupported attribution."),
    "AUDIO_26_DE": ("Ein Teil der Zeugnisse über Bergen-Belsen ist in Tagebüchern und anderen persönlichen Aufzeichnungen erhalten. Solche Quellen bewahren individuelle Perspektiven neben amtlichen Dokumenten und späterer Forschung.", "needs_de_review", "Conservative source-literacy wording; no unsupported attribution."),
    "AUDIO_27_EN": ("Near the former concentration camp is the cemetery of the former prisoner-of-war camp. It commemorates military internees of several nationalities and is distinct from the memorial landscape of Bergen-Belsen concentration camp.", "source_checked", "Schiffer/Seybold transcription: POW cemetery."),
    "AUDIO_27_DE": ("In der Nähe des ehemaligen Konzentrationslagers liegt der Friedhof des früheren Kriegsgefangenenlagers. Er erinnert an Militärinternierte verschiedener Nationalitäten und ist von der Gedenklandschaft des Konzentrationslagers Bergen-Belsen zu unterscheiden.", "needs_de_review", "Schiffer/Seybold transcription: POW cemetery."),
    "AUDIO_28_EN": ("After the former prisoner barracks were burned, some buildings of the former SS camp remained. From 1946 to 1953, the settlement known as Neu-Hohne accommodated German expellees.", "source_checked", "Schiffer/Seybold transcription: Neu-Hohne."),
    "AUDIO_28_DE": ("Nach dem Verbrennen der ehemaligen Häftlingsbaracken blieben einige Gebäude des früheren SS-Lagers erhalten. Von 1946 bis 1953 beherbergte die Siedlung Neu-Hohne deutsche Vertriebene.", "needs_de_review", "Schiffer/Seybold transcription: Neu-Hohne."),
    "AUDIO_29_EN": ("This stone includes a menorah, the seven-branched candelabrum. It is an important symbol in Judaism. Its presence should be read together with the inscription and the individual memorial context.", "source_checked", "Conservative symbol explanation."),
    "AUDIO_29_DE": ("Dieser Stein zeigt eine Menora, den siebenarmigen Leuchter. Sie ist ein wichtiges Symbol im Judentum. Ihre Bedeutung sollte zusammen mit der Inschrift und dem individuellen Gedenkkontext gelesen werden.", "needs_de_review", "Conservative symbol explanation."),
    "AUDIO_30_EN": ("Take the time you need at this memorial. You can continue the route when you are ready.", "source_checked", "App interaction wording."),
    "AUDIO_30_DE": ("Nehmen Sie sich die Zeit, die Sie an diesem Gedenkzeichen brauchen. Sie können die Route fortsetzen, wenn Sie bereit sind.", "needs_de_review", "App interaction wording."),
}


def normalize(text):
    if isinstance(text, dict):
        text = text.get("text", "")
    text = text or ""
    # The research export contains UTF-8 text that was decoded as Latin-1 in
    # some inscription cells. Repair only unmistakable mojibake markers.
    for _ in range(2):
        if not any(marker in text for marker in ("Ã", "â€", "Â")):
            break
        try:
            encoded = bytearray()
            for character in text:
                try:
                    encoded.extend(character.encode("cp1252"))
                except UnicodeEncodeError:
                    # A few cells contain a C1 byte decoded literally.
                    if ord(character) <= 0xFF:
                        encoded.append(ord(character))
                    else:
                        raise
            text = bytes(encoded).decode("utf-8")
        except (UnicodeEncodeError, UnicodeDecodeError):
            break
    return " ".join(text.replace("\u201a", "'").replace("â€ž", "„").replace("â€œ", "“").split())


def add(entries, seen, entry):
    entry["text"] = normalize(entry.get("text"))
    if not entry["text"] or entry["id"] in seen:
        return
    seen.add(entry["id"])
    entries.append(entry)


def main():
    runtime = json.loads(RUNTIME.read_text(encoding="utf-8"))
    canonical = json.loads(CANONICAL.read_text(encoding="utf-8"))
    individual = json.loads(INDIVIDUAL.read_text(encoding="utf-8-sig"))

    entries, seen = [], set()
    canonical_aliases = {}
    for item in canonical["entries"]:
        for alias in item.get("legacyAliases", []):
            canonical_aliases[alias] = item

    # Every existing catalog ID is retained. Source-checked canonical copy
    # replaces only explicit aliases; all other entries stay traceable legacy
    # content rather than being silently rewritten.
    for item in runtime["dialogues"]:
        source = canonical_aliases.get(item["id"])
        add(entries, seen, {
            "id": item["id"],
            "text": source["text"] if source else item["text"],
            "language": "DE" if item["id"].endswith("_DE") else "EN",
            "category": source["category"] if source else item.get("type", "legacy"),
            "script_status": source["status"] if source else "preserved_legacy_pending_source_check",
            "source": source["source"] if source else "Existing runtime catalog; retained pending source-specific review.",
        })

    # Add aliases not represented in the old 97-entry config.
    for item in canonical["entries"]:
        for alias in item.get("legacyAliases", []):
            add(entries, seen, {
                "id": alias,
                "text": item["text"],
                "language": item["language"],
                "category": item["category"],
                "script_status": item["status"],
                "source": item["source"],
            })

    # Individual records are transcribed only from the dataset or an existing
    # localized DialogueSequence subtitle. Empty or disputed source candidates
    # remain absent so the pre-existing asset is preserved untouched.
    for item in individual["items"]:
        for language in ("en", "de"):
            candidate = item["database_inscription_candidates"].get(language)
            sequence = item["sequence_subtitle_candidates"].get(language)
            text = candidate or sequence
            if not text or item["editorial_status"].startswith("NO_SAFE"):
                continue
            suffix = language.upper()
            add(entries, seen, {
                "id": f"{item['runtime_id']}_{suffix}",
                "text": text,
                "language": suffix,
                "category": item["source_type"],
                "script_status": "source_transcription" if candidate else "sequence_transcript_pending_source_check",
                "source": "Bergen_Belsen_Database.json inscription" if candidate else "Existing localized DialogueSequence subtitle",
            })

    for clip_id, text in SAFE_FALLBACKS.items():
        for entry in entries:
            if entry["id"] == clip_id:
                entry.update({
                    "text": text,
                    "category": "mass_grave",
                    "script_status": "safe_interface_context",
                    "source": "Neutral accessibility wording; no unsupported historical detail.",
                })
                break

    for clip_id, (text, status, source) in REVIEWED_OVERRIDES.items():
        for entry in entries:
            if entry["id"] == clip_id:
                entry.update({"text": text, "script_status": status, "source": source})
                break

    payload = {
        "schema_version": 1,
        "purpose": "Authoritative transcript for reviewed/regenerated clips; legacy clips without a safe source remain untouched.",
        "dialogues": entries,
    }
    OUTPUT.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"Wrote {len(entries)} entries to {OUTPUT}")


if __name__ == "__main__":
    main()

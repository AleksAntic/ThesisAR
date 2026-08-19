# Audio & TTS Pipeline — Bergen-Belsen AR Guide

This directory contains the complete offline toolchain to generate multilingual narration audio, bake dialogue ScriptableObjects, and synchronize text subtitles with Unity's runtime catalog.

---

## Pipeline Tools Overview

| Script / File | Purpose |
|---|---|
| **`generate_audio_with_caching.py`** | Connects to the **ElevenLabs TTS API** (multilingual v2) to generate voice narrations in English, German, and Hebrew. Implements SHA-256 content hashing to cache existing `.mp3` clips and avoid redundant API charges. |
| **`bake_historical_dialogues.py`** | Parses `dialogues_config.json` and `Bergen_Belsen_MASTER.json`, computes timing metrics, and formats audio catalogues for `ThesisAR/Assets/Resources/`. |
| **`bake_single_audio.py`** | Quick utility to generate a single targeted narration clip without executing the full batch. |
| **`dialogues_config.json`** | Configuration manifest mapping stop IDs to localized titles, text transcripts, and voice settings. |
| **`Bergen_Belsen_MASTER.json`** | Master spatial database with all 100+ memorial stones, mass graves, inscriptions, and person biographies. |

---

## How to Generate New Narration Audio

### 1. Requirements
Ensure Python 3.10+ is installed and install the required dependencies:
```bash
pip install requests pydub python-dotenv
```

### 2. Configure API Key
Set your ElevenLabs API key in your environment or a `.env` file in this directory:
```bash
export ELEVENLABS_API_KEY="your_api_key_here"
```

### 3. Add or Modify Dialogues
Open `dialogues_config.json` and add/edit the dialogue entry:
```json
{
  "id": "Stone_A1",
  "category": "MemorialStone",
  "titleEN": "Memorial Stone A1",
  "titleDE": "Gedenkstein A1",
  "textEN": "This memorial stone honors...",
  "textDE": "Dieser Gedenkstein erinnert an..."
}
```

### 4. Run Generation
Run the caching generator script:
```bash
python generate_audio_with_caching.py
```
Generated `.mp3` files are saved to `ThesisAR/Assets/Resources/NarrationAudio/` (or `GuidanceAudio/`).

### 5. Bake Dialogue Catalogs for Unity
Execute the dialogue baker:
```bash
python bake_historical_dialogues.py
```
This automatically updates `ThesisAR/Assets/Resources/audio_runtime_catalog.json` and `ui_localization.json`, enabling immediate playback in the Unity app.

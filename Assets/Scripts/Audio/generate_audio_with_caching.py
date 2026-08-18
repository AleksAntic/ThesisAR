import os
import json
import time
from elevenlabs.client import ElevenLabs

# ─── CONFIGURAZIONE API ELEVENLABS ───
API_KEY = os.environ.get("ELEVENLABS_API_KEY")
DEFAULT_VOICE_ID = "QMJTqaMXmGnG8TCm8WQG" # Cached voice identifier
MODEL_ID = "eleven_v3" # Production model optimized for English inflections

INPUT_JSON_FILE = "dialogues_config.json"
OUTPUT_FOLDER = "audio_output"

def run_audio_bake_pipeline():
    if not API_KEY:
        print("[ERROR] Set the ELEVENLABS_API_KEY environment variable before generating audio.")
        return

    # Initialize the ElevenLabs Client using the updated SDK layout
    client = ElevenLabs(api_key=API_KEY)

    if not os.path.exists(INPUT_JSON_FILE):
        print(f"[ERROR] Input configuration file '{INPUT_JSON_FILE}' not found.")
        return

    if not os.path.exists(OUTPUT_FOLDER):
        os.makedirs(OUTPUT_FOLDER)
        print(f"[INFO] Created target output directory: {OUTPUT_FOLDER}")

    with open(INPUT_JSON_FILE, 'r', encoding='utf-8') as f:
        try:
            dialogue_entries = json.load(f)
        except json.JSONDecodeError as e:
            print(f"[CRITICAL] JSON syntax error in '{INPUT_JSON_FILE}': {e}")
            return

    total_items = len(dialogue_entries)
    print(f"[START] Indexed {total_items} dialogue lines for evaluation.")

    # Step 1: Pre-audit scan to calculate token usage parameters
    total_characters = 0
    skipped_count = 0
    to_process_entries = []

    for entry in dialogue_entries:
        entry_id = entry.get("id", "").strip()
        text_content = entry.get("text", "").strip()
        
        if not entry_id or not text_content:
            print(f"[WARNING] Skipping malformed row: missing ID or Text context.")
            continue

        filename = f"{entry_id}.mp3"
        destination_path = os.path.join(OUTPUT_FOLDER, filename)

        # 🛑 CACHING GUARDIAN LAYER: Aborts processing if file already exists
        if os.path.exists(destination_path):
            skipped_count += 1
        else:
            total_characters += len(text_content)
            to_process_entries.append((entry_id, text_content, destination_path, entry.get("voice_id", DEFAULT_VOICE_ID)))

    print(f"\n[PIPELINE AUDIT REPORT]")
    print(f" └─ Already Generated (Skipped - 0 Token Cost): {skipped_count} files")
    print(f" └─ New Lines to Bake: {len(to_process_entries)}")
    print(f" └─ Estimated Token Expenditure: {total_characters} characters")
    print(f"==============================================================\n")

    if not to_process_entries:
        print("[FINALIZE] No new files to generate. Cache completely up to date.")
        return

    # Step 2: Live conversion execution sequence
    for index, (entry_id, text, output_path, voice) in enumerate(to_process_entries):
        print(f"► Baking file [{index + 1}/{len(to_process_entries)}] -> {entry_id}.mp3")
        print(f"  Text: \"{text[:65]}...\"")

        success = execute_tts_generation(client, text, voice, output_path)

        if not success:
            print(f"[FATAL FAILURE] Generation stopped at token ID {entry_id}. Check remaining credits.")
            break

        # Safety delay interval between background HTTP request streams to avoid API rate limiting
        time.sleep(0.5)

    print("\n[PIPELINE COMPLETE] Process finalized successfully. Transfer files to Unity Assets folder.")

def execute_tts_generation(client, text_to_say, voice_id, output_file_path):
    """
    Executes the streaming chunk conversion using ElevenLabs Text-to-Speech API
    and saves the raw byte array into a localized audio file.
    """
    try:
        # Use the updated text_to_speech API conversion protocol
        audio_chunks_generator = client.text_to_speech.convert(
            text=text_to_say,
            voice_id=voice_id,
            model_id=MODEL_ID
        )

        # Write data chunk-by-chunk to stream into memory efficiently
        with open(output_file_path, "wb") as audio_file:
            for chunk in audio_chunks_generator:
                if chunk:
                    audio_file.write(chunk)
        return True

    except Exception as ex:
        print(f"  └─ [ERROR] API conversion failure: {ex}")
        return False

if __name__ == "__main__":
    run_audio_bake_pipeline()

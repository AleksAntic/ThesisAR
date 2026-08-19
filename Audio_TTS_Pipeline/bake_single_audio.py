import os
import sys
from elevenlabs.client import ElevenLabs

# Legge gli argomenti passati da Unity C#
# sys.argv[1] = ID audio (es. DLG_A1)
# sys.argv[2] = Testo in inglese
# sys.argv[3] = Cartella di destinazione di Unity

if len(sys.argv) < 4:
    print("[Python Error] Missing command line arguments from Unity context.")
    sys.exit(1)

AUDIO_ID = sys.argv[1]
TEXT_TO_SAY = sys.argv[2]
DESTINATION_FOLDER = sys.argv[3]

API_KEY = os.environ.get("ELEVENLABS_API_KEY")
DEFAULT_VOICE_ID = "QMJTqaMXmGnG8TCm8WQG"
MODEL_ID = "eleven_v3"

def main():
    if not API_KEY:
        print("[Python Error] Set ELEVENLABS_API_KEY before generating audio.", file=sys.stderr)
        sys.exit(1)

    client = ElevenLabs(api_key=API_KEY)
    
    if not os.path.exists(DESTINATION_FOLDER):
        os.makedirs(DESTINATION_FOLDER)

    target_file_path = os.path.join(DESTINATION_FOLDER, f"{AUDIO_ID}.mp3")

    # Guardiano di Caching: se per qualche motivo il file esiste, non buttiamo token
    if os.path.exists(target_file_path):
        print(f"[Python Cache] File {AUDIO_ID}.mp3 already baked. Skipping API call.")
        return

    try:
        audio_generator = client.text_to_speech.convert(
            text=TEXT_TO_SAY,
            voice_id=DEFAULT_VOICE_ID,
            model_id=MODEL_ID
        )

        with open(target_file_path, "wb") as f:
            for chunk in audio_generator:
                if chunk:
                    f.write(chunk)
                    
        print(f"[Python Success] Successfully baked and delivered: {AUDIO_ID}.mp3")

    except Exception as e:
        print(f"[Python Exception Error] API Failed for {AUDIO_ID}: {e}", file=sys.stderr)
        sys.exit(1)

if __name__ == "__main__":
    main()

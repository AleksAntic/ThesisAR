import os
import json
import time
from elevenlabs.client import ElevenLabs

# ─── CONFIGURAZIONE PARAMETRI ELEVENLABS ───
# Utilizza le tue credenziali e il modello v3 impostati nel precedente script
API_KEY = os.environ.get("ELEVENLABS_API_KEY")
DEFAULT_VOICE_ID = "QMJTqaMXmGnG8TCm8WQG" #
MODEL_ID = "eleven_v3" #

INPUT_MASTER_JSON = "Bergen_Belsen_MASTER.json"
OUTPUT_AUDIO_FOLDER = "audio_output"

def run_master_audio_bake():
    if not API_KEY:
        print("[ERRORE] Imposta la variabile d'ambiente ELEVENLABS_API_KEY prima di generare audio.")
        return

    # Inizializzazione del client ufficiale ElevenLabs
    client = ElevenLabs(api_key=API_KEY)

    if not os.path.exists(INPUT_MASTER_JSON):
        print(f"[ERRORE] Il file database '{INPUT_MASTER_JSON}' non è stato trovato nella cartella corrente.")
        return

    if not os.path.exists(OUTPUT_AUDIO_FOLDER):
        os.makedirs(OUTPUT_AUDIO_FOLDER)
        print(f"[INFO] Creata la cartella di output: {OUTPUT_AUDIO_FOLDER}")

    # Caricamento del database di Bergen-Belsen
    with open(INPUT_MASTER_JSON, 'r', encoding='utf-8') as f:
        try:
            master_data = json.load(f)
        except json.JSONDecodeError as e:
            print(f"[ERRORE CRITICO] Errore di sintassi nel file JSON MASTER: {e}")
            return

    # Il foglio Excel convertito si trova sotto la chiave "Sheet1"
    rows = master_data.get("Sheet1", [])
    if not rows:
        print("[ERRORE] La chiave 'Sheet1' è vuota o non è stata trovata nel JSON.")
        return

    print(f"[INFO] Indicizzati {len(rows)} elementi totali dal database. Inizio filtraggio testi...")

    valid_bake_list = []
    skipped_empty_text = 0
    skipped_already_cached = 0
    total_characters_count = 0

    for entry in rows:
        # 1. Estrazione dinamica dell'ID unico della lapide/monumento
        # Cerca tra i vari formati possibili del foglio Excel (Stone, Other Memorial, Mass Grave)
        item_id = (entry.get("Memorial Stone") or 
                   entry.get("Other Mem Number") or 
                   entry.get("Mass Grave Number") or 
                   "").strip()

        if not item_id:
            # Salta righe di scarto o puramente descrittive senza un ID di mappatura
            continue

        # 2. Estrazione e sanitizzazione del testo in inglese
        # Controlla sia il campo flat che quello nidificato per massima sicurezza
        text_english = entry.get("Text_English", "")
        if not text_english and "inscriptions_by_language" in entry:
            text_english = entry.get("inscriptions_by_language", {}).get("text_english", "")

        # Pulisce gli spazi bianchi
        text_english = str(text_english).strip()

        # 🛑 FILTRO DI SICUREZZA: Se la lapide non ha testo dicono qualcosa, viene scartata qui
        if not text_english or text_english.lower() == "nan" or text_english == "":
            skipped_empty_text += 1
            continue

        # 3. Controllo della Cache locale per salvaguardare i token
        filename = f"{item_id}.mp3"
        destination_path = os.path.join(OUTPUT_AUDIO_FOLDER, filename)

        if os.path.exists(destination_path):
            skipped_already_cached += 1
            continue

        # Se supera tutti i controlli, è un candidato valido per il bake
        total_characters_count += len(text_english)
        valid_bake_list.append((item_id, text_english, destination_path))

    # 📊 REPORT DI AUDIT PRE-BAKE
    print("\n╔════════════════════════════════════════════════════════════")
    print("║ 📊 REPORT COMPLESSIVO ANALISI DATABASE NARRATIVO")
    print("╠════════════════════════════════════════════════════════════")
    print(f"║ 🚫 Righe senza testo in inglese (Scartate a costo 0): {skipped_empty_text}")
    print(f"║ 💾 File audio già presenti in cache (Saltati a costo 0): {skipped_already_cached}")
    print(f"║ 🎤 Nuovi file audio da generare tramite ElevenLabs: {len(valid_bake_list)}")
    print(f"║ 🪙 Consumo stimato dei crediti: {total_characters_count} caratteri")
    print("╚════════════════════════════════════════════════════════════\n")

    if not valid_bake_list:
        print("[FINE] Non ci sono nuovi file audio da generare. Tutto l'archivio è aggiornato!")
        return

    # 4. Esecuzione dei cicli di generazione reali su ElevenLabs
    for index, (item_id, text, output_path) in enumerate(valid_bake_list):
        print(f"► Generating [{index + 1}/{len(valid_bake_list)}] ➔ {item_id}.mp3")
        print(f"   Text: \"{text[:70]}...\"")

        success = generate_voice_file(client, text, DEFAULT_VOICE_ID, output_path)

        if not success:
            print(f"[INTERRUZIONE] Impossibile generare l'audio per il nodo {item_id}. Controlla i token rimasti.")
            break

        # Piccola pausa di sicurezza per rispettare i limiti di rate limit dell'API
        time.sleep(0.5)

    print("\n[PIPELINE TERMINATA] Tutti i file disponibili sono stati processati con successo.")

def generate_voice_file(client, text_to_say, voice_id, output_file_path):
    """
    Esegue la chiamata di sintesi vocale convertendo il testo in un flusso di byte audio.
    """
    try:
        # Metodo ufficiale aggiornato dell'SDK di ElevenLabs
        audio_chunks_iterable = client.text_to_speech.convert(
            text=text_to_say,
            voice_id=voice_id,
            model_id=MODEL_ID
        )

        # Scrittura progressiva dei chunk binari sul file locale
        with open(output_file_path, "wb") as audio_file:
            for chunk in audio_chunks_iterable:
                if chunk:
                    audio_file.write(chunk)
        return True

    except Exception as ex:
        print(f"   └─ [ERRORE API] Errore durante la conversione: {ex}")
        return False

if __name__ == "__main__":
    run_master_audio_bake()

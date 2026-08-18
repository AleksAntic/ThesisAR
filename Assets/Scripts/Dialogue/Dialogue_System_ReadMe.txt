================================================================================
📄 BERGEN-BELSEN COMPANION AR - NARRATIVE & UX ARCHITECTURE SPECIFICATIONS
================================================================================

Questo documento descrive il funzionamento logico, prossemico e temporale del
sistema di dialoghi e della guida AR, definendo i requisiti degli asset audio.

--------------------------------------------------------------------------------
1. LA FILOSOFIA PROSSEMICA: IL DYNAMIC LEAD (AVATAR FRONTALE)
--------------------------------------------------------------------------------
L'avatar si posiziona e cammina sempre DAVANTI all'utente lungo il NavMesh. 
Questo soddisfa due requisiti fondamentali di UX Mobile AR:
- Mantiene l'asset 3D visibile all'interno del frustum della fotocamera dello smartphone.
- Funge da vera guida visiva sul campo, indicando la direzione da seguire.

Se l'utente accelera, la guida aumenta la velocità; se si ferma, la guida si 
arresta a 3-4 metri, si volta verso l'utente e passa in uno stato di Idle.

--------------------------------------------------------------------------------
2. LA GERARCHIA NARRATIVA A TRE LIVELLI
--------------------------------------------------------------------------------
Il sistema organizza i contenuti audio in tre cerchi concentrici per adattarsi 
naturalmente al ritmo del visitatore:

A) LIVELLO MACRO (In Cammino / Global_Walking):
   - Attivo durante i tempi morti di spostamento tra punti distanti.
   - Tratta argomenti generali, storici e di contesto su tutta l'area di Bergen-Belsen.
   - Gestito dall'algoritmo Time-Slicer (Vedi Sezione 3).

B) LIVELLO MESO (Di Soglia / Zone_SocioCultural):
   - Attivato da trigger geografici volumetrici (DialogueZoneTrigger).
   - Commenta macro-aree specifiche quando l'utente le attraversa (es. zona baracche).
   - Ha la precedenza sul livello Macro, interrompendolo con un raccordo fluido.

C) LIVELLO MICRO (Di Focus / Micro_StoneFocus):
   - Attivo quando l'utente raggiunge la sosta solenne presso una lapide o fossa comune.
   - Il movimento si ferma; il tono diventa intimo, biografico e commemorativo.
   - Ha la priorità assoluta su tutti gli altri livelli.

--------------------------------------------------------------------------------
3. ALGORITMO TIME-SLICER & MODALITÀ DI AVANZAMENTO
--------------------------------------------------------------------------------
Quando il RouteManager traccia un percorso, calcola la distanza e la divide per 
la velocità della guida (1.3 m/s), ottenendo i secondi totali di viaggio. 
Lo scheduler esegue un algoritmo di selezione (Knapsack) per riempire lo slot 
temporale con pillole storiche dal Pool Globale, inserendo un silenzio di sicurezza 
di 2 secondi tra una clip e l'altra.

Preferenze UI configurabili dall'utente tramite smartphone:
- AUTOMATICO: Le clip del Time-Slicer avanzano da sole dopo la pausa di silenzio.
- SEMI-AUTOMATICO: La guida recita una battuta, poi attende l'input del touch screen 
  (pulsante "Chiedi di più") prima di procedere con la pillola successiva.

--------------------------------------------------------------------------------
4. GESTIONE DEL DETOUR (DEVIAZIONE IMPROVVISA)
--------------------------------------------------------------------------------
Se l'utente devia dal percorso pianificato per avvicinarsi a una lapide imprevista:
1. Il sistema esegue un fade-out di 0.4s sull'audio in corso per evitare tagli brutali.
2. Inietta una clip audio di raccordo casuale (Vedi checklist sotto).
3. L'avatar calcola una nuova posizione NavMesh di fronte all'utente per accoglierlo.
4. Viene avviato il dialogo Micro specifico del nuovo punto.

--------------------------------------------------------------------------------
5. CHECKLIST DEGLI ASSET AUDIO RICHIESTI & NOMENCLATURA
--------------------------------------------------------------------------------
Tutti i file audio devono essere inseriti nella cartella "Assets/Resources/GuidanceAudio/"
e mappati all'interno dei relativi ScriptableObjects (DialogueSequence).

📁 SOTTO-CARTELLA: Narrative/GlobalWalking (Asset dei dialoghi generici in cammino)
   - Struttura: File brevi con durata compresa tra i 10 e i 25 secondi.
   - Esempi di contenuto: Introduzione al sistema dei campi, vita quotidiana, liberazione.
   - Nota: Compilare accuratamente il campo "Duration" nell'Inspector di ogni linea.

📁 SOTTO-CARTELLA: Detour_Anchors (Frasi di raccordo per interruzioni)
   - Assegnare questi file direttamente allo slot "Detour Anchor Clips" del DialogueManager.
   - File richiesti (Audio di 2-3 secondi):
     * detour_arrival_01.mp3 ("Eccoci, fermiamoci pure qui...")
     * detour_arrival_02.mp3 ("Vedo che ti sei avvicinato a questo settore...")
     * detour_arrival_03.mp3 ("Prenditi pure un momento, questa lapide è importante...")

📁 SOTTO-CARTELLA: Resources/GuidanceAudio/ (ID specifici del Database JSON)
   - I file audio delle singole lapidi e fosse comuni devono corrispondere all'ID esatto.
   - Esempi:
     * F8.mp3 (Audio biografia per la lapide F8)
     * MG1.mp3 (Audio descrittivo per la Fossa Comune Mass Grave 1)
     * WELCOME_EN.mp3 (Audio di benvenuto iniziale del geofencing)

--------------------------------------------------------------------------------
6. SCHEMA JSON AGGIORNATO & IMPORTER AUTOMATICO
--------------------------------------------------------------------------------
A seguito di una revisione, lo schema di "dialogues_config.json" è stato corretto
per allinearsi esattamente al codice C#. Ogni record ora contiene:

   * "type": deve combaciare ESATTAMENTE con l'enum DialogueCategory in C#:
     Global_Walking, Zone_SocioCultural, Micro_StoneFocus, Detour_Transition,
     Systemic_UI (quest'ultimo aggiunto per i messaggi di sistema come WELCOME/
     GOODBYE/LOGISTICS, che prima non avevano un valore enum corrispondente).

   * "text_ui": il testo originale (con a capo, virgolette, ecc.) mostrato come
     sottotitolo a schermo.
   * "text_spoken": versione ripulita del testo, pronta per ElevenLabs (senza
     caratteri che verrebbero letti ad alta voce in modo innaturale).

   * "duration": placeholder a 0.0, da compilare a mano una volta generato il
     file audio reale e conosciuta la sua durata esatta in secondi. Finché è a
     0, l'importer segnala l'entry in Console come "ancora da completare".

   * NOMENCLATURA AUDIO: il nome file atteso in Resources/GuidanceAudio/ è
     "{id}.mp3", dove "id" ora è descrittivo del contenuto (es. "TreeBroken_EN",
     "ExchangeCamp_DE") invece del vecchio "AUDIO_07_EN" opaco. Le lapidi reali
     (A3, A4, A5) e i messaggi di sistema (WELCOME_EN, ecc.) mantengono i loro
     nomi originali, già corretti.

IMPORTER: "DialogueConfigImporter.cs" (menu Unity: ThesisAR > Import Dialogues
From JSON) legge "Assets/Resources/DialogueAssets/dialogues_config.json" e crea/
aggiorna automaticamente un asset DialogueSequence per ogni entry (una riga per
sequenza), collegando l'AudioClip corrispondente per nome file e stampando in
Console la lista di audio mancanti e delle durate ancora a 0.
================================================================================
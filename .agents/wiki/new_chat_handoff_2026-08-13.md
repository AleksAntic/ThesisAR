# ThesisAR — handoff operativo (13 agosto 2026)

## Obiettivo immediato

Chiudere la finalizzazione in modo chirurgico. Non riaprire lavoro gia' accettato senza un bug osservabile. Deadline percepita dall'utente: circa due giorni.

## Regole di lavoro importanti

- Leggere prima `AGENTS.md`, `CODEX_BRIEFING.md` (nella cartella progetto padre), `.agents/wiki/hot.md` e `.agents/wiki/finalization_backlog.md` (cartella progetto padre).
- Usare `point_<ID>` / i relativi `Geom3D` come unica sorgente spaziale. I baked visual non sono affidabili per posizione, altezza o rotazione.
- Non scrivere mai direttamente `Assets/Scenes/emptyy.unity` mentre Unity e' aperto. Una modifica diretta causa il prompt “scene changed on disk”, rischiando le modifiche manuali dell'utente. Per configurare la scena usare l'Editor/MCP e salvare solo con consapevolezza; per preferenze non essenziali usare codice con fallback.
- Evitare grandi refactor e nuove dipendenze. Dopo ogni modifica C# eseguire una ricompilazione Unity; l'MCP puo' andare in timeout anche se il compilatore e' sano, quindi ritentare una volta.

## Blocco appena concluso: mappa 2D e marker

File principale: `Assets/Scripts/Map/Map2DController.cs`.

- La legenda e i marker usano: quadrato blu = memorial stones, triangolo arancione = mass graves, cerchio rosa = other memorials.
- La vista generale usa cluster: il badge scuro riepiloga per categoria, p.es. `square 12, triangle 2`. A maggiore zoom i cluster si sciolgono nei marker puntuali.
- I cluster sono ora fusi per prossimita' e per collisione di badge; una risoluzione UI sposta i badge cluster finche' non si sovrappongono. L'utente lo considera “non perfetto, ma accettabile”. Non dedicare altro tempo alla cosmetica della mappa salvo un nuovo bug concreto.
- Il marker GPS utente ha il proprio disco blu creato a runtime (`GetRuntimeUserMarkerSprite`) e il cono direzionale rimane il figlio `UserGazeCone`. Non deve usare il quadrato/`White Stop` dei marker.
- Il tap su mappa continua a usare i singoli `InteractiveMapPin`; il comportamento dei mini-popup sui cluster non e' stato modificato ed e' una piccola incoerenza nota. Non affrontarla ora salvo richiesta esplicita: la soluzione UX corretta sarebbe tap su cluster => zoom, tap su marker singolo => mini-popup.

## Blocco tour personale

- `TourManager` ha `Continue tour`: al termine della narrazione personale mostra il pulsante e non auto-avanza. `PersonalGuidance.HandleNarrationFinished()` chiama `TourManager.WaitForVisitorToContinue()`.
- `EditorCharacterController` e' editor-only. Il tasto `T` e' solo test: teletrasporta e invoca `PersonalGuidance.ForceArrivalForEditorTesting`, quindi segue narrazione/sottotitoli/pulsante. Non esiste nella build Android.
- L'approccio rispettoso della guida usa 2.75 m laterali e clearance 0.25 m dagli altri anchor. Audit precedente: 171 anchor, 1.220 sample, 1.216 posizioni valide; non aggiungere NavMeshModifier direttamente agli anchor GLB: la scala gerarchica amplifica i volumi e puo' cancellare la NavMesh.
- Ancora da validare manualmente: primi due stop di un tour personale, avatar, path, gesture, audio, nessuna sovrapposizione audio e azione del pulsante Continue tour.

## GLB, marker e modelli

- Nota dettagliata: `.agents/wiki/glb_scale_and_marker_notes.md`.
- Il GLB geolocation plan e' byte-identico alla copia dati; tutti i 169 `point_*` scene matchano il sorgente, salvo un arrotondamento YAML di 2.5 cm per MG13.
- `Geom3D` (dischi blu) e `point_*` sono corretti. `[BakedVisual]_*` e' solo ausilio editoriale, con pivot/altezza/rotazione inaffidabili. Non usarlo per NavMesh, GPS/AR o tour.
- Il reference GLB e' disabilitato e deve rimanere tale in play/build.

## Audio e testi

- Revisione/editorial planning esiste in `.agents/wiki/audio_canonical_draft.json`, `audio_drafts_writer.md`, `audio_source_facts.md` e `individual_audio_scripts.json`.
- Il manifest individuale copre 172 runtime IDs; non rigenerare automaticamente gli elementi marcati `NO_SAFE_SCRIPT_CANDIDATE__DO_NOT_REGENERATE`.
- Le voci/audio non vanno riaperti ora senza una richiesta precisa dell'utente: la parte e' stata considerata provvisoriamente a posto dopo compilazione.

## Prossime priorita' (ordine)

1. **Core UI state machine**: test cumulativo di close/back (database -> detail -> inspector; map -> mini-popup -> detail; settings; Site History). Deve esserci un solo pannello bloccante; side bar drawer; nessun pannello residuo. Non modificare finche' non si riproduce un bug.
2. **Test tour personale** sopra descritto, quindi Intermediate e Impersonal con un memoriale.
3. **Survey, consenso e telemetria**: l'utente fornira' file di professori/slide per normativa e questionario. Non inventare un Google Form definitivo prima di leggerli. Servono consenso separato per partecipazione, telemetria opzionale, collegamento pseudonimo survey-sessione. Il webhook Apps Script va configurato e provato solo con consenso. `SurveyReminderManager` e' ancora un placeholder/toast, non una notifica Android durevole.
4. **Android/device final QA**: build, install, GPS/ARCore/VPS, audio, UI, consenso, recupero JSON, download GLB/offline. Usare `android-emulator-qa` quando disponibile e pertinente.
5. Solo dopo: mini scenario di test in stanza/esterno (avatar, tutorial, GPS) senza campo completo. L'utente ha esplicitamente chiesto di rimandarlo alle ultime fasi.

## Questionario / ricerca: decisioni e idee da conservare

- Il questionario puo' avere una parte tesi essenziale e una parte opzionale quality/UX; conservare un catalogo di domande per farlo revisionare ai due professori.
- Le domande condizionali devono sfruttare telemetria dell'app: cambi frequenti di modalita', cambio dopo una zona, abbandono/riattivazione dell'app, fruizione in gruppo, ragione del cambio (ambiente, preferenza, problema tecnico, batteria ecc.). Mai tracciare l'utente fuori dall'app.
- Alla fine dell'esperienza servira' un pannello di ringraziamento/consenso e invito al questionario, con scelta “ora o dopo”; indicare che farlo presto e' preferibile. Le notifiche differite richiedono una soluzione Android reale, non il toast attuale.
- Possibili costrutti: utilita' e appropriatezza della guida/avatar, wayfinding, comprensione, presenza/cognitive load/empatia (solo misure adattate se non sono scale complete), tono/voce, percezione del valore futuro dell'AR nei memoriali/musei. Non chiamare SUS/IPQ standard senza gli item e lo scoring completi.

## Stato di compilazione

L'ultima compilazione verificata prima dell'handoff e' risultata pulita: 0 errori, 0 warning. L'MCP Unity e' intermittente; timeout/connection failure non equivalgono automaticamente a un errore C#.

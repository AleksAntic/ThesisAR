# ThesisAR — UI Interaction Audit

Date: 2026-08-11  
Scope: Unity scene `emptyy`, UI/navigation, audio/subtitles, map/routes, settings, tutorial, and the three guidance modes.

## Evidence and limits

This is a combined UX and accessibility audit based on the current code paths and the map screenshot supplied on 2026-08-11. Unity MCP did not return a live hierarchy/screenshot during this audit, so runtime-only behaviours must be confirmed in the final device test. This is not a WCAG conformance claim.

## User goals

1. Find and understand a memorial.
2. Hear its narration without losing context.
3. Choose, create, and follow a route.
4. Adjust language, readability, privacy, and tutorial preferences safely.

## Flow health

| # | Flow | Health | Evidence / conclusion |
|---|---|---|---|
| 1 | First launch and tutorial | Needs runtime test | Coachmark blocks background interaction intentionally; replay exists. Verify skip, replay, and persistent completion state. |
| 2 | Sidebar to Search / Map / Settings | Moderate risk | `UIManager` uses a stack but mixes stack transitions and direct `SetActive`; each close path needs a deterministic destination. |
| 3 | Search, result selection, memorial detail | Moderate risk | Search returns matching context; detail can return to previous panel. Audio-close behaviour was recently changed and needs one end-to-end device test. |
| 4 | Detail narration and subtitles | Moderate risk | Persistent audio host and subtitle suspension are appropriate. Verify closing detail preserves narration and restores subtitles exactly once. |
| 5 | Symbol badges and secondary narration | Needs runtime test | Playback path exists; confirm unavailable clips communicate a clear state and do not change the main narration unexpectedly. |
| 6 | 3D inspector | Needs runtime test | It is a nested modal flow; verify Close returns to the expected detail state and never leaves an invisible stack entry. |
| 7 | Map browsing and marker selection | High risk | Current screenshot confirms marker scale, duplicate legend, and colour classification were not visually correct. Code correction is now applied; visual verification remains required. |
| 8 | Official tour, Modify, Guide Me | Moderate risk | Tour editing and route insertion work in the reported test. Verify route line, numbering, stop/start state, and route completion. |
| 9 | Personal / Intermediate / Impersonal guidance | High risk | The modes share audio, avatar, navigation, and panel state. Full device walk tests are required; current source contains multiple time-based and direct-state transitions. |
| 10 | Advanced settings, survey, GPS | Moderate risk | Preference persistence exists. Verify every slider updates real text, reopening reflects applied values, GPS states match permission/service state, and survey returns safely. |

## Full interaction inventory

| Surface / system | User actions covered | Main failure modes to test | Priority |
|---|---|---|---|
| Permission and session start | camera prompt, Start Experience, mode choice | denied permission loop, UI active before AR readiness, wrong default mode | P0 |
| Geospatial / VPS status | GPS badge, VPS state, poor-accuracy state | badge says ready while location is stale, status message blocks another action | P0 |
| AR memorial detection | viewport detection banner, tap prompt, deselect | wrong memorial selected, banner survives a panel change, accidental tap-through | P0 |
| Memorial detail | open from AR, search, or map; previous/next person; close | stale record data, incorrect return destination, arrows hidden after a modal | P0 |
| Detail audio | play, pause, resume, close while active | visual play state out of sync, audio leaks across a new memorial, subtitles not restored | P0 |
| Symbol narration / Ask More | show topics, play symbol, repeat, camp-info topic | no clip feedback, competing narration, inaccessible/unclear badge label | P1 |
| 3D inspector | download/load model, rotate/inspect, close | loading state missing, failed download gives no recovery, invisible overlay blocks detail | P0 |
| Search and filters | free text, category, symbols, clear, select result | result does not explain match, tiny text, no-results state, filter persistence surprise | P1 |
| Map | pan, pinch/scroll, select marker, mini popup, close | marker overlap, colour/legend mismatch, popup follows wrong point, missed tap targets | P0 |
| Route construction | Create Route, add/remove stop, optimise, save | route cleared unexpectedly, order not visible, optimisation changes intent silently | P0 |
| Official tours | select Minimal/Intermediate/Complete, Modify, Stop Modifying | route line missing, edit state label wrong, reset leaves stale numbers | P0 |
| Tour navigation | Guide Me, teleport, pause/wait, arrive, advance, complete | avatar not moving, impossible-to-understand wait state, next stop starts too early | P0 |
| Personal guidance | summon, follow, detour, talking animation | avatar on memorial/building, arm IK points at ground, pacing deadlock | P0 |
| Intermediate guidance | summon hologram, narration, Ask More, dismiss | duplicate avatar, talk animation persists, wrong source/audio language | P0 |
| Impersonal guidance | route / subtitles / arrows | user expects an avatar, subtitles hidden, arrow path stale after map changes | P1 |
| Site history | dropdown open/close, chapter choice, play/stop | dropdown z-order, chapter state not reset, audio conflicts with detail audio | P1 |
| Sidebar and panel navigation | hamburger, close, database/map/settings/history | first tap ignored, stack corrupted by direct close, modal behind modal | P0 |
| Tutorial | automatic launch, highlighted tap, skip, replay | highlight target moved, background interaction leaks, replay preference inconsistent | P1 |
| Appearance and language | EN/DE/HE, three font sliders, opacity | text clips at max size, dropdown values disappear, settings value differs from applied UI | P0 |
| Consent, telemetry, and survey | consent change, diagnostics, open survey, reminder | tracking without consent, broken link does not notify, reminder unavailable on Android | P0 |
| Persistence | restart, saved settings, saved custom routes, cached models | one setting restores visually but not logically, stale route/model cache | P1 |
| Network and offline | GitHub model download, survey, telemetry upload | no offline explanation/retry, UI frozen while waiting, data loss | P0 |

## Required acceptance criteria before release

For every P0 row, document: device, language, starting state, action sequence, expected result, actual result, screenshot/log, and pass/fail. A feature is not release-ready merely because it compiles.

## Audit update — local recording and sharing gate

Telemetry is recorded and exported locally for the current session regardless of the sharing toggle, so a participant can decide to share later without losing earlier observations. Webhook upload remains gated by sharing consent. The UI copy must describe this truthfully as sharing anonymous session data, not as consent to collect data.

The survey action now fails visibly when its placeholder URL has not been replaced. When sharing is OFF it opens the configured survey without appending an anonymous identifier, mode, or duration.

## Confirmed strengths

- The app has one central UI manager, a navigation stack, localized text, persistent audio, a tutorial controller, and explicit route/tour managers.
- The map now obtains category data from `MemorialDataManager`, rather than relying only on identifiers.
- Route editing preserves the route and can insert a new stop by lowest added straight-line detour.
- Settings already persist values through `PlayerPrefs` and include a tutorial replay action.

## High-priority issues

### P0 — release blockers

1. **Map semantic encoding and clutter.** The supplied screenshot shows all markers rendered blue, excessively large, and two legends. The cause was a per-frame overwrite from the 3D material. It has been removed. Verify exactly one legend and three visible categories after compilation.
2. **Navigation-state consistency.** `UIManager` combines `OpenPanel`, direct activation, and stack pushes/pops. A close action must always have one defined result: return to the immediate parent, or return to exploration when narration continues. Audit all X buttons against that rule.
3. **Guided-tour completion.** Make the transition after a memorial explicit: narration finishes, the guide waits, user understands how to continue, then the next target begins. The current implementation waits six seconds and subtitles the instruction; validate it outdoors.

### P1 — before field study

4. **Map accessibility.** Do not rely only on colour. Add three real, high-contrast marker sprites (circle / cross / triangle) and use the same glyphs in the legend. Do not use Unicode characters as final icon assets.
5. **Map density.** At overview zoom, clustering or progressive disclosure is preferable to overlapping individual markers. If clustering is too large a change for the thesis deadline, cap size and preserve tap targets.
6. **Toast policy.** A toast must not obscure a modal, subtitle, primary CTA, or map controls. Queue non-critical messages until the modal closes; place the toast in the reserved top safe area.
7. **Text scaling.** Cap dropdown text independently where its fixed container cannot reflow; verify English, German, and Hebrew at every supported size.
8. **Unavailable audio/error states.** If a narration or symbol clip is missing, show a short plain-language message and keep controls usable.

### P2 — polish if time permits

9. **Route order choice.** Current edit behaviour inserts the lowest-detour stop. Add an explicit `Manual order / Optimise route` control only if it can be explained in one sentence and tested; otherwise retain the current predictable automatic insertion.
10. **Search result rationale.** Keep the matching excerpt on the same line as the name, at readable size, and highlight the matching term where feasible.
11. **Telemetry and survey.** Confirm consent wording, anonymity claim, survey URL/open failure, and that declined sharing suppresses non-essential telemetry.

## Recommended interaction rules

- **Full-screen modal:** replace the current panel; close returns to exactly one parent or exploration, never both.
- **Transient overlay (toast/subtitles):** does not change navigation history and never intercepts a primary control.
- **Narration:** audio can continue after detail closes; subtitle remains visible in exploration. Opening a modal suspends subtitle display but retains latest text.
- **Tour:** a selected official tour enables `Modify`; an unsaved route enables `Create Route`; active modification displays `Stop Modifying`.
- **Tutorial:** blocks underlying interaction only while a blocking step is active. Replay is an explicit settings action, not a persistent toggle.

## Final validation pass

Run on a physical Android device in EN, DE, and HE:

1. First launch, skip/replay tutorial, then open every sidebar destination.
2. Search a partial term, open a result, play/pause/close narration, reopen it, and play a symbol.
3. Open/close the 3D inspector and verify no invisible UI blocks taps.
4. Open the map at overview and zoomed-in scales; verify marker type, legend, tap target, route line, and marker order.
5. Run one official and one edited route in every guidance mode; test teleport, arrival, narration, waiting state, and completion.
6. Change each setting, close/reopen the app, and verify the displayed value equals the applied value.
7. Test permission denied, GPS unavailable, missing audio, no search results, and survey link failure.

## Immediate next implementation batch

1. Verify and tune the corrected map colours/size/legend.
2. At overview zoom, group same-category markers into count badges; restore individual markers and route order labels when zoomed in.
3. Add real circle/cross/triangle marker assets and connect them to the three data categories.
4. Treat every NavMesh partial path as unavailable; rebuild or correct the NavMesh rather than drawing a direct line through an obstacle.
5. Perform the final device validation matrix above; only then fix remaining reproducible defects.

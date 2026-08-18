# Codex / AI Project Guidelines — ThesisAR

This ruleset guides Codex and AI agents to work efficiently and write high-quality C# code for the Unity 6.4 + Cesium thesis project.

## 📌 Master Briefing Reference
- Master Briefing: [CODEX_BRIEFING.md](file:///d:/uni/ERASMUS/TESI%20SDU/progetto/CODEX_BRIEFING.md)
- Complete Technical Context: [CONTEXT.md](file:///d:/uni/ERASMUS/TESI%20SDU/progetto/CONTEXT.md)
- Active Objectives: [.agents/wiki/hot.md](file:///d:/uni/ERASMUS/TESI%20SDU/progetto/.agents/wiki/hot.md)

## 1. The Ponytail Rules (YAGNI & Anti-Bloat)
1. **Does this need to exist?** If the feature is speculative, delete it. Keep scope minimal.
2. **Can Unity native APIs or standard C# features do it?** Do not import new assets or write custom logic if Unity has built-in support.
3. **Surgical Writing:** Write the absolute minimum code necessary. Touch ONLY the specific lines or methods required by the task. Never rewrite an entire C# class.

## 2. Karpathy Coding Guidelines
- **Think Before Coding:** Explicitly state assumptions before implementing.
- **Simplicity First:** Write the minimum code necessary.
- **Test After Every Change:** Keep a tight feedback loop. Compile or run tests immediately after modifying code.

## 3. Unity & Cesium C# Best Practices
- **GC & Performance:** Avoid allocating memory in `Update()`, `FixedUpdate()`, or `LateUpdate()`. Cache references in `Awake()` or `Start()`.
- **Spatial Source of Truth:** Memorial positions use `point_<ID>` empty transforms under `0 Root`.
- **3D Model Inspector:** Individual `.glb` models are loaded dynamically via `GitHubAssetDownloader.cs` (repo `AleksAntic/thesisar-stone-models`).

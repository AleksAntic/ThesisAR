# GLB scale and marker reference

## Source of truth

`Assets/Prefabs/B-B Mem stone geolocation plan update20-05-2026.glb` is byte-identical to
`data/new data/B-B Mem stone geolocation plan update20-05-2026.glb` (SHA-256
`0AF38DF947205FBAD9EDCE2F6DF2D7C2C06FEF2BA23A1FE34BCC81DCACF3FE43`).

The `point_<ID>` nodes in this GLB are the spatial source of truth. Their original blue-disc
`Geom3D` marker meshes are aligned correctly to those points and have local position `(0,0,0)`.

## Why the numbers look unusual

The imported GLB uses multiple nested unit conversions:

| Node | Local scale | Purpose |
|---|---:|---|
| `Assembly-410` | `0.001` | Converts plan coordinates from millimetres to Unity metres. |
| `point_<ID>` | `25.4` | Converts the individual stone asset's inch-based local convention. |
| `point_<ID>` effective world scale | `0.0254` | `0.001 × 25.4`: metres per authored unit. |
| baked visual child | `20` | Existing visual-import multiplier; effective world scale `0.508`. |
| `GeoRoot` and `CesiumGeoreference` | `1` | The Unity/Cesium world itself remains in metres. |

Do **not** force these transforms to scale 1: that would change the imported geometry and marker
coordinates. Use world-space (`transform.position`, `Renderer.bounds`, NavMesh distances) for any
physical measurement. A local `0.5` unit volume under a scaled point is **not** automatically
0.5 world metres.

## Verified marker data (2026-08-13)

- All 169 `point_*` scene coordinates match the source GLB after glTF-to-Unity axis conversion.
- The only observed difference was `point_MG13` at 2.5 cm, caused by scene-YAML rounding.
- Cesium tilesets are optional visual context only and are currently disabled. Their transforms and
  `CesiumGeoreference.scale` are 1; they do not scale the memorial GLB.
- The GLB is geographically placed through `GeoRoot` (`CesiumGlobeAnchor`) and the internal marker
  `point_B-B Geolocation origin 52.757620, 9.912300`.

## Baked visual rule

`[BakedVisual]_<ID>` is an editor-only comparison aid, not a spatial reference. Its imported
mesh pivot, elevation and orientation can differ from the aligned `Geom3D` marker; the MG11a
comparison also confirms that the visual cannot be trusted for connected structures. Do **not**
use baked visual transforms, rotations or bounds for guidance, NavMesh, GPS/AR validation, or
build logic. Use the matching `point_<ID>` / `Geom3D` world position.

Known visual-review cases from 2026-08-13:

- Any manual editor adjustments to baked visuals are purely temporary inspection work and do not
  establish a correct calibration.

The disabled scene object `[Reference] Original GLB (disabled)` is only a comparison aid. Activate
it temporarily in the Editor, then disable it again; it must remain disabled for play/build.

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Editor-only memorial protection and hierarchy organization tools.</summary>
public static class MemorialNavMeshSafetyTools
{
    private const string SavedNavMeshAssetPath = "Assets/Scenes/emptyy/NavMesh-NavMesh Surface.asset";
    private const string OriginalGlbAssetPath = "Assets/Prefabs/B-B Mem stone geolocation plan update20-05-2026.glb";
    private const string OriginalGlbReferenceName = "[Reference] Original GLB (disabled)";

    [MenuItem("Tools/ThesisAR/Diagnose NavMesh Surfaces")]
    private static void DiagnoseNavMeshSurfaces()
    {
        NavMeshSurface[] surfaces = UnityEngine.Object.FindObjectsByType<NavMeshSurface>(FindObjectsInactive.Include);
        foreach (NavMeshSurface surface in surfaces)
        {
            Debug.Log($"[Memorial NavMesh] Surface '{surface.name}': active={surface.gameObject.activeInHierarchy}, " +
                      $"data={(surface.navMeshData != null ? surface.navMeshData.name : "NULL")}, " +
                      $"collect={surface.collectObjects}.", surface);
        }
        Debug.Log($"[Memorial NavMesh] surfaces={surfaces.Length}.");
    }

    [MenuItem("Tools/ThesisAR/Reload Saved NavMesh Data")]
    private static void ReloadSavedNavMeshData()
    {
        int reloaded = 0;
        foreach (NavMeshSurface surface in UnityEngine.Object.FindObjectsByType<NavMeshSurface>(FindObjectsInactive.Include))
        {
            if (surface.navMeshData == null) continue;
            surface.RemoveData();
            surface.AddData();
            reloaded++;
        }
        Debug.Log($"[Memorial NavMesh] Reloaded saved NavMeshData on {reloaded} surface(s), without rebuilding.");
    }

    [MenuItem("Tools/ThesisAR/Restore Saved NavMesh Asset")]
    private static void RestoreSavedNavMeshAsset()
    {
        int restored = 0;
        foreach (NavMeshSurface surface in UnityEngine.Object.FindObjectsByType<NavMeshSurface>(FindObjectsInactive.Include))
        {
            surface.RemoveData();
            AssetDatabase.ImportAsset(SavedNavMeshAssetPath, ImportAssetOptions.ForceUpdate);
            NavMeshData savedData = AssetDatabase.LoadAssetAtPath<NavMeshData>(SavedNavMeshAssetPath);
            if (savedData == null) continue;

            SerializedObject serializedSurface = new SerializedObject(surface);
            serializedSurface.FindProperty("m_NavMeshData").objectReferenceValue = savedData;
            serializedSurface.ApplyModifiedPropertiesWithoutUndo();
            surface.AddData();
            restored++;
        }
        Debug.Log($"[Memorial NavMesh] Restored saved NavMeshData asset(s): {restored}.");
    }

    [MenuItem("Tools/ThesisAR/Sort Memorial Anchors in Hierarchy")]
    private static void SortMemorialAnchorsInHierarchy()
    {
        Dictionary<Transform, List<Transform>> byParent = new Dictionary<Transform, List<Transform>>();
        foreach (Transform anchor in FindMemorialAnchors())
        {
            if (anchor.parent == null) continue;
            if (!byParent.TryGetValue(anchor.parent, out List<Transform> siblings))
            {
                siblings = new List<Transform>();
                byParent.Add(anchor.parent, siblings);
            }
            siblings.Add(anchor);
        }

        int sorted = 0;
        foreach (KeyValuePair<Transform, List<Transform>> group in byParent)
        {
            group.Value.Sort((left, right) => NaturalCompare(left.name, right.name));
            for (int index = 0; index < group.Value.Count; index++)
            {
                Undo.RecordObject(group.Value[index], "Sort Memorial Anchors");
                group.Value[index].SetSiblingIndex(index);
                sorted++;
            }
        }

        Debug.Log($"[Memorial Hierarchy] Sorted {sorted} point_ anchors by ID without renaming or reparenting.");
    }

    [MenuItem("Tools/ThesisAR/Remove Baked Fallback Visuals")]
    private static void RemoveBakedFallbackVisuals()
    {
        int removed = 0;
        foreach (Transform candidate in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
        {
            if (!candidate.name.StartsWith("[BakedVisual]_Fallback_", StringComparison.OrdinalIgnoreCase)) continue;
            Undo.DestroyObjectImmediate(candidate.gameObject);
            removed++;
        }

        Debug.Log($"[Memorial Visuals] Removed {removed} saved fallback visual(s). High-fidelity baked models were not touched.");
    }

    [MenuItem("Tools/ThesisAR/Diagnose Memorial Anchor Parents")]
    private static void DiagnoseMemorialAnchorParents()
    {
        Dictionary<string, int> counts = new Dictionary<string, int>();
        foreach (Transform anchor in FindMemorialAnchors())
        {
            string path = GetHierarchyPath(anchor.parent);
            counts.TryGetValue(path, out int count);
            counts[path] = count + 1;
        }

        foreach (KeyValuePair<string, int> entry in counts)
        {
            Debug.Log($"[Memorial Hierarchy] parent='{entry.Key}', anchors={entry.Value}.");
        }
    }

    [MenuItem("Tools/ThesisAR/Audit Baked Visual Alignment")]
    private static void AuditBakedVisualAlignment()
    {
        int visuals = 0;
        float maximumTransformHorizontalOffset = 0f;
        float maximumMeshHorizontalOffset = 0f;
        float minimumVerticalOffset = float.PositiveInfinity;
        float maximumVerticalOffset = float.NegativeInfinity;
        Transform worstTransformVisual = null;
        Transform worstMeshVisual = null;

        foreach (Transform anchor in FindMemorialAnchors())
        {
            for (int index = 0; index < anchor.childCount; index++)
            {
                Transform child = anchor.GetChild(index);
                if (!child.name.StartsWith("[BakedVisual]_", StringComparison.OrdinalIgnoreCase)) continue;

                Vector3 delta = child.position - anchor.position;
                float transformHorizontalOffset = new Vector2(delta.x, delta.z).magnitude;
                if (transformHorizontalOffset > maximumTransformHorizontalOffset)
                {
                    maximumTransformHorizontalOffset = transformHorizontalOffset;
                    worstTransformVisual = child;
                }
                minimumVerticalOffset = Mathf.Min(minimumVerticalOffset, delta.y);
                maximumVerticalOffset = Mathf.Max(maximumVerticalOffset, delta.y);
                Renderer renderer = child.GetComponentInChildren<Renderer>();
                if (renderer != null)
                {
                    Vector3 meshDelta = renderer.bounds.center - anchor.position;
                    float meshHorizontalOffset = new Vector2(meshDelta.x, meshDelta.z).magnitude;
                    if (meshHorizontalOffset > 0.5f)
                    {
                        Debug.LogWarning($"[Memorial Visuals] Review '{child.name}': mesh-center offset={meshHorizontalOffset:F3}m, " +
                                         $"vertical={meshDelta.y:F3}m.", child);
                    }
                    if (meshHorizontalOffset > maximumMeshHorizontalOffset)
                    {
                        maximumMeshHorizontalOffset = meshHorizontalOffset;
                        worstMeshVisual = child;
                    }
                }
                visuals++;
            }
        }

        Debug.Log($"[Memorial Visuals] baked={visuals}; max transform horizontal offset={maximumTransformHorizontalOffset:F4}m " +
                  $"({(worstTransformVisual != null ? worstTransformVisual.name : "none")}); max mesh-center horizontal offset={maximumMeshHorizontalOffset:F4}m " +
                  $"({(worstMeshVisual != null ? worstMeshVisual.name : "none")}); transform vertical offsets={minimumVerticalOffset:F4}m..{maximumVerticalOffset:F4}m.");
    }

    [MenuItem("Tools/ThesisAR/Audit Baked Visuals Against Original Geometry")]
    private static void AuditBakedVisualsAgainstOriginalGeometry()
    {
        int compared = 0;
        float totalHorizontalOffset = 0f;
        float maximumHorizontalOffset = 0f;
        Transform worstVisual = null;

        foreach (Transform anchor in FindMemorialAnchors())
        {
            Renderer originalRenderer = FindOriginalGeometryRenderer(anchor);
            Transform bakedVisual = FindBakedVisual(anchor);
            Renderer bakedRenderer = bakedVisual != null ? bakedVisual.GetComponentInChildren<Renderer>() : null;
            if (originalRenderer == null || bakedRenderer == null) continue;

            Vector3 delta = originalRenderer.bounds.center - bakedRenderer.bounds.center;
            float horizontalOffset = new Vector2(delta.x, delta.z).magnitude;
            totalHorizontalOffset += horizontalOffset;
            compared++;
            if (horizontalOffset > maximumHorizontalOffset)
            {
                maximumHorizontalOffset = horizontalOffset;
                worstVisual = bakedVisual;
            }
        }

        float average = compared == 0 ? 0f : totalHorizontalOffset / compared;
        Debug.Log($"[Memorial Visuals] original-geometry comparison: compared={compared}, average horizontal offset={average:F3}m, " +
                  $"max={maximumHorizontalOffset:F3}m ({(worstVisual != null ? worstVisual.name : "none")}).");
    }

    [MenuItem("Tools/ThesisAR/Align Baked Visuals To Original Geometry")]
    private static void AlignBakedVisualsToOriginalGeometry()
    {
        int aligned = 0;
        foreach (Transform anchor in FindMemorialAnchors())
        {
            Renderer originalRenderer = FindOriginalGeometryRenderer(anchor);
            Transform bakedVisual = FindBakedVisual(anchor);
            Renderer bakedRenderer = bakedVisual != null ? bakedVisual.GetComponentInChildren<Renderer>() : null;
            if (originalRenderer == null || bakedRenderer == null) continue;

            Vector3 delta = originalRenderer.bounds.center - bakedRenderer.bounds.center;
            delta.y = 0f;
            if (delta.sqrMagnitude < 0.000001f) continue;
            Undo.RecordObject(bakedVisual, "Align Baked Visual To Original Geometry");
            bakedVisual.position += delta;
            aligned++;
        }

        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log($"[Memorial Visuals] Horizontally aligned {aligned} baked visual(s) to their original GLB geometry.");
    }

    [MenuItem("Tools/ThesisAR/Center Baked Visuals On Markers")]
    private static void CenterBakedVisualsOnMarkers()
    {
        int centered = 0;
        foreach (Transform anchor in FindMemorialAnchors())
        {
            Transform bakedVisual = FindBakedVisual(anchor);
            Renderer bakedRenderer = bakedVisual != null ? bakedVisual.GetComponentInChildren<Renderer>() : null;
            if (bakedRenderer == null) continue;

            Vector3 delta = anchor.position - bakedRenderer.bounds.center;
            delta.y = 0f;
            if (delta.sqrMagnitude < 0.000001f) continue;
            Undo.RecordObject(bakedVisual, "Center Baked Visual On Marker");
            bakedVisual.position += delta;
            centered++;
        }

        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log($"[Memorial Visuals] Centered {centered} baked visual(s) horizontally on their point_ marker.");
    }

    [MenuItem("Tools/ThesisAR/Fix I31 and I35 Baked Materials")]
    private static void FixI31AndI35BakedMaterials()
    {
        SetBakedVisualMaterial("I31", "Assets/Resources/Stones/I31.glb");
        SetBakedVisualMaterial("I35", "Assets/Resources/Stones/I35.glb");
        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log("[Memorial Visuals] Reassigned I31 and I35 to their matching source materials.");
    }

    [MenuItem("Tools/ThesisAR/Create Disabled Original GLB Reference")]
    private static void CreateDisabledOriginalGlbReference()
    {
        Transform currentModel = GameObject.Find("B-B Mem stone geolocation plan update20-05-2026")?.transform;
        if (currentModel == null)
        {
            Debug.LogWarning("[Memorial Reference] Current GLB instance was not found.");
            return;
        }

        Transform existing = currentModel.parent.Find(OriginalGlbReferenceName);
        if (existing != null)
        {
            Debug.Log("[Memorial Reference] Disabled original GLB reference already exists.", existing);
            return;
        }

        GameObject originalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(OriginalGlbAssetPath);
        if (originalPrefab == null)
        {
            Debug.LogError($"[Memorial Reference] Could not load '{OriginalGlbAssetPath}'.");
            return;
        }

        GameObject reference = (GameObject)PrefabUtility.InstantiatePrefab(originalPrefab, currentModel.parent);
        Undo.RegisterCreatedObjectUndo(reference, "Create Original GLB Reference");
        reference.name = OriginalGlbReferenceName;
        reference.transform.localPosition = currentModel.localPosition;
        reference.transform.localRotation = currentModel.localRotation;
        reference.transform.localScale = currentModel.localScale;
        reference.SetActive(false);
        EditorSceneManager.MarkAllScenesDirty();
        Debug.Log("[Memorial Reference] Created disabled, unmodified GLB reference. Activate it only for visual comparison.", reference);
    }

    [MenuItem("Tools/ThesisAR/Group Memorial Anchors by ID")]
    private static void GroupMemorialAnchorsById()
    {
        List<Transform> anchors = FindMemorialAnchors();
        if (anchors.Count == 0) return;

        Transform sharedParent = anchors[0].parent;
        if (sharedParent == null || anchors.Exists(anchor => anchor.parent != sharedParent))
        {
            Debug.LogWarning("[Memorial Hierarchy] Points do not share one parent; grouping was not applied.");
            return;
        }

        GameObject prefabRoot = PrefabUtility.GetNearestPrefabInstanceRoot(sharedParent.gameObject);
        if (prefabRoot != null)
        {
            PrefabUtility.UnpackPrefabInstance(prefabRoot, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
        }

        Dictionary<string, Transform> groups = new Dictionary<string, Transform>();
        foreach (Transform child in sharedParent)
        {
            if (child.name.StartsWith("[Points] ", StringComparison.Ordinal))
            {
                groups[child.name.Substring("[Points] ".Length)] = child;
            }
        }

        int moved = 0;
        foreach (Transform anchor in anchors)
        {
            string groupId = GetAnchorGroupId(anchor.name);
            if (!groups.TryGetValue(groupId, out Transform group))
            {
                GameObject groupObject = new GameObject("[Points] " + groupId);
                Undo.RegisterCreatedObjectUndo(groupObject, "Create Memorial Point Group");
                group = groupObject.transform;
                group.SetParent(sharedParent, false);
                groups.Add(groupId, group);
            }

            Vector3 localPosition = anchor.localPosition;
            Quaternion localRotation = anchor.localRotation;
            Vector3 localScale = anchor.localScale;
            Undo.SetTransformParent(anchor, group, "Group Memorial Points");
            anchor.localPosition = localPosition;
            anchor.localRotation = localRotation;
            anchor.localScale = localScale;
            moved++;
        }

        List<Transform> orderedGroups = new List<Transform>(groups.Values);
        orderedGroups.Sort((left, right) => string.Compare(left.name, right.name, StringComparison.OrdinalIgnoreCase));
        for (int index = 0; index < orderedGroups.Count; index++)
        {
            orderedGroups[index].SetSiblingIndex(index);
            List<Transform> children = new List<Transform>();
            foreach (Transform child in orderedGroups[index]) children.Add(child);
            children.Sort((left, right) => NaturalCompare(left.name, right.name));
            for (int childIndex = 0; childIndex < children.Count; childIndex++) children[childIndex].SetSiblingIndex(childIndex);
        }

        EditorSceneManager.MarkAllScenesDirty();
        int grouped = anchors.FindAll(anchor => anchor.parent != sharedParent).Count;
        Debug.Log($"[Memorial Hierarchy] Grouped {grouped}/{moved} anchors into {orderedGroups.Count} visible ID groups, preserving local transforms.");
    }

    private static List<Transform> FindMemorialAnchors()
    {
        List<Transform> anchors = new List<Transform>();
        foreach (Transform candidate in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
        {
            if (candidate == null || !candidate.name.StartsWith("point_", StringComparison.OrdinalIgnoreCase)) continue;
            string id = candidate.name.Substring("point_".Length);
            string lowerId = id.ToLowerInvariant();
            if (id == "0" || id == "1" || id == "2" || id == "3" ||
                lowerId.Contains("origin") || lowerId.Contains("anchor") ||
                lowerId.Contains("test") || lowerId.Contains("setup")) continue;
            anchors.Add(candidate);
        }
        return anchors;
    }

    private static int NaturalCompare(string left, string right)
    {
        string leftId = left.Substring("point_".Length);
        string rightId = right.Substring("point_".Length);
        Match leftMatch = Regex.Match(leftId, "^([A-Z]+)(\\d+)([A-Za-z]?)$");
        Match rightMatch = Regex.Match(rightId, "^([A-Z]+)(\\d+)([A-Za-z]?)$");
        if (!leftMatch.Success || !rightMatch.Success) return string.Compare(leftId, rightId, StringComparison.OrdinalIgnoreCase);

        int prefix = string.Compare(leftMatch.Groups[1].Value, rightMatch.Groups[1].Value, StringComparison.OrdinalIgnoreCase);
        if (prefix != 0) return prefix;
        int number = int.Parse(leftMatch.Groups[2].Value).CompareTo(int.Parse(rightMatch.Groups[2].Value));
        return number != 0 ? number : string.Compare(leftMatch.Groups[3].Value, rightMatch.Groups[3].Value, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetHierarchyPath(Transform transform)
    {
        if (transform == null) return "<scene root>";
        List<string> names = new List<string>();
        for (Transform current = transform; current != null; current = current.parent)
        {
            names.Add(current.name);
        }
        names.Reverse();
        return string.Join("/", names);
    }

    private static string GetAnchorGroupId(string anchorName)
    {
        string id = anchorName.Substring("point_".Length);
        Match match = Regex.Match(id, "^([A-Za-z]+)");
        return match.Success ? match.Groups[1].Value.ToUpperInvariant() : "Other";
    }

    private static Transform FindBakedVisual(Transform anchor)
    {
        for (int index = 0; index < anchor.childCount; index++)
        {
            Transform child = anchor.GetChild(index);
            if (child.name.StartsWith("[BakedVisual]_", StringComparison.OrdinalIgnoreCase)) return child;
        }
        return null;
    }

    private static Renderer FindOriginalGeometryRenderer(Transform anchor)
    {
        for (int index = 0; index < anchor.childCount; index++)
        {
            Transform child = anchor.GetChild(index);
            if (!child.name.StartsWith("Geom3D", StringComparison.OrdinalIgnoreCase)) continue;
            Renderer renderer = child.GetComponentInChildren<Renderer>();
            if (renderer != null) return renderer;
        }
        return null;
    }

    private static void SetBakedVisualMaterial(string id, string sourceAssetPath)
    {
        GameObject sourceAsset = AssetDatabase.LoadAssetAtPath<GameObject>(sourceAssetPath);
        if (sourceAsset == null)
        {
            Debug.LogError($"[Memorial Visuals] Missing source asset '{sourceAssetPath}'.");
            return;
        }

        Material sourceMaterial = null;
        foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(sourceAssetPath))
        {
            if (asset is Material material)
            {
                sourceMaterial = material;
                break;
            }
        }
        if (sourceMaterial == null)
        {
            Debug.LogError($"[Memorial Visuals] No material found in '{sourceAssetPath}'.");
            return;
        }

        foreach (Transform anchor in FindMemorialAnchors())
        {
            if (!string.Equals(anchor.name, "point_" + id, StringComparison.OrdinalIgnoreCase)) continue;
            Renderer renderer = FindBakedVisual(anchor)?.GetComponentInChildren<Renderer>();
            if (renderer == null) break;
            Undo.RecordObject(renderer, "Fix Baked Visual Material");
            renderer.sharedMaterial = sourceMaterial;
            return;
        }
        Debug.LogError($"[Memorial Visuals] Baked visual for '{id}' was not found.");
    }
}
#endif

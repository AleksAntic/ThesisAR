using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class StoneEditorBaker : MonoBehaviour
{
    [System.Serializable]
    public class StoneAssetMapping
    {
        [HideInInspector] public string listLabel; // Unity usa questo campo come titolo della riga nell'Inspector!
        public string stoneID;
        public GameObject assignedModel;
    }

    [Header("📋 Interactive Mapping Matrix (Check & Edit Here)")]
    public List<StoneAssetMapping> stoneMappings = new List<StoneAssetMapping>();

    [Header("📐 Global Model Import Fixes (Rotation & Scale)")]
    [Tooltip("Rotazione di correzione per l'importazione (90, 0, 0 per i GLB da Blender).")]
    [SerializeField] private Vector3 localRotationOffset = new Vector3(90f, 0f, 0f);

    [Tooltip("Moltiplicatore di scala per i modelli 3D delle lapidi.")]
    [SerializeField] private Vector3 localScaleMultiplier = new Vector3(20f, 20f, 20f); // ◄ IMPOSTATO A 20 DI DEFAULT

    [Header("🛰️ Ground Physics Snapping")]
    [Tooltip("Se attivo, rileva l'altezza del terreno tramite Raycast e vi adagia il modello.")]
    [SerializeField] private bool autoSnapToTerrainMesh = true;
    [Tooltip("Poiché il modello è ruotato di 90° su X, l'asse Z locale punta verso l'alto. Regola questo valore se affondano.")]
    [SerializeField] private float manualVerticalLift = 0f;

    [Header("⚙️ Fallback Assets Configuration")]
    [SerializeField] private GameObject defaultStoneFallbackPrefab;

#if UNITY_EDITOR
    [ContextMenu("🔍 Step 1: Populate and Audit Matrix from Scene")]
    public void PopulateAndAuditMatrix()
    {
        GameObject[] allObjects = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
        List<string> activeSceneIDs = new List<string>();

        foreach (GameObject obj in allObjects)
        {
            if (obj != null && obj.name.StartsWith("point_"))
            {
                string id = obj.name.Replace("point_", "").Trim();
                if (!activeSceneIDs.Contains(id)) activeSceneIDs.Add(id);
            }
        }

        List<StoneAssetMapping> updatedMatrix = new List<StoneAssetMapping>();

        foreach (string id in activeSceneIDs)
        {
            StoneAssetMapping existingPair = stoneMappings.Find(m => m.stoneID.Equals(id, System.StringComparison.OrdinalIgnoreCase));

            if (existingPair != null)
            {
                if (existingPair.assignedModel == null)
                {
                    existingPair.assignedModel = Resources.Load<GameObject>($"Stones/{id}");
                }

                existingPair.listLabel = existingPair.assignedModel != null ? $"✔ Stone: {id}" : $"🚨 [MANCANTE] {id}";
                updatedMatrix.Add(existingPair);
            }
            else
            {
                StoneAssetMapping newPair = new StoneAssetMapping { stoneID = id };
                newPair.assignedModel = Resources.Load<GameObject>($"Stones/{id}");
                newPair.listLabel = newPair.assignedModel != null ? $"✔ Stone: {id}" : $"🚨 [MANCANTE] {id}";
                updatedMatrix.Add(newPair);
            }
        }

        stoneMappings = updatedMatrix;
        stoneMappings.Sort((a, b) => a.stoneID.CompareTo(b.stoneID));

        EditorUtility.SetDirty(this);
        Debug.Log("[Stone Audit] Matrix sincronizzata nell'Inspector.");
    }

    [ContextMenu("⚙️ Step 2: Pre-Bake and Materialize 3D Stones Using Matrix Data")]
    public void PreBakeStonesFromMatrix()
    {
        if (stoneMappings.Count == 0) return;

        int successCount = 0;

        foreach (StoneAssetMapping mapping in stoneMappings)
        {
            GameObject parentNode = GameObject.Find("point_" + mapping.stoneID);
            if (parentNode == null) continue;

            Transform legacyContainer = parentNode.transform.Find("Baked_3D_Instance");
            if (legacyContainer != null) DestroyImmediate(legacyContainer.gameObject);

            GameObject instanceHolder = new GameObject("Baked_3D_Instance");
            instanceHolder.transform.SetParent(parentNode.transform, false);

            Vector3 computedLocalPosition = Vector3.zero;

            if (autoSnapToTerrainMesh)
            {
                Vector3 rayOrigin = parentNode.transform.position + (Vector3.up * 50f);
                if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 100f))
                {
                    float hitWorldY = hit.point.y;
                    float relativeLocalY = hitWorldY - parentNode.transform.position.y;

                    // Correzione asse Z locale dovuto alla rotazione di 90° su X
                    computedLocalPosition = new Vector3(0f, 0f, relativeLocalY + manualVerticalLift);
                }
                else
                {
                    computedLocalPosition = new Vector3(0f, 0f, manualVerticalLift);
                }
            }
            else
            {
                computedLocalPosition = new Vector3(0f, 0f, manualVerticalLift);
            }

            GameObject modelToSpawn = mapping.assignedModel != null ? mapping.assignedModel : defaultStoneFallbackPrefab;

            if (modelToSpawn != null)
            {
                GameObject spawnedObj = PrefabUtility.InstantiatePrefab(modelToSpawn, instanceHolder.transform) as GameObject;
                if (spawnedObj != null)
                {
                    spawnedObj.transform.localPosition = computedLocalPosition;
                    spawnedObj.transform.localRotation = Quaternion.Euler(localRotationOffset);
                    spawnedObj.transform.localScale = localScaleMultiplier; // ◄ APPLICA LA SCALA X20
                }
            }
            else
            {
                GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.transform.SetParent(instanceHolder.transform, false);
                // Scala il cubo di fallback in proporzione
                cube.transform.localScale = Vector3.Scale(new Vector3(0.5f, 1.2f, 0.2f), localScaleMultiplier);
                cube.transform.localPosition = computedLocalPosition + new Vector3(0f, 0f, 0.6f);
                cube.transform.localRotation = Quaternion.Euler(localRotationOffset);
            }
            successCount++;
        }

        EditorUtility.SetDirty(gameObject);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log($"[Stone Baker] Pre-bake completato. Modelli scalati a 20.");
    }

    [ContextMenu("❌ Step 3: Clear All Pre-Baked Stones")]
    public void ClearAllBakedStones()
    {
        GameObject[] allObjects = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include);
        foreach (GameObject obj in allObjects)
        {
            if (obj != null && obj.name.StartsWith("point_"))
            {
                Transform legacyContainer = obj.transform.Find("Baked_3D_Instance");
                if (legacyContainer != null) DestroyImmediate(legacyContainer.gameObject);
            }
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
    }
#endif
}

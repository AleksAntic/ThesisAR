using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Editor utility to clear/unpack missing prefab references on scene GameObjects ([BakedVisual]_*)
/// so Unity's Build Pipeline can build the APK cleanly without "Missing Prefab Asset" errors.
/// </summary>
public class FixMissingBakedVisualPrefabs
{
    [MenuItem("ThesisAR/Fix Missing Prefabs & Prepare Build")]
    public static void UnpackMissingBakedVisualPrefabs()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/emptyy.unity", OpenSceneMode.Single);

        int unpackedCount = 0;
        GameObject[] allObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (GameObject go in allObjects)
        {
            if (PrefabUtility.IsPartOfPrefabInstance(go))
            {
                PrefabInstanceStatus status = PrefabUtility.GetPrefabInstanceStatus(go);
                if (status == PrefabInstanceStatus.MissingAsset)
                {
                    PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                    unpackedCount++;
                }
            }
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        Debug.Log($"[FixMissingPrefabs] Successfully unpacked {unpackedCount} missing prefab instances in emptyy.unity. Scene saved!");
    }
}

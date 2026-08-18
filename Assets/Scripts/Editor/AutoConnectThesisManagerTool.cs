#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class AutoConnectThesisManagerTool
{
    [MenuItem("ThesisAR/Auto-Connect All ThesisManager Inspector References")]
    public static void ConnectAllReferences()
    {
        ThesisManager manager = Object.FindAnyObjectByType<ThesisManager>(FindObjectsInactive.Include);
        if (manager == null)
        {
            Debug.LogError("[AutoConnectTool] ❌ ThesisManager component not found in the active scene!");
            return;
        }

        Undo.RecordObject(manager, "Auto-Connect ThesisManager References");
        SerializedObject so = new SerializedObject(manager);

        // 1. Resolve Guidance System References (Auto-Create missing GameObjects in Scene if absent)
        PersonalGuidance personal = Object.FindAnyObjectByType<PersonalGuidance>(FindObjectsInactive.Include);
        if (personal == null)
        {
            GameObject persGo = GameObject.Find("GuidanceSystem_Personal") ?? new GameObject("GuidanceSystem_Personal");
            Undo.RegisterCreatedObjectUndo(persGo, "Create GuidanceSystem_Personal");
            personal = persGo.GetComponent<PersonalGuidance>() ?? persGo.AddComponent<PersonalGuidance>();
        }

        IntermediateGuidance intermediate = Object.FindAnyObjectByType<IntermediateGuidance>(FindObjectsInactive.Include);
        if (intermediate == null)
        {
            GameObject interGo = GameObject.Find("AR_Core_Controller/GuidanceSystem_Intermediate") ?? GameObject.Find("GuidanceSystem_Intermediate");
            if (interGo == null)
            {
                GameObject parentContainer = GameObject.Find("AR_Core_Controller") ?? GameObject.Find("_Managers");
                interGo = new GameObject("GuidanceSystem_Intermediate");
                if (parentContainer != null) interGo.transform.SetParent(parentContainer.transform, false);
                Undo.RegisterCreatedObjectUndo(interGo, "Create GuidanceSystem_Intermediate");
            }
            intermediate = interGo.GetComponent<IntermediateGuidance>() ?? interGo.AddComponent<IntermediateGuidance>();
        }

        ImpersonalGuidance impersonal = Object.FindAnyObjectByType<ImpersonalGuidance>(FindObjectsInactive.Include);
        if (impersonal == null)
        {
            GameObject impGo = GameObject.Find("GuidanceSystem_Impersonal") ?? new GameObject("GuidanceSystem_Impersonal");
            Undo.RegisterCreatedObjectUndo(impGo, "Create GuidanceSystem_Impersonal");
            impersonal = impGo.GetComponent<ImpersonalGuidance>() ?? impGo.AddComponent<ImpersonalGuidance>();
        }

        so.FindProperty("personalGuidanceInstance").objectReferenceValue = personal;
        so.FindProperty("intermediateGuidanceInstance").objectReferenceValue = intermediate;
        so.FindProperty("impersonalGuidanceInstance").objectReferenceValue = impersonal;

        Debug.Log($"[AutoConnectTool] Connected PersonalGuidance: '{personal.gameObject.name}'");
        Debug.Log($"[AutoConnectTool] Connected IntermediateGuidance: '{intermediate.gameObject.name}'");
        Debug.Log($"[AutoConnectTool] Connected ImpersonalGuidance: '{impersonal.gameObject.name}'");

        // 2. Resolve Core Managers
        UIManager ui = Object.FindAnyObjectByType<UIManager>(FindObjectsInactive.Include);
        ARWayfindingManager wayfinding = Object.FindAnyObjectByType<ARWayfindingManager>(FindObjectsInactive.Include);
        MemorialSpawner spawner = Object.FindAnyObjectByType<MemorialSpawner>(FindObjectsInactive.Include);

        if (ui != null)
        {
            var prop = so.FindProperty("uiManager");
            if (prop != null) prop.objectReferenceValue = ui;
        }
        if (wayfinding != null)
        {
            var prop = so.FindProperty("wayfindingManager");
            if (prop != null) prop.objectReferenceValue = wayfinding;
        }
        if (spawner != null)
        {
            var prop = so.FindProperty("memorialSpawner");
            if (prop != null) prop.objectReferenceValue = spawner;
        }

        // 3. Resolve Single Avatar Instance in Scene
        GameObject avatarInstance = GameObject.Find("SingleGuideAvatarInstance") ?? GameObject.Find("GuideCharacterInstance");
        if (avatarInstance == null)
        {
            var animators = Object.FindObjectsByType<Animator>(FindObjectsInactive.Include);
            foreach (var anim in animators)
            {
                if (anim.transform.root.name.Contains("Guide") || anim.transform.root.name.Contains("MaleCharacter"))
                {
                    avatarInstance = anim.gameObject;
                    break;
                }
            }
        }

        if (avatarInstance != null)
        {
            avatarInstance.name = "SingleGuideAvatarInstance";
            var prop = so.FindProperty("guideAvatarInstance");
            if (prop != null) prop.objectReferenceValue = avatarInstance;
            ActiveGuideAvatarRegistry.RegisterSingleAvatarInstance(avatarInstance);
            Debug.Log($"[AutoConnectTool] Connected GuideAvatarInstance: '{avatarInstance.name}'");
        }

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(manager);
        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);

        Debug.Log("[AutoConnectTool] ✅ SUCCESS! All ThesisManager Inspector references auto-connected and scene marked dirty!");
    }
}
#endif

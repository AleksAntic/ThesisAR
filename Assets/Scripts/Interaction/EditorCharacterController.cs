using UnityEngine;
#if UNITY_EDITOR
using UnityEngine.InputSystem;
#endif

/// <summary>
/// A lightweight developer simulator component allowing flight movement using WASD + Mouse
/// in the Unity Editor when testing spatial AR tracking. Auto-destructs in mobile builds.
/// </summary>
public class EditorCharacterController : MonoBehaviour
{
#if UNITY_EDITOR
    [Header("🚀 Simulation Movement Settings")]
    [SerializeField] private float moveSpeed = 5.0f;
    [SerializeField] private float fastMoveSpeed = 15.0f;
    [SerializeField] private float lookSensitivity = 0.15f;

    private Vector2 rotation = Vector2.zero;

    void Start()
    {
        // Auto-attach main camera rig to this simulation player so movement/rotation affect the camera view
        if (Camera.main != null)
        {
            Transform cameraRig = Camera.main.transform;
            if (cameraRig.parent != null)
            {
                cameraRig = cameraRig.parent.parent != null ? cameraRig.parent.parent : cameraRig.parent;
            }

            // Align player position and rotation with the camera rig's starting position in the editor
            transform.position = cameraRig.position;
            transform.rotation = cameraRig.rotation;

            cameraRig.SetParent(transform, false);
            cameraRig.localPosition = Vector3.zero;
            cameraRig.localRotation = Quaternion.identity;
            Debug.Log($"[EditorCharacterController] Auto-attached Camera Rig '{cameraRig.name}' at position {transform.position}.");
        }

        // Cache starting rotation to prevent camera snapping on start
        rotation.y = transform.localEulerAngles.y;
        rotation.x = transform.localEulerAngles.x;

        Debug.Log("[EditorCharacterController] Initialized. Hold RIGHT-CLICK to aim and use WASD keys to fly. Hold SHIFT for turbo speed, SPACE/CTRL for vertical movement.");
    }

    void Update()
    {
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;

        if (keyboard == null || mouse == null) return;

        // 1. Aim/Rotation mechanics: Only capture mouse delta when Right-Click is actively held
        if (mouse.rightButton.isPressed)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            Vector2 mouseDelta = mouse.delta.ReadValue();
            rotation.y += mouseDelta.x * lookSensitivity;
            rotation.x -= mouseDelta.y * lookSensitivity;
            rotation.x = Mathf.Clamp(rotation.x, -85f, 85f); // Prevent camera flipping upside down

            transform.localRotation = Quaternion.Euler(rotation.x, rotation.y, 0f);
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // 2. Fly/Translate mechanics: Read inputs and move relative to flat XZ axes (prevents height drift when looking up/down)
        float speed = keyboard.leftShiftKey.isPressed ? fastMoveSpeed : moveSpeed;
        Vector3 direction = Vector3.zero;

        Vector3 forward = transform.forward;
        forward.y = 0f;
        forward.Normalize();

        Vector3 right = transform.right;
        right.y = 0f;
        right.Normalize();

        if (keyboard.wKey.isPressed) direction += forward;
        if (keyboard.sKey.isPressed) direction -= forward;
        if (keyboard.aKey.isPressed) direction -= right;
        if (keyboard.dKey.isPressed) direction += right;

        // Vertical movement bindings (Space or E = Up, Left-Ctrl, C or Q = Down)
        if (keyboard.spaceKey.isPressed || keyboard.eKey.isPressed) direction += Vector3.up;
        if (keyboard.leftCtrlKey.isPressed || keyboard.cKey.isPressed || keyboard.qKey.isPressed) direction -= Vector3.up;

        if (direction.sqrMagnitude > 0.01f)
        {
            transform.position += direction.normalized * speed * Time.deltaTime;
        }

        // 3. TELEPORT CHEAT: Instantly fast-travel near the active destination when pressing the 'T' key
        if (keyboard.tKey.wasPressedThisFrame)
        {
            Transform targetMarker = null;
            string activeTargetID = "";
            PersonalGuidance guidance = null;

            // Primary source: check active tour manager first to prevent sync issues
            var tourMgr = FindAnyObjectByType<TourManager>(FindObjectsInactive.Include);
            var spawner = FindAnyObjectByType<MemorialSpawner>(FindObjectsInactive.Include);
            
            if (tourMgr != null && tourMgr.IsTourActiveAndRunning)
            {
                activeTargetID = tourMgr.GetCurrentTargetStoneID();
                if (!string.IsNullOrEmpty(activeTargetID) && spawner != null)
                {
                    GameObject nodeGo = spawner.GetSpawnedMemorial(activeTargetID);
                    if (nodeGo == null)
                    {
                        // Fallback: search scene directly for marker if spawner hasn't linked it yet
                        nodeGo = GameObject.Find(activeTargetID) ?? 
                                 GameObject.Find("point_" + activeTargetID) ?? 
                                 GameObject.Find("Stone_" + activeTargetID) ??
                                 GameObject.Find("Grave_" + activeTargetID);
                        
                        if (nodeGo == null)
                        {
                            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
                            {
                                if (go != null && go.scene.name != null &&
                                    (go.name.Equals(activeTargetID, System.StringComparison.OrdinalIgnoreCase) ||
                                     go.name.Equals("point_" + activeTargetID, System.StringComparison.OrdinalIgnoreCase) ||
                                     go.name.Equals("Stone_" + activeTargetID, System.StringComparison.OrdinalIgnoreCase) ||
                                     go.name.Equals("Grave_" + activeTargetID, System.StringComparison.OrdinalIgnoreCase)))
                                {
                                    nodeGo = go;
                                    break;
                                }
                            }
                        }
                    }
                    if (nodeGo != null) targetMarker = nodeGo.transform;
                }
            }

            // Fallback 1: Try resolving destination from active Personal Guidance system
            if (targetMarker == null)
            {
                guidance = FindAnyObjectByType<PersonalGuidance>(FindObjectsInactive.Include);
                if (guidance != null)
                {
                    var targetNode = guidance.GetType().GetField("targetNodeObject", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(guidance) as GameObject;
                    activeTargetID = guidance.GetType().GetField("activeTargetID", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(guidance) as string;
                    
                    if (targetNode != null) targetMarker = targetNode.transform;
                    else if (!string.IsNullOrEmpty(activeTargetID))
                    {
                        GameObject nodeGo = GameObject.Find(activeTargetID) ?? 
                                             GameObject.Find("point_" + activeTargetID) ?? 
                                             GameObject.Find("Stone_" + activeTargetID) ??
                                             GameObject.Find("Grave_" + activeTargetID);
                        if (nodeGo != null) targetMarker = nodeGo.transform;
                    }
                }
            }

            // Fallback 2: Try resolving destination from ARWayfindingManager
            if (targetMarker == null)
            {
                var wayfinding = FindAnyObjectByType<ARWayfindingManager>(FindObjectsInactive.Include);
                if (wayfinding != null)
                {
                    var targetTrans = wayfinding.GetType().GetField("targetTransform", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(wayfinding) as Transform;
                    activeTargetID = wayfinding.GetType().GetField("currentTargetID", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(wayfinding) as string;
                    
                    if (targetTrans != null) targetMarker = targetTrans;
                    else if (!string.IsNullOrEmpty(activeTargetID))
                    {
                        GameObject nodeGo = GameObject.Find(activeTargetID) ?? 
                                             GameObject.Find("point_" + activeTargetID) ?? 
                                             GameObject.Find("Stone_" + activeTargetID) ??
                                             GameObject.Find("Grave_" + activeTargetID);
                        if (nodeGo != null) targetMarker = nodeGo.transform;
                    }
                }
            }

            if (targetMarker == null && !string.IsNullOrEmpty(activeTargetID))
            {
                Debug.LogWarning($"[Teleport Cheat] Teleport failed: target destination ID '{activeTargetID}' could not be resolved in the scene hierarchy!");
            }

            if (targetMarker != null)
            {
                // Teleport player 1.5 meters behind/adjacent to the target stone (within the 2.0m arrival threshold)
                Vector3 dest = targetMarker.position - targetMarker.forward * 1.5f;
                // Add 1.8m Y height offset to place the developer camera at eye level (avoiding underground spawn)
                dest.y = targetMarker.position.y + 1.8f;
                
                transform.position = dest;
                Debug.Log($"[Developer Teleport] Fast-traveled developer to target '{targetMarker.name}' at position {dest}");

                // Teleport the guide companion avatar instantly alongside the player
                GameObject avatar = null;
                if (guidance != null)
                {
                    avatar = guidance.GetAvatarInstance();
                }

                // Backup search: if guidance script returned null, look for active guide companion dynamically
                if (avatar == null)
                {
                    Debug.LogWarning("[Developer Teleport] companion avatar was null on active PersonalGuidance. Scanning scene for active guide agents...");
                    var agents = FindObjectsByType<UnityEngine.AI.NavMeshAgent>();
                    foreach (var a in agents)
                    {
                        if (a.gameObject != gameObject && (a.name.Contains("Guide") || a.name.Contains("Companion") || a.name.Contains("Avatar") || a.name.Contains("Clone")))
                        {
                            avatar = a.gameObject;
                            Debug.Log($"[Developer Teleport] Backup system matched companion target candidate: '{avatar.name}'");
                            break;
                        }
                    }
                }

                if (avatar != null)
                {
                    var agent = avatar.GetComponent<UnityEngine.AI.NavMeshAgent>();
                    // Position avatar 1.0 meters to the front/right of the target stone
                    Vector3 agentDest = targetMarker.position - targetMarker.forward * 1.0f + targetMarker.right * 0.8f;
                    agentDest.y = targetMarker.position.y;

                    if (agent != null && agent.enabled)
                    {
                        // Projected position verification onto NavMesh
                        if (UnityEngine.AI.NavMesh.SamplePosition(agentDest, out UnityEngine.AI.NavMeshHit hit, 15.0f, UnityEngine.AI.NavMesh.AllAreas))
                        {
                            bool warpSuccess = agent.Warp(hit.position);
                            Debug.Log($"[Developer Teleport] Warping companion agent '{avatar.name}' to: {hit.position}, success={warpSuccess}");
                        }
                        else
                        {
                            agent.enabled = false;
                            avatar.transform.position = agentDest;
                            agent.enabled = true;
                            Debug.LogWarning($"[Developer Teleport] NavMesh projection failed near target. Forced transform placement to: {agentDest}");
                        }
                    }
                    else
                    {
                        avatar.transform.position = agentDest;
                        Debug.Log($"[Developer Teleport] Relocated companion Transform '{avatar.name}' to: {agentDest} (agent is null/disabled)");
                    }

                    // Reorient the avatar to look directly at the stone
                    Vector3 lookDir = (targetMarker.position - avatar.transform.position).normalized;
                    lookDir.y = 0f;
                    if (lookDir.sqrMagnitude > 0.001f)
                    {
                        avatar.transform.rotation = Quaternion.LookRotation(lookDir);
                    }

                    // Force the companion guide animator to enter the talking state and stop walking
                    var anim = avatar.GetComponentInChildren<Animator>();
                    if (anim != null)
                    {
                        anim.SetBool("IsTalking", true);
                        anim.SetFloat("Speed", 0f);
                        Debug.Log($"[Developer Teleport] Forced companion animator '{avatar.name}' into Talking state (Speed=0, IsTalking=true)");
                    }
                }
                else
                {
                    Debug.LogError("[Developer Teleport] Companion guide avatar could NOT be found in active scene hierarchy. Teleport skipped for companion.");
                }

                // Force the Stone Populator to load the 3D model immediately instead of waiting for the tick
                var populator = FindAnyObjectByType<RuntimeStonePopulator>(FindObjectsInactive.Include);
                if (populator != null)
                {
                    populator.ForceInstantDistanceCheck();
                    Debug.Log("[Developer Teleport] Instantly forced Stone Populator geographical distance evaluation.");
                }

                ARWayfindingManager wayfinding = FindAnyObjectByType<ARWayfindingManager>(FindObjectsInactive.Include);
                if (wayfinding != null && !string.IsNullOrEmpty(activeTargetID))
                {
                    wayfinding.ForceArrivalForEditorTesting(activeTargetID);
                    Debug.Log($"[Developer Teleport] Simulated route arrival for memorial: '{activeTargetID}'");
                }
            }
            else
            {
                Debug.LogWarning("[Developer Teleport] No active target destination found. Select a stone or start a tour stops list first!");
            }
        }
    }
#else
    void Awake()
    {
        Destroy(this);
    }
#endif
}

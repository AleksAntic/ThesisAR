using UnityEngine;

/// <summary>
/// Runtime controller that drives the Unity Inverse Kinematics (IK) matrix.
/// Procedurally rotates the neck bones to track the user's camera transform,
/// and dynamically lifts the right arm to point explicitly at the active memorial target.
/// All variables, properties, and internal logs are strictly maintained in English.
/// </summary>
[RequireComponent(typeof(Animator))]
public class CompanionIKController : MonoBehaviour
{
    private Animator animator;
    private AudioSource audioSource;
    private Transform lookAtTarget;
    private Transform pointingTarget;

    private float headLookWeight = 0f;
    private float armPointingWeight = 0f;

    [Header("⚡ Interpolation Smoothing")]
    [SerializeField] private float lookTransitionSpeed = 3.0f;
    [SerializeField] private float pointTransitionSpeed = 4.0f;

    void Awake()
    {
        animator = GetComponent<Animator>();
        audioSource = GetComponentInChildren<AudioSource>();
    }

    void Update()
    {
        if (animator == null) return;

        // FAILSAFE: The GuideAvatar instance is persistent and sometimes its manager
        // (IntermediateGuidance) gets disabled or crashes due to parent hierarchy issues,
        // leaving the animator stuck in IsTalking = true forever.
        // This failsafe runs on the avatar itself and forcefully stops the animation if no audio is playing.
        if (animator.GetBool("IsTalking"))
        {
            bool isPlaying = false;
            if (audioSource != null && audioSource.isPlaying) isPlaying = true;
            if (NarrationManager.Instance != null && NarrationManager.Instance.IsSpeaking) isPlaying = true;

            if (!isPlaying)
            {
                animator.SetBool("IsTalking", false);
                if (animator.layerCount > 1)
                {
                    animator.SetLayerWeight(1, 0f);
                }
            }
        }
    }

    /// <summary>
    /// Public interface to dynamically update spatial tracking points from guidance systems.
    /// </summary>
    public void SetTrackingTargets(Transform cameraTarget, Transform monumentTarget, bool shouldPoint)
    {
        lookAtTarget = cameraTarget;
        pointingTarget = monumentTarget;
        
        // Target weight interpolation goals
        headLookWeight = (cameraTarget != null) ? 1.0f : 0f;
        armPointingWeight = (monumentTarget != null && shouldPoint) ? 1.0f : 0f;
    }

    public void SetHeadLookTarget(Transform cameraTarget)
    {
        lookAtTarget = cameraTarget;
        headLookWeight = (cameraTarget != null) ? 1.0f : 0f;
    }

    public void SetChestOrientTarget(Transform target)
    {
        pointingTarget = target;
        armPointingWeight = (target != null) ? 1.0f : 0f;
    }

    private float currentLookWeight = 0f;
    private float currentPointWeight = 0f;

    void OnAnimatorIK(int layerIndex)
    {
        if (animator == null) return;

        // 1. Smoothly interpolate weight transformations to prevent snapping cuts
        currentLookWeight = Mathf.MoveTowards(currentLookWeight, headLookWeight, Time.deltaTime * lookTransitionSpeed);
        currentPointWeight = Mathf.MoveTowards(currentPointWeight, armPointingWeight, Time.deltaTime * pointTransitionSpeed);

        // 2. Procedural Head/Neck Tracking Execution
        if (lookAtTarget != null && currentLookWeight > 0.01f)
        {
            animator.SetLookAtWeight(currentLookWeight, 0.2f, 0.8f, 0.0f, 0.5f);
            animator.SetLookAtPosition(lookAtTarget.position);
        }

        // 3. Procedural Right Arm Pointing Execution (Lifts bone towards target vectors)
        if (pointingTarget != null && currentPointWeight > 0.01f)
        {
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, currentPointWeight);
            animator.SetIKRotationWeight(AvatarIKGoal.RightHand, currentPointWeight * 0.5f);

            // Project arm position slightly forward towards the monument target bounds
            Vector3 handTargetPosition = pointingTarget.position;

            // Capture the CURRENT physical hand bone position before overwriting the IK target,
            // otherwise GetIKPosition() would just return the value we are about to set below,
            // making the direction vector collapse to zero every frame.
            Transform rightHandBone = animator.GetBoneTransform(HumanBodyBones.RightHand);
            Vector3 currentHandPosition = rightHandBone != null ? rightHandBone.position : transform.position;

            animator.SetIKPosition(AvatarIKGoal.RightHand, handTargetPosition);

            // Reorient hand rotation constraints to align flat with target direction
            Vector3 directionToTarget = (handTargetPosition - currentHandPosition).normalized;
            if (directionToTarget.sqrMagnitude > 0.01f)
            {
                animator.SetIKRotation(AvatarIKGoal.RightHand, Quaternion.LookRotation(directionToTarget));
            }
        }
        else
        {
            animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 0f);
            animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 0f);
        }
    }
}
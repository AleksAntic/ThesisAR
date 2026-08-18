using UnityEngine;

/// <summary>
/// Controls individual 2D AR directional arrow sprites spawned flat on the physical terrain.
/// Locks orientation instantly to eliminate jitter caused by runtime manager recalculation loops.
/// </summary>
public class DirectionalArrow : MonoBehaviour
{
    [Header("✨ Floating Animation Parameters")]
    [SerializeField] private float hoverAmplitude = 0.02f;
    [SerializeField] private float hoverFrequency = 2.0f;

    private Vector3 baseWorldPosition;
    private float randomTimeOffset;

    void Awake()
    {
        // Desynchronize floating loops across instances
        randomTimeOffset = Random.Range(0f, 5f);
    }

    void Update()
    {
        // Apply a gentle vertical wave animation relative to the strict ground baseline position
        float newY = Mathf.Max(0f, baseWorldPosition.y + (Mathf.Sin(Time.time * hoverFrequency + randomTimeOffset) * hoverAmplitude));
        transform.position = new Vector3(baseWorldPosition.x, newY, baseWorldPosition.z);
    }

    /// <summary>
    /// Instantly snaps the flat sprite rotation to face the next waypoint on the horizontal plane.
    /// </summary>
    public void SetLookTarget(Vector3 targetWorldPos)
    {
        Vector3 direction = targetWorldPos - transform.position;
        direction.y = 0f; // Lock tracking onto a strict horizontal plane

        if (direction.sqrMagnitude > 0.01f)
        {
            float angleY = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            // Force exactly 90 degrees on X to keep the sprite perfectly flat on the ground terrain
            transform.rotation = Quaternion.Euler(90f, angleY, 0f);
        }
    }

    public void SetBasePosition(Vector3 worldPos)
    {
        baseWorldPosition = worldPos;
        transform.position = worldPos;
    }
}

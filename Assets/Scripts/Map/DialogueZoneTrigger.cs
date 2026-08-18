using UnityEngine;

/// <summary>
/// Runtime component attached to custom spatial dialogue zones.
/// Contains no editor references, ensuring 100% build stability.
/// </summary>
public class DialogueZoneTrigger : MonoBehaviour
{
    public string dialogueId;
    public float radius = 10f;

    private void OnDrawGizmos()
    {
        // Disegna una sfera colorata trasparente nella scena per aiutare il designer visivamente
        Gizmos.color = new Color(0.18f, 0.48f, 0.93f, 0.25f);
        Gizmos.DrawSphere(transform.position, radius);
        Gizmos.color = new Color(0.18f, 0.48f, 0.93f, 1f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
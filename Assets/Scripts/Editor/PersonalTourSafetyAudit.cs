#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Read-only placement audit for the Personal guide's memorial approach rule.</summary>
public static class PersonalTourSafetyAudit
{
    private const float VisitorSampleDistance = 5f;
    private const int VisitorSampleCount = 8;

    [MenuItem("Tools/ThesisAR/Audit Personal Tour Positions")]
    private static void AuditPositions()
    {
        PersonalGuidance guide = PersonalGuidance.Instance ??
            Object.FindAnyObjectByType<PersonalGuidance>(FindObjectsInactive.Include);
        if (guide == null)
        {
            Debug.LogError("[Personal Tour Audit] No PersonalGuidance component is loaded.");
            return;
        }

        List<Transform> anchors = new List<Transform>();
        foreach (Transform candidate in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
        {
            if (candidate != null && candidate.name.StartsWith("point_", System.StringComparison.OrdinalIgnoreCase))
                anchors.Add(candidate);
        }

        int testedVisitorPositions = 0;
        int acceptedApproaches = 0;
        int blockedAnchors = 0;
        int overlapAtHalfMeterRadius = CountOverlappingAnchorPairs(anchors, 0.5f);
        int overlapAtOneMeterRadius = CountOverlappingAnchorPairs(anchors, 1.0f);
        int blockedAtHalfMeterClearance = CountBlockedAnchors(guide, anchors, 0.5f);
        int blockedAtThreeQuarterMeterClearance = CountBlockedAnchors(guide, anchors, 0.75f);
        int blockedAtOneMeterClearance = CountBlockedAnchors(guide, anchors, 1.0f);

        foreach (Transform anchor in anchors)
        {
            int validSamples = 0;
            int validApproaches = 0;
            for (int sample = 0; sample < VisitorSampleCount; sample++)
            {
                float angle = sample * (360f / VisitorSampleCount) * Mathf.Deg2Rad;
                Vector3 desiredVisitorPosition = anchor.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * VisitorSampleDistance;
                if (!NavMesh.SamplePosition(desiredVisitorPosition, out NavMeshHit visitorHit, 2f, NavigationAreaMask.VisitorWalkable))
                    continue;

                validSamples++;
                testedVisitorPositions++;
                if (guide.TryFindRespectfulApproachPosition(anchor, visitorHit.position, out _))
                {
                    validApproaches++;
                    acceptedApproaches++;
                }
            }

            if (validSamples > 0 && validApproaches == 0)
            {
                blockedAnchors++;
                Debug.LogWarning($"[Personal Tour Audit] No respectful approach for {anchor.name} from {validSamples} navigable visitor samples.", anchor);
            }
        }

        Debug.Log(
            $"[Personal Tour Audit] anchors={anchors.Count}; visitorSamples={testedVisitorPositions}; " +
            $"acceptedApproaches={acceptedApproaches}; blockedAnchors={blockedAnchors}; " +
            $"blockedAtClearance(0.5m/0.75m/1.0m)={blockedAtHalfMeterClearance}/" +
            $"{blockedAtThreeQuarterMeterClearance}/{blockedAtOneMeterClearance}; " +
            $"overlappingPairs(radius 0.5m)={overlapAtHalfMeterRadius}; " +
            $"overlappingPairs(radius 1.0m)={overlapAtOneMeterRadius}."
        );
    }

    private static int CountOverlappingAnchorPairs(List<Transform> anchors, float radius)
    {
        int overlaps = 0;
        float maximumDistanceSquared = 4f * radius * radius;
        for (int first = 0; first < anchors.Count; first++)
        {
            for (int second = first + 1; second < anchors.Count; second++)
            {
                Vector3 offset = anchors[first].position - anchors[second].position;
                offset.y = 0f;
                if (offset.sqrMagnitude < maximumDistanceSquared) overlaps++;
            }
        }
        return overlaps;
    }

    private static int CountBlockedAnchors(PersonalGuidance guide, List<Transform> anchors, float clearance)
    {
        int blocked = 0;
        foreach (Transform anchor in anchors)
        {
            int validSamples = 0;
            bool hasApproach = false;
            for (int sample = 0; sample < VisitorSampleCount; sample++)
            {
                float angle = sample * (360f / VisitorSampleCount) * Mathf.Deg2Rad;
                Vector3 desiredVisitorPosition = anchor.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * VisitorSampleDistance;
                if (!NavMesh.SamplePosition(desiredVisitorPosition, out NavMeshHit visitorHit, 2f, NavigationAreaMask.VisitorWalkable))
                    continue;

                validSamples++;
                if (guide.TryFindRespectfulApproachPosition(anchor, visitorHit.position, clearance, out _))
                {
                    hasApproach = true;
                    break;
                }
            }

            if (validSamples > 0 && !hasApproach) blocked++;
        }
        return blocked;
    }
}
#endif

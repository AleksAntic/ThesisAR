using UnityEngine.AI;

/// <summary>Shared mask for visitor routes. Buildings and forest are never valid shortcuts.</summary>
public static class NavigationAreaMask
{
    private static int visitorWalkableMask = -1;

    public static int VisitorWalkable
    {
        get
        {
            if (visitorWalkableMask >= 0) return visitorWalkableMask;

            visitorWalkableMask = 0;
            AddArea("Walkable_Roads");
            AddArea("Walkable_Grass");

            if (visitorWalkableMask != 0) return visitorWalkableMask;

            visitorWalkableMask = NavMesh.AllAreas;
            return visitorWalkableMask;
        }
    }

    private static void AddArea(string areaName)
    {
        int areaIndex = NavMesh.GetAreaFromName(areaName);
        if (areaIndex >= 0) visitorWalkableMask |= 1 << areaIndex;
    }
}

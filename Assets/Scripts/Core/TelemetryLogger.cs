using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Advanced Spatio-Temporal Telemetry Recording Engine for ThesisAR User Study.
/// Captures continuous position trajectories (X, Z, Time) and timestamped interaction streams
/// (exact sequence of UI clicks, stone visits, audio events, and navigation route recalculations).
/// Exports structured JSON, Trajectory CSVs, and Action Stream CSVs for spatial heatmap generation
/// and sequential process mining.
/// </summary>
public class TelemetryLogger : MonoBehaviour
{
    public static TelemetryLogger Instance { get; private set; }

    [Header("⚙️ Telemetry Configuration")]
    [Tooltip("Enable tracking metrics during runtime sessions.")]
    [SerializeField] private bool trackingEnabled = true;

    [Tooltip("Sampling interval for spatial trajectory points (seconds). Default: 1.0s")]
    [SerializeField] private float trajectorySamplingInterval = 1.0f;

    [Header("🚶 User Position Tracking")]
    [SerializeField] private Transform userCameraTransform;

    [System.Serializable]
    public class TrajectoryPoint
    {
        public float timeSec;
        public float posX;
        public float posY;
        public float posZ;
        public string activeStoneID;
        public string guidanceMode;
        public bool isNavigating;
    }

    [System.Serializable]
    public class ChronologicalEvent
    {
        public float timeSec;
        public string timestampISO;
        public string eventType;
        public string targetID;
        public string details;
        public float posX;
        public float posZ;
    }

    [System.Serializable]
    public class StoneDwellData
    {
        public string stoneID;
        public float totalDwellSeconds;
        public int visitCount;
    }

    [System.Serializable]
    public class NarrationListenData
    {
        public string clipID;
        public float totalDurationSeconds;
        public float totalListenedSeconds;
        public float completionRatio; // 0.0 to 1.0
    }

    [System.Serializable]
    public class TelemetryReport
    {
        public string sessionID;
        public string participantID;
        public string guidanceMode;
        public string startTimeISO;
        public float sessionDurationSeconds;
        public float totalDistanceWalkedMeters;
        public Vector3 startPosition;
        public int mapOpenCount;
        public int searchUseCount;
        public int modeSwitchCount;
        public int inspectorOpenCount;

        public List<StoneDwellData> stoneDwellTimes = new List<StoneDwellData>();
        public List<NarrationListenData> narrationListens = new List<NarrationListenData>();
        public List<TrajectoryPoint> trajectory = new List<TrajectoryPoint>();
        public List<ChronologicalEvent> actionStream = new List<ChronologicalEvent>();
    }

    private TelemetryReport currentReport;
    private Dictionary<string, float> activeStoneEntryTimes = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, StoneDwellData> stoneDwellDict = new Dictionary<string, StoneDwellData>(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, NarrationListenData> narrationDict = new Dictionary<string, NarrationListenData>(StringComparer.OrdinalIgnoreCase);

    private Vector3 lastUserPosition;
    private float nextTrajectorySampleTime = 0f;
    private string activeNarrationID = null;
    private float activeNarrationStartTime = 0f;
    private float activeNarrationDuration = 0f;
    private string currentActiveStoneID = "";

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        if (userCameraTransform == null && Camera.main != null)
        {
            userCameraTransform = Camera.main.transform;
        }

        if (userCameraTransform != null)
        {
            lastUserPosition = userCameraTransform.position;
        }

        InitializeSession();
    }

    /// <summary>
    /// Starts or resets the active telemetry session data structure.
    /// </summary>
    public void InitializeSession()
    {
        string participantID = ThesisManager.Instance != null ? ThesisManager.Instance.AnonymousUserID : "USER_GUEST";
        string mode = ThesisManager.Instance != null ? ThesisManager.Instance.CurrentMode.ToString() : "Personal";
        Vector3 startPos = userCameraTransform != null ? userCameraTransform.position : Vector3.zero;

        currentReport = new TelemetryReport
        {
            sessionID = Guid.NewGuid().ToString("N").Substring(0, 8),
            participantID = participantID,
            guidanceMode = mode,
            startTimeISO = DateTime.Now.ToString("o"),
            sessionDurationSeconds = 0f,
            totalDistanceWalkedMeters = 0f,
            startPosition = startPos,
            mapOpenCount = 0,
            searchUseCount = 0,
            modeSwitchCount = 0,
            inspectorOpenCount = 0,
            stoneDwellTimes = new List<StoneDwellData>(),
            narrationListens = new List<NarrationListenData>(),
            trajectory = new List<TrajectoryPoint>(),
            actionStream = new List<ChronologicalEvent>()
        };

        activeStoneEntryTimes.Clear();
        stoneDwellDict.Clear();
        narrationDict.Clear();
        nextTrajectorySampleTime = Time.time;

        LogEvent("SESSION_START", mode, $"Initial position: ({startPos.x:F2}, {startPos.z:F2})");
        Debug.Log($"[TelemetryLogger] Initialized new spatio-temporal session '{currentReport.sessionID}' at start position ({startPos.x:F2}, {startPos.z:F2}).");
    }

    void Update()
    {
        if (!trackingEnabled || currentReport == null) return;

        // 1. Accumulate physical distance walked by AR Camera
        if (userCameraTransform != null)
        {
            Vector3 currentPos = userCameraTransform.position;
            float deltaDist = Vector3.Distance(currentPos, lastUserPosition);
            if (deltaDist > 0.05f && deltaDist < 10f) // Sanity check filter against sudden GPS warp jumps
            {
                currentReport.totalDistanceWalkedMeters += deltaDist;
                lastUserPosition = currentPos;
            }
        }

        // 2. Update total session duration
        currentReport.sessionDurationSeconds += Time.deltaTime;

        // 3. Sample Spatial Trajectory Point (X, Y, Z, Time)
        if (Time.time >= nextTrajectorySampleTime)
        {
            SampleTrajectoryPoint();
            nextTrajectorySampleTime = Time.time + trajectorySamplingInterval;
        }
    }

    public void SetTrackingEnabled(bool enabled)
    {
        if (trackingEnabled == enabled)
            return;

        trackingEnabled = enabled;
        if (trackingEnabled)
        {
            InitializeSession();
            return;
        }

        currentReport = null;
        activeStoneEntryTimes.Clear();
        stoneDwellDict.Clear();
        narrationDict.Clear();
    }

    private void SampleTrajectoryPoint()
    {
        if (userCameraTransform == null || currentReport == null) return;

        Vector3 pos = userCameraTransform.position;
        bool isNav = ThesisManager.Instance != null && ThesisManager.Instance.CurrentGuidanceSystem != null;

        TrajectoryPoint pt = new TrajectoryPoint
        {
            timeSec = currentReport.sessionDurationSeconds,
            posX = pos.x,
            posY = pos.y,
            posZ = pos.z,
            activeStoneID = currentActiveStoneID,
            guidanceMode = currentReport.guidanceMode,
            isNavigating = isNav
        };

        currentReport.trajectory.Add(pt);
    }

    #region 📍 Chronological Action Stream Logging
    /// <summary>
    /// Logs a time-stamped interaction event into the chronological action sequence.
    /// </summary>
    public void LogEvent(string eventType, string targetID = "", string details = "")
    {
        if (currentReport == null || !trackingEnabled) return;

        Vector3 pos = userCameraTransform != null ? userCameraTransform.position : Vector3.zero;

        ChronologicalEvent evt = new ChronologicalEvent
        {
            timeSec = currentReport.sessionDurationSeconds,
            timestampISO = DateTime.Now.ToString("o"),
            eventType = eventType,
            targetID = targetID,
            details = details,
            posX = pos.x,
            posZ = pos.z
        };

        currentReport.actionStream.Add(evt);
        Debug.Log($"[TelemetryLogger Event] [{evt.timeSec:F1}s] {eventType} -> target: '{targetID}', details: '{details}' at ({pos.x:F1}, {pos.z:F1})");
    }
    #endregion

    #region 📍 Dwell Time Tracking
    public void OnStoneEntered(string stoneID)
    {
        if (string.IsNullOrEmpty(stoneID) || !trackingEnabled) return;

        currentActiveStoneID = stoneID;
        activeStoneEntryTimes[stoneID] = Time.time;

        if (!stoneDwellDict.TryGetValue(stoneID, out StoneDwellData data))
        {
            data = new StoneDwellData { stoneID = stoneID, totalDwellSeconds = 0f, visitCount = 0 };
            stoneDwellDict[stoneID] = data;
        }
        data.visitCount++;

        LogEvent("STONE_ARRIVED", stoneID, $"Visit #{data.visitCount}");
    }

    public void OnStoneExited(string stoneID)
    {
        if (string.IsNullOrEmpty(stoneID) || !trackingEnabled) return;

        if (string.Equals(currentActiveStoneID, stoneID, StringComparison.OrdinalIgnoreCase))
        {
            currentActiveStoneID = "";
        }

        if (activeStoneEntryTimes.TryGetValue(stoneID, out float entryTime))
        {
            float elapsed = Time.time - entryTime;
            activeStoneEntryTimes.Remove(stoneID);

            if (stoneDwellDict.TryGetValue(stoneID, out StoneDwellData data))
            {
                data.totalDwellSeconds += elapsed;
                LogEvent("STONE_LEFT", stoneID, $"Dwell time: {elapsed:F1}s");
            }
        }
    }
    #endregion

    #region 🎧 Narration Listening Metrics
    public void OnNarrationStarted(string clipID, float clipDurationSeconds)
    {
        if (string.IsNullOrEmpty(clipID) || !trackingEnabled) return;

        if (!string.IsNullOrEmpty(activeNarrationID))
        {
            OnNarrationStopped(activeNarrationID);
        }

        activeNarrationID = clipID;
        activeNarrationStartTime = Time.time;
        activeNarrationDuration = clipDurationSeconds;

        if (!narrationDict.TryGetValue(clipID, out NarrationListenData data))
        {
            data = new NarrationListenData
            {
                clipID = clipID,
                totalDurationSeconds = clipDurationSeconds,
                totalListenedSeconds = 0f,
                completionRatio = 0f
            };
            narrationDict[clipID] = data;
        }

        LogEvent("AUDIO_STARTED", clipID, $"Duration: {clipDurationSeconds:F1}s");
    }

    public void OnNarrationStopped(string clipID)
    {
        if (!trackingEnabled) return;

        string idToStop = string.IsNullOrEmpty(clipID) ? activeNarrationID : clipID;
        if (string.IsNullOrEmpty(idToStop)) return;

        float listened = Time.time - activeNarrationStartTime;
        activeNarrationID = null;

        if (narrationDict.TryGetValue(idToStop, out NarrationListenData data))
        {
            data.totalListenedSeconds += listened;
            if (data.totalDurationSeconds > 0)
            {
                data.completionRatio = Mathf.Clamp01(data.totalListenedSeconds / data.totalDurationSeconds);
            }
            LogEvent("AUDIO_STOPPED", idToStop, $"Listened: {listened:F1}s / {data.totalDurationSeconds:F1}s ({data.completionRatio * 100:F0}%)");
        }
    }
    #endregion

    #region 🖱️ Specific UI Interactivity Loggers
    public void LogMapToggled(bool isOpen = true)
    {
        if (currentReport != null) currentReport.mapOpenCount++;
        LogEvent(isOpen ? "MAP_OPENED" : "MAP_CLOSED");
    }

    public void LogSearchUsed(string query = "")
    {
        if (currentReport != null) currentReport.searchUseCount++;
        LogEvent("SEARCH_QUERY", query);
    }

    public void LogModeSwitched(string newMode = "")
    {
        if (currentReport != null)
        {
            currentReport.modeSwitchCount++;
            if (!string.IsNullOrEmpty(newMode)) currentReport.guidanceMode = newMode;
        }
        LogEvent("MODE_CHANGED", newMode);
    }

    public void LogInspectorOpened(string modelName = "")
    {
        if (currentReport != null) currentReport.inspectorOpenCount++;
        LogEvent("INSPECTOR_OPENED", modelName);
    }

    public void LogInspectorClosed(string modelName = "")
    {
        LogEvent("INSPECTOR_CLOSED", modelName);
    }

    public void LogStoneSelected(string stoneID)
    {
        LogEvent("STONE_SELECTED", stoneID);
    }

    public void LogStoneDeselected(string stoneID)
    {
        LogEvent("STONE_DESELECTED", stoneID);
    }
    #endregion

    #region 💾 Export Methods (JSON, Trajectory CSV, Action Stream CSV)
    public void SaveTelemetryLogs()
    {
        if (!trackingEnabled || currentReport == null) return;

        currentReport.stoneDwellTimes = new List<StoneDwellData>(stoneDwellDict.Values);
        currentReport.narrationListens = new List<NarrationListenData>(narrationDict.Values);

        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string prefix = $"{currentReport.participantID}_{currentReport.guidanceMode}_{timestamp}";

        string jsonPath = Path.Combine(Application.persistentDataPath, $"session_{prefix}.json");
        string trajCsvPath = Path.Combine(Application.persistentDataPath, $"trajectory_{prefix}.csv");
        string streamCsvPath = Path.Combine(Application.persistentDataPath, $"action_stream_{prefix}.csv");
        string summaryCsvPath = Path.Combine(Application.persistentDataPath, $"summary_{prefix}.csv");

        // 1. Export Master JSON
        string jsonOutput = JsonUtility.ToJson(currentReport, true);
        File.WriteAllText(jsonPath, jsonOutput);

        // 2. Export Spatial Trajectory CSV
        string trajCsv = BuildTrajectoryCsv(currentReport);
        File.WriteAllText(trajCsvPath, trajCsv);

        // 3. Export Action Sequence Stream CSV
        string streamCsv = BuildActionStreamCsv(currentReport);
        File.WriteAllText(streamCsvPath, streamCsv);

        // 4. Export Executive Summary CSV
        string summaryCsv = BuildSummaryCsv(currentReport);
        File.WriteAllText(summaryCsvPath, summaryCsv);

        Debug.Log($"[TelemetryLogger] 💾 Master Session exported to JSON: '{jsonPath}'");
        Debug.Log($"[TelemetryLogger] 🗺️ Trajectory CSV exported: '{trajCsvPath}' ({currentReport.trajectory.Count} points)");
        Debug.Log($"[TelemetryLogger] ⏱️ Action Stream CSV exported: '{streamCsvPath}' ({currentReport.actionStream.Count} events)");
    }

    private string BuildTrajectoryCsv(TelemetryReport report)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("TimeSec,PosX,PosY,PosZ,ActiveStoneID,GuidanceMode,IsNavigating");
        foreach (var pt in report.trajectory)
        {
            sb.AppendLine($"{pt.timeSec:F2},{pt.posX:F3},{pt.posY:F3},{pt.posZ:F3},{pt.activeStoneID},{pt.guidanceMode},{pt.isNavigating}");
        }
        return sb.ToString();
    }

    private string BuildActionStreamCsv(TelemetryReport report)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("TimeSec,TimestampISO,EventType,TargetID,Details,PosX,PosZ");
        foreach (var evt in report.actionStream)
        {
            string cleanDetails = evt.details != null ? evt.details.Replace(",", ";") : "";
            sb.AppendLine($"{evt.timeSec:F2},{evt.timestampISO},{evt.eventType},{evt.targetID},{cleanDetails},{evt.posX:F3},{evt.posZ:F3}");
        }
        return sb.ToString();
    }

    private string BuildSummaryCsv(TelemetryReport report)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("ParticipantID,GuidanceMode,SessionDurationSec,DistanceWalkedMeters,StartPosX,StartPosZ,MapOpens,SearchUses,ModeSwitches,InspectorOpens,TotalStonesVisited,AvgAudioListenRatio");

        int totalStones = report.stoneDwellTimes.Count;
        float avgRatio = 0f;
        if (report.narrationListens.Count > 0)
        {
            float sumRatio = 0f;
            foreach (var n in report.narrationListens) sumRatio += n.completionRatio;
            avgRatio = sumRatio / report.narrationListens.Count;
        }

        sb.AppendLine($"{report.participantID},{report.guidanceMode},{report.sessionDurationSeconds:F1},{report.totalDistanceWalkedMeters:F1},{report.startPosition.x:F2},{report.startPosition.z:F2},{report.mapOpenCount},{report.searchUseCount},{report.modeSwitchCount},{report.inspectorOpenCount},{totalStones},{avgRatio:F2}");
        return sb.ToString();
    }

    void OnApplicationPause(bool paused)
    {
        if (paused) SaveTelemetryLogs();
    }

    void OnDestroy()
    {
        SaveTelemetryLogs();
    }
    #endregion
}

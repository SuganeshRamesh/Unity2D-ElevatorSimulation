using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ElevatorManager : MonoBehaviour
{
    public static ElevatorManager Instance;

    [HideInInspector] public List<Elevator> elevators = new List<Elevator>();

    // Floor indicator lights [floorIndex][elevatorIndex]
    private Image[,] floorIndicators;
    // Direction arrows   [elevatorIndex]
    private Image[] arrowUpImages;
    private Image[] arrowDownImages;
    // Stats labels
    private TextMeshProUGUI[] tripLabels;
    private TextMeshProUGUI[] waitLabels;
    // Speed slider
    private Slider speedSlider;
    private TextMeshProUGUI speedLabel;
    // Log panel
    private Transform logContent;
    private TMP_FontAsset ledFont;
    private int logLineCount = 0;
    private const int MaxLogLines = 50;

    private static readonly Color ColIndicatorOff = new Color(0.15f, 0.15f, 0.20f);
    private static readonly Color ColIndicatorOn = new Color(0.20f, 0.90f, 0.40f);

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Update()
    {
        // Live stats refresh
        if (tripLabels == null) return;
        for (int i = 0; i < elevators.Count; i++)
        {
            if (tripLabels[i] != null)
                tripLabels[i].text = $"Trips: {elevators[i].totalTrips}";
            if (waitLabels[i] != null)
            {
                float avg = elevators[i].requestsServed > 0
                    ? elevators[i].totalWaitTime / elevators[i].requestsServed
                    : 0f;
                waitLabels[i].text = $"Avg Wait: {avg:F1}s";
            }
        }

        // Speed slider
        if (speedSlider != null && speedLabel != null)
        {
            float s = speedSlider.value;
            speedLabel.text = $"Speed: {s:F0} px/s";
            foreach (var e in elevators) e.moveSpeed = s;
        }
    }

    // ─── Dispatch ─────────────────────────────────────────────

    public void RequestElevator(int floor)
    {
        Elevator best = FindBestElevator(floor);
        if (best != null) best.AddRequest(floor);
        else Elevator.OnLogEvent?.Invoke($"All lifts busy for Floor {floor} — queued");
    }

    private Elevator FindBestElevator(int floor)
    {
        Elevator best = null;
        int bestScore = int.MaxValue;
        foreach (var e in elevators)
        {
            if (e.IsFull()) continue;
            int score = Score(e, floor);
            if (score < bestScore) { bestScore = score; best = e; }
        }
        return best;
    }

    private int Score(Elevator e, int floor)
    {
        int dist = e.DistanceTo(floor);
        if (e.IsIdle()) return dist;
        if (e.state == ElevatorState.MovingUp && floor >= e.currentFloor) return dist;
        if (e.state == ElevatorState.MovingDown && floor <= e.currentFloor) return dist;
        return dist + 50;
    }

    // ─── Floor Indicator API ──────────────────────────────────

    public void RegisterFloorIndicators(Image[,] indicators)
    {
        floorIndicators = indicators;
    }

    public void RegisterArrows(Image[] up, Image[] down)
    {
        arrowUpImages = up;
        arrowDownImages = down;
    }

    public void RegisterStatLabels(TextMeshProUGUI[] trips, TextMeshProUGUI[] waits)
    {
        tripLabels = trips;
        waitLabels = waits;
    }

    public void RegisterSpeedSlider(Slider s, TextMeshProUGUI lbl)
    {
        speedSlider = s;
        speedLabel = lbl;
    }

    public void RegisterLogPanel(Transform content, TMP_FontAsset font)
    {
        logContent = content;
        ledFont = font;
        Elevator.OnLogEvent += AddLogEntry;
    }

    public void UpdateFloorIndicators()
    {
        if (floorIndicators == null) return;
        // Reset all
        for (int f = 0; f < 4; f++)
            for (int e = 0; e < elevators.Count; e++)
                floorIndicators[f, e].color = ColIndicatorOff;
        // Light up current floors
        for (int i = 0; i < elevators.Count; i++)
        {
            int cf = Mathf.Clamp(elevators[i].currentFloor, 0, 3);
            floorIndicators[cf, i].color = ColIndicatorOn;
        }
    }

    public void UpdateArrows(int elevIdx, ElevatorState state)
    {
        if (arrowUpImages == null || elevIdx >= arrowUpImages.Length) return;

        var upHelper = arrowUpImages[elevIdx]?.GetComponent<ArrowColorHelper>();
        var downHelper = arrowDownImages[elevIdx]?.GetComponent<ArrowColorHelper>();

        upHelper?.SetActive(state == ElevatorState.MovingUp);
        downHelper?.SetActive(state == ElevatorState.MovingDown);
    }


    // ─── Log Panel ────────────────────────────────────────────

    private void AddLogEntry(string message)
    {
        if (logContent == null) return;
        if (logLineCount >= MaxLogLines)
        {
            Destroy(logContent.GetChild(0).gameObject);
            logLineCount--;
        }

        var go = new GameObject("LogLine", typeof(RectTransform));
        go.transform.SetParent(logContent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0, 22);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = $"[{System.DateTime.Now:HH:mm:ss}]  {message}";
        tmp.fontSize = 11;
        tmp.color = new Color(0.7f, 0.95f, 0.7f);
        tmp.alignment = TextAlignmentOptions.Left;
        if (ledFont != null) tmp.font = ledFont;

        logLineCount++;

        // Auto-scroll
        var scroll = logContent.GetComponentInParent<UnityEngine.UI.ScrollRect>();
        if (scroll != null)
        {
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = 0f;
        }
    }

    private void OnDestroy()
    {
        Elevator.OnLogEvent -= AddLogEntry;
    }
}

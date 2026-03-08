using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class Elevator : MonoBehaviour
{
    // ─── Inspector ────────────────────────────────────────────
    [Header("Identity")]
    public int elevatorID = 1;

    [Header("Movement")]
    public float floorHeight = 130f;
    public float moveSpeed = 170f;
    public float doorWaitTime = 1.8f;
    public int maxCapacity = 5;
    public float idleReturnDelay = 5f;

    [Header("UI References")]
    public RectTransform elevatorRect;
    public Image carImage;
    public Image doorLeftImage;
    public Image doorRightImage;
    public TextMeshProUGUI floorLabel;
    public TextMeshProUGUI stateLabel;
    public TextMeshProUGUI queueLabel;
    public TextMeshProUGUI passengerLabel;
    public Image cableImage;

    [Header("Audio")]
    public AudioSource movingAudioSource;
    public AudioSource dingAudioSource;
    public AudioSource floorTickAudioSource;

    // ─── Runtime ──────────────────────────────────────────────
    [HideInInspector] public int currentFloor = 0;
    [HideInInspector] public ElevatorState state = ElevatorState.Idle;
    [HideInInspector] public int passengers = 0;

    // SCAN algorithm: sorted ascending list + direction
    private List<int> upQueue = new List<int>();   // floors above
    private List<int> downQueue = new List<int>();   // floors below
    private bool scanUp = true;
    private bool isProcessing = false;
    private Coroutine idleReturnCoroutine;

    private readonly string[] floorNames = { "G", "1", "2", "3" };

    // Colors
    public static readonly Color ColIdle = new Color(0.18f, 0.50f, 0.95f);
    public static readonly Color ColMoving = new Color(0.95f, 0.70f, 0.10f);
    public static readonly Color ColDoorOpen = new Color(0.15f, 0.85f, 0.40f);
    public static readonly Color ColFull = new Color(0.90f, 0.25f, 0.25f);
    public static readonly Color ColDoor = new Color(0.06f, 0.06f, 0.10f);

    // Events for log panel
    public static System.Action<string> OnLogEvent;

    // Stats
    [HideInInspector] public int totalTrips = 0;
    [HideInInspector] public float totalWaitTime = 0f;
    [HideInInspector] public int requestsServed = 0;

    private Dictionary<int, float> requestTimestamps = new Dictionary<int, float>();

    // ─── Public API ───────────────────────────────────────────

    public bool IsIdle() => state == ElevatorState.Idle && upQueue.Count == 0 && downQueue.Count == 0;
    public bool IsFull() => passengers >= maxCapacity;
    public int DistanceTo(int floor) => Mathf.Abs(currentFloor - floor);
    public int PendingCount() => upQueue.Count + downQueue.Count;

    public void AddRequest(int floor)
    {
        if (IsFull())
        {
            OnLogEvent?.Invoke($"Lift {elevatorID} FULL — request Floor {floorNames[floor]} redirected");
            return;
        }

        if (floor == currentFloor && IsIdle())
        {
            StartCoroutine(OpenDoors());
            return;
        }

        if (RequestExists(floor)) return;

        requestTimestamps[floor] = Time.time;

        if (floor >= currentFloor) { upQueue.Add(floor); upQueue.Sort(); }
        else { downQueue.Add(floor); downQueue.Sort(); downQueue.Reverse(); }

        RefreshQueueLabel();
        OnLogEvent?.Invoke($"Floor {floorNames[floor]} → Lift {elevatorID} dispatched");

        if (idleReturnCoroutine != null) { StopCoroutine(idleReturnCoroutine); idleReturnCoroutine = null; }
        if (!isProcessing) StartCoroutine(ProcessSCAN());
    }

    // ─── SCAN Queue Processing ────────────────────────────────

    private IEnumerator ProcessSCAN()
    {
        isProcessing = true;

        while (upQueue.Count > 0 || downQueue.Count > 0)
        {
            int target = -1;

            if (scanUp && upQueue.Count > 0)
            {
                target = upQueue[0];
                upQueue.RemoveAt(0);
            }
            else if (!scanUp && downQueue.Count > 0)
            {
                target = downQueue[0];
                downQueue.RemoveAt(0);
            }
            else
            {
                // Flip direction
                scanUp = !scanUp;
                continue;
            }

            RefreshQueueLabel();
            if (target == currentFloor)
            {
                yield return StartCoroutine(OpenDoors());
                continue;
            }

            state = target > currentFloor ? ElevatorState.MovingUp : ElevatorState.MovingDown;
            RefreshStateLabel();
            UpdateDirectionArrows();

            yield return StartCoroutine(MoveToFloor(target));
            yield return StartCoroutine(OpenDoors());

            // Record stats
            if (requestTimestamps.ContainsKey(target))
            {
                totalWaitTime += Time.time - requestTimestamps[target];
                requestTimestamps.Remove(target);
                requestsServed++;
            }
            totalTrips++;
        }

        state = ElevatorState.Idle;
        RefreshStateLabel();
        UpdateDirectionArrows();
        isProcessing = false;

        // Start idle return countdown
        idleReturnCoroutine = StartCoroutine(IdleReturnToGround());
    }

    // ─── Idle Return ──────────────────────────────────────────

    private IEnumerator IdleReturnToGround()
    {
        yield return new WaitForSeconds(idleReturnDelay);
        if (IsIdle() && currentFloor != 0)
        {
            OnLogEvent?.Invoke($"Lift {elevatorID} returning to Ground (idle)");
            AddRequest(0);
        }
    }

    // ─── Movement ─────────────────────────────────────────────

    private IEnumerator MoveToFloor(int targetFloor)
    {
        SetCarColor(ColMoving);
        PlayMovingSound(true);
        UpdateCable();

        Vector2 startPos = elevatorRect.anchoredPosition;
        Vector2 endPos = new Vector2(startPos.x, targetFloor * floorHeight);
        float distance = Mathf.Abs(endPos.y - startPos.y);
        float duration = distance / moveSpeed;
        float elapsed = 0f;
        int lastFloorPassed = currentFloor;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float tSmooth = t * t * (3f - 2f * t);
            elevatorRect.anchoredPosition = Vector2.Lerp(startPos, endPos, tSmooth);

            // Mid-travel floor label + tick sound
            int displayFloor = Mathf.RoundToInt(Mathf.Lerp(currentFloor, targetFloor, tSmooth));
            displayFloor = Mathf.Clamp(displayFloor, 0, floorNames.Length - 1);
            floorLabel.text = floorNames[displayFloor];

            if (displayFloor != lastFloorPassed)
            {
                lastFloorPassed = displayFloor;
                PlayFloorTick();
                ElevatorManager.Instance.UpdateFloorIndicators();
            }

            UpdateCable();
            yield return null;
        }

        elevatorRect.anchoredPosition = endPos;
        currentFloor = targetFloor;
        RefreshFloorLabel();
        PlayMovingSound(false);
        ElevatorManager.Instance.UpdateFloorIndicators();
    }

    // ─── Door Animation ───────────────────────────────────────

    private IEnumerator OpenDoors()
    {
        state = ElevatorState.DoorOpen;
        RefreshStateLabel();
        SetCarColor(ColDoorOpen);
        PlayDing();

        // Simulate boarding: add random passengers
        int boarding = Random.Range(0, Mathf.Min(2, maxCapacity - passengers) + 1);
        int alighting = Random.Range(0, passengers + 1);
        passengers = Mathf.Clamp(passengers - alighting + boarding, 0, maxCapacity);
        RefreshPassengerLabel();

        yield return StartCoroutine(AnimateDoors(open: true));
        yield return new WaitForSeconds(doorWaitTime);
        yield return StartCoroutine(AnimateDoors(open: false));

        state = ElevatorState.Idle;
        SetCarColor(passengers >= maxCapacity ? ColFull : ColIdle);
        RefreshStateLabel();
    }

    private IEnumerator AnimateDoors(bool open)
    {
        if (doorLeftImage == null || doorRightImage == null) yield break;

        float duration = 0.4f;
        float elapsed = 0f;
        var leftRT = doorLeftImage.rectTransform;
        var rightRT = doorRightImage.rectTransform;
        float carW = elevatorRect.sizeDelta.x;
        float halfW = carW * 0.5f;

        Vector2 lClosed = new Vector2(-halfW * 0.25f, 0f);
        Vector2 rClosed = new Vector2(halfW * 0.25f, 0f);
        Vector2 lOpen = new Vector2(-halfW * 0.75f, 0f);
        Vector2 rOpen = new Vector2(halfW * 0.75f, 0f);

        Vector2 lStart = open ? lClosed : lOpen;
        Vector2 lEnd = open ? lOpen : lClosed;
        Vector2 rStart = open ? rClosed : rOpen;
        Vector2 rEnd = open ? rOpen : rClosed;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            leftRT.anchoredPosition = Vector2.Lerp(lStart, lEnd, t);
            rightRT.anchoredPosition = Vector2.Lerp(rStart, rEnd, t);
            yield return null;
        }
        leftRT.anchoredPosition = lEnd;
        rightRT.anchoredPosition = rEnd;
    }

    // ─── Cable ────────────────────────────────────────────────

    private void UpdateCable()
    {
        if (cableImage == null) return;
        float carTopY = elevatorRect.anchoredPosition.y + elevatorRect.sizeDelta.y * 0.5f;
        float shaftTopY = 4 * floorHeight;
        float cableLen = shaftTopY - carTopY;
        cableImage.rectTransform.sizeDelta = new Vector2(4f, Mathf.Max(0, cableLen));
        cableImage.rectTransform.anchoredPosition = new Vector2(0f, carTopY + cableLen * 0.5f);
    }

    // ─── Audio ────────────────────────────────────────────────

    private void PlayMovingSound(bool play)
    {
        if (movingAudioSource == null) return;
        if (play) { movingAudioSource.loop = true; movingAudioSource.Play(); }
        else movingAudioSource.Stop();
    }

    private void PlayDing() { if (dingAudioSource != null) dingAudioSource.Play(); }
    private void PlayFloorTick() { if (floorTickAudioSource != null) floorTickAudioSource.Play(); }

    // ─── UI Refresh ───────────────────────────────────────────

    public void RefreshFloorLabel()
    {
        if (floorLabel != null) floorLabel.text = floorNames[currentFloor];
    }

    public void RefreshStateLabel()
    {
        if (stateLabel == null) return;
        switch (state)
        {
            case ElevatorState.Idle: stateLabel.text = "IDLE"; break;
            case ElevatorState.MovingUp: stateLabel.text = "▲ UP"; break;
            case ElevatorState.MovingDown: stateLabel.text = "▼ DOWN"; break;
            case ElevatorState.DoorOpen: stateLabel.text = "⬛ OPEN"; break;
        }
    }

    private void RefreshQueueLabel()
    {
        if (queueLabel == null) return;
        string q = "";
        foreach (int f in upQueue) q += $"▲{floorNames[f]} ";
        foreach (int f in downQueue) q += $"▼{floorNames[f]} ";
        queueLabel.text = q.Trim();
    }

    private void RefreshPassengerLabel()
    {
        if (passengerLabel != null)
            passengerLabel.text = $"👤 {passengers}/{maxCapacity}";
    }

    private void SetCarColor(Color c) { if (carImage != null) carImage.color = c; }

    private void UpdateDirectionArrows()
    {
        ElevatorManager.Instance.UpdateArrows(elevatorID - 1, state);
    }

    private bool RequestExists(int floor)
    {
        return upQueue.Contains(floor) || downQueue.Contains(floor);
    }

    // ─── Init ─────────────────────────────────────────────────

    private void Start()
    {
        SetCarColor(ColIdle);
        RefreshFloorLabel();
        RefreshStateLabel();
        RefreshPassengerLabel();
        UpdateCable();
    }
}

using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIManager : MonoBehaviour
{
    [Header("Canvas")]
    public RectTransform canvasRect;

    [Header("Font")]
    public TMP_FontAsset ledFont;

    [Header("Audio Clips")]
    public AudioClip dingClip;
    public AudioClip humClip;
    public AudioClip tickClip;
    public AudioClip clickClip;

    // ─── Layout ───────────────────────────────────────────────
    private const int Floors = 4;
    private const int ElevCount = 3;
    private const float FloorH = 130f;
    private const float ShaftW = 110f;
    private const float ShaftGap = 18f;
    private const float GroundY = 100f;
    private const float ShaftStartX = 240f;

    private readonly string[] FloorNames = { "G", "1", "2", "3" };

    // ─── Colors ───────────────────────────────────────────────
    private static readonly Color ColBG = new Color(0.04f, 0.04f, 0.07f);
    private static readonly Color ColPanel = new Color(0.08f, 0.08f, 0.13f);
    private static readonly Color ColShaft = new Color(0.10f, 0.10f, 0.16f);
    private static readonly Color ColShaftBdr = new Color(0.22f, 0.22f, 0.32f);
    private static readonly Color ColFloorLine = new Color(0.22f, 0.22f, 0.30f);
    private static readonly Color ColCar = new Color(0.18f, 0.50f, 0.95f);
    private static readonly Color ColDoor = new Color(0.06f, 0.06f, 0.10f);
    private static readonly Color ColBtn = new Color(0.12f, 0.38f, 0.75f);
    private static readonly Color ColBtnHover = new Color(0.22f, 0.55f, 1.00f);
    private static readonly Color ColText = new Color(0.85f, 0.95f, 1.00f);
    private static readonly Color ColAccent = new Color(0.40f, 0.85f, 1.00f);
    private static readonly Color ColIndicOff = new Color(0.15f, 0.15f, 0.20f);
    private static readonly Color ColCable = new Color(0.50f, 0.50f, 0.60f);
    private static readonly Color ColArrowOff = new Color(0.18f, 0.18f, 0.24f);

    // ─── Tracked references ───────────────────────────────────
    private Image[,] floorIndicators = new Image[Floors, ElevCount];
    private Image[] arrowUp = new Image[ElevCount];
    private Image[] arrowDown = new Image[ElevCount];
    private TextMeshProUGUI[] tripLabels = new TextMeshProUGUI[ElevCount];
    private TextMeshProUGUI[] waitLabels = new TextMeshProUGUI[ElevCount];
    private Button[] callButtons = new Button[Floors];
    private Image[] callBtnImages = new Image[Floors];
    private bool[] callPending = new bool[Floors];

    private AudioSource clickSource;

    // ─────────────────────────────────────────────────────────

    private void Start()
    {
        BuildUI();
        ElevatorManager.Instance.RegisterFloorIndicators(floorIndicators);
        ElevatorManager.Instance.RegisterArrows(arrowUp, arrowDown);
        ElevatorManager.Instance.RegisterStatLabels(tripLabels, waitLabels);
        ElevatorManager.Instance.UpdateFloorIndicators();

        // Subscribe to floor arrivals for button reset
        Elevator.OnLogEvent += HandleLogForButtonReset;
    }

    private void OnDestroy()
    {
        Elevator.OnLogEvent -= HandleLogForButtonReset;
    }

    // ─── UI Construction ──────────────────────────────────────

    private void BuildUI()
    {
        // Background
        var bg = MakePanel("BG", canvasRect, Vector2.zero, Vector2.one, ColBG);
        bg.GetComponent<RectTransform>().sizeDelta = Vector2.zero;

        // Click audio source
        clickSource = new GameObject("ClickAudio").AddComponent<AudioSource>();
        clickSource.transform.SetParent(transform);
        clickSource.playOnAwake = false;
        clickSource.spatialBlend = 0f;
        clickSource.volume = 0.6f;
        clickSource.clip = clickClip;

        BuildTitleBar();
        BuildFloorLabelsAndLines();
        BuildShaftsAndElevators();
        BuildCallButtons();
        BuildLogPanel();
        BuildSpeedSlider();
        BuildStatsPanel();
    }

    // ─── Title ────────────────────────────────────────────────

    private void BuildTitleBar()
    {
        var bar = MakePanel("TitleBar", canvasRect,
            new Vector2(0, 1), new Vector2(1, 1), ColPanel);
        var rt = bar.GetComponent<RectTransform>();
        rt.anchoredPosition = new Vector2(0, -30);
        rt.sizeDelta = new Vector2(0, 60);

        MakeTMP("Title", rt, "🏢   ELEVATOR SIMULATION   🏢",
            new Vector2(0.5f, 0.5f), Vector2.zero,
            new Vector2(700, 50), 26, ColAccent);
    }

    // ─── Floor Labels + Lines ─────────────────────────────────

    private void BuildFloorLabelsAndLines()
    {
        float totalShaftWidth = ElevCount * (ShaftW + ShaftGap);

        for (int f = 0; f < Floors; f++)
        {
            float y = GroundY + f * FloorH;

            MakeTMP($"FL_{f}", canvasRect, $"Floor  {FloorNames[f]}",
                Vector2.zero, new Vector2(60, y),
                new Vector2(130, 40), 16, ColText);

            // Floor line
            var line = MakePanel($"Line_{f}", canvasRect, Vector2.zero, Vector2.zero, ColFloorLine);
            var lrt = line.GetComponent<RectTransform>();
            lrt.anchoredPosition = new Vector2(ShaftStartX + totalShaftWidth * 0.5f - ShaftGap * 0.5f, y - FloorH * 0.5f);
            lrt.sizeDelta = new Vector2(totalShaftWidth + 10f, 2f);

            // Floor indicators row (one per elevator)
            for (int e = 0; e < ElevCount; e++)
            {
                float xPos = ShaftStartX + e * (ShaftW + ShaftGap) + ShaftW * 0.5f;
                var dot = MakePanel($"Ind_{f}_{e}", canvasRect, Vector2.zero, Vector2.zero, ColIndicOff);
                var drt = dot.GetComponent<RectTransform>();
                drt.anchoredPosition = new Vector2(xPos, y + FloorH * 0.45f);
                drt.sizeDelta = new Vector2(14f, 14f);
                floorIndicators[f, e] = dot.GetComponent<Image>();
            }
        }
    }

    // ─── Shafts + Elevator Cars ───────────────────────────────

    private void BuildShaftsAndElevators()
    {
        for (int i = 0; i < ElevCount; i++)
        {
            float xPos = ShaftStartX + i * (ShaftW + ShaftGap) + ShaftW * 0.5f;
            float shaftH = Floors * FloorH;
            float shaftCY = GroundY + shaftH * 0.5f - FloorH * 0.5f;

            // Shaft border
            var sb = MakePanel($"ShaftBdr_{i}", canvasRect, Vector2.zero, Vector2.zero, ColShaftBdr);
            var sbrT = sb.GetComponent<RectTransform>();
            sbrT.anchoredPosition = new Vector2(xPos, shaftCY);
            sbrT.sizeDelta = new Vector2(ShaftW + 4f, shaftH + 4f);

            // Shaft
            var sh = MakePanel($"Shaft_{i}", canvasRect, Vector2.zero, Vector2.zero, ColShaft);
            var shT = sh.GetComponent<RectTransform>();
            shT.anchoredPosition = new Vector2(xPos, shaftCY);
            shT.sizeDelta = new Vector2(ShaftW, shaftH);

            // Shaft label
            MakeTMP($"ShaftLbl_{i}", canvasRect, $"LIFT  {i + 1}",
                Vector2.zero, new Vector2(xPos, GroundY + shaftH + 22),
                new Vector2(ShaftW, 28), 15, ColAccent);

            // Direction arrows (above shaft)
            var arrowPanel = MakePanel($"ArrowPanel_{i}", canvasRect, Vector2.zero, Vector2.zero,
                new Color(0.08f, 0.08f, 0.12f));
            var apRT = arrowPanel.GetComponent<RectTransform>();
            apRT.anchoredPosition = new Vector2(xPos, GroundY + shaftH + 52);
            apRT.sizeDelta = new Vector2(ShaftW, 26f);

            var upTMP = MakeTMP($"ArrowUp_{i}", apRT.GetComponent<RectTransform>(), "▲",
                new Vector2(0.3f, 0.5f), Vector2.zero, new Vector2(30, 24), 16, ColArrowOff);
            var downTMP = MakeTMP($"ArrowDn_{i}", apRT.GetComponent<RectTransform>(), "▼",
                new Vector2(0.7f, 0.5f), Vector2.zero, new Vector2(30, 24), 16, ColArrowOff);
            arrowUp[i] = upTMP.GetComponent<Image>() ?? upTMP.AddComponent<Image>();
            arrowDown[i] = downTMP.GetComponent<Image>() ?? downTMP.AddComponent<Image>();
            // Use TMP color instead of Image color for arrows
            var upArrowTMP = upTMP.GetComponent<TextMeshProUGUI>();
            var downArrowTMP = downTMP.GetComponent<TextMeshProUGUI>();

            // Store TMP refs for color updates (override with TMP approach)
            arrowUp[i] = RegisterArrowTMP(upArrowTMP, i, true);
            arrowDown[i] = RegisterArrowTMP(downArrowTMP, i, false);

            // Cable
            var cable = MakePanel($"Cable_{i}", canvasRect, Vector2.zero, Vector2.zero, ColCable);
            var cableT = cable.GetComponent<RectTransform>();
            cableT.anchoredPosition = new Vector2(xPos, GroundY + shaftH);
            cableT.sizeDelta = new Vector2(4f, 0f);

            // ── Elevator Car ──────────────────────────────────
            var carGO = MakePanel($"Car_{i}", canvasRect, Vector2.zero, Vector2.zero, ColCar);
            var carRT = carGO.GetComponent<RectTransform>();
            var carImg = carGO.GetComponent<Image>();
            carRT.anchoredPosition = new Vector2(xPos, GroundY);
            carRT.sizeDelta = new Vector2(ShaftW - 10f, FloorH - 16f);

            // Floor number display
            var fnBG = MakePanel("FloorBG", carRT, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Color(0f, 0f, 0f, 0.65f));
            var fnRT = fnBG.GetComponent<RectTransform>();
            fnRT.anchoredPosition = new Vector2(0, -3);
            fnRT.sizeDelta = new Vector2(ShaftW - 18f, 36f);
            var floorTMP = MakeTMP("FloorNum", fnRT, FloorNames[0],
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(ShaftW - 20f, 34f), 26, ColAccent);

            // State label
            var stateTMP = MakeTMP("State", carRT, "IDLE",
                new Vector2(0.5f, 0f), new Vector2(0, 5),
                new Vector2(ShaftW - 14f, 22f), 12, ColText);

            // Queue label
            var queueTMP = MakeTMP("Queue", carRT, "",
                new Vector2(0.5f, 0.5f), new Vector2(0, 0),
                new Vector2(ShaftW - 14f, 20f), 10,
                new Color(1f, 1f, 0.5f));

            // Passenger label
            var passTMP = MakeTMP("Pass", carRT, "👤 0/5",
                new Vector2(0.5f, 0f), new Vector2(0, 20),
                new Vector2(ShaftW - 14f, 20f), 10,
                new Color(0.8f, 1f, 0.8f));

            // Doors
            var doorLeft = MakeDoor("DoorL", carRT, -1);
            var doorRight = MakeDoor("DoorR", carRT, 1);

            // Stats labels (below shaft)
            var tl = MakeTMP($"Trips_{i}", canvasRect, "Trips: 0",
                Vector2.zero, new Vector2(xPos, GroundY - 20),
                new Vector2(ShaftW, 20), 11, new Color(0.6f, 0.8f, 0.6f));
            var wl = MakeTMP($"Wait_{i}", canvasRect, "Avg Wait: 0.0s",
                Vector2.zero, new Vector2(xPos, GroundY - 38),
                new Vector2(ShaftW, 20), 10, new Color(0.6f, 0.7f, 0.8f));
            tripLabels[i] = tl.GetComponent<TextMeshProUGUI>();
            waitLabels[i] = wl.GetComponent<TextMeshProUGUI>();

            // ── Audio sources ─────────────────────────────────
            var movAudio = carGO.AddComponent<AudioSource>();
            movAudio.clip = humClip; movAudio.loop = true;
            movAudio.volume = 0.25f; movAudio.playOnAwake = false;
            movAudio.spatialBlend = 0f;

            var dingAudio = carGO.AddComponent<AudioSource>();
            dingAudio.clip = dingClip; dingAudio.loop = false;
            dingAudio.volume = 0.8f; dingAudio.playOnAwake = false;
            dingAudio.spatialBlend = 0f;

            var tickAudio = carGO.AddComponent<AudioSource>();
            tickAudio.clip = tickClip; tickAudio.loop = false;
            tickAudio.volume = 0.3f; tickAudio.playOnAwake = false;
            tickAudio.spatialBlend = 0f;

            // ── Apply font ────────────────────────────────────
            if (ledFont != null)
            {
                ApplyFont(floorTMP, ledFont);
                ApplyFont(stateTMP, ledFont);
                ApplyFont(queueTMP, ledFont);
                ApplyFont(passTMP, ledFont);
            }

            // ── Wire Elevator component ───────────────────────
            var elev = carGO.AddComponent<Elevator>();
            elev.elevatorID = i + 1;
            elev.floorHeight = FloorH;
            elev.moveSpeed = 170f;
            elev.doorWaitTime = 1.8f;
            elev.maxCapacity = 5;
            elev.idleReturnDelay = 6f;
            elev.elevatorRect = carRT;
            elev.carImage = carImg;
            elev.doorLeftImage = doorLeft.GetComponent<Image>();
            elev.doorRightImage = doorRight.GetComponent<Image>();
            elev.cableImage = cable.GetComponent<Image>();
            elev.floorLabel = floorTMP.GetComponent<TextMeshProUGUI>();
            elev.stateLabel = stateTMP.GetComponent<TextMeshProUGUI>();
            elev.queueLabel = queueTMP.GetComponent<TextMeshProUGUI>();
            elev.passengerLabel = passTMP.GetComponent<TextMeshProUGUI>();
            elev.movingAudioSource = movAudio;
            elev.dingAudioSource = dingAudio;
            elev.floorTickAudioSource = tickAudio;

            ElevatorManager.Instance.elevators.Add(elev);
        }
    }

    // ─── Call Buttons ─────────────────────────────────────────

    private void BuildCallButtons()
    {
        float callX = ShaftStartX + ElevCount * (ShaftW + ShaftGap) + 30f;

        MakeTMP("CallHdr", canvasRect, "CALL",
            Vector2.zero,
            new Vector2(callX + 55, GroundY + Floors * FloorH + 22),
            new Vector2(110, 28), 16, ColAccent);

        for (int f = Floors - 1; f >= 0; f--)
        {
            float y = GroundY + f * FloorH;
            int fc = f;

            // Glow bg
            var glow = MakePanel($"BtnGlow_{f}", canvasRect, Vector2.zero, Vector2.zero,
                new Color(0.08f, 0.25f, 0.60f, 0.3f));
            var glowRT = glow.GetComponent<RectTransform>();
            glowRT.anchoredPosition = new Vector2(callX + 55, y);
            glowRT.sizeDelta = new Vector2(118, 68);

            var btn = MakeButton($"CallBtn_{f}", canvasRect,
                $"▲  CALL\nFLOOR  {FloorNames[f]}",
                Vector2.zero, new Vector2(callX + 55, y),
                new Vector2(110, 60), ColBtn, ColBtnHover,
                () => OnCallButtonPressed(fc));

            callButtons[f] = btn.GetComponent<Button>();
            callBtnImages[f] = btn.GetComponent<Image>();
        }
    }

    private void OnCallButtonPressed(int floor)
    {
        if (clickSource != null && clickClip != null) clickSource.Play();

        // Flash floor row
        StartCoroutine(FlashFloorRow(floor));

        // Set button to waiting state
        if (!callPending[floor])
        {
            callPending[floor] = true;
            StartCoroutine(PulseButton(floor));
        }

        ElevatorManager.Instance.RequestElevator(floor);
    }

    private System.Collections.IEnumerator FlashFloorRow(int floor)
    {
        float y = GroundY + floor * FloorH - FloorH * 0.5f;
        float totalW = ElevCount * (ShaftW + ShaftGap) + 10f;
        float centerX = ShaftStartX + totalW * 0.5f - ShaftGap * 0.5f;

        var flash = MakePanel($"FlashRow_{floor}", canvasRect,
            Vector2.zero, Vector2.zero, new Color(0.4f, 0.8f, 1f, 0.15f));
        var frt = flash.GetComponent<RectTransform>();
        frt.anchoredPosition = new Vector2(centerX, y);
        frt.sizeDelta = new Vector2(totalW, FloorH);

        yield return new WaitForSeconds(0.35f);
        Destroy(flash);
    }

    private System.Collections.IEnumerator PulseButton(int floor)
    {
        float elapsed = 0f;
        while (callPending[floor] && elapsed < 30f)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.PingPong(elapsed * 2f, 1f);
            if (callBtnImages[floor] != null)
                callBtnImages[floor].color = Color.Lerp(ColBtn,
                    new Color(0.3f, 0.7f, 1f), t);
            yield return null;
        }
        if (callBtnImages[floor] != null)
            callBtnImages[floor].color = ColBtn;
    }

    // Reset button when elevator arrives
    private void HandleLogForButtonReset(string msg)
    {
        for (int f = 0; f < Floors; f++)
        {
            if (msg.Contains($"arrived at Floor {FloorNames[f]}") ||
                msg.Contains($"Floor {FloorNames[f]} →"))
            {
                callPending[f] = false;
            }
        }
    }

    // ─── Log Panel ────────────────────────────────────────────

    private void BuildLogPanel()
    {
        float logX = ShaftStartX + ElevCount * (ShaftW + ShaftGap) + 155f;
        float logW = 280f;
        float logH = Floors * FloorH + 20f;

        var logBG = MakePanel("LogBG", canvasRect, Vector2.zero, Vector2.zero, ColPanel);
        var logBGRT = logBG.GetComponent<RectTransform>();
        logBGRT.anchoredPosition = new Vector2(logX + logW * 0.5f, GroundY + logH * 0.5f - FloorH * 0.5f);
        logBGRT.sizeDelta = new Vector2(logW, logH);

        MakeTMP("LogHdr", logBGRT, "EVENT LOG",
            new Vector2(0.5f, 1f), new Vector2(0, -14),
            new Vector2(logW - 10, 26), 14, ColAccent);

        // Scroll view
        var scrollGO = new GameObject("LogScroll", typeof(RectTransform), typeof(ScrollRect));
        scrollGO.transform.SetParent(logBGRT, false);
        var scrollRT = scrollGO.GetComponent<RectTransform>();
        scrollRT.anchorMin = new Vector2(0, 0); scrollRT.anchorMax = Vector2.one;
        scrollRT.offsetMin = new Vector2(5, 5); scrollRT.offsetMax = new Vector2(-5, -28);

        var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewport.transform.SetParent(scrollGO.transform, false);
        var vpRT = viewport.GetComponent<RectTransform>();
        vpRT.anchorMin = Vector2.zero; vpRT.anchorMax = Vector2.one;
        vpRT.sizeDelta = Vector2.zero;
        viewport.GetComponent<Image>().color = new Color(0, 0, 0, 0.01f);
        viewport.GetComponent<Mask>().showMaskGraphic = false;

        var content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(viewport.transform, false);
        var contentRT = content.GetComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0, 1); contentRT.anchorMax = new Vector2(1, 1);
        contentRT.pivot = new Vector2(0.5f, 1f);
        contentRT.sizeDelta = new Vector2(0, 0);

        var vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.spacing = 1f;

        var csf = content.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var sr = scrollGO.GetComponent<ScrollRect>();
        sr.content = contentRT;
        sr.viewport = vpRT;
        sr.horizontal = false;
        sr.vertical = true;
        sr.scrollSensitivity = 20f;

        ElevatorManager.Instance.RegisterLogPanel(content.transform, ledFont);
        ElevatorManager.Instance.RegisterSpeedSlider(null, null);
    }

    // ─── Speed Slider ─────────────────────────────────────────

    private void BuildSpeedSlider()
    {
        float panelY = GroundY - 70f;
        float panelX = ShaftStartX + (ElevCount * (ShaftW + ShaftGap)) * 0.5f;

        var bg = MakePanel("SpeedBG", canvasRect, Vector2.zero, Vector2.zero, ColPanel);
        var rt = bg.GetComponent<RectTransform>();
        rt.anchoredPosition = new Vector2(panelX, panelY);
        rt.sizeDelta = new Vector2(380, 44);

        MakeTMP("SpeedLbl", rt, "ELEVATOR SPEED",
            new Vector2(0.15f, 0.5f), Vector2.zero,
            new Vector2(140, 36), 13, ColText);

        var valueLbl = MakeTMP("SpeedVal", rt, "Speed: 170 px/s",
            new Vector2(0.82f, 0.5f), Vector2.zero,
            new Vector2(120, 36), 12, ColAccent);

        // Slider
        var sliderGO = new GameObject("SpeedSlider", typeof(RectTransform));
        sliderGO.transform.SetParent(rt, false);
        var slRT = sliderGO.GetComponent<RectTransform>();
        slRT.anchorMin = slRT.anchorMax = new Vector2(0.5f, 0.5f);
        slRT.anchoredPosition = new Vector2(10, 0);
        slRT.sizeDelta = new Vector2(150, 20);

        // Background
        var slBG = MakePanel("SlBG", slRT, Vector2.zero, Vector2.one,
            new Color(0.15f, 0.15f, 0.22f));
        slBG.GetComponent<RectTransform>().sizeDelta = Vector2.zero;

        // Fill area
        var fillArea = new GameObject("FillArea", typeof(RectTransform));
        fillArea.transform.SetParent(sliderGO.transform, false);
        var faRT = fillArea.GetComponent<RectTransform>();
        faRT.anchorMin = Vector2.zero; faRT.anchorMax = Vector2.one;
        faRT.offsetMin = new Vector2(5, 0); faRT.offsetMax = new Vector2(-5, 0);

        var fill = MakePanel("Fill", faRT, Vector2.zero, Vector2.one,
            new Color(0.2f, 0.6f, 1f));
        fill.GetComponent<RectTransform>().sizeDelta = Vector2.zero;

        // Handle
        var handleArea = new GameObject("HandleArea", typeof(RectTransform));
        handleArea.transform.SetParent(sliderGO.transform, false);
        var haRT = handleArea.GetComponent<RectTransform>();
        haRT.anchorMin = Vector2.zero; haRT.anchorMax = Vector2.one;
        haRT.sizeDelta = Vector2.zero;

        var handle = MakePanel("Handle", haRT, Vector2.zero, Vector2.zero,
            new Color(0.5f, 0.85f, 1f));
        handle.GetComponent<RectTransform>().sizeDelta = new Vector2(20, 20);

        var slider = sliderGO.AddComponent<Slider>();
        slider.minValue = 80f;
        slider.maxValue = 350f;
        slider.value = 170f;
        slider.fillRect = fill.GetComponent<RectTransform>();
        slider.handleRect = handle.GetComponent<RectTransform>();
        slider.targetGraphic = handle.GetComponent<Image>();

        var valTMP = valueLbl.GetComponent<TextMeshProUGUI>();
        slider.onValueChanged.AddListener((v) => {
            valTMP.text = $"Speed: {v:F0} px/s";
            foreach (var e in ElevatorManager.Instance.elevators)
                e.moveSpeed = v;
        });

        ElevatorManager.Instance.RegisterSpeedSlider(slider, valTMP);
    }

    // ─── Stats Panel ──────────────────────────────────────────

    private void BuildStatsPanel()
    {
        // Stats are wired into per-shaft labels built in BuildShaftsAndElevators()
        // Header label
        MakeTMP("StatsHdr", canvasRect, "── STATS ──",
            Vector2.zero,
            new Vector2(ShaftStartX + ElevCount * (ShaftW + ShaftGap) * 0.5f - ShaftGap * 0.5f,
                        GroundY - 10),
            new Vector2(ElevCount * (ShaftW + ShaftGap), 20), 12,
            new Color(0.5f, 0.6f, 0.7f));
    }

    // ─── Helpers ──────────────────────────────────────────────

    private GameObject MakePanel(string name, RectTransform parent,
        Vector2 ancMin, Vector2 ancMax, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = ancMin; rt.anchorMax = ancMax;
        go.GetComponent<Image>().color = color;
        return go;
    }

    private GameObject MakeTMP(string name, RectTransform parent, string text,
        Vector2 anchor, Vector2 pos, Vector2 size, int fs, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = fs;
        tmp.color = color; tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = FontStyles.Bold;
        if (ledFont != null) tmp.font = ledFont;
        return go;
    }

    private GameObject MakeButton(string name, RectTransform parent,
        string label, Vector2 anchor, Vector2 pos, Vector2 size,
        Color normal, Color hover, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = anchor;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        go.GetComponent<Image>().color = normal;
        var btn = go.GetComponent<Button>();
        var cb = btn.colors;
        cb.normalColor = normal; cb.highlightedColor = hover;
        cb.pressedColor = new Color(normal.r * .5f, normal.g * .5f, normal.b * .5f);
        btn.colors = cb;
        btn.onClick.AddListener(onClick);
        var tgo = new GameObject("Lbl", typeof(RectTransform));
        tgo.transform.SetParent(go.transform, false);
        var trt = tgo.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.sizeDelta = Vector2.zero;
        var tmp = tgo.AddComponent<TextMeshProUGUI>();
        tmp.text = label; tmp.fontSize = 13;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white; tmp.fontStyle = FontStyles.Bold;
        if (ledFont != null) tmp.font = ledFont;
        return go;
    }

    private GameObject MakeDoor(string name, RectTransform parent, int side)
    {
        float carW = ShaftW - 10f;
        float carH = FloorH - 16f;
        float doorW = carW * 0.5f - 2f;
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(doorW, carH - 4f);
        rt.anchoredPosition = new Vector2(side * (carW * 0.25f - 1f), 0f);
        go.GetComponent<Image>().color = ColDoor;
        return go;
    }

    private Image RegisterArrowTMP(TextMeshProUGUI tmp, int idx, bool isUp)
    {
        // We use a dummy Image on same GO to allow color updates via ElevatorManager
        // Actual coloring done via TMP color — attach a helper component
        var helper = tmp.gameObject.AddComponent<ArrowColorHelper>();
        helper.tmp = tmp;
        helper.offColor = ColArrowOff;
        helper.onColor = isUp ? new Color(0.2f, 0.9f, 0.4f) : new Color(0.9f, 0.4f, 0.2f);
        // Return dummy image (won't be used directly)
        return tmp.gameObject.AddComponent<Image>();
    }

    private void ApplyFont(GameObject go, TMP_FontAsset font)
    {
        var t = go.GetComponent<TextMeshProUGUI>();
        if (t != null) t.font = font;
    }

    private void ApplyFont(TextMeshProUGUI t, TMP_FontAsset font)
    {
        if (t != null) t.font = font;
    }
}

using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Image = UnityEngine.UI.Image;
using Text = UnityEngine.UI.Text;
using Object = UnityEngine.Object;

public class StartupCheckScreen : MonoBehaviour
{
    [Header("References")]
    public UnityPLCConnectBridge bridge;

    [Header("VR & Startup Options")]
    [Tooltip("Automatically start as soon as all 5 PLC checks pass.")]
    public bool autoStartWhenReady = false;
    [Tooltip("Distance in meters in front of the VR camera to spawn the panel.")]
    public float panelDistance = 1.8f;

    private Canvas _canvas;
    private Text[] _checkTexts = new Text[5];
    private Text _hintText;
    private Button _startButton;
    private bool _started = false;

    private void Awake()
    {
        if (bridge == null)
            bridge = Object.FindAnyObjectByType<UnityPLCConnectBridge>();

        BuildWorldSpaceUI();
        FreezeSimulation(true);
    }

    private void Start()
    {
        PositionInFrontOfCamera();
    }

    private void Update()
    {
        if (_started) return;

        if (bridge == null)
        {
            bridge = Object.FindAnyObjectByType<UnityPLCConnectBridge>();
            if (bridge == null) return;
        }

        _hintText.text = string.Empty;

        bool check1 = bridge.serviceAlive || bridge.serviceExeFound;
        bool check2 = bridge.serviceAlive;
        bool check3 = check2 && (bridge.isConnected || (bridge.statusByte != 0 && bridge.statusByte != 1));
        bool check4 = check3 && (bridge.isConnected || bridge.statusByte != 2);
        bool check5 = check4 && bridge.isConnected;

        UpdateCheckUI(0, check1, "1. PLCSimUnity Process / Executable Ready", "Start PLCSimUnity.exe manually before pressing Play");
        UpdateCheckUI(1, check2, "2. PLCSimUnity Control Service Active", "Ensure Unity and PLCSimUnity.exe run at the same Admin privilege level");
        UpdateCheckUI(2, check3, $"3. Siemens PLCSIM Advanced Connected (Status: {bridge.statusByte})", "Click 'Connect to Selected Instance' in PLCSimUnity Control Center");
        UpdateCheckUI(3, check4, $"4. Virtual Instance Active ('{bridge.instanceName}')", $"Verify '{bridge.instanceName}' is powered ON and in RUN in PLCSIM Manager");
        UpdateCheckUI(4, check5, $"5. Target CPU State: RUN & Ready (State: {bridge.plcOperatingState})", GetPlcStateHint(bridge.plcOperatingState));

        bool allReady = check1 && check2 && check3 && check4 && check5;

        if (allReady)
        {
            _hintText.color = new Color(0.4f, 1f, 0.4f);
            _hintText.text = "<b>Ready:</b> Pull VR Trigger on button, or press <b>[SPACE] / [ENTER]</b>.";
            _startButton.interactable = true;

            if (autoStartWhenReady || IsStartKeyPressed())
            {
                OnStartClicked();
            }
        }
        else
        {
            _hintText.color = Color.yellow;
            _startButton.interactable = false;
        }
    }

    private bool IsStartKeyPressed()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
            return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            return true;
#endif
        return false;
    }

    private void UpdateCheckUI(int index, bool passed, string label, string failHint)
    {
        string mark = passed ? "<color=#00FF00>✔</color>" : "<color=#FF0000>✘</color>";
        _checkTexts[index].text = $"{mark} {label}";
        if (!passed && string.IsNullOrEmpty(_hintText.text))
        {
            _hintText.text = $"<b>Diagnostic Hint:</b> {failHint}";
        }
    }

    private string GetPlcStateHint(byte state)
    {
        switch (state)
        {
            case 0: return "Shared memory Status/State is 0 — match Admin privileges or check PLCSimUnity.";
            case 3: return "Virtual CPU is in STOP mode. Toggle CPU to RUN in PLCSIM Manager.";
            case 1:
            case 2:
            case 4: return "Virtual CPU starting up...";
            default: return "Verify Project2 is in RUN and connected in PLCSimUnity.";
        }
    }

    [ContextMenu("Start Simulation Now")]
    public void OnStartClicked()
    {
        if (_started) return;
        _started = true;

        if (_canvas != null)
            _canvas.enabled = false;

        FreezeSimulation(false);

        if (bridge != null)
            bridge.BeginSimulation();
    }

    private void OnDestroy()
    {
        FreezeSimulation(false);
    }

    private void FreezeSimulation(bool freeze)
    {
        Time.timeScale = freeze ? 0f : 1f;
    }

    private void PositionInFrontOfCamera()
    {
        if (_canvas == null) return;

        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 forward = cam.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f)
                forward = Vector3.forward;
            forward.Normalize();

            _canvas.transform.position = cam.transform.position + forward * Mathf.Max(0.8f, panelDistance);
            _canvas.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }
        else
        {
            _canvas.transform.position = new Vector3(0f, 1.4f, Mathf.Max(0.8f, panelDistance));
            _canvas.transform.rotation = Quaternion.identity;
        }
    }

    private Font GetDefaultUIFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return font;
    }

    private void BuildWorldSpaceUI()
    {
        if (Object.FindAnyObjectByType<EventSystem>() == null)
        {
            GameObject esObj = new GameObject("EventSystem");
            esObj.AddComponent<EventSystem>();
            esObj.AddComponent<StandaloneInputModule>();
        }

        GameObject canvasObj = new GameObject("StartupCheckCanvas");
        _canvas = canvasObj.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.WorldSpace;
        _canvas.worldCamera = Camera.main;
        _canvas.sortingOrder = 100;

        RectTransform canvasRt = canvasObj.GetComponent<RectTransform>();
        canvasRt.sizeDelta = new Vector2(1000f, 650f);
        canvasRt.localScale = Vector3.one * 0.0015f; // 1.5m x 0.975m floating VR panel

        canvasObj.AddComponent<GraphicRaycaster>();

        // Attach XR Interaction Toolkit's TrackedDeviceGraphicRaycaster if present in project
        Type xrRaycasterType = Type.GetType("UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster, Unity.XR.Interaction.Toolkit");
        if (xrRaycasterType != null)
            canvasObj.AddComponent(xrRaycasterType);

        GameObject panel = new GameObject("Panel");
        panel.transform.SetParent(canvasObj.transform, false);
        Image bg = panel.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.1f, 0.12f, 0.96f);
        RectTransform panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin = Vector2.zero;
        panelRt.anchorMax = Vector2.one;
        panelRt.offsetMin = panelRt.offsetMax = Vector2.zero;

        Font font = GetDefaultUIFont();

        for (int i = 0; i < 5; i++)
        {
            GameObject checkObj = new GameObject($"Check_{i}");
            checkObj.transform.SetParent(panel.transform, false);
            Text txt = checkObj.AddComponent<Text>();
            txt.font = font;
            txt.fontSize = 26;
            txt.color = Color.white;
            txt.supportRichText = true;
            RectTransform rt = checkObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.06f, 0.80f - (i * 0.11f));
            rt.anchorMax = new Vector2(0.94f, 0.89f - (i * 0.11f));
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            _checkTexts[i] = txt;
        }

        GameObject hintObj = new GameObject("HintText");
        hintObj.transform.SetParent(panel.transform, false);
        _hintText = hintObj.AddComponent<Text>();
        _hintText.font = font;
        _hintText.fontSize = 22;
        _hintText.color = Color.yellow;
        _hintText.supportRichText = true;
        RectTransform hintRt = hintObj.GetComponent<RectTransform>();
        hintRt.anchorMin = new Vector2(0.06f, 0.21f);
        hintRt.anchorMax = new Vector2(0.94f, 0.30f);
        hintRt.offsetMin = hintRt.offsetMax = Vector2.zero;

        GameObject btnObj = new GameObject("StartButton");
        btnObj.transform.SetParent(panel.transform, false);
        Image btnImg = btnObj.AddComponent<Image>();
        btnImg.color = new Color(0.15f, 0.55f, 0.2f);
        _startButton = btnObj.AddComponent<Button>();
        _startButton.interactable = false;
        _startButton.onClick.AddListener(OnStartClicked);
        RectTransform btnRt = btnObj.GetComponent<RectTransform>();
        btnRt.anchorMin = new Vector2(0.28f, 0.05f);
        btnRt.anchorMax = new Vector2(0.72f, 0.18f);
        btnRt.offsetMin = btnRt.offsetMax = Vector2.zero;

        GameObject btnTxtObj = new GameObject("Text");
        btnTxtObj.transform.SetParent(btnObj.transform, false);
        Text btnTxt = btnTxtObj.AddComponent<Text>();
        btnTxt.font = font;
        btnTxt.fontSize = 26;
        btnTxt.color = Color.white;
        btnTxt.text = "START SIMULATION";
        btnTxt.alignment = TextAnchor.MiddleCenter;
        RectTransform btnTxtRt = btnTxtObj.GetComponent<RectTransform>();
        btnTxtRt.anchorMin = Vector2.zero;
        btnTxtRt.anchorMax = Vector2.one;
        btnTxtRt.offsetMin = btnTxtRt.offsetMax = Vector2.zero;
    }
}
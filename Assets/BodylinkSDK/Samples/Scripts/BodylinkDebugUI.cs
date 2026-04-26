using System;
using System.Collections;
using System.Text;
using BodylinkSDK;
using UnityEngine;
using UnityEngine.UI;

/*
UI Setup:
- Canvas (Screen Space Overlay)
- Panel (top-right corner)
- Optional Text for global SDK status
- Two sections:
  - Player1Panel
  - Player2Panel
- Each panel contains:
  - Text (info)
  - Slider (confidence bar)
*/
public class BodylinkDebugUI : MonoBehaviour
{
    private const int MaxPlayers = 2;
    private const string NoGestureText = "None";

    [Serializable]
    private sealed class PlayerDebugState
    {
        public string GestureName = NoGestureText;
        public float Confidence;
        public float LastGestureTime = -1f;
        public bool Detected;
    }

    [Header("Startup")]
    [SerializeField] private bool autoInitialize = true;
    [SerializeField, Min(0.05f)] private float statusRefreshInterval = 0.25f;
    [SerializeField, Min(0.1f)] private float gestureDetectedDuration = 1f;

    [Header("UI References")]
    [SerializeField] private GameObject player1Panel;
    [SerializeField] private GameObject player2Panel;
    [SerializeField] private Text globalStatusText;
    [SerializeField] private Text player1Text;
    [SerializeField] private Text player2Text;
    [SerializeField] private Slider player1ConfidenceSlider;
    [SerializeField] private Slider player2ConfidenceSlider;

    [Header("Confidence Colors")]
    [SerializeField] private Color highConfidenceColor = new Color(0.1f, 0.8f, 0.2f);
    [SerializeField] private Color mediumConfidenceColor = new Color(1f, 0.85f, 0.15f);
    [SerializeField] private Color lowConfidenceColor = new Color(0.95f, 0.2f, 0.2f);

    private readonly PlayerDebugState[] players =
    {
        new PlayerDebugState(),
        new PlayerDebugState()
    };

    private readonly GameObject[] playerPanels = new GameObject[MaxPlayers];
    private readonly Text[] playerTexts = new Text[MaxPlayers];
    private readonly Slider[] confidenceSliders = new Slider[MaxPlayers];
    private readonly Image[] sliderFillImages = new Image[MaxPlayers];
    private readonly StringBuilder textBuilder = new StringBuilder(128);

    private Bodylink bodylink;
    private BodylinkEvents inputEvents;
    private Coroutine initializeRoutine;
    private WaitForSeconds statusRefreshWait;
    private bool isSubscribed;

    public bool AutoInitialize
    {
        get => autoInitialize;
        set => autoInitialize = value;
    }

    private void OnEnable()
    {
        CacheInspectorReferences();
        AutoFindReferences();
        ConfigureSliders();
        RefreshAllUI();

        if (autoInitialize)
        {
            StartDebugUI();
        }
    }

    private void OnDisable()
    {
        StopDebugUI();
    }

    public void StartDebugUI()
    {
        if (initializeRoutine != null)
        {
            return;
        }

        statusRefreshWait = new WaitForSeconds(statusRefreshInterval);
        initializeRoutine = StartCoroutine(InitializeAndRefreshRoutine());
    }

    public void StopDebugUI()
    {
        if (initializeRoutine != null)
        {
            StopCoroutine(initializeRoutine);
            initializeRoutine = null;
        }

        Unsubscribe();
    }

    private IEnumerator InitializeAndRefreshRoutine()
    {
        while (isActiveAndEnabled)
        {
            if (!isSubscribed)
            {
                TrySubscribe();
            }

            RefreshDetectionStatus();
            RefreshAllUI();

            yield return statusRefreshWait;
        }
    }

    private void TrySubscribe()
    {
        Bodylink nextBodylink = Bodylink.Instance;
        if (nextBodylink == null)
        {
            return;
        }

        if (bodylink != nextBodylink)
        {
            Unsubscribe();
            bodylink = nextBodylink;
            bodylink.OnInitialized += HandleSdkStateChanged;
            bodylink.OnDisposed += HandleSdkStateChanged;
        }

        BodylinkEvents nextInputEvents = bodylink.inputEvents;
        if (nextInputEvents == null)
        {
            return;
        }

        inputEvents = nextInputEvents;
        inputEvents.OnGesture += HandleGesture;
        isSubscribed = true;
        RefreshAllUI();
    }

    private void Unsubscribe()
    {
        if (inputEvents != null && isSubscribed)
        {
            inputEvents.OnGesture -= HandleGesture;
        }

        if (bodylink != null)
        {
            bodylink.OnInitialized -= HandleSdkStateChanged;
            bodylink.OnDisposed -= HandleSdkStateChanged;
        }

        inputEvents = null;
        bodylink = null;
        isSubscribed = false;
    }

    private void HandleSdkStateChanged()
    {
        RefreshDetectionStatus();
        RefreshAllUI();
    }

    private void HandleGesture(int playerIndex, string gestureName, object[] values)
    {
        if (playerIndex < 0 || playerIndex >= MaxPlayers)
        {
            return;
        }

        if (!IsPlayerSlotEnabled(playerIndex))
        {
            return;
        }

        PlayerDebugState state = players[playerIndex];
        state.GestureName = string.IsNullOrEmpty(gestureName) ? NoGestureText : gestureName;
        state.Confidence = ExtractConfidence(values);
        state.LastGestureTime = Time.unscaledTime;
        state.Detected = true;

        RefreshPlayerUI(playerIndex);
        RefreshGlobalStatus();
    }

    private void RefreshDetectionStatus()
    {
        float now = Time.unscaledTime;
        for (int i = 0; i < MaxPlayers; i++)
        {
            if (!IsPlayerSlotEnabled(i))
            {
                players[i].Detected = false;
                continue;
            }

            bool hasRecentGesture = players[i].LastGestureTime >= 0f &&
                                    now - players[i].LastGestureTime <= gestureDetectedDuration;
            players[i].Detected = HasValidPlayerPose(i) || hasRecentGesture;
        }
    }

    private bool HasValidPlayerPose(int playerIndex)
    {
        Bodylink instance = Bodylink.Instance;
        if (instance == null || !instance.IsInitialized || instance.players == null)
        {
            return false;
        }

        if (playerIndex < 0 || playerIndex >= instance.players.Length || instance.players[playerIndex] == null)
        {
            return false;
        }

        var points = instance.players[playerIndex].bodyRawPoints2D;
        if (points == null || points.Count == 0)
        {
            return false;
        }

        int checkCount = Mathf.Min(points.Count, 5);
        for (int i = 0; i < checkCount; i++)
        {
            if (points[i].visibility > 0f || points[i].presence > 0f)
            {
                return true;
            }
        }

        return false;
    }

    private void RefreshAllUI()
    {
        RefreshPanelVisibility();
        RefreshGlobalStatus();
        for (int i = 0; i < MaxPlayers; i++)
        {
            RefreshPlayerUI(i);
        }
    }

    private void RefreshPanelVisibility()
    {
        for (int i = 0; i < MaxPlayers; i++)
        {
            GameObject panel = playerPanels[i];
            if (panel == null)
            {
                continue;
            }

            bool shouldShow = i == 0 || IsPlayerSlotEnabled(i);
            if (panel.activeSelf != shouldShow)
            {
                panel.SetActive(shouldShow);
            }
        }
    }

    private void RefreshGlobalStatus()
    {
        if (globalStatusText == null)
        {
            return;
        }

        Bodylink instance = Bodylink.Instance;
        bool initialized = instance != null && instance.IsInitialized;

        textBuilder.Length = 0;
        textBuilder.Append("SDK: ");
        textBuilder.Append(initialized ? "Initialized" : "Not Initialized");
        textBuilder.Append('\n');
        textBuilder.Append("Active Players: ");
        textBuilder.Append(GetConfiguredPlayerCount());
        textBuilder.Append('\n');
        textBuilder.Append("Detected Players: ");
        textBuilder.Append(GetDetectedPlayerCount());

        globalStatusText.text = textBuilder.ToString();
    }

    private int GetConfiguredPlayerCount()
    {
        Bodylink instance = Bodylink.Instance;
        if (instance == null)
        {
            return 0;
        }

        return Mathf.Clamp(instance.numberOfPlayers, 1, MaxPlayers);
    }

    private int GetDetectedPlayerCount()
    {
        int detectedCount = 0;
        for (int i = 0; i < MaxPlayers; i++)
        {
            if (players[i].Detected)
            {
                detectedCount++;
            }
        }

        return detectedCount;
    }

    private bool IsPlayerSlotEnabled(int playerIndex)
    {
        return playerIndex >= 0 && playerIndex < GetConfiguredPlayerCount();
    }

    private void RefreshPlayerUI(int playerIndex)
    {
        if (playerIndex < 0 || playerIndex >= MaxPlayers)
        {
            return;
        }

        PlayerDebugState state = players[playerIndex];
        Color confidenceColor = GetConfidenceColor(state.Confidence);

        Text infoText = playerTexts[playerIndex];
        if (infoText != null)
        {
            textBuilder.Length = 0;
            textBuilder.Append("Player ");
            textBuilder.Append(playerIndex);
            textBuilder.Append('\n');
            textBuilder.Append("Status: ");
            textBuilder.Append(state.Detected ? "Detected" : "Not Detected");
            textBuilder.Append('\n');
            textBuilder.Append("Gesture: ");
            textBuilder.Append(state.GestureName);
            textBuilder.Append('\n');
            textBuilder.Append("Confidence: ");
            textBuilder.Append(state.Confidence.ToString("0.00"));

            infoText.text = textBuilder.ToString();
            infoText.color = confidenceColor;
        }

        Slider slider = confidenceSliders[playerIndex];
        if (slider != null)
        {
            slider.value = state.Confidence;
        }

        Image fillImage = sliderFillImages[playerIndex];
        if (fillImage != null)
        {
            fillImage.color = confidenceColor;
        }
    }

    private static float ExtractConfidence(object[] values)
    {
        if (values == null || values.Length == 0)
        {
            return 0f;
        }

        for (int i = 0; i < values.Length; i++)
        {
            if (TryGetFloat(values[i], out float confidence))
            {
                return Mathf.Clamp01(Mathf.Abs(confidence));
            }
        }

        return 0f;
    }

    private static bool TryGetFloat(object value, out float result)
    {
        switch (value)
        {
            case float floatValue:
                result = floatValue;
                return true;
            case double doubleValue:
                result = (float)doubleValue;
                return true;
            case int intValue:
                result = intValue;
                return true;
            case long longValue:
                result = longValue;
                return true;
            case short shortValue:
                result = shortValue;
                return true;
            case byte byteValue:
                result = byteValue;
                return true;
            default:
                result = 0f;
                return false;
        }
    }

    private Color GetConfidenceColor(float confidence)
    {
        if (confidence > 0.7f)
        {
            return highConfidenceColor;
        }

        if (confidence > 0.4f)
        {
            return mediumConfidenceColor;
        }

        return lowConfidenceColor;
    }

    private void CacheInspectorReferences()
    {
        playerPanels[0] = player1Panel;
        playerPanels[1] = player2Panel;
        playerTexts[0] = player1Text;
        playerTexts[1] = player2Text;
        confidenceSliders[0] = player1ConfidenceSlider;
        confidenceSliders[1] = player2ConfidenceSlider;
    }

    private void AutoFindReferences()
    {
        Transform[] childTransforms = GetComponentsInChildren<Transform>(true);
        Text[] textComponents = GetComponentsInChildren<Text>(true);
        Slider[] sliderComponents = GetComponentsInChildren<Slider>(true);

        if (playerPanels[0] == null)
        {
            playerPanels[0] = FindGameObjectByName(childTransforms, "player1panel", "player 1 panel", "p1panel", "p1 panel");
        }

        if (playerPanels[1] == null)
        {
            playerPanels[1] = FindGameObjectByName(childTransforms, "player2panel", "player 2 panel", "p2panel", "p2 panel");
        }

        if (globalStatusText == null)
        {
            globalStatusText = FindTextByName(textComponents, "global", "status", "sdk");
        }

        if (playerTexts[0] == null)
        {
            playerTexts[0] = FindTextByName(textComponents, "player1", "player 1", "playerone", "p1");
        }

        if (playerTexts[1] == null)
        {
            playerTexts[1] = FindTextByName(textComponents, "player2", "player 2", "playertwo", "p2");
        }

        AssignFallbackTextReferences(textComponents);

        if (confidenceSliders[0] == null)
        {
            confidenceSliders[0] = FindSliderByName(sliderComponents, "player1", "player 1", "p1");
        }

        if (confidenceSliders[1] == null)
        {
            confidenceSliders[1] = FindSliderByName(sliderComponents, "player2", "player 2", "p2");
        }

        AssignFallbackSliderReferences(sliderComponents);
        AssignFallbackPanelReferences();
    }

    private void ConfigureSliders()
    {
        for (int i = 0; i < MaxPlayers; i++)
        {
            Slider slider = confidenceSliders[i];
            if (slider == null)
            {
                continue;
            }

            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.value = players[i].Confidence;

            sliderFillImages[i] = slider.fillRect != null
                ? slider.fillRect.GetComponent<Image>()
                : null;
        }
    }

    private void AssignFallbackTextReferences(Text[] textComponents)
    {
        for (int i = 0; i < textComponents.Length; i++)
        {
            int slot = GetFirstMissingTextSlot();
            if (slot < 0)
            {
                return;
            }

            Text text = textComponents[i];
            if (text == null || text == globalStatusText || IsAlreadyAssigned(text))
            {
                continue;
            }

            playerTexts[slot] = text;
        }
    }

    private void AssignFallbackSliderReferences(Slider[] sliderComponents)
    {
        for (int i = 0; i < sliderComponents.Length; i++)
        {
            int slot = GetFirstMissingSliderSlot();
            if (slot < 0)
            {
                return;
            }

            Slider slider = sliderComponents[i];
            if (slider == null || IsAlreadyAssigned(slider))
            {
                continue;
            }

            confidenceSliders[slot] = slider;
        }
    }

    private void AssignFallbackPanelReferences()
    {
        if (playerPanels[0] == null)
        {
            playerPanels[0] = ResolvePanelFromReferences(playerTexts[0], confidenceSliders[0]);
        }

        if (playerPanels[1] == null)
        {
            playerPanels[1] = ResolvePanelFromReferences(playerTexts[1], confidenceSliders[1]);
        }
    }

    private GameObject ResolvePanelFromReferences(Text text, Slider slider)
    {
        if (text != null && text.transform.parent != null)
        {
            return text.transform.parent.gameObject;
        }

        if (slider != null && slider.transform.parent != null)
        {
            return slider.transform.parent.gameObject;
        }

        return null;
    }

    private int GetFirstMissingTextSlot()
    {
        for (int i = 0; i < MaxPlayers; i++)
        {
            if (playerTexts[i] == null)
            {
                return i;
            }
        }

        return -1;
    }

    private int GetFirstMissingSliderSlot()
    {
        for (int i = 0; i < MaxPlayers; i++)
        {
            if (confidenceSliders[i] == null)
            {
                return i;
            }
        }

        return -1;
    }

    private bool IsAlreadyAssigned(Text text)
    {
        return playerTexts[0] == text || playerTexts[1] == text;
    }

    private bool IsAlreadyAssigned(Slider slider)
    {
        return confidenceSliders[0] == slider || confidenceSliders[1] == slider;
    }

    private static Text FindTextByName(Text[] textComponents, params string[] tokens)
    {
        for (int i = 0; i < textComponents.Length; i++)
        {
            Text text = textComponents[i];
            if (text != null && NameContainsAny(text.name, tokens))
            {
                return text;
            }
        }

        return null;
    }

    private static GameObject FindGameObjectByName(Transform[] transforms, params string[] tokens)
    {
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform target = transforms[i];
            if (target != null && NameContainsAny(target.name, tokens))
            {
                return target.gameObject;
            }
        }

        return null;
    }

    private static Slider FindSliderByName(Slider[] sliders, params string[] tokens)
    {
        for (int i = 0; i < sliders.Length; i++)
        {
            Slider slider = sliders[i];
            if (slider != null && NameContainsAny(slider.name, tokens))
            {
                return slider;
            }
        }

        return null;
    }

    private static bool NameContainsAny(string source, string[] tokens)
    {
        if (string.IsNullOrEmpty(source))
        {
            return false;
        }

        for (int i = 0; i < tokens.Length; i++)
        {
            if (source.IndexOf(tokens[i], StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }
}

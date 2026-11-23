using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class AAH_HospitalPauseMenu : MonoBehaviour
{
    private static readonly Color Ink = new Color32(25, 36, 45, 250);
    private static readonly Color Control = new Color32(58, 82, 91, 255);
    private static readonly Color Cream = new Color32(248, 232, 194, 255);
    private const string VolumeKey = "VetNightmare.Pause.MasterVolume";
    private RectTransform overlay, actions, options;
    private Button pause, resume, settings, back;
    private TMP_Text volume, status;
    private AAH_GameManager manager;
    private SC_FPSController player;
    private AAH_PlayerInventory inventory;
    private bool paused, loading, inventoryWasEnabled, couldMove, cursorVisible, audioPaused;
    private CursorLockMode cursorLock;
    private float previousSpeed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RestoreVolume()
    {
        if (PlayerPrefs.HasKey(VolumeKey)) AudioListener.volume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey));
    }

    private void Start()
    {
        manager = GetComponent<AAH_GameManager>();
        foreach (var root in gameObject.scene.GetRootGameObjects())
        {
            if (player == null) player = root.GetComponentInChildren<SC_FPSController>(true);
            if (inventory == null) inventory = root.GetComponentInChildren<AAH_PlayerInventory>(true);
        }
        if (EventSystem.current == null)
        {
            var e = new GameObject("Hospital UI Events", typeof(EventSystem));
            e.transform.SetParent(transform, false);
            e.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        var canvasObject = new GameObject("Hospital Pause Canvas", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;
        var rootRect = (RectTransform)canvas.transform;
        pause = MakeButton(rootRect, "Tạm dừng (P)", 0f, Open, 150f);
        var pin = (RectTransform)pause.transform;
        pin.anchorMin = pin.anchorMax = pin.pivot = new Vector2(0, 1);
        pin.anchoredPosition = new Vector2(14, -14);

        overlay = Rect(rootRect, "Pause overlay", 0, 0, 0);
        Stretch(overlay);
        overlay.gameObject.AddComponent<Image>().color = new Color32(9, 15, 21, 220);
        var card = Rect(overlay, "Pause panel", 366, 390, 0);
        card.gameObject.AddComponent<Image>().color = Ink;
        actions = Rect(card, "Actions", 350, 375, 0);
        Title(actions, "Tạm dừng", 126, 35);
        resume = MakeButton(actions, "Tiếp tục", 66, Close);
        MakeButton(actions, "Chơi lại ngày này", 14, () => Load(gameObject.scene.name));
        settings = MakeButton(actions, "Cài đặt", -38, () => Settings(true));
        MakeButton(actions, "Về menu chính", -90, () => Load("menu"));
        MakeButton(actions, "Thoát game", -142, Quit);
        options = Rect(card, "Settings", 350, 375, 0);
        Title(options, "Cài đặt", 122, 35);
        volume = Title(options, "", 62, 23);
        MakeButton(options, "Giảm âm lượng", 8, () => ChangeVolume(-0.1f));
        MakeButton(options, "Tăng âm lượng", -44, () => ChangeVolume(0.1f));
        back = MakeButton(options, "Quay lại", -112, () => Settings(false));
        options.gameObject.SetActive(false);
        status = Title(overlay, "", -226, 18);
        overlay.gameObject.SetActive(false);
    }

    private static RectTransform Rect(Transform parent, string name, float w, float h, float y)
    {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(w, h);
        rect.anchoredPosition = new Vector2(0f, y);
        return rect;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static TMP_Text Title(RectTransform parent, string value, float y, float fontSize)
    {
        var text = Rect(parent, "Label", parent.sizeDelta.x > 0 ? parent.sizeDelta.x : 600, 48, y)
            .gameObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.color = Cream;
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        return text;
    }

    private static Button MakeButton(RectTransform parent, string value, float y,
        UnityEngine.Events.UnityAction action, float w = 276)
    {
        var rect = Rect(parent, value, w, 44, y);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = Control;
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);
        Title(rect, value, 0, 20);
        return button;
    }

    private void Update()
    {
        if (loading || pause == null) return;
        bool available = manager != null && manager.shiftActive && !manager.shiftEnded;
        pause.gameObject.SetActive(!paused && available && Time.timeScale > 0f);
        var keys = Keyboard.current;
        if (keys == null) return;
        if (paused && keys.escapeKey.wasPressedThisFrame && options.gameObject.activeSelf) Settings(false);
        else if (paused && (keys.escapeKey.wasPressedThisFrame || keys.pKey.wasPressedThisFrame)) Close();
        else if (available && keys.pKey.wasPressedThisFrame) Open();
    }

    private void Open()
    {
        if (paused || loading || manager == null || manager.shiftEnded || Time.timeScale <= 0f) return;
        // Releasing the inventory's Alt mode restores its underlying gameplay/cursor state.
        inventoryWasEnabled = inventory != null && inventory.enabled;
        if (inventoryWasEnabled) inventory.enabled = false;
        cursorLock = Cursor.lockState;
        cursorVisible = Cursor.visible;
        couldMove = player != null && player.canMove;
        previousSpeed = Time.timeScale;
        audioPaused = AudioListener.pause;
        paused = true;
        if (player != null) player.canMove = false;
        Time.timeScale = 0f;
        AudioListener.pause = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        pause.gameObject.SetActive(false);
        overlay.gameObject.SetActive(true);
        status.text = "";
        Settings(false);
        Focus(resume);
    }

    private static void Focus(Button button)
    {
        if (EventSystem.current != null && button != null)
            EventSystem.current.SetSelectedGameObject(button.gameObject);
    }

    private void Close()
    {
        if (!paused || loading) return;
        paused = false;
        overlay.gameObject.SetActive(false);
        Time.timeScale = previousSpeed;
        AudioListener.pause = audioPaused;
        if (inventoryWasEnabled && inventory != null) inventory.enabled = true;
        if (player != null) player.canMove = couldMove;
        Cursor.lockState = cursorLock;
        Cursor.visible = cursorVisible;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    private void Settings(bool show)
    {
        options.gameObject.SetActive(show);
        actions.gameObject.SetActive(!show);
        volume.text = $"Âm lượng   {Mathf.RoundToInt(AudioListener.volume * 100)}%";
        Focus(show ? back : settings);
    }

    private void ChangeVolume(float amount)
    {
        AudioListener.volume = Mathf.Clamp01(AudioListener.volume + amount);
        PlayerPrefs.SetFloat(VolumeKey, AudioListener.volume);
        PlayerPrefs.Save();
        volume.text = $"Âm lượng   {Mathf.RoundToInt(AudioListener.volume * 100)}%";
    }

    private void Load(string scene)
    {
        if (loading) return;
        try
        {
            AsyncOperation op = null;
            if (Application.CanStreamedLevelBeLoaded(scene)) op = SceneManager.LoadSceneAsync(scene);
#if UNITY_EDITOR
            else
            {
                string path = scene == gameObject.scene.name ? gameObject.scene.path : "Assets/Scenes/" + scene + ".unity";
                if (UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(path) != null)
                    op = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(path,
                        new LoadSceneParameters(LoadSceneMode.Single));
            }
#endif
            if (op == null) { status.text = "Chưa bật scene trong Build Profiles: " + scene; return; }
            loading = true;
            Time.timeScale = 1f;
            AudioListener.pause = audioPaused;
        }
        catch (System.Exception ex) { status.text = "Không mở được scene " + scene; UnityEngine.Debug.LogException(ex); }
    }

    private void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OnDisable()
    {
        if (paused && !loading) Close();
    }
}

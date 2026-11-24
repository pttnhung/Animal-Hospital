using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The menu is authored in the scene. Keep its button, artwork and navigation.
public sealed class AAH_MainMenuSettings : MonoBehaviour
{
    private Button _opener;
    private Button.ButtonClickedEvent _previousClick;
    private RectTransform _panel;
    private TMP_Text _volume;
    private Button _back;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        SceneManager.sceneLoaded -= BindMenu;
        SceneManager.sceneLoaded += BindMenu;
    }

    private static void BindMenu(Scene scene, LoadSceneMode mode)
    {
        if (!string.Equals(scene.name, "menu", StringComparison.OrdinalIgnoreCase)) return;
        Button labelledButton = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var button in root.GetComponentsInChildren<Button>(true))
            {
                for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                {
                    if (button.onClick.GetPersistentMethodName(i) != "ShowSettingsUI") continue;
                    Install(button);
                    return;
                }
                var caption = button.GetComponentInChildren<TMP_Text>(true);
                if ((labelledButton == null || !labelledButton.gameObject.activeInHierarchy) && caption != null &&
                    string.Equals(caption.text.Trim(), "Cài đặt", StringComparison.OrdinalIgnoreCase))
                    labelledButton = button;
            }
        }
        if (labelledButton != null) Install(labelledButton);
    }

    private static void Install(Button button)
    {
        if (button.GetComponent<AAH_MainMenuSettings>() != null) return;
        var settings = button.gameObject.AddComponent<AAH_MainMenuSettings>();
        settings._opener = button;
        settings._previousClick = button.onClick;
        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(settings.Open);
    }

    private void Open()
    {
        if (_panel == null) BuildPanel();
        if (_panel == null) return;
        _panel.SetAsLastSibling();
        _panel.gameObject.SetActive(true);
        DisplayVolume();
        _back.Select();
    }

    private void BuildPanel()
    {
        var canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;
        _panel = AAH_SoloGameOver.Element(canvas.rootCanvas.transform, "MainMenuSettings", 0f, 0f, 0f);
        _panel.anchorMin = Vector2.zero;
        _panel.anchorMax = Vector2.one;
        _panel.offsetMin = _panel.offsetMax = Vector2.zero;
        _panel.gameObject.AddComponent<Image>().color = new Color32(22, 29, 34, 255);

        AAH_SoloGameOver.Label(_panel, "Cài đặt", 48f, 120f);
        _volume = AAH_SoloGameOver.Label(_panel, "", 26f, 55f);
        var down = AAH_SoloGameOver.ButtonAt(_panel, "Giảm âm lượng", -12f, () => ChangeVolume(-0.1f));
        var up = AAH_SoloGameOver.ButtonAt(_panel, "Tăng âm lượng", -76f, () => ChangeVolume(0.1f));
        _back = AAH_SoloGameOver.ButtonAt(_panel, "Quay lại", -140f, Close);
        Link(down, _back, up);
        Link(up, down, _back);
        Link(_back, up, down);
    }

    private static void Link(Button button, Button above, Button below)
    {
        var navigation = new Navigation { mode = Navigation.Mode.Explicit };
        navigation.selectOnUp = above;
        navigation.selectOnDown = below;
        button.navigation = navigation;
    }

    private void ChangeVolume(float delta)
    {
        AAH_SoloGameOver.AdjustMasterVolume(delta);
        DisplayVolume();
    }

    private void DisplayVolume()
    {
        _volume.text = "Âm lượng tổng " + Mathf.RoundToInt(AudioListener.volume * 100f) + "%";
    }

    private void Update()
    {
        if (_panel == null || !_panel.gameObject.activeSelf) return;
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            Close();
    }

    private void Close()
    {
        _panel.gameObject.SetActive(false);
        _opener.Select();
    }

    private void OnDestroy()
    {
        if (_opener != null && _previousClick != null) _opener.onClick = _previousClick;
        if (_panel != null) Destroy(_panel.gameObject);
    }
}

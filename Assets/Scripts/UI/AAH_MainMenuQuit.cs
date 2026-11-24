using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

internal static class AAH_MainMenuQuit
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        SceneManager.sceneLoaded -= Connect;
        SceneManager.sceneLoaded += Connect;
    }

    private static void Connect(Scene scene, LoadSceneMode mode)
    {
        if (!string.Equals(scene.name, "menu", StringComparison.OrdinalIgnoreCase)) return;

        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var button in root.GetComponentsInChildren<Button>(true))
            {
                var caption = button.GetComponentInChildren<TMP_Text>(true);
                string label = caption != null ? caption.text : button.GetComponentInChildren<Text>(true)?.text;
                if (!string.Equals(label?.Trim(), "Thoát", StringComparison.OrdinalIgnoreCase)) continue;

                button.onClick.RemoveListener(Quit);
                button.onClick.AddListener(Quit);
            }
        }
    }

    private static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}

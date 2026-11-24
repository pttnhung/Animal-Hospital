using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(500)]
public sealed class AAH_HospitalHudLayout : MonoBehaviour
{
    private static readonly Color PlateColor = new Color32(27, 32, 39, 196);
    private static readonly Color Heading = new Color32(255, 220, 138, 255);
    private readonly List<Caption> captions = new List<Caption>();
    private readonly List<TMP_Text> guides = new List<TMP_Text>();
    private RectTransform hud, inventoryBar;
    private TMP_Text queue, useHint;
    private AAH_PlayerInventory inventory;
    private static readonly string[] ChocolateHints =
    {
        "", "nhấp chuột trái để sử dụng  ·  Còn 1/3 miếng",
        "nhấp chuột trái để sử dụng  ·  Còn 2/3 miếng",
        "nhấp chuột trái để sử dụng  ·  Còn 3/3 miếng"
    };
    private Vector2 lastSize;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "Hospital_day1" && scene.name != "Hospital_day2") return;
        foreach (var root in scene.GetRootGameObjects())
        {
            var manager = root.GetComponentInChildren<AAH_GameManager>(true);
            if (manager == null) continue;
            if (!manager.TryGetComponent<AAH_HospitalHudLayout>(out _))
                manager.gameObject.AddComponent<AAH_HospitalHudLayout>();
            if (!manager.TryGetComponent<AAH_HospitalPauseMenu>(out _))
                manager.gameObject.AddComponent<AAH_HospitalPauseMenu>();
            return;
        }
    }

    private void Start()
    {
        foreach (var root in gameObject.scene.GetRootGameObjects())
        {
            if (inventory == null) inventory = root.GetComponentInChildren<AAH_PlayerInventory>(true);
            foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
            {
                if (canvas.renderMode == RenderMode.WorldSpace) continue;
                if (canvas.name == "PlayerHUD_Canvas")
                    inventoryBar = canvas.transform.Find("InventoryBar") as RectTransform;
                if (canvas.name == "DoorInteractionCanvas") hud = canvas.transform as RectTransform;
            }
            var sequence = root.GetComponentInChildren<AAH_CheckInSequence>(true);
            if (sequence != null && sequence.stepCameraTripod != null)
                sequence.stepCameraTripod.interactionDistance += 0.5f;
        }
        if (inventoryBar != null)
        {
            inventoryBar.localScale *= 0.65f;
            inventoryBar.anchoredPosition = new Vector2(inventoryBar.anchoredPosition.x, 18f);
        }
        if (hud == null) return;
        foreach (var label in hud.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            var id = label.name;
            if (id == "TXT_Day1Objective")
            {
                // This label and its panel are authored in the scene. Content updates
                // must not replace their Inspector layout or typography.
                continue;
            }
            else if (id == "TXT_CheckInQueue")
            {
                queue = label;
                Style(label, 17, 22, new Color32(202, 235, 240, 255));
                AddPlate(label);
            }
            else if (id.EndsWith("_GuidanceText"))
            {
                guides.Add(label);
                Style(label, 18, 24, new Color32(239, 244, 247, 255));
                var caption = AddPlate(label);
                string arrowName = id.Substring(0, id.Length - "Text".Length) + "Arrow";
                caption.arrow = hud.Find(arrowName) as RectTransform;
                if (caption.arrow != null)
                {
                    foreach (var part in caption.arrow.GetComponentsInChildren<Image>(true))
                    {
                        if (!part.TryGetComponent<UnityEngine.UI.Outline>(out var outline))
                            outline = part.gameObject.AddComponent<UnityEngine.UI.Outline>();
                        outline.effectColor = new Color32(16, 29, 41, 245);
                        outline.effectDistance = new Vector2(2f, -2f);
                    }
                    caption.arrow.localScale = Vector3.one * 1.15f;
                }
            }
            else if (id.EndsWith("Prompt") || id == "PromptText")
            {
                Style(label, 18, 25, new Color32(248, 247, 240, 255));
                if (id != "PromptText" || label.transform.parent.GetComponent<Image>() == null)
                    AddPlate(label);
            }
        }
        var hintObject = new GameObject("HeldConsumableHint", typeof(RectTransform), typeof(TextMeshProUGUI));
        hintObject.transform.SetParent(hud, false);
        useHint = hintObject.GetComponent<TextMeshProUGUI>();
        if (queue != null) useHint.font = queue.font;
        Style(useHint, 17, 23, Color.white);
        useHint.text = "";
        AddPlate(useHint);

        Arrange();
    }

    private static void Style(TMP_Text text, int min, int max, Color color)
    {
        text.color = color;
        text.enableAutoSizing = true;
        text.fontSizeMin = min;
        text.fontSizeMax = max;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
    }

    private void Arrange()
    {
        lastSize = hud.rect.size;
        float width = Mathf.Min(1000f, lastSize.x * 0.58f);
        if (queue != null) Place(queue.rectTransform, 0f, 24f, width, 32f);
        float guideWidth = Mathf.Min(width - 68f, 640f);
        foreach (var label in guides)
            Place(label.rectTransform, 28f, -label.rectTransform.anchoredPosition.y, guideWidth, 62f);
        foreach (var caption in captions)
            if (caption.arrow != null) Place(caption.arrow, 0f, 150f, 70f, 70f);
        var hint = useHint.rectTransform;
        hint.anchorMin = hint.anchorMax = new Vector2(0.5f, 0f);
        hint.pivot = new Vector2(0.5f, 0.5f);
        hint.sizeDelta = new Vector2(Mathf.Min(lastSize.x * 0.85f, 640f), 44f);
        hint.anchoredPosition = new Vector2(0f, 115f);
    }

    private static void Place(RectTransform r, float x, float y, float width, float height)
    {
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 1f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = new Vector2(width, height);
        r.anchoredPosition = new Vector2(x, -y);
    }

    private class Caption
    {
        public TMP_Text label;
        public RectTransform plate, arrow;
        public string previousText;
        public Vector2 previousSize;
    }

    private Caption AddPlate(TMP_Text label)
    {
        var rect = label.rectTransform;
        var obj = new GameObject(label.name + "_Backdrop", typeof(RectTransform), typeof(Image));
        var plate = (RectTransform)obj.transform;
        plate.SetParent(rect.parent, false);
        plate.SetSiblingIndex(rect.GetSiblingIndex());
        var image = obj.GetComponent<Image>();
        image.color = PlateColor;
        image.raycastTarget = false;
        obj.SetActive(false);
        var caption = new Caption { label = label, plate = plate };
        captions.Add(caption);
        return caption;
    }

    private void LateUpdate()
    {
        if (hud == null) return;
        if (lastSize != hud.rect.size) Arrange();
        var held = inventory != null ? inventory.SelectedItem : null;
        string hintText = "";
        if (held != null && held.Consumable && !inventory.BodyCarried && !inventory.CursorReleased &&
            Time.timeScale > 0f && Cursor.lockState == CursorLockMode.Locked)
        {
            hintText = held.ItemId == "chocolate"
                ? ChocolateHints[Mathf.Clamp(inventory.SelectedUses, 0, 3)]
                : "nhấp chuột trái để sử dụng";
        }
        if (useHint.text != hintText) useHint.text = hintText;
        foreach (var caption in captions)
        {
            var label = caption.label;
            if (label == null) continue;
            bool visible = label.isActiveAndEnabled && !string.IsNullOrWhiteSpace(label.text);
            var plate = caption.plate;
            if (plate.gameObject.activeSelf != visible) plate.gameObject.SetActive(visible);
            if (!visible) continue;
            var rect = label.rectTransform;
            plate.position = rect.TransformPoint(rect.rect.center);
            if (caption.previousText != label.text || caption.previousSize != rect.rect.size)
            {
                caption.previousText = label.text;
                caption.previousSize = rect.rect.size;
                Vector2 content = label.GetPreferredValues(label.text, rect.rect.width, Mathf.Infinity);
                plate.sizeDelta = new Vector2(Mathf.Min(content.x, rect.rect.width) + 22f,
                    Mathf.Min(content.y, rect.rect.height) + 6f);
            }
            if (caption.arrow != null)
                caption.arrow.anchoredPosition = new Vector2(
                    rect.anchoredPosition.x - plate.sizeDelta.x * 0.5f - 47f, rect.anchoredPosition.y);
        }
    }
}

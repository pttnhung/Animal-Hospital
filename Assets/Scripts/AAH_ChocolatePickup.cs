using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Attach to the chocolate on the table. The held copy uses the imported model asset.
[DisallowMultipleComponent]
public sealed class AAH_ChocolatePickup : MonoBehaviour
{
    [SerializeField] private GameObject _heldModel;
    [SerializeField] private Sprite _icon;
    [SerializeField, Min(0.1f)] private float _reach = 5f;
    [SerializeField, Min(0.1f)] private float _holdSeconds = 1f;
    [SerializeField, Min(0.1f)] private float _refillSeconds = 360f;

    private readonly List<Renderer> _visuals = new List<Renderer>();
    private readonly List<Collider> _colliders = new List<Collider>();
    private AAH_PlayerInventory _inventory;
    private Camera _camera;
    private AAH_ItemDefinition _item;
    private readonly AAH_WorldHover _hover = new AAH_WorldHover();
    private bool _refilling;
    private float _readyAt, _hold;
    private string _hint;

#if UNITY_EDITOR
    private void Reset()
    {
        _heldModel = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Equipment/chocolate.glb");
        string iconPath = UnityEditor.AssetDatabase.GUIDToAssetPath("0cd13a041a563454b8d477ef7db2750c");
        if (string.IsNullOrEmpty(iconPath))
        {
            foreach (var guid in UnityEditor.AssetDatabase.FindAssets("chocolate_icon t:Texture2D"))
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) != "chocolate_icon") continue;
                iconPath = path;
                break;
            }
        }
        if (!string.IsNullOrEmpty(iconPath))
            _icon = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
    }
#endif

    private void Start()
    {
        if (_heldModel == null || _heldModel.scene.IsValid())
        {
            Debug.LogError("Chocolate: kéo model chocolate từ cửa sổ Project vào ô Held Model.", this);
            enabled = false;
            return;
        }

        _inventory = FindFirstObjectByType<AAH_PlayerInventory>();
        var player = FindFirstObjectByType<SC_FPSController>();
        _camera = player != null && player.playerCamera != null ? player.playerCamera : Camera.main;
        _item = AAH_ItemDefinition.CreateChocolate(_heldModel, _icon);

        foreach (var visual in GetComponentsInChildren<Renderer>())
        {
            if (!visual.enabled) continue;
            _visuals.Add(visual);
            if (!visual.TryGetComponent<MeshFilter>(out var filter) || filter.sharedMesh == null) continue;
            bool hasSolid = false;
            foreach (var existing in visual.GetComponents<Collider>())
                if (existing.enabled && !existing.isTrigger) hasSolid = true;
            if (hasSolid) continue;
            var box = visual.gameObject.AddComponent<BoxCollider>();
            box.center = filter.sharedMesh.bounds.center;
            box.size = filter.sharedMesh.bounds.size;
        }
        foreach (var collider in GetComponentsInChildren<Collider>())
            if (collider.enabled) _colliders.Add(collider);
    }

    private void SetAvailable(bool available)
    {
        foreach (var visual in _visuals) visual.enabled = available;
        foreach (var collider in _colliders) collider.enabled = available;
    }

    private bool LookingAtBar()
    {
        if (_inventory == null || _camera == null || _inventory.BodyCarried || _inventory.CursorReleased ||
            Cursor.lockState != CursorLockMode.Locked || Time.timeScale <= 0f) return false;
        var checkIn = AAH_CheckInSequence.Instance;
        if (checkIn != null && checkIn.BlocksWorldInteraction) return false;
        Transform eye = _camera.transform;
        return Physics.Raycast(eye.position, eye.forward, out var hit, _reach,
                   Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
               && hit.collider.GetComponentInParent<AAH_ChocolatePickup>() == this;
    }

    private void Update()
    {
        _hint = null;
        if (_refilling)
        {
            if (Time.time < _readyAt) return;
            SetAvailable(true);
            _refilling = false;
        }
        if (!LookingAtBar() || Keyboard.current == null)
        {
            _hold = 0f;
            return;
        }
        if (!_inventory.HasFreeSlot)
        {
            _hold = 0f;
            _hint = "Túi đồ đã đầy (5/5)";
            return;
        }
        _hold = Keyboard.current.eKey.isPressed ? _hold + Time.deltaTime : 0f;
        if (_hold < _holdSeconds)
        {
            _hint = _hold <= 0f ? "Giữ [E] lấy chocolate" :
                $"Giữ [E] lấy chocolate  {Mathf.FloorToInt(100f * _hold / _holdSeconds)}%";
            return;
        }
        _hold = 0f;
        if (!_inventory.TryAdd(_item, out int slot)) return;
        _inventory.Select(slot);
        SetAvailable(false);
        _refilling = true;
        _readyAt = Time.time + _refillSeconds;
    }

    private void LateUpdate()
    {
        if (_hint != null && !_refilling && Time.timeScale > 0f)
            _hover.Draw(transform, _camera, _visuals);
    }

    private void OnDestroy() => _hover.Dispose();

    private void OnGUI()
    {
        if (_hint != null) AAH_InteractionHints.Draw(_hint);
    }

    private void OnDisable()
    {
        _hold = 0f;
        _hint = null;
    }
}

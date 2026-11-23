using System.Collections.Generic;
using UnityEngine;

/// <summary>Frames the selected inventory model in the lower-right sixth of the player's view.</summary>
public class AAH_EquippedItemView : MonoBehaviour
{
    [SerializeField] private AAH_PlayerInventory _inventory;
    [SerializeField] private Transform _anchor;
    [SerializeField] private Camera _camera;
    [SerializeField] private Vector2 _viewportPosition = new Vector2(5f / 6f, 0.25f);
    [Tooltip("Original hold distance. The close hold uses 85% of this distance.")]
    [SerializeField, Min(0.1f)] private float _distance = 0.6f;
    [Tooltip("Fraction of the lower-right cell the model can span along either axis.")]
    [SerializeField, Range(0.25f, 0.9f)] private float _cellFill = 0.52f;

    private GameObject _instance;
    private AAH_ItemDefinition _shownItem;
    private Vector3 _sourceScale, _centeredOffset;
    // Bounds corners in anchor space, relative to the centre of the held model.
    private readonly List<Vector3> _corners = new List<Vector3>();
    public GameObject CurrentInstance => _instance;

    private void OnEnable()
    {
        if (_inventory == null) return;
        _inventory.SelectionChanged += HandleSelectionChanged;
        _inventory.InventoryChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        if (_inventory == null) return;
        _inventory.SelectionChanged -= HandleSelectionChanged;
        _inventory.InventoryChanged -= Refresh;
    }

    private void HandleSelectionChanged(int index) => Refresh();

    private void Refresh()
    {
        var item = _inventory.BodyCarried ? null : _inventory.SelectedItem;
        if (item == _shownItem && (_instance != null || item == null || item.EquippedModel == null)) return;
        if (_instance != null)
        {
            _instance.SetActive(false);
            Destroy(_instance);
        }
        _instance = null;
        _shownItem = item;
        _corners.Clear();
        if (item == null || item.EquippedModel == null || _anchor == null) return;

        _instance = Instantiate(item.EquippedModel, _anchor);
        _instance.name = "Equipped_" + item.ItemId;
        Transform model = _instance.transform;
        model.localPosition = Vector3.zero;
        model.localRotation = item.EquippedRotation;
        foreach (var collider in _instance.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        foreach (var body in _instance.GetComponentsInChildren<Rigidbody>(true)) body.isKinematic = true;
        _instance.SetActive(true);
        _sourceScale = model.localScale;

        var bounds = new Bounds();
        foreach (var renderer in _instance.GetComponentsInChildren<Renderer>())
        {
            if (!renderer.enabled) continue;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Matrix4x4 toAnchor = _anchor.worldToLocalMatrix * renderer.localToWorldMatrix;
            Bounds box = renderer.localBounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = toAnchor.MultiplyPoint3x4(new Vector3(
                    (i & 1) == 0 ? box.min.x : box.max.x,
                    (i & 2) == 0 ? box.min.y : box.max.y,
                    (i & 4) == 0 ? box.min.z : box.max.z));
                if (_corners.Count == 0) bounds = new Bounds(corner, Vector3.zero);
                else bounds.Encapsulate(corner);
                _corners.Add(corner);
            }
        }
        _centeredOffset = -bounds.center;
        for (int i = 0; i < _corners.Count; i++) _corners[i] += _centeredOffset;
        FrameItem();
    }

    private void LateUpdate() => FrameItem();

    private void FrameItem()
    {
        if (_camera == null || _anchor == null || _instance == null || _corners.Count == 0) return;
        float halfWidth = _cellFill / 6f;
        float halfHeight = _cellFill / 4f;
        float x = Mathf.Clamp(_viewportPosition.x, 2f / 3f + halfWidth, 1f - halfWidth);
        float y = Mathf.Clamp(_viewportPosition.y, halfHeight, 0.5f - halfHeight);
        float near = _camera.nearClipPlane + 0.01f;
        float depth = Mathf.Max(_distance * 0.85f, near + 0.15f);
        _anchor.position = _camera.ViewportToWorldPoint(new Vector3(x, y, depth));

        // In clip space each edge has a linear distance to an expanding model corner.
        // The tightest edge sets a uniform scale even with an off-axis physical camera.
        Matrix4x4 view = _camera.worldToCameraMatrix * _anchor.localToWorldMatrix;
        Matrix4x4 clip = _camera.projectionMatrix * view;
        Vector4 center = clip.GetColumn(3);
        float left = 2f * (x - halfWidth) - 1f, right = 2f * (x + halfWidth) - 1f;
        float bottom = 2f * (y - halfHeight) - 1f, top = 2f * (y + halfHeight) - 1f;
        float scale = float.PositiveInfinity;
        foreach (var corner in _corners)
        {
            Vector4 offset = clip * new Vector4(corner.x, corner.y, corner.z, 0f);
            Limit(ref scale, center.x - left * center.w, left * offset.w - offset.x);
            Limit(ref scale, right * center.w - center.x, offset.x - right * offset.w);
            Limit(ref scale, center.y - bottom * center.w, bottom * offset.w - offset.y);
            Limit(ref scale, top * center.w - center.y, offset.y - top * offset.w);
            Limit(ref scale, depth - near, view.MultiplyVector(corner).z);
        }
        if (float.IsInfinity(scale) || scale <= 0f) return;
        _instance.transform.localScale = _sourceScale * scale;
        _instance.transform.localPosition = _centeredOffset * scale;
    }

    private static void Limit(ref float scale, float room, float approach)
    {
        if (approach > 0f) scale = Mathf.Min(scale, room / approach);
    }
}

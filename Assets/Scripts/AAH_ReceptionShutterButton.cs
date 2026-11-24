using UnityEngine;

public sealed class AAH_ReceptionShutterButton : MonoBehaviour
{
    [Header("Existing button interaction")]
    [SerializeField] private Collider _buttonCollider;
    [SerializeField] private Camera _playerCamera;
    [SerializeField] private Transform _player;
    [SerializeField] private AAH_CheckInSequence _checkIn;
    [SerializeField, Min(0.1f)] private float _interactionDistance = 5f;
    [SerializeField, Min(0.1f)] private float _rayDistance = 10f;
    [SerializeField] private UnityEngine.Events.UnityEvent _onPressed = new UnityEngine.Events.UnityEvent();
    private UnityEngine.InputSystem.InputAction _interact;
    public UnityEngine.Events.UnityEvent OnPressed => _onPressed;
    private AAH_FocusOutline _outline;
    private Transform _appearance;
    private readonly RaycastHit[] _aimHits = new RaycastHit[32];
    public bool IsFocused => IsAimed();
    private AAH_ReceptionShutter _curtain;
    private bool _showHint;
    private LineRenderer _practiceRing;
    private Bounds _practiceBounds;
    internal bool PracticeActive => _practiceRing != null;

    internal bool BeginDay1Practice(GameObject ringStyle)
    {
        if (PracticeActive) return true;
        if (_buttonCollider == null || _playerCamera == null || _curtain == null ||
            ringStyle == null || !ringStyle.TryGetComponent<LineRenderer>(out var style)) return false;

        _practiceBounds = _appearance != null && _appearance.TryGetComponent<Renderer>(out var face)
            ? face.bounds : _buttonCollider.bounds;
        var ringObject = new GameObject("Vòng hướng dẫn nút cửa sập");
        ringObject.layer = _buttonCollider.gameObject.layer;
        ringObject.transform.SetParent(transform, false);
        _practiceRing = ringObject.AddComponent<LineRenderer>();
        _practiceRing.sharedMaterial = style.sharedMaterial;
        _practiceRing.colorGradient = style.colorGradient;
        _practiceRing.useWorldSpace = true;
        _practiceRing.loop = true;
        _practiceRing.positionCount = 48;
        _practiceRing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _practiceRing.receiveShadows = false;
        UpdatePracticeRing();
        return true;
    }

    private void UpdatePracticeRing()
    {
        // Put the circle on the player-facing side of this wall-mounted button.
        var towardPlayer = _playerCamera.transform.position - _practiceBounds.center;
        var normal = Vector3.ProjectOnPlane(towardPlayer, Vector3.up).normalized;
        if (normal.sqrMagnitude < 0.001f) normal = Vector3.forward;
        var across = Vector3.Cross(Vector3.up, normal);
        var extents = _practiceBounds.extents;
        float halfWidth = Mathf.Abs(across.x) * extents.x + Mathf.Abs(across.z) * extents.z;
        float depth = Mathf.Abs(normal.x) * extents.x + Mathf.Abs(normal.z) * extents.z;
        float radius = Mathf.Max(0.12f, Mathf.Sqrt(halfWidth * halfWidth + extents.y * extents.y) * 1.12f);
        var center = _practiceBounds.center + normal * (depth + 0.025f);
        _practiceRing.widthMultiplier = Mathf.Clamp(radius * 0.08f, 0.025f, 0.1f);
        for (int i = 0; i < _practiceRing.positionCount; i++)
        {
            float angle = i * (2f * Mathf.PI / _practiceRing.positionCount);
            _practiceRing.SetPosition(i, center + radius * (across * Mathf.Cos(angle) + Vector3.up * Mathf.Sin(angle)));
        }
    }

    internal void EndDay1Practice()
    {
        if (_practiceRing == null) return;
        _practiceRing.enabled = false;
        Destroy(_practiceRing.gameObject);
        _practiceRing = null;
    }

    private void Update()
    {
        if (PracticeActive) UpdatePracticeRing();
        _showHint = CanUseButton();
        _outline?.Show(_showHint);
    }
    private void OnGUI()
    {
        if (_showHint && _curtain != null)
            AAH_InteractionHints.Draw(_curtain.IsClosed ? "[E] Mở cửa sập" : "[E] Đóng cửa sập");
    }
    private void Awake()
    {
        _curtain = FindFirstObjectByType<AAH_ReceptionShutter>();
        if (_buttonCollider != null)
        {
            _appearance = _buttonCollider.transform.Find("ShutterButton_ReferenceAppearance");
            if (_appearance == null)
            {
                foreach (var mesh in FindObjectsByType<MeshFilter>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                {
                    if (mesh.gameObject.scene != gameObject.scene || mesh.name != "ShutterButton_ReferenceAppearance") continue;
                    _appearance = mesh.transform;
                    break;
                }
            }
            var target = _appearance != null ? _appearance : _buttonCollider.transform;
            _outline = new AAH_FocusOutline(target, edgePixels: 2);
            var filter = target.GetComponent<MeshFilter>();
            if (_appearance != null && filter != null && filter.sharedMesh != null && target.GetComponent<Collider>() == null)
            {
                var bounds = filter.sharedMesh.bounds;
                var hitbox = target.gameObject.AddComponent<BoxCollider>();
                hitbox.isTrigger = true;
                hitbox.center = bounds.center;
                hitbox.size = Vector3.Max(bounds.size, Vector3.one * 0.01f);
            }
        }
        _interact = new UnityEngine.InputSystem.InputAction("ReceptionShutter", UnityEngine.InputSystem.InputActionType.Button, "<Keyboard>/e");
        _interact.performed += HandlePress;
    }
    private void OnEnable() { _interact?.Enable(); }
    private bool IsAimed()
    {
        if (_buttonCollider == null || _playerCamera == null || Time.timeScale <= 0f) return false;
        var ray = new Ray(_playerCamera.transform.position, _playerCamera.transform.forward);
        if (!Physics.Raycast(ray, out var hit, _rayDistance, ~0, QueryTriggerInteraction.Collide)) return false;
        if (hit.collider != _buttonCollider && hit.transform != _appearance)
        {
            // The moving curtain can cover its own switch collider. Other obstacles still block the ray.
            var curtain = hit.collider.GetComponentInParent<AAH_ReceptionShutter>();
            if (curtain == null) return false;
            var hits = _aimHits;
            int count = Physics.RaycastNonAlloc(ray, hits, _rayDistance, ~0, QueryTriggerInteraction.Collide);
            if (count == hits.Length)
            {
                hits = Physics.RaycastAll(ray, _rayDistance, ~0, QueryTriggerInteraction.Collide);
                count = hits.Length;
            }
            float closest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var candidate = hits[i];
                if (candidate.collider.GetComponentInParent<AAH_ReceptionShutter>() == curtain || candidate.distance >= closest) continue;
                closest = candidate.distance;
                hit = candidate;
            }
            if (hit.collider != _buttonCollider && hit.transform != _appearance) return false;
        }
        Vector3 position = _player != null ? _player.position : _playerCamera.transform.position;
        Vector3 nearest = hit.point; nearest.y = position.y;
        return Vector3.Distance(position, nearest) <= _interactionDistance;
    }
    private bool CanUseButton()
    {
        return Cursor.lockState == CursorLockMode.Locked && _curtain != null &&
            (_checkIn == null || !_checkIn.HasModalInteraction) &&
            (AAH_GameManager.Instance == null || !AAH_GameManager.Instance.shiftEnded) && IsAimed();
    }
    private void HandlePress(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        if (!CanUseButton()) return;
        bool wasClosed = _curtain.IsClosed;
        _onPressed.Invoke();
        if (PracticeActive && _curtain.IsClosed != wasClosed) EndDay1Practice();
    }
    private void OnDisable() { EndDay1Practice(); _showHint = false; _outline?.Show(false); _interact?.Disable(); }
    private void OnDestroy()
    {
        _outline?.Dispose();
        if (_interact == null) return;
        _interact.performed -= HandlePress; _interact.Dispose();
    }
}

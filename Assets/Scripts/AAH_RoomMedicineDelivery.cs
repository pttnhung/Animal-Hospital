using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class AAH_RoomTreatmentSequence
{
    [System.Serializable]
    private class MedicineStock
    {
        public Texture2D image;
        public GameObject[] bottles;
    }
    [Header("Medicine delivery")]
    [SerializeField] private MedicineStock[] _medicineStocks;
    [SerializeField] private RectTransform _guideArrow;
    [SerializeField] private TMPro.TMP_Text _guideText;
    [Tooltip("Local player's five-slot inventory. Resolved automatically in the hospital scene.")]
    [SerializeField] private AAH_PlayerInventory _inventory;
    [SerializeField] private GameObject _treatmentPanel;
    [SerializeField] private UnityEngine.UI.Image _treatmentFill;
    [SerializeField] private TMPro.TMP_Text _treatmentStatus;
    [SerializeField] private Transform _hospitalExit;
    [SerializeField, Min(0.1f)] private float _treatmentDuration = 10f;
    private readonly List<MedicineOrder> _orders = new List<MedicineOrder>();
    private readonly List<GameObject> _pickupProxies = new List<GameObject>();
    private int _focusedOrder = -1;
    private GameObject _focusedBottle;
    private Coroutine _recovery;
    // Only one prescription can use the player's hands and guidance at a time.
    // Analysis and recovery continue independently in every room.
    private static AAH_RoomTreatmentSequence _deliveryOwner;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetDeliveryOwner() { _deliveryOwner = null; }
    private bool OwnsDelivery()
    {
        if (_deliveryOwner == null && Stage == TreatmentStage.CollectingMedicine) _deliveryOwner = this;
        return _deliveryOwner == this;
    }
    public int CollectedMedicineCount { get; private set; }
    public int DeliveredMedicineCount { get; private set; }
    public float TreatmentProgress { get; private set; }
    private class MedicineOrder
    {
        public string label;
        public MedicineStock stock;
        public bool collected;
        public bool delivered;
        public GameObject source;
        public AAH_ItemDefinition item;
        public int slot = -1;
    }
    private MedicineDisplayEntry ResolveMedicine(string medicine)
    {
        var catalog = MedicineCatalog;
        if (catalog == null || string.IsNullOrWhiteSpace(medicine)) return null;
        foreach (var entry in catalog)
        {
            if (entry == null) continue;
            if (string.Equals(entry.label, medicine.Trim(), System.StringComparison.OrdinalIgnoreCase)) return entry;
            if (string.IsNullOrEmpty(entry.aliases)) continue;
            foreach (var alias in entry.aliases.Split('|'))
                if (string.Equals(alias.Trim(), medicine.Trim(), System.StringComparison.OrdinalIgnoreCase)) return entry;
        }
        return null;
    }
    private void ResetPatientDelivery()
    {
        DisableDelivery();
        ClearMedicineItems();
        CollectedMedicineCount = DeliveredMedicineCount = 0;
        TreatmentProgress = 0f;
        _focusedOrder = -1; _focusedBottle = null;
        if (_treatmentPanel != null) _treatmentPanel.SetActive(false);
        if (_treatmentFill != null) _treatmentFill.fillAmount = 0f;
        if (_treatmentStatus != null) _treatmentStatus.text = "";
    }
    private void ClearMedicineItems()
    {
        foreach (var order in _orders)
        {
            if (order.source != null) order.source.SetActive(true);
            if (order.item == null) continue;
            if (_inventory != null && _inventory.GetItem(order.slot) == order.item)
                _inventory.RemoveAt(order.slot);
            Destroy(order.item);
        }
        _orders.Clear();
    }
    private void InitializeDelivery()
    {
        if (_inventory == null && _player != null)
            _inventory = _player.GetComponentInParent<AAH_PlayerInventory>();
        if (_inventory == null && _playerCamera != null)
            _inventory = _playerCamera.GetComponentInParent<AAH_PlayerInventory>();
        if (_inventory == null)
            _inventory = FindFirstObjectByType<AAH_PlayerInventory>();
        if (_guideArrow != null) _guideArrow.gameObject.SetActive(false);
        if (_guideText != null) _guideText.text = "";
        if (_treatmentPanel != null) _treatmentPanel.SetActive(false);
        if (_medicineStocks == null) return;
        foreach (var stock in _medicineStocks)
        {
            if (stock == null || stock.bottles == null) continue;
            foreach (var bottle in stock.bottles)
            {
                if (bottle == null) continue;
                bool hasPickup = false;
                foreach (var existing in bottle.GetComponentsInChildren<Collider>(true))
                    if (existing.name == "MedicinePickup") { hasPickup = true; break; }
                if (hasPickup) continue;
                var renderers = bottle.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) continue;
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                var proxy = new GameObject("MedicinePickup");
                proxy.transform.SetParent(bottle.transform, false);
                proxy.transform.SetPositionAndRotation(bounds.center, Quaternion.identity);
                var collider = proxy.AddComponent<BoxCollider>();
                collider.isTrigger = true;
                var scale = proxy.transform.lossyScale;
                collider.size = new Vector3((bounds.size.x + 0.12f) / Mathf.Abs(scale.x), (bounds.size.y + 0.12f) / Mathf.Abs(scale.y), (bounds.size.z + 0.12f) / Mathf.Abs(scale.z));
                _pickupProxies.Add(proxy);
            }
        }
    }
    private void BeginMedicineDelivery()
    {
        ClearMedicineItems(); CollectedMedicineCount = 0; DeliveredMedicineCount = 0;
        if (_medicines != null)
            foreach (var medicine in _medicines)
            {
                if (string.IsNullOrWhiteSpace(medicine)) continue;
                var entry = ResolveMedicine(medicine);
                MedicineStock stock = null;
                if (entry != null && _medicineStocks != null)
                    foreach (var candidate in _medicineStocks) if (candidate != null && candidate.image == entry.image) { stock = candidate; break; }
                if (stock == null || stock.bottles == null || stock.bottles.Length == 0)
                {
                    Debug.LogError("No shelf stock configured for prescribed medicine: " + medicine, this);
                    if (_guideText != null) _guideText.text = "Thiếu thuốc trên kệ: " + medicine;
                    return;
                }
                _orders.Add(new MedicineOrder { label = entry.label, stock = stock });
            }
        Stage = TreatmentStage.CollectingMedicine;
        if (_orders.Count == 0) StartRecovery();
    }
    private bool SelectDeliveryTarget()
    {
        _focusedOrder = -1; _focusedBottle = null;
        if (!OwnsDelivery() || (_inventory != null && _inventory.BodyCarried)) return false;
        if (_playerCamera == null || _patient == null || _patient.State != AAH_PatientActor.PatientState.AtBed || Time.timeScale <= 0f || (_checkIn != null && _checkIn.BlocksWorldInteraction)) return false;
        RaycastHit hit;
        if (!Physics.Raycast(_playerCamera.transform.position, _playerCamera.transform.forward, out hit, _rayDistance, ~0, QueryTriggerInteraction.Collide)) return false;
        var position = _player != null ? _player.position : _playerCamera.transform.position;
        if (!WithinInteractionDistance(hit, position)) return false;
        if (hit.collider == _patientCollider) return CollectedMedicineCount > DeliveredMedicineCount;
        for (int i = 0; i < _orders.Count; i++)
        {
            var order = _orders[i];
            if (order.collected) continue;
            foreach (var bottle in order.stock.bottles)
                if (bottle != null && bottle.activeInHierarchy && (hit.transform == bottle.transform || hit.transform.IsChildOf(bottle.transform)))
                { _focusedOrder = i; _focusedBottle = bottle; return true; }
        }
        return false;
    }
    private void HandleMedicineInteraction()
    {
        if (!SelectDeliveryTarget()) return;
        if (_focusedOrder >= 0)
        {
            var order = _orders[_focusedOrder];
            if (_inventory == null || !_inventory.HasFreeSlot) return;
            var item = AAH_ItemDefinition.CreateMedicine(order.label, order.stock.image, _focusedBottle);
            if (!_inventory.TryAdd(item, out int slot))
            {
                Destroy(item);
                return;
            }

            order.item = item;
            order.slot = slot;
            order.source = _focusedBottle;
            order.source.SetActive(false);
            order.collected = true;
            CollectedMedicineCount++;
            _inventory.Select(slot);
            return;
        }

        var selected = SelectedMedicineOrder();
        if (selected == null) return;
        _inventory.RemoveAt(selected.slot);
        Destroy(selected.item);
        selected.item = null;
        selected.delivered = true;
        DeliveredMedicineCount++;
        if (DeliveredMedicineCount == _orders.Count) StartRecovery();
    }
    public bool DiscardSelectedDose(AAH_PlayerInventory inventory)
    {
        if (inventory != _inventory) return false;
        var order = SelectedMedicineOrder();
        if (order == null) return false;
        inventory.RemoveAt(order.slot);
        if (order.source != null) order.source.SetActive(true);
        Destroy(order.item);
        order.item = null;
        order.source = null;
        order.slot = -1;
        order.collected = false;
        CollectedMedicineCount--;
        return true;
    }

    private MedicineOrder SelectedMedicineOrder()
    {
        if (_inventory == null || _inventory.SelectedItem == null) return null;
        foreach (var order in _orders)
            if (!order.delivered && order.slot == _inventory.SelectedIndex && order.item == _inventory.SelectedItem)
                return order;
        return null;
    }
    private string MedicinePrompt()
    {
        if (!IsFocused) return "";
        if (_inventory == null) return "Chưa kết nối túi đồ của người chơi";
        if (_focusedOrder >= 0)
            return _inventory.HasFreeSlot ? "[E] Lấy " + _orders[_focusedOrder].label : "Túi đồ đã đầy (5/5)";
        var selected = SelectedMedicineOrder();
        return selected != null ? "[E] Đặt " + selected.label : "Chọn ô thuốc của bệnh nhân này";
    }
    private readonly Dictionary<GameObject, Collider> _shelfTargets = new Dictionary<GameObject, Collider>();

    private Bounds ShelfBounds(GameObject bottle)
    {
        if (!_shelfTargets.TryGetValue(bottle, out var collider))
        {
            var pickup = bottle.transform.Find("MedicinePickup");
            collider = pickup != null ? pickup.GetComponent<Collider>() : null;
            _shelfTargets.Add(bottle, collider);
        }
        if (collider != null) return collider.bounds;
        var visual = bottle.GetComponentInChildren<Renderer>();
        return visual != null ? visual.bounds : new Bounds(bottle.transform.position, Vector3.one * 0.2f);
    }

    private MedicineOrder NextShelfTarget(out GameObject nearest)
    {
        nearest = null;
        Vector3 origin = _player != null ? _player.position : transform.position;
        foreach (var order in _orders)
        {
            if (order.collected) continue;
            float best = float.PositiveInfinity;
            foreach (var bottle in order.stock.bottles)
            {
                if (bottle == null || !bottle.activeInHierarchy) continue;
                float distance = (origin - ShelfBounds(bottle).center).sqrMagnitude;
                if (distance < best) { best = distance; nearest = bottle; }
            }
            return order;
        }
        return null;
    }

    internal int TutorialRoomNumber => _roomNumber;
    internal bool GuidesPrescription => Stage == TreatmentStage.CollectingMedicine && _deliveryOwner == this;

    private readonly Dictionary<Renderer, int[]> _tutorialFootVertices = new Dictionary<Renderer, int[]>();

    private static int TutorialMeshRoot(int[] parents, int index)
    {
        while (parents[index] != index)
        {
            parents[index] = parents[parents[index]];
            index = parents[index];
        }
        return index;
    }

    private Bounds TutorialDeviceFoot(Renderer device, bool computer)
    {
        if (device == null || !device.TryGetComponent<MeshFilter>(out var filter) ||
            filter.sharedMesh == null || !filter.sharedMesh.isReadable) return default;
        var vertices = filter.sharedMesh.vertices;
        if (!_tutorialFootVertices.TryGetValue(device, out var foot))
        {
            // Imported devices combine disconnected screen, support and accessory meshes.
            // Weld coincident vertices before separating those parts by triangle connectivity.
            var parents = new int[vertices.Length];
            var world = new Vector3[vertices.Length];
            var welded = new Dictionary<Vector3Int, int>();
            for (int i = 0; i < vertices.Length; i++)
            {
                parents[i] = i;
                world[i] = device.transform.TransformPoint(vertices[i]);
                var key = Vector3Int.RoundToInt(world[i] * 10000f);
                if (welded.TryGetValue(key, out int previous)) parents[i] = TutorialMeshRoot(parents, previous);
                else welded.Add(key, i);
            }
            var triangles = filter.sharedMesh.triangles;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int root = TutorialMeshRoot(parents, triangles[i]);
                parents[TutorialMeshRoot(parents, triangles[i + 1])] = root;
                parents[TutorialMeshRoot(parents, triangles[i + 2])] = root;
            }
            var parts = new Dictionary<int, List<int>>();
            var bounds = new Dictionary<int, Bounds>();
            int body = -1;
            for (int i = 0; i < vertices.Length; i++)
            {
                int root = TutorialMeshRoot(parents, i);
                if (!parts.ContainsKey(root))
                {
                    parts.Add(root, new List<int>());
                    bounds.Add(root, new Bounds(world[i], Vector3.zero));
                }
                parts[root].Add(i);
                var partBounds = bounds[root];
                partBounds.Encapsulate(world[i]);
                bounds[root] = partBounds;
                if (body < 0 || parts[root].Count > parts[body].Count) body = root;
            }
            int basePart = -1;
            if (body >= 0)
            {
                var bodyBounds = bounds[body];
                float bodyArea = bodyBounds.size.x * bodyBounds.size.z;
                foreach (var part in parts)
                {
                    var b = bounds[part.Key];
                    float area = b.size.x * b.size.z;
                    // The wide, thin support beneath the body is neither a mouse nor a table.
                    if (part.Value.Count < 12 || b.size.y > bodyBounds.size.y * 0.25f ||
                        area < bodyArea * (computer ? 0.15f : 0.3f) ||
                        area > bodyArea * (computer ? 0.8f : 2.5f) ||
                        b.min.y > bodyBounds.min.y) continue;
                    if (basePart < 0 || b.min.y < bounds[basePart].min.y) basePart = part.Key;
                }
            }
            foot = basePart >= 0 ? parts[basePart].ToArray() : System.Array.Empty<int>();
            _tutorialFootVertices.Add(device, foot);
        }
        if (foot.Length == 0) return default;
        var result = new Bounds(device.transform.TransformPoint(vertices[foot[0]]), Vector3.zero);
        foreach (int index in foot) result.Encapsulate(device.transform.TransformPoint(vertices[index]));
        return result;
    }

    internal string TutorialAction(out Bounds bounds)
    {
        bounds = default;
        if (_patient == null) return null;
        switch (Stage)
        {
            case TreatmentStage.WaitingForBed:
                return "Theo " + _patient.DisplayName + " đến giường khám.";
            case TreatmentStage.Patient:
                bounds = _patient.VisualBounds;
                return "Đến cạnh giường, nhìn " + _patient.DisplayName + " và nhấn E để khám.";
            case TreatmentStage.Scan:
                if (_scan != null) bounds = AAH_GameManager.Instance != null && AAH_GameManager.Instance.currentDay == 1
                    ? TutorialDeviceFoot(_scan, false) : _scan.bounds;
                return "Nhìn vào máy quét có vòng xanh, nhấn E để phân tích bệnh.";
            case TreatmentStage.Computer:
                if (_computer != null) bounds = AAH_GameManager.Instance != null && AAH_GameManager.Instance.currentDay == 1
                    ? TutorialDeviceFoot(_computer, true) : _computer.bounds;
                return "Nhìn vào máy tính có vòng xanh, nhấn E để phân tích đơn thuốc.";
            case TreatmentStage.Analyzing:
                return "Chờ máy tính phân tích đơn thuốc.";
            case TreatmentStage.CollectingMedicine:
                if (!GuidesPrescription) return "Chờ hoàn tất đơn thuốc đang được hướng dẫn.";
                var order = NextShelfTarget(out var bottle);
                if (order != null)
                {
                    if (bottle != null) bounds = ShelfBounds(bottle);
                    if (_inventory != null && !_inventory.HasFreeSlot)
                        return "Túi đầy. Dùng vật phẩm hoặc bỏ bớt đồ trước khi lấy thuốc.";
                    return "Tìm " + order.label + " trên kệ, nhìn lọ có vòng xanh và nhấn E.";
                }
                bounds = _patient.VisualBounds;
                return "Chọn ô thuốc (1–5), nhìn bệnh nhân và nhấn E để đặt thuốc.";
            case TreatmentStage.Treating:
                return "Đã đặt đủ thuốc. Chờ bệnh nhân hồi phục.";
            default:
                return null;
        }
    }

    private void UpdateDelivery()
    {
        bool active = Stage == TreatmentStage.CollectingMedicine && OwnsDelivery();
        if (_guideArrow != null) _guideArrow.gameObject.SetActive(active);
        if (!active)
        {
            if (_guideText != null) _guideText.text = "";
            return;
        }
        var order = NextShelfTarget(out var bottle);
        Vector3 target = bottle != null ? ShelfBounds(bottle).center : _patient.VisualBounds.center;
        string instruction = order != null ? "Lấy " + order.label + " trên kệ thuốc" :
            "Về phòng " + _roomNumber.ToString("00") + ", đặt thuốc cho bệnh nhân";
        if (_guideArrow != null)
        {
            var direction = target - _playerCamera.transform.position;
            float angle = Vector3.SignedAngle(Vector3.ProjectOnPlane(_playerCamera.transform.forward, Vector3.up), Vector3.ProjectOnPlane(direction, Vector3.up), Vector3.up);
            _guideArrow.localRotation = Quaternion.Euler(0f, 0f, -angle);
        }
        if (_guideText != null) _guideText.text = instruction + "\nĐã lấy: " + CollectedMedicineCount + "/" + _orders.Count + " | Đã đặt: " + DeliveredMedicineCount + "/" + _orders.Count;
        if (_prompt != null) _prompt.text = MedicinePrompt();
        SetOutline(_patientOutline, IsFocused && _focusedOrder < 0 && CollectedMedicineCount > DeliveredMedicineCount);
    }
    private void StartRecovery()
    {
        if (_deliveryOwner == this) _deliveryOwner = null;
        Stage = TreatmentStage.Treating;
        ShowMedicineImages(false);
        if (_medicineText != null) _medicineText.text = "";
        if (_guideArrow != null) _guideArrow.gameObject.SetActive(false);
        if (_guideText != null) _guideText.text = "";
        SetOutline(_patientOutline, false);
        _recovery = StartCoroutine(RecoveryRoutine());
    }
    private IEnumerator RecoveryRoutine()
    {
        TreatmentProgress = 0f;
        if (_treatmentPanel != null) _treatmentPanel.SetActive(true);
        float elapsed = 0f;
        while (elapsed < _treatmentDuration)
        {
            TreatmentProgress = Mathf.Clamp01(elapsed / _treatmentDuration);
            if (_treatmentFill != null) _treatmentFill.fillAmount = TreatmentProgress;
            if (_treatmentStatus != null) _treatmentStatus.SetText("ĐANG ĐIỀU TRỊ\n{0:0}%", TreatmentProgress * 100f);
            yield return null; elapsed += Time.deltaTime;
        }
        TreatmentProgress = 1f;
        if (_treatmentFill != null) _treatmentFill.fillAmount = 1f;
        if (_treatmentStatus != null) _treatmentStatus.text = "HOÀN TẤT\n100%";
        yield return null;
        if (_treatmentPanel != null) _treatmentPanel.SetActive(false);
        if (_patientCollider != null) _patientCollider.enabled = false;
        Stage = TreatmentStage.Discharged;
        while (_patient != null && _patient.ThreatPending) yield return null;
        if (_patient != null) PatientRecovered?.Invoke(_patient);
        while (_patient != null && _patient.HoldDischarge) yield return null;
        if (_patient != null) _patient.LeaveHospital(_hospitalExit);
        _recovery = null;
    }
    private void DisableDelivery()
    {
        if (_deliveryOwner == this) _deliveryOwner = null;
        if (_guideArrow != null) _guideArrow.gameObject.SetActive(false);
        if (_guideText != null) _guideText.text = "";
        if (_recovery != null) { StopCoroutine(_recovery); _recovery = null; }
    }
}

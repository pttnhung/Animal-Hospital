using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AAH_Day1Story : MonoBehaviour
{
    [Header("Existing systems")]
    [SerializeField] private AAH_GameManager _gameManager;
    [SerializeField] private AAH_CheckInSequence _dialogue;
    [SerializeField] private AAH_RoomTreatmentSequence[] _treatmentRooms;
    [SerializeField] private AAH_DrMupple _mupple;
    [SerializeField] private Transform _player;
    [SerializeField] private AAH_Day1Dialogue _data;
    private AAH_PatientActor _polly;
    private Coroutine _examination;
    private const int Day1PatientCount = 3;
    private readonly HashSet<string> _recoveredPatientIds = new HashSet<string>();
    private bool _examined, _muppleEntranceStarted;
    public bool Completed { get; private set; }
    private bool IsDay1 => _gameManager != null && _gameManager.currentDay == 1;

    private LineRenderer _roomRing;
    private string _lastObjective;
    private float _nextGuideUpdate;
    private AAH_PatientActor _tutorialPatient;
    private readonly List<AAH_PatientActor> _tutorialAcceptedPatients = new List<AAH_PatientActor>();
    private bool _tutorialIntroductionActive;
    private bool _tutorialReturningToReception;

    private IEnumerator TutorialIntroduction()
    {
        _tutorialIntroductionActive = true;
        string[] lines =
        {
            "Bạn là một bác sĩ thực tập tại bệnh viện Animal Hospital",
            "Nhiệm vụ của bạn là hoàn thành ca trực đêm, từ 6:00 - 12:00",
            "Khi hoàn thành mỗi ca trực, Dr. Mupple sẽ tới và xem báo cáo ca trực của bạn"
        };
        foreach (string line in lines)
        {
            _gameManager.SetDay1Objective("Ngày 1\n" + line);
            yield return new WaitForSeconds(5f);
        }
        _tutorialIntroductionActive = false;
        _lastObjective = null;
        _nextGuideUpdate = 0f;
    }

    private void LateUpdate()
{
        if (_gameManager == null || _dialogue == null || !_gameManager.shiftActive ||
            _gameManager.shiftEnded || _muppleEntranceStarted || Time.timeScale <= 0f)
        {
            HideRing();
            return;
        }
        if (Time.unscaledTime < _nextGuideUpdate) return;
        _nextGuideUpdate = Time.unscaledTime + 0.1f;

        var room = ChooseTutorialRoom();
        Bounds bounds = default;
        string action;
        if (IsDay1)
        {
            // Keep the existing ring target; the HUD now describes the whole treatment phase.
            if (room != null) room.TutorialAction(out bounds);
            if (_tutorialReturningToReception)
                action = "Bệnh nhân đang được chữa trị, trở về phòng tiếp nhận để thực hiện quy trình";
            else if (room != null && _tutorialPatient != null &&
                _tutorialPatient.State == AAH_PatientActor.PatientState.AtBed)
                action = "Khám bệnh, phân tích bệnh và lấy thuốc ở hành lang";
            else if (_tutorialPatient != null && _tutorialPatient.RoomNumber > 0)
                action = "Vào phòng số " + _tutorialPatient.RoomNumber + " để khám bệnh và chữa trị";
            else
                action = ReceptionInstruction();
        }
        else if (_dialogue.panelClipboard != null && _dialogue.panelClipboard.activeInHierarchy)
            action = "Đọc hồ sơ và bấm nút ký xác nhận để tiếp nhận bệnh nhân.";
        else if (_dialogue.BlocksWorldInteraction && _dialogue.Busy)
            action = ReceptionInstruction();
        else if (room != null)
            action = "Phòng " + room.TutorialRoomNumber.ToString("00") + " · " + room.TutorialAction(out bounds);
        else
            action = ReceptionInstruction();

        if (bounds.size.sqrMagnitude > 0f && (room == null || !room.HasActiveDeviceRing))
            ShowRing(bounds);
        else HideRing();
        if (!IsDay1 || _tutorialIntroductionActive) return;
        string message = "Chữa trị " + _recoveredPatientIds.Count + "/3 bệnh nhân\n" + action;
        if (message == _lastObjective) return;
        _lastObjective = message;
        _gameManager.SetDay1Objective(message);
    }

    private AAH_RoomTreatmentSequence TutorialRoomFor(AAH_PatientActor patient)
    {
        if (patient == null || _treatmentRooms == null) return null;
        foreach (var room in _treatmentRooms)
            if (room != null && room.isActiveAndEnabled && room.CurrentPatient == patient)
                return room;
        return null;
    }

    private bool IsTutorialCaseActive(AAH_PatientActor patient)
    {
        return patient != null && patient.gameObject.activeInHierarchy && !patient.IsRejected &&
            patient.State != AAH_PatientActor.PatientState.Dead &&
            !_recoveredPatientIds.Contains(patient.PatientId) &&
            (patient == _dialogue.CurrentPatient || TutorialRoomFor(patient) != null);
    }

    private void RememberTutorialAdmission(string patientName, int roomNumber)
    {
        if (!IsDay1 || _dialogue == null) return;
        var patient = _dialogue.CurrentPatient;
        if (patient != null && !_tutorialAcceptedPatients.Contains(patient))
            _tutorialAcceptedPatients.Add(patient);
    }

    private AAH_RoomTreatmentSequence ChooseTutorialRoom()
    {
        if (IsDay1)
        {
            var currentRoom = TutorialRoomFor(_tutorialPatient);
            if (currentRoom != null &&
                (currentRoom.Stage == AAH_RoomTreatmentSequence.TreatmentStage.Treating ||
                 currentRoom.Stage == AAH_RoomTreatmentSequence.TreatmentStage.Discharged))
                _tutorialReturningToReception = true;

            if (_tutorialReturningToReception)
            {
                var nextPatient = _dialogue.CurrentPatient;
                bool receivingNext = nextPatient != null && nextPatient != _tutorialPatient &&
                    !nextPatient.IsRejected &&
                    ((_dialogue.currentStep == AAH_CheckInSequence.CheckInStep.Step1_Clipboard &&
                      _dialogue.panelClipboard != null && _dialogue.panelClipboard.activeInHierarchy) ||
                     (_dialogue.currentStep >= AAH_CheckInSequence.CheckInStep.Step2_CameraTripod &&
                      _dialogue.currentStep <= AAH_CheckInSequence.CheckInStep.Completed_Leaving));
                if (receivingNext)
                {
                    _tutorialPatient = nextPatient;
                    _tutorialReturningToReception = false;
                }
                else
                    return currentRoom;
            }
            if (!IsTutorialCaseActive(_tutorialPatient)) _tutorialPatient = null;
            if (_tutorialPatient == null)
            {
                foreach (var patient in _tutorialAcceptedPatients)
                    if (IsTutorialCaseActive(patient)) { _tutorialPatient = patient; break; }

                // Existing room cases take precedence when this component starts mid-shift.
                if (_tutorialPatient == null && _treatmentRooms != null)
                    foreach (var room in _treatmentRooms)
                        if (room != null && room.isActiveAndEnabled && IsTutorialCaseActive(room.CurrentPatient))
                        { _tutorialPatient = room.CurrentPatient; break; }

                if (_tutorialPatient == null && IsTutorialCaseActive(_dialogue.CurrentPatient))
                    _tutorialPatient = _dialogue.CurrentPatient;
            }
            return TutorialRoomFor(_tutorialPatient);
        }
        if (_treatmentRooms == null) return null;
        AAH_RoomTreatmentSequence best = null;
        int priority = int.MaxValue;
        float bestDistance = float.PositiveInfinity;
        foreach (var room in _treatmentRooms)
        {
            if (room == null || !room.isActiveAndEnabled || room.CurrentPatient == null) continue;
            int rank;
            if (room.GuidesPrescription) rank = 0;
            else switch (room.Stage)
            {
                case AAH_RoomTreatmentSequence.TreatmentStage.Patient:
                case AAH_RoomTreatmentSequence.TreatmentStage.Scan:
                case AAH_RoomTreatmentSequence.TreatmentStage.Computer: rank = 1; break;
                case AAH_RoomTreatmentSequence.TreatmentStage.WaitingForBed: rank = 2; break;
                case AAH_RoomTreatmentSequence.TreatmentStage.Analyzing:
                case AAH_RoomTreatmentSequence.TreatmentStage.Treating: rank = 3; break;
                default: continue;
            }
            if (rank >= 2 && _dialogue.HasPatient &&
                _dialogue.currentStep != AAH_CheckInSequence.CheckInStep.Completed_Leaving) continue;
            float distance = _player == null ? 0f :
                (_player.position - room.CurrentPatient.transform.position).sqrMagnitude;
            if (rank < priority || (rank == priority && distance < bestDistance))
            {
                best = room;
                priority = rank;
                bestDistance = distance;
            }
        }
        return best;
    }

    private string ReceptionInstruction()
    {
        if (IsDay1)
        {
            if (_tutorialPatient == null ||
                _tutorialPatient.State == AAH_PatientActor.PatientState.Approaching)
                return "Đến phòng tiếp nhận chờ bệnh nhân";
            return "Tiếp nhận thông tin đăng ký hồ sơ bệnh nhân";
        }
        if (_dialogue.panelDialogue != null && _dialogue.panelDialogue.activeInHierarchy && _dialogue.Busy)
            return "Lắng nghe cuộc hội thoại để tiếp tục.";
        switch (_dialogue.currentStep)
        {
            case AAH_CheckInSequence.CheckInStep.Step1_Clipboard:
                return "Nhìn hồ sơ trên quầy, nhấn E để mở và ký tiếp nhận.";
            case AAH_CheckInSequence.CheckInStep.Step2_CameraTripod:
                return _dialogue.Busy ? "Chờ ảnh thẻ hiện ra." : "Nhìn máy ảnh trên chân đỡ, nhấn E để chụp ảnh thẻ.";
            case AAH_CheckInSequence.CheckInStep.Step3_ComputerRegister:
                return _dialogue.Busy ? "Chờ máy tính hoàn tất đăng ký." : "Nhìn máy tính lễ tân, nhấn E để tạo hồ sơ bệnh nhân.";
            case AAH_CheckInSequence.CheckInStep.Step4_PrinterRoomCard:
                return _dialogue.Busy ? "Chờ máy in thẻ phòng." : "Nhìn máy in cạnh máy tính, nhấn E để in thẻ phòng.";
            case AAH_CheckInSequence.CheckInStep.Completed_Leaving:
                return "Theo bệnh nhân đến phòng ghi trên thẻ khám.";
            default:
                return "WASD để đi, rê chuột để nhìn. Đến quầy lễ tân chờ bệnh nhân.";
        }
    }

    private void ShowRing(Bounds target, bool deviceFootprint = false)
    {
        if (_roomRing == null)
        {
            var step = _dialogue.stepComputer;
            if (step == null || step.highlightRing == null ||
                !step.highlightRing.TryGetComponent<LineRenderer>(out var style)) return;
            var obj = new GameObject("Vòng hướng dẫn phòng khám");
            obj.transform.SetParent(transform, false);
            _roomRing = obj.AddComponent<LineRenderer>();
            _roomRing.sharedMaterial = style.sharedMaterial;
            _roomRing.colorGradient = style.colorGradient;
            _roomRing.loop = true;
            _roomRing.useWorldSpace = true;
            _roomRing.positionCount = 64;
            _roomRing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _roomRing.receiveShadows = false;
        }
        float padding = deviceFootprint ? 0.05f :
            Mathf.Clamp(Mathf.Max(target.extents.x, target.extents.z) * 0.13f, 0.08f, 0.3f);
        Vector3 center = target.center;
        center.y = target.min.y + (deviceFootprint ? 0.025f : Mathf.Max(target.size.y * 0.27f, 0.05f));
        _roomRing.widthMultiplier = deviceFootprint ? 0.025f : Mathf.Clamp(padding * 0.45f, 0.035f, 0.11f);
        for (int i = 0; i < _roomRing.positionCount; i++)
        {
            if (deviceFootprint)
            {
                // A rounded perimeter encloses the entire foot rather than cutting its corners.
                int corner = i / 16;
                float angle = (corner + (i % 16) / 15f) * Mathf.PI * 0.5f;
                float x = corner == 0 || corner == 3 ? target.extents.x : -target.extents.x;
                float z = corner < 2 ? target.extents.z : -target.extents.z;
                _roomRing.SetPosition(i, center + new Vector3(
                    x + padding * Mathf.Cos(angle), 0f, z + padding * Mathf.Sin(angle)));
            }
            else
            {
                float angle = i * 2f * Mathf.PI / _roomRing.positionCount;
                _roomRing.SetPosition(i, center + new Vector3(
                    (target.extents.x + padding) * Mathf.Cos(angle), 0f,
                    (target.extents.z + padding) * Mathf.Sin(angle)));
            }
        }
        _roomRing.enabled = true;
    }

    private void HideRing()
    {
        if (_roomRing != null) _roomRing.enabled = false;
    }

    private void OnEnable()
    {
        if (_dialogue != null) _dialogue.OnPatientCheckedIn += RememberTutorialAdmission;
        if (_treatmentRooms != null) foreach (var room in _treatmentRooms)
{
            if (room == null) continue;
            room.ExaminationStarted += OnExamination;
            room.PatientRecovered += OnRecovery;
        }
        if (_mupple != null) _mupple.Day1DialogueCompleted += OnMuppleCompleted;
    }
    private void Start()
    {
        if (!IsDay1) return;
        if (_data == null || _dialogue == null || _dialogue.panelDialogue == null || _dialogue.txtDialogueContent == null || _mupple == null || _player == null)
        { Debug.LogError("Day 1 story references are incomplete.", this); enabled = false; return; }
        StartCoroutine(TutorialIntroduction());
    }
    private void OnExamination(AAH_PatientActor patient)
    {
        if (!IsDay1 || _examined || patient == null || patient.DisplayName != "Polly") return;
        _examined = true; _polly = patient;
        _examination = StartCoroutine(ExaminationDialogue());
    }
    private IEnumerator ExaminationDialogue()
    {
        yield return _dialogue.ShowStoryDialogue("Polly", _data, _data.Examination);
        _examination = null;
    }
    private void OnRecovery(AAH_PatientActor patient)
    {
        if (!IsDay1 || _muppleEntranceStarted || patient == null ||
            string.IsNullOrEmpty(patient.PatientId) || !_recoveredPatientIds.Add(patient.PatientId))
            return;

        if (patient == _polly)
        {
            patient.HoldDischarge = true;
            StartCoroutine(RecoveryDialogue());
        }

        if (_recoveredPatientIds.Count < Day1PatientCount) return;

        _muppleEntranceStarted = true;
        _gameManager.SetDay1Objective("Chữa trị 3/3 bệnh nhân\nNgày 1: Lắng nghe lời dặn của bác sĩ Mupple.");
        StartCoroutine(_mupple.PlayDay1Entrance(_player, _dialogue, _data));
    }
    private IEnumerator RecoveryDialogue()
    {
        while (_examination != null) yield return null;
        yield return _dialogue.ShowStoryDialogue("Polly", _data, _data.Recovery);
        if (_polly != null) _polly.HoldDischarge = false;
    }
    public void EndDay2Queue()
    {
        if (_gameManager == null || _gameManager.currentDay != 2 || _muppleEntranceStarted) return;
        if (_mupple == null || _data == null || _dialogue == null || _dialogue.panelDialogue == null || _dialogue.txtDialogueContent == null)
        {
            Debug.LogError("Thiếu tham chiếu Mupple hoặc phần hội thoại trong scene Ngày 2.", this);
            return;
        }
        _muppleEntranceStarted = true;
        _gameManager.SetDay1Objective("Ngày 2: Gặp bác sĩ Mupple ở quầy lễ tân.");
        StartCoroutine(_mupple.PlayDay2Entrance(_player, _dialogue, _data, FinishDay2));
    }
    private void FinishDay2()
    {
        if (Completed) return;
        Completed = true;
        _mupple.TriggerStoreRoomExit();
        _gameManager.CompleteDay2Story();
    }
    private void OnMuppleCompleted()
    {
        if (!IsDay1 || !_muppleEntranceStarted || Completed) return;
        Completed = true;
        _gameManager.CompleteDay1Story();
        _mupple.TriggerStoreRoomExit();
    }
    private void OnDestroy()
    {
        if (_roomRing != null) Destroy(_roomRing.gameObject);
    }

    private void OnDisable()
    {
        HideRing();
        if (_dialogue != null) _dialogue.OnPatientCheckedIn -= RememberTutorialAdmission;
        StopAllCoroutines();
        if (_polly != null) _polly.HoldDischarge = false;
        if (_treatmentRooms != null) foreach (var room in _treatmentRooms)
        {
            if (room == null) continue;
            room.ExaminationStarted -= OnExamination;
            room.PatientRecovered -= OnRecovery;
        }
        if (_mupple != null) _mupple.Day1DialogueCompleted -= OnMuppleCompleted;
    }
}

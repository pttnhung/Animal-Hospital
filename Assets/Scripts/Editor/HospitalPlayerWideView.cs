using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

internal static class HospitalPlayerWideView
{
    private const float FocalLengthMm = 24f;

    [MenuItem("Tools/Hospital/Góc nhìn người chơi 24 mm")]
    private static void Apply()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            (scene.name != "Hospital_day1" && scene.name != "Hospital_day2"))
        {
            UnityEngine.Debug.LogWarning("Thoát Play Mode và mở scene Hospital_day1 hoặc Hospital_day2.");
            return;
        }

        foreach (var root in scene.GetRootGameObjects())
        {
            var player = root.GetComponentInChildren<SC_FPSController>(true);
            if (player == null || player.playerCamera == null) continue;
            var camera = player.playerCamera;
            Undo.RecordObject(camera, "Góc nhìn người chơi 24 mm");
            camera.usePhysicalProperties = true;
            camera.focalLength = FocalLengthMm;
            PrefabUtility.RecordPrefabInstancePropertyModifications(camera);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = camera.gameObject;
            UnityEngine.Debug.Log("Đã đặt PlayerCamera về 24 mm trong " + scene.name + ". Nhấn Ctrl+S để lưu.", camera);
            return;
        }
        UnityEngine.Debug.LogWarning("Không tìm thấy camera được gán trên SC_FPSController.");
    }
}

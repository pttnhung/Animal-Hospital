using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

internal static class VetNightmareCameraReachEditor
{
    [MenuItem("Tools/Vet Nightmare/Tăng tầm máy ảnh (+1)")]
    private static void IncreaseReach()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Hãy thoát Play Mode trước khi chỉnh máy ảnh.");
            return;
        }

        var scene = SceneManager.GetActiveScene();
        if (scene.name != "Hospital_day1" && scene.name != "Hospital_day2")
        {
            Debug.LogWarning("Hãy mở scene Hospital_day1 hoặc Hospital_day2.");
            return;
        }

        int count = 0;
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var step in root.GetComponentsInChildren<AAH_InteractableStep>(true))
            {
                if (step.gameObject.name != "CameraTripod_Interactable")
                    continue;

                Undo.RecordObject(step, "Tăng tầm máy ảnh");
                float previous = step.interactionDistance;
                step.interactionDistance = previous + 1f;
                if (PrefabUtility.IsPartOfPrefabInstance(step))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(step);
                Debug.Log($"Máy ảnh ({scene.name}): {previous} → {step.interactionDistance}.", step);
                count++;
            }
        }

        if (count > 0)
            EditorSceneManager.MarkSceneDirty(scene);
        else
            Debug.LogWarning($"Không tìm thấy CameraTripod_Interactable trong {scene.name}.");
    }
}

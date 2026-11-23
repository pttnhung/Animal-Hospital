using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Submit the same material as the treatment rooms, just for frames with a focused target.
// No extra child is attached to the model (the inventory clones the actual medicine bottle).
internal sealed class AAH_WorldHover
{
    private Transform _target;
    private readonly List<Renderer> _sources = new List<Renderer>();
    private readonly Dictionary<SkinnedMeshRenderer, Mesh> _baked = new Dictionary<SkinnedMeshRenderer, Mesh>();
    private Material _material;
    private static Material _lastStyle;
    private static Scene _lastScene;

    public static Material StyleFor(Scene scene)
    {
        if (_lastScene == scene && _lastStyle != null) return _lastStyle;
        if (!scene.IsValid() || !scene.isLoaded) return null;
        foreach (var root in scene.GetRootGameObjects())
        foreach (var room in root.GetComponentsInChildren<AAH_RoomTreatmentSequence>(true))
        {
            if (room.HoverMaterial == null) continue;
            _lastScene = scene;
            return _lastStyle = room.HoverMaterial;
        }
        return null;
    }

    public void Draw(Transform target, Camera camera, IEnumerable<Renderer> parts = null)
    {
        if (target == null || camera == null || !target.gameObject.activeInHierarchy) return;
        if (_target != target)
        {
            Dispose();
            _target = target;
            _material = StyleFor(target.gameObject.scene);
            if (parts == null) target.GetComponentsInChildren(true, _sources);
            else _sources.AddRange(parts);
        }
        if (_material == null) return;
        foreach (var source in _sources)
        {
            if (source == null || !source.enabled || source.forceRenderingOff || !source.gameObject.activeInHierarchy) continue;
            if (source.name == "TreatmentOutline" || source.name == "Focus outline" ||
                source.GetComponentInParent<Canvas>() != null) continue;
            Mesh mesh;
            if (source is SkinnedMeshRenderer skin)
            {
                if (skin.sharedMesh == null) continue;
                if (!_baked.TryGetValue(skin, out mesh))
                    _baked.Add(skin, mesh = new Mesh { name = "Patient hover pose" });
                skin.BakeMesh(mesh, false);
            }
            else if (source is MeshRenderer && source.TryGetComponent<MeshFilter>(out var filter))
                mesh = filter.sharedMesh;
            else continue;
            if (mesh == null) continue;
            var settings = new RenderParams(_material)
            {
                camera = camera,
                layer = source.gameObject.layer,
                renderingLayerMask = source.renderingLayerMask,
                worldBounds = source.bounds,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false
            };
            int first = 0;
            int end = mesh.subMeshCount;
            if (source is MeshRenderer batch && batch.isPartOfStaticBatch)
            {
                first = batch.subMeshStartIndex;
                end = Mathf.Min(end, first + batch.sharedMaterials.Length);
            }
            for (int i = first; i < end; i++)
                Graphics.RenderMesh(settings, mesh, i, source.localToWorldMatrix);
        }
    }

    public void Dispose()
    {
        foreach (var mesh in _baked.Values) if (mesh != null) Object.Destroy(mesh);
        _baked.Clear();
        _sources.Clear();
        _target = null;
    }
}

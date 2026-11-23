using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

internal sealed class AAH_FocusOutline
{
    private readonly List<GameObject> _layers = new List<GameObject>();
    private Material _mask, _edge;
    private readonly Transform _root;
    private AAH_WorldHover _roomHover;
    private Camera _camera;
    // A positive width opts one control into the screen-space outline.
    private readonly int _edgePixels;
    private bool _checkedRoomStyle;
    private bool _visible;
    public AAH_FocusOutline(Transform root, int edgePixels = 0)
    {
        _root = root;
        _edgePixels = edgePixels;
    }
    private void BuildFallbackLayers(Transform root)
    {
        var shader = Resources.Load<Shader>("VetNightmare/FocusOutline");
        if (shader == null)
        {
            if (AAH_WorldHover.StyleFor(root.gameObject.scene) != null)
                _roomHover = new AAH_WorldHover();
            else Debug.LogError("Thiếu shader VetNightmare/FocusOutline", root);
            return;
        }
        _mask = LayerMaterial(shader, 3010, 0, CompareFunction.Always, StencilOp.Replace);
        _edge = LayerMaterial(shader, 3011, _edgePixels > 0 ? _edgePixels : 3, CompareFunction.NotEqual, StencilOp.Keep);
        foreach (var source in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (source.sharedMesh == null || !source.TryGetComponent<MeshRenderer>(out var original) || !original.enabled) continue;
            AddLayer(source, _mask);
            AddLayer(source, _edge);
        }
    }
    private static Material LayerMaterial(Shader shader, int queue, int pixels, CompareFunction test, StencilOp op)
    {
        var m = new Material(shader) { renderQueue = queue };
        m.SetInt("_ColorMask", pixels == 0 ? 0 : 15);
        m.SetInt("_Test", (int)test);
        m.SetInt("_Op", (int)op);
        m.SetFloat("_Pixels", pixels);
        return m;
    }
    private void AddLayer(MeshFilter source, Material material)
    {
        var obj = new GameObject("Focus outline");
        obj.SetActive(false);
        obj.layer = source.gameObject.layer;
        obj.transform.SetParent(source.transform, false);
        var mesh = source.sharedMesh;
        obj.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = obj.AddComponent<MeshRenderer>();
        var materials = new Material[mesh.subMeshCount];
        for (int i = 0; i < materials.Length; i++) materials[i] = material;
        renderer.sharedMaterials = materials;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        var block = new MaterialPropertyBlock();
        block.SetVector("_Center", mesh.bounds.center);
        renderer.SetPropertyBlock(block);
        _layers.Add(obj);
    }
    public void Show(bool visible)
    {
        if (!_checkedRoomStyle && visible)
        {
            _checkedRoomStyle = true;
            if (_edgePixels == 0 && AAH_WorldHover.StyleFor(_root.gameObject.scene) != null)
                _roomHover = new AAH_WorldHover();
            else BuildFallbackLayers(_root);
        }
        if (_roomHover != null)
        {
            if (visible)
            {
                if (_camera == null || !_camera.isActiveAndEnabled) _camera = Camera.main;
                _roomHover.Draw(_root, _camera);
            }
            return;
        }
        if (_visible == visible) return;
        _visible = visible;
        foreach (var layer in _layers) layer.SetActive(visible);
    }
    public void Dispose()
    {
        _roomHover?.Dispose();
        foreach (var layer in _layers) if (layer != null) Object.Destroy(layer);
        if (_mask != null) Object.Destroy(_mask);
        if (_edge != null) Object.Destroy(_edge);
    }
}

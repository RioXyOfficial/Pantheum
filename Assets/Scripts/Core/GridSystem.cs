using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Pantheum.Core
{
    [DefaultExecutionOrder(-100)]
    public class GridSystem : MonoBehaviour
    {
        public static GridSystem Instance { get; private set; }

        [SerializeField] private float _cellSize    = 1f;
        [SerializeField] private bool  _debugDraw   = true;
        [SerializeField] private int   _debugRadius = 10;

        private readonly HashSet<Vector2Int> _occupied = new();

        // Placement preview — set each frame by BuildingPlacer, drawn in OnRenderObject.
        private bool      _hasPreview;
        private Vector3   _previewCenter;
        private Vector2Int _previewSize;
        private bool      _previewValid;

        private Material _lineMat;

        public float CellSize => _cellSize;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()  => RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
        private void OnDisable() => RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;

        // ── Public API ────────────────────────────────────────────────────────

        public Vector3 Snap(Vector3 worldPos, Vector2Int size)
        {
            float offsetX = (size.x % 2 == 1) ? _cellSize * 0.5f : 0f;
            float offsetZ = (size.y % 2 == 1) ? _cellSize * 0.5f : 0f;
            float x = Mathf.Round((worldPos.x - offsetX) / _cellSize) * _cellSize + offsetX;
            float z = Mathf.Round((worldPos.z - offsetZ) / _cellSize) * _cellSize + offsetZ;
            return new Vector3(x, worldPos.y, z);
        }

        public bool CanPlace(Vector3 center, Vector2Int size)
        {
            var origin = GetOriginCell(center, size);
            for (int x = 0; x < size.x; x++)
                for (int z = 0; z < size.y; z++)
                    if (_occupied.Contains(new Vector2Int(origin.x + x, origin.y + z)))
                        return false;
            return true;
        }

        public void Occupy(Vector3 center, Vector2Int size)
        {
            var origin = GetOriginCell(center, size);
            for (int x = 0; x < size.x; x++)
                for (int z = 0; z < size.y; z++)
                    _occupied.Add(new Vector2Int(origin.x + x, origin.y + z));
            Debug.Log($"[GridSystem] Occupy  center={center} size={size} origin={origin} totalCells={_occupied.Count}");
        }

        public void Release(Vector3 center, Vector2Int size)
        {
            var origin = GetOriginCell(center, size);
            for (int x = 0; x < size.x; x++)
                for (int z = 0; z < size.y; z++)
                    _occupied.Remove(new Vector2Int(origin.x + x, origin.y + z));
            Debug.Log($"[GridSystem] Release center={center} size={size} origin={origin} totalCells={_occupied.Count}");
        }

        // Called by BuildingPlacer each frame during placement preview.
        public void SetPlacementPreview(Vector3 center, Vector2Int size, bool valid)
        {
            _hasPreview    = true;
            _previewCenter = center;
            _previewSize   = size;
            _previewValid  = valid;
        }

        public void ClearPlacementPreview() => _hasPreview = false;

        // Legacy helpers kept for compatibility — now delegate to SetPlacementPreview.
        public void DrawDebugGrid(Vector3 center)     { /* grid drawn in OnRenderObject */ }
        public void DrawDebugFootprint(Vector3 center, Vector2Int size, bool valid)
            => SetPlacementPreview(center, size, valid);

        // ── GL Rendering (URP-compatible) ─────────────────────────────────────

        private void OnEndCameraRendering(ScriptableRenderContext context, Camera cam)
        {
            // Only render for the main camera.
            if (cam != Camera.main) return;
            if (!_debugDraw) return;
            if (!EnsureLineMaterial()) return;

            // Set up world-space matrices explicitly — required in URP.
            GL.PushMatrix();
            GL.LoadProjectionMatrix(cam.projectionMatrix);
            GL.modelview = cam.worldToCameraMatrix;

            _lineMat.SetPass(0);
            GL.Begin(GL.LINES);

            DrawGridGL();
            DrawOccupiedCellsGL();
            if (_hasPreview) DrawFootprintGL();

            GL.End();
            GL.PopMatrix();

            _hasPreview = false;
        }

        private void DrawGridGL()
        {
            float y      = 0.05f;
            float half   = _debugRadius * _cellSize;
            float gridX  = _hasPreview ? _previewCenter.x : 0f;
            float gridZ  = _hasPreview ? _previewCenter.z : 0f;

            // Snap grid origin so it aligns with cell boundaries.
            float startX = Mathf.Floor((gridX - half) / _cellSize) * _cellSize;
            float startZ = Mathf.Floor((gridZ - half) / _cellSize) * _cellSize;
            float endX   = startX + _debugRadius * 2 * _cellSize;
            float endZ   = startZ + _debugRadius * 2 * _cellSize;

            GL.Color(new Color(0.7f, 0.7f, 0.7f, 0.35f));
            for (float ix = startX; ix <= endX + 0.001f; ix += _cellSize)
            {
                GL.Vertex3(ix, y, startZ);
                GL.Vertex3(ix, y, endZ);
            }
            for (float iz = startZ; iz <= endZ + 0.001f; iz += _cellSize)
            {
                GL.Vertex3(startX, y, iz);
                GL.Vertex3(endX,   y, iz);
            }
        }

        private void DrawOccupiedCellsGL()
        {
            float y   = 0.08f;
            GL.Color(new Color(1f, 0.2f, 0.2f, 0.9f));
            foreach (var cell in _occupied)
            {
                float x0 = cell.x * _cellSize;
                float z0 = cell.y * _cellSize;
                float x1 = x0 + _cellSize;
                float z1 = z0 + _cellSize;
                // Border
                GL.Vertex3(x0, y, z0); GL.Vertex3(x1, y, z0);
                GL.Vertex3(x1, y, z0); GL.Vertex3(x1, y, z1);
                GL.Vertex3(x1, y, z1); GL.Vertex3(x0, y, z1);
                GL.Vertex3(x0, y, z1); GL.Vertex3(x0, y, z0);
                // Cross
                GL.Vertex3(x0, y, z0); GL.Vertex3(x1, y, z1);
                GL.Vertex3(x1, y, z0); GL.Vertex3(x0, y, z1);
            }
        }

        private void DrawFootprintGL()
        {
            float y      = 0.1f;
            var   origin = GetOriginCell(_previewCenter, _previewSize);
            float x0     = origin.x * _cellSize;
            float z0     = origin.y * _cellSize;
            float x1     = x0 + _previewSize.x * _cellSize;
            float z1     = z0 + _previewSize.y * _cellSize;

            GL.Color(_previewValid ? new Color(0f, 1f, 0f, 0.8f) : new Color(1f, 0f, 0f, 0.8f));
            GL.Vertex3(x0, y, z0); GL.Vertex3(x1, y, z0);
            GL.Vertex3(x1, y, z0); GL.Vertex3(x1, y, z1);
            GL.Vertex3(x1, y, z1); GL.Vertex3(x0, y, z1);
            GL.Vertex3(x0, y, z1); GL.Vertex3(x0, y, z0);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private Vector2Int GetOriginCell(Vector3 center, Vector2Int size)
        {
            int ox = Mathf.RoundToInt(center.x / _cellSize - size.x * 0.5f);
            int oz = Mathf.RoundToInt(center.z / _cellSize - size.y * 0.5f);
            return new Vector2Int(ox, oz);
        }

        private bool EnsureLineMaterial()
        {
            if (_lineMat != null) return true;

            var shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null)
            {
                Debug.LogWarning("[GridSystem] Hidden/Internal-Colored shader not found.");
                return false;
            }
            _lineMat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            _lineMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _lineMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _lineMat.SetInt("_Cull",     (int)UnityEngine.Rendering.CullMode.Off);
            _lineMat.SetInt("_ZWrite",   0);
            _lineMat.SetInt("_ZTest",    (int)UnityEngine.Rendering.CompareFunction.Always);
            return true;
        }

        private void OnDestroy()
        {
            if (_lineMat != null) Destroy(_lineMat);
        }
    }
}

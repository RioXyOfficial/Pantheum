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


        private readonly HashSet<Vector2Int> _occupied = new();

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

        public void SetPlacementPreview(Vector3 center, Vector2Int size, bool valid)
        {
            _hasPreview    = true;
            _previewCenter = center;
            _previewSize   = size;
            _previewValid  = valid;
        }

        public void ClearPlacementPreview() => _hasPreview = false;

        private void OnEndCameraRendering(ScriptableRenderContext context, Camera cam)
        {
            if (cam != Camera.main) return;
            if (!_debugDraw) return;
            if (!EnsureLineMaterial()) return;

            GL.PushMatrix();
            GL.LoadProjectionMatrix(cam.projectionMatrix);
            GL.modelview = cam.worldToCameraMatrix;

            _lineMat.SetPass(0);
            GL.Begin(GL.LINES);

            if (_hasPreview)
                DrawUnifiedGridGL();

            GL.End();
            GL.PopMatrix();

            _hasPreview = false;
        }

        private void DrawUnifiedGridGL()
        {
            float y           = 0.08f;
            const int padding = 3;

            var footprintOrigin = GetOriginCell(_previewCenter, _previewSize);

            int minCX = footprintOrigin.x - padding;
            int minCZ = footprintOrigin.y - padding;
            int maxCX = footprintOrigin.x + _previewSize.x + padding;
            int maxCZ = footprintOrigin.y + _previewSize.y + padding;

            var footprint = new HashSet<Vector2Int>();
            for (int fx = 0; fx < _previewSize.x; fx++)
                for (int fz = 0; fz < _previewSize.y; fz++)
                    footprint.Add(new Vector2Int(footprintOrigin.x + fx, footprintOrigin.y + fz));

            Color colGrid      = new Color(0.7f, 0.7f, 0.7f, 0.35f);
            Color colOccupied  = new Color(1f,   0f,   0f,   0.5f);
            Color colFootprint = _previewValid ? new Color(0f, 1f, 0f, 0.5f)
                                               : new Color(1f, 0f, 0f, 0.5f);

            int CellPriority(Vector2Int c)
            {
                if (footprint.Contains(c)) return 2;
                if (_occupied.Contains(c)) return 1;
                return 0;
            }

            bool InBounds(int cx, int cz) =>
                cx >= minCX && cx < maxCX && cz >= minCZ && cz < maxCZ;

            int EdgePriority(Vector2Int a, bool aIn, Vector2Int b, bool bIn) =>
                Mathf.Max(aIn ? CellPriority(a) : -1, bIn ? CellPriority(b) : -1);

            Color PriorityColor(int p) =>
                p == 2 ? colFootprint : p == 1 ? colOccupied : colGrid;

            for (int czLine = minCZ; czLine <= maxCZ; czLine++)
            {
                float z = czLine * _cellSize;
                for (int cx = minCX; cx < maxCX; cx++)
                {
                    int prio = EdgePriority(
                        new Vector2Int(cx, czLine - 1), InBounds(cx, czLine - 1),
                        new Vector2Int(cx, czLine),     InBounds(cx, czLine));
                    if (prio < 0) continue;
                    GL.Color(PriorityColor(prio));
                    GL.Vertex3(cx       * _cellSize, y, z);
                    GL.Vertex3((cx + 1) * _cellSize, y, z);
                }
            }

            for (int cxLine = minCX; cxLine <= maxCX; cxLine++)
            {
                float x = cxLine * _cellSize;
                for (int cz = minCZ; cz < maxCZ; cz++)
                {
                    int prio = EdgePriority(
                        new Vector2Int(cxLine - 1, cz), InBounds(cxLine - 1, cz),
                        new Vector2Int(cxLine,     cz), InBounds(cxLine,     cz));
                    if (prio < 0) continue;
                    GL.Color(PriorityColor(prio));
                    GL.Vertex3(x, y, cz       * _cellSize);
                    GL.Vertex3(x, y, (cz + 1) * _cellSize);
                }
            }
        }

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

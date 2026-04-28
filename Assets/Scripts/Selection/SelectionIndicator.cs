using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Pantheum.Buildings;
using Pantheum.Core;

namespace Pantheum.Selection
{
    public class SelectionIndicator : MonoBehaviour
    {
        private readonly Dictionary<Selectable, GameObject> _discs = new();
        private Material _mat;
        private Mesh _discMesh;

        private void Update()
        {
            if (SelectionManager.Instance == null) return;

            var selected = SelectionManager.Instance.Selected;
            var current  = new HashSet<Selectable>(selected);

            var toRemove = new List<Selectable>();
            foreach (var kv in _discs)
                if (!current.Contains(kv.Key)) { Destroy(kv.Value); toRemove.Add(kv.Key); }
            foreach (var s in toRemove) _discs.Remove(s);

            foreach (var sel in selected)
                if (sel != null && !_discs.ContainsKey(sel))
                    _discs[sel] = SpawnDisc(sel);
        }

        private GameObject SpawnDisc(Selectable sel)
        {
            var go = new GameObject("_SelectionDisc");
            go.transform.SetParent(sel.transform, worldPositionStays: false);
            bool isBuilding = sel.GetComponent<BuildingBase>() != null;
            float yOffset;
            if (isBuilding)
            {
                var rend = sel.GetComponentInChildren<Renderer>();
                yOffset = rend != null
                    ? rend.bounds.min.y - sel.transform.position.y + 0.01f
                    : -0.499f;
            }
            else
            {
                yOffset = -0.999f;
            }
            go.transform.localPosition = new Vector3(0f, yOffset, 0f);
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale    = Vector3.one;

            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();

            mf.sharedMesh     = BuildMesh(GetRadius(sel));
            mr.sharedMaterial = GetMaterial();
            mr.shadowCastingMode  = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows     = false;

            return go;
        }

        private static float GetRadius(Selectable sel)
        {
            var building = sel.GetComponent<BuildingBase>();
            if (building != null)
            {
                var rend = sel.GetComponentInChildren<Renderer>();
                if (rend != null)
                    return Mathf.Max(rend.bounds.extents.x, rend.bounds.extents.z) * 0.4f;
            }

            var agent = sel.GetComponent<NavMeshAgent>();
            if (agent != null) return agent.radius * 1.5f;

            var col = sel.GetComponent<Collider>();
            if (col != null)
                return Mathf.Max(col.bounds.extents.x, col.bounds.extents.z) * 1.5f;

            return 0.6f;
        }

        private Mesh BuildMesh(float radius)
        {
            const int   segments  = 64;
            const float innerFrac = 0.85f;

            var mesh = new Mesh { name = "SelectionDisc" };

            var verts  = new Vector3[segments * 2];
            var colors = new Color32[segments * 2];
            var tris   = new int[segments * 6];

            var outer = new Color32(38, 255, 38, 255);
            var inner = new Color32(38, 255, 38, 0);

            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                float cos = Mathf.Cos(a), sin = Mathf.Sin(a);

                verts [i]            = new Vector3(cos * radius * innerFrac, 0f, sin * radius * innerFrac);
                verts [i + segments] = new Vector3(cos * radius,             0f, sin * radius);
                colors[i]            = inner;
                colors[i + segments] = outer;

                int next = (i + 1) % segments;
                int t    = i * 6;
                tris[t]   = i;            tris[t+1] = i + segments; tris[t+2] = next;
                tris[t+3] = next;         tris[t+4] = i + segments; tris[t+5] = next + segments;
            }

            mesh.vertices  = verts;
            mesh.colors32  = colors;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }

        private Material GetMaterial()
        {
            if (_mat != null) return _mat;

            var shader = Shader.Find("Sprites/Default");
            _mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            _mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return _mat;
        }

        private void OnDestroy()
        {
            foreach (var kv in _discs)
                if (kv.Value != null) Destroy(kv.Value);
            if (_mat != null) Destroy(_mat);
        }
    }
}

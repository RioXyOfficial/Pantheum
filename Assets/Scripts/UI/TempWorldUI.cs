using UnityEngine;

namespace Pantheum.UI
{
    /// <summary>
    /// IMGUI placeholder implementing all three display interfaces.
    /// Attach to any building, unit, or construction site and wire up the
    /// corresponding provider reference in the Inspector.
    ///
    /// To replace with proper world-space UI: implement the interfaces in a
    /// new component, swap the provider reference — zero logic changes required.
    /// </summary>
    public class TempWorldUI : MonoBehaviour, IHealthDisplay, IProgressDisplay, IWorkerCountDisplay
    {
        [SerializeField] private float _worldHeightOffset = 2f; // ajuster selon la taille du prefab

        private float _currentHealth, _maxHealth;
        private float _progress;
        private int _workerCurrent, _workerMax;

        private bool _isSelected;
        private bool _showHealth;
        private bool _showProgress;
        private bool _showWorkerCount;

        public void SetSelected(bool selected)
        {
            _isSelected = selected;
            _showHealth = _maxHealth > 0f && (_isSelected || _currentHealth < _maxHealth);
        }

        private Camera _cam;
        private GUIStyle _centeredLabel;

        private float _barWidth = 67f;

        private void Awake()
        {
            _cam = Camera.main;
            _centeredLabel = new GUIStyle();
            _centeredLabel.alignment = TextAnchor.MiddleCenter;
            _centeredLabel.normal.textColor = Color.white;

            // Units get a narrower bar automatically.
            if (GetComponent<Pantheum.Units.UnitBase>() != null)
                _barWidth = 33f;
        }

        // ── IHealthDisplay ────────────────────────────────────────────────
        public void UpdateHealth(float current, float max)
        {
            _currentHealth = current;
            _maxHealth = max;
            _showHealth = max > 0f && (_isSelected || current < max);
        }

        // ── IProgressDisplay ──────────────────────────────────────────────
        public void UpdateProgress(float t)
        {
            _progress = t;
            _showProgress = t < 1f; // visible dès t=0, disparaît quand la construction est finie
        }

        // ── IWorkerCountDisplay ───────────────────────────────────────────
        public void UpdateWorkerCount(int current, int max)
        {
            _workerCurrent = current;
            _workerMax = max;
            _showWorkerCount = true; // always visible on Castles
        }

        // ── IMGUI ─────────────────────────────────────────────────────────
        private void OnGUI()
        {
            if (_cam == null) return;

            Vector3 screenPos = _cam.WorldToScreenPoint(transform.position + Vector3.up * _worldHeightOffset);
            if (screenPos.z < 0f) return; // behind camera

            float x = screenPos.x;
            float y = Screen.height - screenPos.y; // flip Y for GUI space

            float barW = _barWidth, barH = 4f, rowH = 7f;
            float row = 0f;

            if (_showHealth)
            {
                DrawBar(x - barW * 0.5f, y + row, barW, barH,
                    _currentHealth / _maxHealth, Color.red, Color.green);
                row += rowH;
            }

            if (_showProgress)
            {
                DrawBar(x - barW * 0.5f, y + row, barW, barH,
                    _progress, Color.grey, Color.cyan);
                row += rowH;
            }

            if (_showWorkerCount)
            {
                GUI.Label(new Rect(x - barW * 0.5f, y + row, barW, 20f),
                    $"{_workerCurrent} / {_workerMax}", _centeredLabel);
            }
        }

        private static void DrawBar(float x, float y, float w, float h,
                                    float fill, Color bg, Color fg)
        {
            GUI.color = bg;
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
            GUI.color = fg;
            GUI.DrawTexture(new Rect(x, y, w * Mathf.Clamp01(fill), h), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}

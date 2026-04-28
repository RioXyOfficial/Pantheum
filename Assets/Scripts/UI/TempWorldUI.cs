using UnityEngine;

namespace Pantheum.UI
{
    public class TempWorldUI : MonoBehaviour, IHealthDisplay, IProgressDisplay, IWorkerCountDisplay
    {
        [SerializeField] private float _worldHeightOffset = 2f;

        private float _currentHealth, _maxHealth;
        private float _progress;
        private int _workerCurrent, _workerMax;

        private bool _isSelected;
        private bool _showHealth;
        private bool _showProgress;
        private bool _showWorkerCount;

        private Camera _cam;
        private GUIStyle _centeredLabel;
        private float _barWidth = 67f;

        public void SetSelected(bool selected)
        {
            _isSelected = selected;
            _showHealth = _maxHealth > 0f && (_isSelected || _currentHealth < _maxHealth);
        }

        private void Awake()
        {
            _cam = Camera.main;
            _centeredLabel = new GUIStyle
            {
                alignment = TextAnchor.MiddleCenter
            };
            _centeredLabel.normal.textColor = Color.white;

            if (GetComponent<Pantheum.Units.UnitBase>() != null)
                _barWidth = 33f;
        }

        public void UpdateHealth(float current, float max)
        {
            _currentHealth = current;
            _maxHealth = max;
            _showHealth = max > 0f && (_isSelected || current < max);
        }

        public void UpdateProgress(float t)
        {
            _progress = t;
            _showProgress = t < 1f;
        }

        public void UpdateWorkerCount(int current, int max)
        {
            _workerCurrent = current;
            _workerMax = max;
            _showWorkerCount = true;
        }

        private void OnGUI()
        {
            if (_cam == null)
                _cam = Camera.main;

            if (_cam == null) return;

            Vector3 screenPos = _cam.WorldToScreenPoint(transform.position + Vector3.up * _worldHeightOffset);
            if (screenPos.z < 0f) return;

            float x = screenPos.x;
            float y = Screen.height - screenPos.y;

            float barW = _barWidth, barH = 4f, rowH = 7f;
            float row = 0f;

            if (_showHealth)
            {
                float fill = _maxHealth > 0f ? _currentHealth / _maxHealth : 0f;
                DrawBar(x - barW * 0.5f, y + row, barW, barH, fill, Color.red, Color.green);
                row += rowH;
            }

            if (_showProgress)
            {
                DrawBar(x - barW * 0.5f, y + row, barW, barH, _progress, Color.grey, Color.cyan);
                row += rowH;
            }

            if (_showWorkerCount)
            {
                GUI.Label(new Rect(x - barW * 0.5f, y + row, barW, 20f),
                    $"{_workerCurrent} / {_workerMax}", _centeredLabel);
            }
        }

        private static void DrawBar(float x, float y, float w, float h, float fill, Color bg, Color fg)
        {
            GUI.color = bg;
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
            GUI.color = fg;
            GUI.DrawTexture(new Rect(x, y, w * Mathf.Clamp01(fill), h), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}
using UnityEngine;
using UnityEngine.InputSystem;

namespace Pantheum.Core
{
    /// <summary>
    /// Standard RTS camera.
    /// Attach directly to the Main Camera.
    ///
    /// Controls:
    ///   WASD / Arrow keys  — pan
    ///   Scroll wheel       — zoom (moves along forward axis)
    ///   Middle mouse drag  — pan (alternative)
    /// </summary>
    public class RTSCamera : MonoBehaviour
    {
        [Header("Pan")]
        [SerializeField] private float _panSpeed = 20f;
        [SerializeField] private float _edgeScrollThickness = 20f; // px; set to 0 to disable
        [SerializeField] private Vector2 _panLimitX = new(-80f, 80f);
        [SerializeField] private Vector2 _panLimitZ = new(-80f, 80f);

        [Header("Zoom")]
        [SerializeField] private float _zoomSpeed = 40f;
        [SerializeField] private float _minZoomY = 5f;
        [SerializeField] private float _maxZoomY = 60f;

        private Vector3 _lastMousePos;
        private bool _isMiddlePanning;

        /// <summary>
        /// Repositions the camera so that its forward ray hits worldPos on the ground,
        /// preserving the current Y height and rotation.
        /// </summary>
        public void CenterOn(Vector3 worldPos)
        {
            Vector3 fwd = transform.forward;
            float   y   = transform.position.y;

            // Avoid division by zero (camera looking straight up/down edge-case)
            float t = (fwd.y < -0.001f) ? y / -fwd.y : 0f;

            float x = Mathf.Clamp(worldPos.x - fwd.x * t, _panLimitX.x, _panLimitX.y);
            float z = Mathf.Clamp(worldPos.z - fwd.z * t, _panLimitZ.x, _panLimitZ.y);
            transform.position = new Vector3(x, y, z);
        }

        private void Update()
        {
            HandleKeyboardPan();
            HandleEdgeScroll();
            HandleZoom();
            HandleMiddleMousePan();
        }

        private void HandleKeyboardPan()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            Vector3 dir = Vector3.zero;

            if (kb.wKey.isPressed || kb.upArrowKey.isPressed)    dir += GetForwardFlat();
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed)  dir -= GetForwardFlat();
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) dir += transform.right;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)  dir -= transform.right;

            MovePosition(dir.normalized * (_panSpeed * Time.deltaTime));
        }

        private void HandleEdgeScroll()
        {
            if (_edgeScrollThickness <= 0f) return;

            var mouse = Mouse.current;
            if (mouse == null) return;

            Vector2 mp = mouse.position.ReadValue();

            // Ignore si la souris est hors de la fenêtre (mode fenêtré / éditeur)
            if (mp.x < 0f || mp.x > Screen.width || mp.y < 0f || mp.y > Screen.height) return;

            Vector3 dir = Vector3.zero;

            if (mp.x < _edgeScrollThickness)                          dir -= transform.right;
            if (mp.x > Screen.width  - _edgeScrollThickness)         dir += transform.right;
            if (mp.y < _edgeScrollThickness)                          dir -= GetForwardFlat();
            if (mp.y > Screen.height - _edgeScrollThickness)         dir += GetForwardFlat();

            MovePosition(dir.normalized * (_panSpeed * Time.deltaTime));
        }

        private void HandleZoom()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Approximately(scroll, 0f)) return;

            Vector3 pos = transform.position;
            pos += transform.forward * (scroll * _zoomSpeed * Time.deltaTime);
            pos.x = Mathf.Clamp(pos.x, _panLimitX.x, _panLimitX.y);
            pos.y = Mathf.Clamp(pos.y, _minZoomY, _maxZoomY);
            pos.z = Mathf.Clamp(pos.z, _panLimitZ.x, _panLimitZ.y);
            transform.position = pos;
        }

        private void HandleMiddleMousePan()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            if (mouse.middleButton.wasPressedThisFrame)
            {
                _lastMousePos = mouse.position.ReadValue();
                _isMiddlePanning = true;
            }
            if (mouse.middleButton.wasReleasedThisFrame)
                _isMiddlePanning = false;

            if (!_isMiddlePanning) return;

            Vector3 current = mouse.position.ReadValue();
            Vector3 delta = _lastMousePos - current;
            _lastMousePos = current;

            Vector3 move = (transform.right * delta.x + GetForwardFlat() * delta.y)
                           * (_panSpeed * 0.02f);
            MovePosition(move);
        }

        private void MovePosition(Vector3 delta)
        {
            Vector3 pos = transform.position + delta;
            pos.x = Mathf.Clamp(pos.x, _panLimitX.x, _panLimitX.y);
            pos.z = Mathf.Clamp(pos.z, _panLimitZ.x, _panLimitZ.y);
            transform.position = pos;
        }

        // Camera forward projected onto XZ plane (ignores pitch)
        private Vector3 GetForwardFlat()
        {
            Vector3 f = transform.forward;
            f.y = 0f;
            return f.normalized;
        }
    }
}

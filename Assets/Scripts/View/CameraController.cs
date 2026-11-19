using UnityEngine;
using UnityEngine.EventSystems;

namespace HexQuest
{
    /// <summary>Orthographic board camera: right/middle-drag or WASD to pan, scroll wheel to zoom.</summary>
    [RequireComponent(typeof(Camera))]
    public class CameraController : MonoBehaviour
    {
        Camera cam;
        Bounds limits;
        bool dragging;
        Vector3 dragOrigin;
        const float MinSize = 3f, MaxSize = 16f;

        void Awake()
        {
            cam = GetComponent<Camera>();
            cam.orthographic = true;
        }

        public void Frame(Bounds b)
        {
            limits = b;
            float fitV = b.extents.y;
            float fitH = b.extents.x / cam.aspect;
            cam.orthographicSize = Mathf.Clamp(Mathf.Max(fitV, fitH) * 1.05f, MinSize, MaxSize);
            transform.position = new Vector3(b.center.x, b.center.y - 0.4f, -10f);
        }

        public void FocusOn(Vector3 worldPos)
        {
            transform.position = new Vector3(worldPos.x, worldPos.y, -10f);
            ClampToLimits();
        }

        void Update()
        {
            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

            float scroll = Input.mouseScrollDelta.y;
            if (!overUi && Mathf.Abs(scroll) > 0.01f)
                cam.orthographicSize = Mathf.Clamp(cam.orthographicSize - scroll * 0.8f, MinSize, MaxSize);

            if ((Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2)) && !overUi)
            {
                dragging = true;
                dragOrigin = cam.ScreenToWorldPoint(Input.mousePosition);
            }
            if (dragging && (Input.GetMouseButton(1) || Input.GetMouseButton(2)))
            {
                Vector3 now = cam.ScreenToWorldPoint(Input.mousePosition);
                Vector3 delta = dragOrigin - now;
                delta.z = 0f;
                transform.position += delta;
            }
            else dragging = false;

            var pan = new Vector3(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"), 0f);
            transform.position += pan * (cam.orthographicSize * 1.2f * Time.deltaTime);

            ClampToLimits();
        }

        void ClampToLimits()
        {
            if (limits.size == Vector3.zero) return;
            var p = transform.position;
            p.x = Mathf.Clamp(p.x, limits.min.x, limits.max.x);
            p.y = Mathf.Clamp(p.y, limits.min.y, limits.max.y);
            p.z = -10f;
            transform.position = p;
        }
    }
}

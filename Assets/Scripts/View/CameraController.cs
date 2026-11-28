using UnityEngine;
using UnityEngine.EventSystems;

namespace HexQuest
{
    /// <summary>Orthographic board camera with smooth zoom/pan, bounds and screen shake.</summary>
    [RequireComponent(typeof(Camera))]
    public class CameraController : MonoBehaviour
    {
        public bool InputEnabled = true;

        Camera cam;
        Bounds limits;
        Vector3 focus = new Vector3(0f, 0f, -10f);
        float targetSize = 8f;
        bool dragging;
        Vector3 dragOrigin;
        float shakeAmp, shakeTime, shakeLeft;
        const float MinSize = 3.5f, MaxSize = 15f;

        void Awake()
        {
            cam = GetComponent<Camera>();
            cam.orthographic = true;
            focus = new Vector3(transform.position.x, transform.position.y, -10f);
            targetSize = cam.orthographicSize;
        }

        public void Frame(Bounds b, bool instant = true)
        {
            limits = b;
            float fitV = b.extents.y;
            float fitH = b.extents.x / cam.aspect;
            targetSize = Mathf.Clamp(Mathf.Max(fitV, fitH) * 1.02f, MinSize, MaxSize);
            focus = new Vector3(b.center.x, b.center.y - 0.35f, -10f);
            if (instant) { cam.orthographicSize = targetSize; transform.position = focus; }
        }

        public void FocusOn(Vector3 worldPos)
        {
            focus = new Vector3(worldPos.x, worldPos.y, -10f);
            Clamp();
        }

        public void Shake(float amplitude, float duration)
        {
            shakeAmp = amplitude; shakeTime = duration; shakeLeft = duration;
        }

        void Update()
        {
            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

            if (InputEnabled)
            {
                float scroll = Input.mouseScrollDelta.y;
                if (!overUi && Mathf.Abs(scroll) > 0.01f)
                    targetSize = Mathf.Clamp(targetSize - scroll * 0.9f, MinSize, MaxSize);

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
                    focus += delta;
                }
                else dragging = false;

                var pan = new Vector3(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"), 0f);
                focus += pan * (cam.orthographicSize * 1.3f * Time.unscaledDeltaTime);
            }
            else dragging = false;

            Clamp();
            cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetSize, 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime));

            Vector3 offset = Vector3.zero;
            if (shakeLeft > 0f)
            {
                shakeLeft -= Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(shakeLeft / Mathf.Max(0.01f, shakeTime));
                offset = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0f) * shakeAmp * k;
            }
            transform.position = Vector3.Lerp(transform.position, focus + offset, offset == Vector3.zero ? 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime) : 1f);
        }

        void Clamp()
        {
            if (limits.size == Vector3.zero) return;
            focus.x = Mathf.Clamp(focus.x, limits.min.x, limits.max.x);
            focus.y = Mathf.Clamp(focus.y, limits.min.y, limits.max.y);
            focus.z = -10f;
        }
    }
}

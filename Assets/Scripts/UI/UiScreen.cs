using System.Collections;
using UnityEngine;

namespace HexQuest
{
    /// <summary>Base for a full-screen or modal UI page: builds itself once, fades in/out.</summary>
    public abstract class UiScreen : MonoBehaviour
    {
        protected RectTransform Root;
        CanvasGroup group;
        Coroutine fade;

        public event System.Action Closed;
        public bool IsOpen { get; private set; }

        public void Build(Transform canvasRoot)
        {
            Root = UIKit.NewRect(GetType().Name, canvasRoot).Stretch();
            group = Root.gameObject.AddComponent<CanvasGroup>();
            OnBuild(Root);
            Root.gameObject.SetActive(false);
        }

        protected abstract void OnBuild(RectTransform root);
        protected virtual void OnOpen() { }

        /// <summary>Return true if the screen handled Escape itself.</summary>
        public virtual bool OnEscape() { return false; }

        public void Open(float duration = 0.22f)
        {
            IsOpen = true;
            Root.gameObject.SetActive(true);
            Root.SetAsLastSibling();
            OnOpen();
            StartFade(1f, duration, false);
        }

        public void Close(float duration = 0.15f)
        {
            if (!IsOpen) return;
            IsOpen = false;
            StartFade(0f, duration, true);
            if (Closed != null) Closed();
        }

        void StartFade(float to, float duration, bool deactivate)
        {
            if (fade != null) StopCoroutine(fade);
            fade = StartCoroutine(Fade(to, duration, deactivate));
        }

        IEnumerator Fade(float to, float duration, bool deactivate)
        {
            float from = group.alpha;
            group.blocksRaycasts = !deactivate;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
                yield return null;
            }
            group.alpha = to;
            if (deactivate) Root.gameObject.SetActive(false);
        }
    }
}

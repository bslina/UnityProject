using System.Collections;
using UnityEngine;

namespace GameUI
{
    // Base for every menu screen/popup: Show() / Hide() with a short fade (works while paused).
    [RequireComponent(typeof(CanvasGroup))]
    public class UIPanel : MonoBehaviour
    {
        [SerializeField] float fadeDuration = 0.15f;

        CanvasGroup group;
        Coroutine fading;

        public bool IsOpen => gameObject.activeSelf;

        protected virtual void Awake() => group = GetComponent<CanvasGroup>();

        public virtual void Show()
        {
            if (group == null) group = GetComponent<CanvasGroup>();
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            group.interactable = true;
            group.blocksRaycasts = true;
            FadeTo(1f, false);
        }

        public virtual void Hide()
        {
            if (!gameObject.activeSelf) return;
            group.interactable = false;
            group.blocksRaycasts = false;
            FadeTo(0f, true);
        }

        public void HideImmediate()
        {
            if (fading != null) StopCoroutine(fading);
            fading = null;
            if (group == null) group = GetComponent<CanvasGroup>();
            group.alpha = 0f;
            gameObject.SetActive(false);
        }

        void FadeTo(float target, bool deactivateAtEnd)
        {
            if (fading != null) StopCoroutine(fading);
            if (fadeDuration <= 0f || !isActiveAndEnabled)
            {
                group.alpha = target;
                if (deactivateAtEnd) gameObject.SetActive(false);
                return;
            }
            fading = StartCoroutine(FadeRoutine(target, deactivateAtEnd));
        }

        IEnumerator FadeRoutine(float target, bool deactivateAtEnd)
        {
            float start = group.alpha;
            for (float t = 0f; t < fadeDuration; t += Time.unscaledDeltaTime)
            {
                group.alpha = Mathf.Lerp(start, target, t / fadeDuration);
                yield return null;
            }
            group.alpha = target;
            fading = null;
            if (deactivateAtEnd) gameObject.SetActive(false);
        }
    }
}

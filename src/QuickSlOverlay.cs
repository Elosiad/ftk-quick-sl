using System;
using UnityEngine;
using UnityEngine.UI;

namespace FtkQuickSL
{
    public static class QuickSlOverlay
    {
        private static GameObject _canvasGo;
        private static RectTransform _canvasRt;
        private static Font _cachedFont;

        // Fullscreen seamless reload mask
        private static GameObject _maskGo;
        private static CanvasGroup _maskGroup;
        private static Image _maskImage;
        private static Text _statusText;

        private enum MaskState
        {
            Hidden,
            FadingIn,
            Opaque,
            FadingOut
        }

        private static MaskState _maskState = MaskState.Hidden;
        private static float _maskDuration;
        private static float _maskElapsed;
        private static float _maskStartAlpha;
        private static Action _onOpaqueCallback;
        private static bool _opaqueWaitOneFrame;

        // Hold-to-end-turn progress bar
        private static GameObject _holdRootGo;
        private static RectTransform _holdRootRt;
        private static CanvasGroup _holdGroup;
        private static RectTransform _holdFillRt;

        public static void CaptureNativeFont(Font font)
        {
            if (font != null && _cachedFont == null)
            {
                _cachedFont = font;
                if (_statusText != null)
                {
                    _statusText.font = _cachedFont;
                }
            }
        }

        private static Font GetFont()
        {
            if (_cachedFont != null) return _cachedFont;
            try
            {
                if (uiOptionsMenu.Instance != null && uiOptionsMenu.Instance.m_SaveGameButton != null)
                {
                    Text t = uiOptionsMenu.Instance.m_SaveGameButton.GetComponentInChildren<Text>(true);
                    if (t != null && t.font != null)
                    {
                        _cachedFont = t.font;
                        return _cachedFont;
                    }
                }
            }
            catch { }

            try
            {
                _cachedFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            catch { }
            return _cachedFont;
        }

        private static void EnsureOverlayCreated()
        {
            if (_canvasGo != null) return;

            _canvasGo = new GameObject("FtkQuickSlOverlayCanvas");
            UnityEngine.Object.DontDestroyOnLoad(_canvasGo);

            Canvas canvas = _canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32760;

            CanvasScaler scaler = _canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            _canvasGo.AddComponent<GraphicRaycaster>();
            _canvasRt = _canvasGo.GetComponent<RectTransform>();

            CreateMaskObjects();
            CreateHoldProgressObjects();
        }

        private static void CreateMaskObjects()
        {
            _maskGo = new GameObject("SeamlessMask");
            _maskGo.transform.SetParent(_canvasGo.transform, false);

            RectTransform rt = _maskGo.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            _maskImage = _maskGo.AddComponent<Image>();
            _maskImage.color = Color.black;
            _maskImage.raycastTarget = true;

            _maskGroup = _maskGo.AddComponent<CanvasGroup>();
            _maskGroup.alpha = 0f;
            _maskGroup.blocksRaycasts = true;
            _maskGroup.interactable = false;

            GameObject textGo = new GameObject("StatusText");
            textGo.transform.SetParent(_maskGo.transform, false);
            RectTransform textRt = textGo.AddComponent<RectTransform>();
            textRt.anchorMin = new Vector2(0.15f, 0.42f);
            textRt.anchorMax = new Vector2(0.85f, 0.58f);
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            _statusText = textGo.AddComponent<Text>();
            _statusText.font = GetFont();
            _statusText.fontSize = 26;
            _statusText.alignment = TextAnchor.MiddleCenter;
            _statusText.color = new Color(0.92f, 0.86f, 0.72f, 1f);
            _statusText.raycastTarget = false;
            _statusText.text = string.Empty;

            _maskGo.SetActive(false);
        }

        private static void CreateHoldProgressObjects()
        {
            _holdRootGo = new GameObject("HoldEndTurnProgress");
            _holdRootGo.transform.SetParent(_canvasGo.transform, false);

            _holdRootRt = _holdRootGo.AddComponent<RectTransform>();
            _holdRootRt.anchorMin = new Vector2(0.5f, 0.5f);
            _holdRootRt.anchorMax = new Vector2(0.5f, 0.5f);
            _holdRootRt.pivot = new Vector2(0.5f, 0.5f);
            _holdRootRt.sizeDelta = new Vector2(74f, 10f);

            Image bg = _holdRootGo.AddComponent<Image>();
            bg.color = new Color(0.06f, 0.05f, 0.05f, 0.88f);
            bg.raycastTarget = false;

            _holdGroup = _holdRootGo.AddComponent<CanvasGroup>();
            _holdGroup.alpha = 0f;
            _holdGroup.blocksRaycasts = false;
            _holdGroup.interactable = false;

            GameObject innerGo = new GameObject("InnerTrack");
            innerGo.transform.SetParent(_holdRootGo.transform, false);
            RectTransform innerRt = innerGo.AddComponent<RectTransform>();
            innerRt.anchorMin = Vector2.zero;
            innerRt.anchorMax = Vector2.one;
            innerRt.offsetMin = new Vector2(2f, 2f);
            innerRt.offsetMax = new Vector2(-2f, -2f);

            Image innerImg = innerGo.AddComponent<Image>();
            innerImg.color = new Color(0.18f, 0.16f, 0.14f, 0.9f);
            innerImg.raycastTarget = false;

            GameObject fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(innerGo.transform, false);
            _holdFillRt = fillGo.AddComponent<RectTransform>();
            _holdFillRt.anchorMin = Vector2.zero;
            _holdFillRt.anchorMax = new Vector2(0f, 1f);
            _holdFillRt.offsetMin = Vector2.zero;
            _holdFillRt.offsetMax = Vector2.zero;

            Image fillImg = fillGo.AddComponent<Image>();
            fillImg.color = new Color(0.86f, 0.68f, 0.28f, 0.98f);
            fillImg.raycastTarget = false;

            _holdRootGo.SetActive(false);
        }

        public static void FadeInAndRun(float duration, string status, Action onOpaque)
        {
            EnsureOverlayCreated();
            _statusText.font = GetFont();
            _statusText.text = status ?? string.Empty;

            _maskGo.SetActive(true);
            _maskGo.transform.SetAsLastSibling();
            _maskImage.raycastTarget = true;
            _maskGroup.blocksRaycasts = true;

            _maskStartAlpha = _maskGroup.alpha;
            _maskDuration = Mathf.Max(0.02f, duration);
            _maskElapsed = 0f;
            _onOpaqueCallback = onOpaque;
            _opaqueWaitOneFrame = false;
            _maskState = MaskState.FadingIn;
        }

        public static void SetStatus(string status)
        {
            if (_statusText != null && _maskGo != null && _maskGo.activeSelf)
            {
                _statusText.text = status ?? string.Empty;
            }
        }

        public static void FadeOut(float duration)
        {
            if (_maskGo == null || !_maskGo.activeSelf)
            {
                _maskState = MaskState.Hidden;
                _onOpaqueCallback = null;
                return;
            }

            _onOpaqueCallback = null;
            _opaqueWaitOneFrame = false;
            _maskImage.raycastTarget = false;
            _maskGroup.blocksRaycasts = false;
            _maskStartAlpha = _maskGroup.alpha;
            _maskDuration = Mathf.Max(0.02f, duration);
            _maskElapsed = 0f;
            _maskState = MaskState.FadingOut;
        }

        public static void HideImmediate()
        {
            _maskState = MaskState.Hidden;
            _onOpaqueCallback = null;
            _opaqueWaitOneFrame = false;
            if (_maskGroup != null)
            {
                _maskGroup.alpha = 0f;
                _maskGroup.blocksRaycasts = false;
            }
            if (_maskImage != null)
                _maskImage.raycastTarget = false;
            if (_maskGo != null)
                _maskGo.SetActive(false);
        }

        public static void ShowHoldProgress(Vector2 screenPos, float progress01, float alpha01)
        {
            EnsureOverlayCreated();
            if (!_holdRootGo.activeSelf)
                _holdRootGo.SetActive(true);

            _holdGroup.alpha = Mathf.Clamp01(alpha01);
            _holdFillRt.anchorMax = new Vector2(Mathf.Clamp01(progress01), 1f);

            Vector2 localPos;
            if (_canvasRt != null &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRt, screenPos, null, out localPos))
            {
                float halfW = _holdRootRt.sizeDelta.x * 0.5f + 8f;
                float halfH = _holdRootRt.sizeDelta.y * 0.5f + 8f;
                float x = Mathf.Clamp(localPos.x, _canvasRt.rect.xMin + halfW, _canvasRt.rect.xMax - halfW);
                float y = Mathf.Clamp(localPos.y - 24f, _canvasRt.rect.yMin + halfH, _canvasRt.rect.yMax - halfH);
                _holdRootRt.anchoredPosition = new Vector2(x, y);
            }
        }

        public static void HideHoldProgress()
        {
            if (_holdRootGo != null && _holdRootGo.activeSelf)
            {
                _holdGroup.alpha = 0f;
                _holdRootGo.SetActive(false);
            }
        }

        public static void Tick()
        {
            if (_maskGo == null || _maskState == MaskState.Hidden || _maskState == MaskState.Opaque)
                return;

            if (_maskState == MaskState.FadingIn)
            {
                if (_opaqueWaitOneFrame)
                {
                    _opaqueWaitOneFrame = false;
                    _maskState = MaskState.Opaque;
                    Action cb = _onOpaqueCallback;
                    _onOpaqueCallback = null;
                    if (cb != null) cb();
                    return;
                }

                _maskElapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(_maskElapsed / _maskDuration);
                _maskGroup.alpha = Mathf.Lerp(_maskStartAlpha, 1f, t);
                if (t >= 1f)
                {
                    _maskGroup.alpha = 1f;
                    // Wait one frame at full black before executing SceneManager.LoadScene so the frame paints cleanly.
                    _opaqueWaitOneFrame = true;
                }
            }
            else if (_maskState == MaskState.FadingOut)
            {
                _maskElapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(_maskElapsed / _maskDuration);
                _maskGroup.alpha = Mathf.Lerp(_maskStartAlpha, 0f, t);
                if (t >= 1f)
                {
                    HideImmediate();
                }
            }
        }

        public static void Cleanup()
        {
            HideImmediate();
            HideHoldProgress();
            if (_canvasGo != null)
            {
                UnityEngine.Object.Destroy(_canvasGo);
            }
            _canvasGo = null;
            _canvasRt = null;
            _maskGo = null;
            _maskGroup = null;
            _maskImage = null;
            _statusText = null;
            _holdRootGo = null;
            _holdRootRt = null;
            _holdGroup = null;
            _holdFillRt = null;
        }
    }
}

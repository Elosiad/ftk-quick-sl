using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FtkQuickSL
{
    public class QuickSlHudButtonBehaviour : MonoBehaviour,
        IPointerEnterHandler,
        IPointerClickHandler,
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler
    {
        public RectTransform DragTarget;
        public RectTransform ParentRect;

        private Button _button;
        private bool _dragging;
        private Vector2 _dragOffset;

        private void Awake()
        {
            _button = GetComponent<Button>();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_button != null && !_button.interactable) return;
            try
            {
                if (AudioManager.Instance != null)
                    AudioManager.Instance.MainMenuButtonOver();
            }
            catch { }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData == null || eventData.button != PointerEventData.InputButton.Left)
                return;
            if (_button != null && !_button.interactable)
                return;
            try
            {
                if (AudioManager.Instance != null)
                    AudioManager.Instance.MainMenuButtonClick();
            }
            catch { }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData == null || eventData.button != PointerEventData.InputButton.Right)
                return;
            if (DragTarget == null) DragTarget = GetComponent<RectTransform>();
            if (ParentRect == null && DragTarget != null && DragTarget.parent != null)
                ParentRect = DragTarget.parent as RectTransform;
            if (DragTarget == null || ParentRect == null) return;

            Vector2 localPointer;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                ParentRect, eventData.position, eventData.pressEventCamera, out localPointer))
            {
                _dragging = true;
                _dragOffset = DragTarget.anchoredPosition - localPointer;
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging || eventData == null || DragTarget == null || ParentRect == null)
                return;

            Vector2 localPointer;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                ParentRect, eventData.position, eventData.pressEventCamera, out localPointer))
            {
                DragTarget.anchoredPosition = ClampRectToParent(DragTarget, ParentRect, localPointer + _dragOffset);
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_dragging) return;
            _dragging = false;
            if (DragTarget != null && FtkQuickSLPlugin.Instance != null)
            {
                Vector2 clamped = ClampRectToParent(DragTarget, ParentRect, DragTarget.anchoredPosition);
                DragTarget.anchoredPosition = clamped;
                FtkQuickSLPlugin.Instance.SaveHudOffset(clamped);
            }
        }

        public static Vector2 ClampRectToParent(RectTransform target, RectTransform parent, Vector2 anchoredPos)
        {
            if (parent == null || target == null)
                return anchoredPos;

            float parentW = parent.rect.width;
            float parentH = parent.rect.height;
            float btnW = target.rect.width > 1f ? target.rect.width : target.sizeDelta.x;
            float btnH = target.rect.height > 1f ? target.rect.height : target.sizeDelta.y;

            if (parentW <= 10f || parentH <= 10f)
                return anchoredPos;

            float minX = -(parentW - btnW - 4f);
            float maxX = -4f;
            float minY = -(parentH - btnH - 4f);
            float maxY = -4f;

            if (minX > maxX) minX = maxX;
            if (minY > maxY) minY = maxY;

            return new Vector2(
                Mathf.Clamp(anchoredPos.x, minX, maxX),
                Mathf.Clamp(anchoredPos.y, minY, maxY));
        }
    }

    public static class NativeUiInjector
    {
        private const string HudBarName = "FtkQuickSlHudBar";
        private const string HudSaveButtonName = "FtkQuickSlHudSaveBtn";
        private const string HudLoadButtonName = "FtkQuickSlHudLoadBtn";
        private const string OptionsButtonName = "FtkQuickSlOptionsBtn";
        private const string QuickSaveMenuButtonName = "FtkQuickSlQuickSaveBtn";
        private const string SaveMenuButtonName = "FtkQuickSlSaveBtn";

        private const float HudButtonWidth = 38f;
        private const float HudButtonHeight = 26f;
        private const float HudButtonGap = 6f;
        private const float HudBarWidth = HudButtonWidth * 2f + HudButtonGap;

        private static GameObject _hudBarGo;
        private static RectTransform _hudBarRt;
        private static Button _hudSaveButton;
        private static Text _hudSaveText;
        private static Button _hudLoadButton;
        private static Text _hudLoadText;

        private static float _hudRefreshTimer;
        private static float _hudCreateCooldown;
        private static float _saveSuccessFeedbackTimer;

        private static bool _hasAutoPos;
        private static Vector2 _lastAutoAnchoredPos;

        private static bool _optionsLayoutShifted;
        private static bool _saveLayoutShifted;

        private static readonly List<GameObject> _hiddenMainOptionObjects = new List<GameObject>();
        private static GameObject _hiddenMainRootObject;

        public static bool IsChineseLocale()
        {
            try
            {
                if (uiOptionsMenu.Instance != null && uiOptionsMenu.Instance.m_LanguageText != null)
                {
                    string langText = uiOptionsMenu.Instance.m_LanguageText.text;
                    if (!string.IsNullOrEmpty(langText))
                    {
                        for (int i = 0; i < langText.Length; i++)
                        {
                            if (langText[i] >= '\u4e00' && langText[i] <= '\u9fff')
                                return true;
                        }
                        if (langText.IndexOf("Chinese", StringComparison.OrdinalIgnoreCase) >= 0)
                            return true;
                    }
                }
            }
            catch { }

            SystemLanguage sysLang = Application.systemLanguage;
            return sysLang == SystemLanguage.Chinese ||
                   sysLang == SystemLanguage.ChineseSimplified ||
                   sysLang == SystemLanguage.ChineseTraditional;
        }

        private static string GetHudSaveLabel()
        {
            if (_saveSuccessFeedbackTimer > 0f) return "✓";
            if (FtkQuickSLPlugin.Instance != null && !string.IsNullOrEmpty(FtkQuickSLPlugin.Instance.SaveButtonLabel.Value))
                return FtkQuickSLPlugin.Instance.SaveButtonLabel.Value;
            return "S";
        }

        private static string GetHudLoadLabel()
        {
            if (FtkQuickSLPlugin.Instance != null && !string.IsNullOrEmpty(FtkQuickSLPlugin.Instance.LoadButtonLabel.Value))
                return FtkQuickSLPlugin.Instance.LoadButtonLabel.Value;
            return "L";
        }

        private static string GetOptionsLoadLabel()
        {
            return IsChineseLocale() ? "快捷读档" : "Quick SL";
        }

        private static string GetQuickSaveMenuLabel()
        {
            return IsChineseLocale() ? "快速保存（不退出）" : "Quick Save";
        }

        private static string GetQuickLoadMenuLabel()
        {
            return IsChineseLocale() ? "重载上一自动存档" : "Reload Last Autosave";
        }

        public static void NotifyQuickSaveSuccess()
        {
            _saveSuccessFeedbackTimer = 1.0f;
            RefreshHudButtonState();
        }

        public static void CleanupHudButton()
        {
            if (_hudBarGo != null)
            {
                UnityEngine.Object.Destroy(_hudBarGo);
            }
            _hudBarGo = null;
            _hudBarRt = null;
            _hudSaveButton = null;
            _hudSaveText = null;
            _hudLoadButton = null;
            _hudLoadText = null;
            _saveSuccessFeedbackTimer = 0f;
        }

        public static void TickHudButton()
        {
            try
            {
                if (_saveSuccessFeedbackTimer > 0f)
                {
                    _saveSuccessFeedbackTimer -= Time.unscaledDeltaTime;
                    if (_saveSuccessFeedbackTimer <= 0f)
                    {
                        _saveSuccessFeedbackTimer = 0f;
                        RefreshHudButtonState();
                    }
                }

                bool shouldShow = ShouldShowHudButton();

                if (!shouldShow)
                {
                    if (_hudBarGo != null && _hudBarGo.activeSelf)
                        _hudBarGo.SetActive(false);
                    return;
                }

                if (_hudBarGo == null)
                {
                    _hudCreateCooldown -= Time.unscaledDeltaTime;
                    if (_hudCreateCooldown > 0f) return;
                    _hudCreateCooldown = 0.5f;

                    if (!TryCreateHudBar())
                        return;
                }

                if (!_hudBarGo.activeSelf)
                {
                    _hudBarGo.SetActive(true);
                    _hudBarGo.transform.SetAsLastSibling();
                    UpdateHudBarPosition();
                    RefreshHudButtonState();
                }

                _hudRefreshTimer += Time.unscaledDeltaTime;
                if (_hudRefreshTimer >= 0.5f)
                {
                    _hudRefreshTimer = 0f;
                    UpdateHudBarPosition();
                    RefreshHudButtonState();
                }
            }
            catch (Exception ex)
            {
                FtkQuickSLPlugin.LogSource.LogDebug("TickHudButton skipped: " + ex.Message);
            }
        }

        private static bool ShouldShowHudButton()
        {
            if (FtkQuickSLPlugin.Instance != null && !FtkQuickSLPlugin.Instance.ShowHudButton.Value)
                return false;
            if (QuickReloadController.IsBusy)
                return false;
            if (uiStartGame.Instance == null || !uiStartGame.Instance.m_GameStarted)
                return false;
            if (FTKHub.Instance == null)
                return false;
            if (FTKUI.Instance == null || FTKUI.Instance.m_MainCanvas == null)
                return false;
            if (uiOptionsMenu.Instance != null && uiOptionsMenu.Instance.m_Showing)
                return false;
            if (uiSystemDialog.Instance != null && uiSystemDialog.Instance.gameObject.activeInHierarchy)
                return false;
            if (FTKUI.Instance.m_InGameFade != null &&
                FTKUI.Instance.m_InGameFade.gameObject.activeInHierarchy &&
                FTKUI.Instance.m_InGameFade.color.a > 0.1f)
                return false;

            return true;
        }

        private static bool TryCreateHudBar()
        {
            uiOptionsMenu optMenu = uiOptionsMenu.Instance;
            if (optMenu == null)
                optMenu = UnityEngine.Object.FindObjectOfType<uiOptionsMenu>();
            if (optMenu == null || optMenu.m_SaveGameButton == null)
                return false;

            Text templateText = optMenu.m_SaveGameButton.GetComponentInChildren<Text>(true);
            if (templateText != null)
            {
                QuickSlOverlay.CaptureNativeFont(templateText.font);
            }

            RectTransform canvasRt = FTKUI.Instance.m_MainCanvas;
            Transform existing = canvasRt.Find(HudBarName);
            if (existing != null)
            {
                _hudBarGo = existing.gameObject;
                _hudBarRt = _hudBarGo.GetComponent<RectTransform>();
                Transform sTr = _hudBarGo.transform.Find(HudSaveButtonName);
                Transform lTr = _hudBarGo.transform.Find(HudLoadButtonName);
                if (sTr != null && lTr != null)
                {
                    _hudSaveButton = sTr.GetComponent<Button>();
                    _hudSaveText = sTr.GetComponentInChildren<Text>(true);
                    _hudLoadButton = lTr.GetComponent<Button>();
                    _hudLoadText = lTr.GetComponentInChildren<Text>(true);
                    return _hudSaveButton != null && _hudLoadButton != null;
                }
                UnityEngine.Object.Destroy(_hudBarGo);
            }

            _hudBarGo = new GameObject(HudBarName);
            _hudBarGo.transform.SetParent(canvasRt, false);
            _hudBarRt = _hudBarGo.AddComponent<RectTransform>();
            _hudBarRt.anchorMin = new Vector2(1f, 1f);
            _hudBarRt.anchorMax = new Vector2(1f, 1f);
            _hudBarRt.pivot = new Vector2(1f, 1f);
            _hudBarRt.sizeDelta = new Vector2(HudBarWidth, HudButtonHeight);
            _hudBarRt.localScale = Vector3.one;
            _hudBarRt.localRotation = Quaternion.identity;

            _hudSaveButton = CreateCompactHudButton(
                optMenu.m_SaveGameButton,
                _hudBarRt,
                canvasRt,
                HudSaveButtonName,
                GetHudSaveLabel(),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                delegate { QuickReloadController.RequestQuickSave(); });

            _hudLoadButton = CreateCompactHudButton(
                optMenu.m_SaveGameButton,
                _hudBarRt,
                canvasRt,
                HudLoadButtonName,
                GetHudLoadLabel(),
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                delegate { QuickReloadController.RequestQuickReload(); });

            if (_hudSaveButton == null || _hudLoadButton == null)
            {
                CleanupHudButton();
                return false;
            }

            _hudSaveText = _hudSaveButton.GetComponentInChildren<Text>(true);
            _hudLoadText = _hudLoadButton.GetComponentInChildren<Text>(true);

            _hudBarGo.transform.SetAsLastSibling();
            UpdateHudBarPosition();
            RefreshHudButtonState();
            FtkQuickSLPlugin.LogSource.LogInfo("Created native-style [S][L] HUD bar.");
            return true;
        }

        private static Button CreateCompactHudButton(
            Button template,
            RectTransform barRt,
            RectTransform canvasRt,
            string name,
            string label,
            Vector2 anchor,
            Vector2 pivot,
            UnityEngine.Events.UnityAction onClick)
        {
            Button btn = CloneNativeButton(template, barRt, name, label, false, onClick);
            if (btn == null) return null;

            RectTransform rt = btn.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = anchor;
                rt.anchorMax = anchor;
                rt.pivot = pivot;
                rt.sizeDelta = new Vector2(HudButtonWidth, HudButtonHeight);
                rt.anchoredPosition = Vector2.zero;
                rt.localScale = Vector3.one;
                rt.localRotation = Quaternion.identity;
            }

            Text txt = btn.GetComponentInChildren<Text>(true);
            if (txt != null)
            {
                RectTransform textRt = txt.GetComponent<RectTransform>();
                if (textRt != null)
                {
                    textRt.anchorMin = Vector2.zero;
                    textRt.anchorMax = Vector2.one;
                    textRt.offsetMin = new Vector2(2f, 1f);
                    textRt.offsetMax = new Vector2(-2f, -1f);
                }
                txt.alignment = TextAnchor.MiddleCenter;
                txt.resizeTextForBestFit = true;
                txt.resizeTextMinSize = 10;
                txt.resizeTextMaxSize = 16;
            }

            QuickSlHudButtonBehaviour beh = btn.gameObject.AddComponent<QuickSlHudButtonBehaviour>();
            beh.DragTarget = barRt;
            beh.ParentRect = canvasRt;
            return btn;
        }

        private static void UpdateHudBarPosition()
        {
            if (_hudBarRt == null || FTKUI.Instance == null || FTKUI.Instance.m_MainCanvas == null)
                return;

            RectTransform canvasRt = FTKUI.Instance.m_MainCanvas;
            bool useCustom = FtkQuickSLPlugin.Instance != null && FtkQuickSLPlugin.Instance.HudUseCustomPosition.Value;

            if (useCustom)
            {
                Vector2 custom = new Vector2(
                    FtkQuickSLPlugin.Instance.HudOffsetX.Value,
                    FtkQuickSLPlugin.Instance.HudOffsetY.Value);
                _hudBarRt.anchoredPosition = QuickSlHudButtonBehaviour.ClampRectToParent(_hudBarRt, canvasRt, custom);
                return;
            }

            Vector2 autoPos;
            if (TryComputeTopBarPosition(canvasRt, out autoPos))
            {
                _hasAutoPos = true;
                _lastAutoAnchoredPos = autoPos;
                _hudBarRt.anchoredPosition = QuickSlHudButtonBehaviour.ClampRectToParent(_hudBarRt, canvasRt, autoPos);
            }
            else if (_hasAutoPos)
            {
                _hudBarRt.anchoredPosition = QuickSlHudButtonBehaviour.ClampRectToParent(_hudBarRt, canvasRt, _lastAutoAnchoredPos);
            }
            else
            {
                _hudBarRt.anchoredPosition = QuickSlHudButtonBehaviour.ClampRectToParent(_hudBarRt, canvasRt, new Vector2(-150f, -4f));
            }
        }

        private static bool TryComputeTopBarPosition(RectTransform canvasRt, out Vector2 anchoredPos)
        {
            anchoredPos = Vector2.zero;
            try
            {
                uiGameTrackerHUD tracker = uiGameTrackerHUD.Instance;
                if (tracker == null || tracker.m_CurrentDay == null || tracker.m_CurrentLoreValue == null)
                    return false;

                if (!tracker.m_CurrentDay.gameObject.activeInHierarchy ||
                    !tracker.m_CurrentLoreValue.gameObject.activeInHierarchy)
                    return false;

                RectTransform dayRt = tracker.m_CurrentDay.rectTransform;
                RectTransform loreRt = tracker.m_CurrentLoreValue.rectTransform;
                if (dayRt == null || loreRt == null) return false;

                Canvas trackerCanvas = tracker.GetComponentInParent<Canvas>();
                Canvas mainCanvas = canvasRt.GetComponentInParent<Canvas>();
                Camera trackerCam = (trackerCanvas != null && trackerCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                    ? trackerCanvas.worldCamera : null;
                Camera mainCam = (mainCanvas != null && mainCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                    ? mainCanvas.worldCamera : null;

                Vector3[] dayCorners = new Vector3[4];
                Vector3[] loreCorners = new Vector3[4];
                dayRt.GetWorldCorners(dayCorners);
                loreRt.GetWorldCorners(loreCorners);

                Vector3 dayRightWorld = 0.5f * (dayCorners[2] + dayCorners[3]);
                Vector3 loreLeftWorld = 0.5f * (loreCorners[0] + loreCorners[1]);

                Vector2 dayScreen = RectTransformUtility.WorldToScreenPoint(trackerCam, dayRightWorld);
                Vector2 loreScreen = RectTransformUtility.WorldToScreenPoint(trackerCam, loreLeftWorld);

                Vector2 dayLocal;
                Vector2 loreLocal;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, dayScreen, mainCam, out dayLocal))
                    return false;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRt, loreScreen, mainCam, out loreLocal))
                    return false;

                if (loreLocal.x <= dayLocal.x + 60f)
                    return false;

                // Keep a 42px margin left of the lore value text so its book icon is never overlapped.
                float maxRightX = loreLocal.x - 42f;
                float minRightX = dayLocal.x + 12f + HudBarWidth;
                float barRightX = maxRightX >= minRightX
                    ? maxRightX
                    : 0.5f * (dayLocal.x + loreLocal.x + HudBarWidth);

                float barTopY = Mathf.Min(canvasRt.rect.yMax - 3f, loreLocal.y + HudButtonHeight * 0.5f);

                anchoredPos = new Vector2(barRightX - canvasRt.rect.xMax, barTopY - canvasRt.rect.yMax);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void RefreshHudButtonState()
        {
            string reason;
            if (_hudSaveButton != null)
            {
                _hudSaveButton.interactable = QuickReloadController.CanQuickSave(out reason);
            }
            if (_hudSaveText != null)
            {
                _hudSaveText.text = GetHudSaveLabel();
            }

            if (_hudLoadButton != null)
            {
                _hudLoadButton.interactable = QuickReloadController.CanQuickReload(out reason);
            }
            if (_hudLoadText != null)
            {
                _hudLoadText.text = GetHudLoadLabel();
            }
        }

        public static void InjectIntoOptionsMain(uiOptionsMenu menu)
        {
            if (menu == null || menu.m_MainOptions == null || menu.m_SaveGameButton == null || menu.m_ReportBugs == null)
                return;

            RestoreMainOptionsVisibility(true);

            Transform parent = menu.m_SaveGameButton.transform.parent;
            if (parent == null) return;

            bool inGame = uiStartGame.Instance != null && uiStartGame.Instance.m_GameStarted;

            Transform existing = parent.Find(OptionsButtonName);
            if (!inGame)
            {
                if (existing != null)
                    existing.gameObject.SetActive(false);
                return;
            }

            Button quickBtn;
            if (existing == null)
            {
                _optionsLayoutShifted = false;
                quickBtn = CloneNativeButton(
                    menu.m_SaveGameButton,
                    parent,
                    OptionsButtonName,
                    GetOptionsLoadLabel(),
                    true,
                    delegate { QuickReloadController.RequestQuickReload(); });

                if (quickBtn == null) return;

                if (parent.GetComponent<LayoutGroup>() != null)
                {
                    quickBtn.transform.SetSiblingIndex(menu.m_SaveGameButton.transform.GetSiblingIndex() + 1);
                }
                else
                {
                    float? hintStep = MeasureStep(
                        menu.m_ControlButton != null ? menu.m_ControlButton.transform : null,
                        menu.m_SaveGameButton.transform);
                    InsertVerticallyBelow(
                        parent,
                        menu.m_SaveGameButton.transform,
                        menu.m_ReportBugs.transform,
                        new Transform[] { quickBtn.transform },
                        hintStep,
                        ref _optionsLayoutShifted);
                }
            }
            else
            {
                quickBtn = existing.GetComponent<Button>();
            }

            if (quickBtn == null) return;

            quickBtn.gameObject.SetActive(true);
            SetButtonText(quickBtn.gameObject, GetOptionsLoadLabel());
            string reason;
            quickBtn.interactable = QuickReloadController.CanQuickReload(out reason);

            uiItemIcon.SetNavigatePairUpDown(menu.m_SaveGameButton.transform, quickBtn.transform);
            uiItemIcon.SetNavigatePairUpDown(quickBtn.transform, menu.m_ReportBugs.transform);

            RebuildLayoutHierarchy(parent, menu.m_MainOptions.transform);
            menu.m_MainOptions.UpdateSelectables();
        }

        public static void InjectIntoOptionsSave(uiOptionsSave saveMenu)
        {
            if (saveMenu == null || saveMenu.m_JustExit == null || saveMenu.m_Back == null)
                return;

            Transform parent = saveMenu.m_JustExit.transform.parent;
            if (parent == null) return;

            Transform existingSave = parent.Find(QuickSaveMenuButtonName);
            Transform existingLoad = parent.Find(SaveMenuButtonName);
            Button quickSaveBtn;
            Button quickLoadBtn;

            if (existingSave == null || existingLoad == null)
            {
                if (existingSave != null) UnityEngine.Object.DestroyImmediate(existingSave.gameObject);
                if (existingLoad != null) UnityEngine.Object.DestroyImmediate(existingLoad.gameObject);

                _saveLayoutShifted = false;
                quickSaveBtn = CloneNativeButton(
                    saveMenu.m_JustExit,
                    parent,
                    QuickSaveMenuButtonName,
                    GetQuickSaveMenuLabel(),
                    true,
                    delegate { QuickReloadController.RequestQuickSave(); });

                quickLoadBtn = CloneNativeButton(
                    saveMenu.m_JustExit,
                    parent,
                    SaveMenuButtonName,
                    GetQuickLoadMenuLabel(),
                    true,
                    delegate { QuickReloadController.RequestQuickReload(); });

                if (quickSaveBtn == null || quickLoadBtn == null) return;

                if (parent.GetComponent<LayoutGroup>() != null)
                {
                    int idx = saveMenu.m_JustExit.transform.GetSiblingIndex();
                    quickSaveBtn.transform.SetSiblingIndex(idx + 1);
                    quickLoadBtn.transform.SetSiblingIndex(idx + 2);
                }
                else
                {
                    float? hintStep = MeasureStep(
                        saveMenu.m_SaveExit != null ? saveMenu.m_SaveExit.transform : null,
                        saveMenu.m_JustExit.transform);
                    InsertVerticallyBelow(
                        parent,
                        saveMenu.m_JustExit.transform,
                        saveMenu.m_Back.transform,
                        new Transform[] { quickSaveBtn.transform, quickLoadBtn.transform },
                        hintStep,
                        ref _saveLayoutShifted);
                }
            }
            else
            {
                quickSaveBtn = existingSave.GetComponent<Button>();
                quickLoadBtn = existingLoad.GetComponent<Button>();
            }

            if (quickSaveBtn == null || quickLoadBtn == null) return;

            string reason;
            quickSaveBtn.gameObject.SetActive(true);
            SetButtonText(quickSaveBtn.gameObject, GetQuickSaveMenuLabel());
            quickSaveBtn.interactable = QuickReloadController.CanQuickSave(out reason);

            quickLoadBtn.gameObject.SetActive(true);
            SetButtonText(quickLoadBtn.gameObject, GetQuickLoadMenuLabel());
            quickLoadBtn.interactable = QuickReloadController.CanQuickReload(out reason);

            List<Transform> navOrder = new List<Transform>();
            if (saveMenu.m_SaveExit != null && saveMenu.m_SaveExit.gameObject.activeSelf)
                navOrder.Add(saveMenu.m_SaveExit.transform);
            navOrder.Add(saveMenu.m_JustExit.transform);
            navOrder.Add(quickSaveBtn.transform);
            navOrder.Add(quickLoadBtn.transform);
            navOrder.Add(saveMenu.m_Back.transform);

            for (int i = 0; i < navOrder.Count - 1; i++)
            {
                uiItemIcon.SetNavigatePairUpDown(navOrder[i], navOrder[i + 1]);
            }

            HideMainOptionsBehindSaveMenu(saveMenu);
            RebuildLayoutHierarchy(parent, saveMenu.transform);
            saveMenu.UpdateSelectables();
        }

        public static void OnOptionsSaveClosed(uiOptionsSave saveMenu)
        {
            bool keepShowing = uiOptionsMenu.Instance != null && uiOptionsMenu.Instance.m_Showing;
            RestoreMainOptionsVisibility(keepShowing);

            if (keepShowing && uiOptionsMenu.Instance.m_MainOptions != null)
            {
                Transform start = uiOptionsMenu.Instance.m_SaveGameButton != null
                    ? uiOptionsMenu.Instance.m_SaveGameButton.transform.parent
                    : uiOptionsMenu.Instance.m_MainOptions.transform;
                RebuildLayoutHierarchy(start, uiOptionsMenu.Instance.m_MainOptions.transform);
            }
        }

        public static void OnOptionsMainFocused(uiOptionsMain mainOptions)
        {
            RestoreMainOptionsVisibility(true);
            if (mainOptions != null)
            {
                Transform start = uiOptionsMenu.Instance != null && uiOptionsMenu.Instance.m_SaveGameButton != null
                    ? uiOptionsMenu.Instance.m_SaveGameButton.transform.parent
                    : mainOptions.transform;
                RebuildLayoutHierarchy(start, mainOptions.transform);
            }
        }

        private static void HideMainOptionsBehindSaveMenu(uiOptionsSave saveMenu)
        {
            RestoreMainOptionsVisibility(false);

            uiOptionsMenu optMenu = uiOptionsMenu.Instance;
            if (optMenu == null || optMenu.m_MainOptions == null) return;

            uiOptionsMain mainOpt = optMenu.m_MainOptions;
            if (saveMenu != null && saveMenu.transform.IsChildOf(mainOpt.transform))
                return;

            bool blockerInside =
                (mainOpt.m_Blocker != null && mainOpt.m_Blocker.transform.IsChildOf(mainOpt.transform)) ||
                (mainOpt.m_SubBlocker != null && mainOpt.m_SubBlocker.transform.IsChildOf(mainOpt.transform));

            if (blockerInside)
            {
                for (int i = 0; i < mainOpt.transform.childCount; i++)
                {
                    Transform child = mainOpt.transform.GetChild(i);
                    if (!child.gameObject.activeSelf) continue;

                    bool holdsBlocker =
                        (mainOpt.m_Blocker != null && (mainOpt.m_Blocker.transform == child || mainOpt.m_Blocker.transform.IsChildOf(child))) ||
                        (mainOpt.m_SubBlocker != null && (mainOpt.m_SubBlocker.transform == child || mainOpt.m_SubBlocker.transform.IsChildOf(child)));

                    if (!holdsBlocker)
                    {
                        _hiddenMainOptionObjects.Add(child.gameObject);
                        child.gameObject.SetActive(false);
                    }
                }
            }
            else if (mainOpt.gameObject.activeSelf)
            {
                _hiddenMainRootObject = mainOpt.gameObject;
                _hiddenMainOptionObjects.Add(mainOpt.gameObject);
                mainOpt.gameObject.SetActive(false);
            }
        }

        private static void RestoreMainOptionsVisibility(bool restoreMainRoot)
        {
            for (int i = 0; i < _hiddenMainOptionObjects.Count; i++)
            {
                GameObject go = _hiddenMainOptionObjects[i];
                if (go == null) continue;
                if (!restoreMainRoot && go == _hiddenMainRootObject) continue;
                if (!go.activeSelf)
                {
                    go.SetActive(true);
                }
            }
            _hiddenMainOptionObjects.Clear();
            _hiddenMainRootObject = null;
        }

        private static void RebuildLayoutHierarchy(Transform start, Transform stopAt)
        {
            if (start == null) return;
            Canvas.ForceUpdateCanvases();

            Transform cur = start;
            int safety = 0;
            while (cur != null && safety < 10)
            {
                RectTransform rt = cur as RectTransform;
                if (rt != null)
                {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
                }
                if (cur == stopAt) break;
                cur = cur.parent;
                safety++;
            }

            Canvas.ForceUpdateCanvases();
        }

        private static Button CloneNativeButton(
            Button template,
            Transform parent,
            string name,
            string label,
            bool keepFtkButton,
            UnityEngine.Events.UnityAction onClick)
        {
            if (template == null || parent == null) return null;

            GameObject go = UnityEngine.Object.Instantiate(template.gameObject, parent, false);
            go.name = name;
            go.transform.localScale = Vector3.one;
            go.transform.localRotation = Quaternion.identity;

            FTKLocalizationUI[] locComponents = go.GetComponentsInChildren<FTKLocalizationUI>(true);
            for (int i = 0; i < locComponents.Length; i++)
            {
                UnityEngine.Object.DestroyImmediate(locComponents[i]);
            }

            MonoBehaviour[] behaviours = go.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour mb = behaviours[i];
                if (mb == null) continue;
                string typeName = mb.GetType().Name;
                if (typeName.IndexOf("ToolTip", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    UnityEngine.Object.DestroyImmediate(mb);
                }
            }

            if (!keepFtkButton)
            {
                uiFTKButton[] ftkButtons = go.GetComponentsInChildren<uiFTKButton>(true);
                for (int i = 0; i < ftkButtons.Length; i++)
                {
                    UnityEngine.Object.DestroyImmediate(ftkButtons[i]);
                }

                FTKSelectable[] selectables = go.GetComponentsInChildren<FTKSelectable>(true);
                for (int i = 0; i < selectables.Length; i++)
                {
                    UnityEngine.Object.DestroyImmediate(selectables[i]);
                }
            }

            Button btn = go.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick = new Button.ButtonClickedEvent();
                if (onClick != null)
                {
                    btn.onClick.AddListener(onClick);
                }
            }

            SetButtonText(go, label);
            return btn;
        }

        private static void SetButtonText(GameObject go, string label)
        {
            if (go == null) return;
            Text text = go.GetComponentInChildren<Text>(true);
            if (text != null)
            {
                text.text = label;
            }
        }

        private static float? MeasureStep(Transform upper, Transform lower)
        {
            RectTransform uRt = upper as RectTransform;
            RectTransform lRt = lower as RectTransform;
            if (uRt == null || lRt == null) return null;
            float diff = uRt.anchoredPosition.y - lRt.anchoredPosition.y;
            if (diff > 20f && diff < 120f) return diff;
            return null;
        }

        private static void InsertVerticallyBelow(
            Transform parent,
            Transform anchorAbove,
            Transform anchorBelow,
            Transform[] insertedList,
            float? hintStep,
            ref bool shiftedOnce)
        {
            RectTransform aboveRt = anchorAbove as RectTransform;
            RectTransform belowRt = anchorBelow as RectTransform;
            if (aboveRt == null || insertedList == null || insertedList.Length == 0) return;

            int count = insertedList.Length;
            float step = hintStep.HasValue ? hintStep.Value : (aboveRt.rect.height > 10f ? aboveRt.rect.height + 10f : 52f);
            float belowGap = belowRt != null ? (aboveRt.anchoredPosition.y - belowRt.anchoredPosition.y) : 0f;
            if (!hintStep.HasValue && belowGap > 20f && belowGap < 90f)
            {
                step = belowGap;
            }

            if (!shiftedOnce)
            {
                shiftedOnce = true;
                float neededGap = step * (count + 0.75f);
                if (belowRt == null || belowGap < neededGap)
                {
                    float shiftAmount = step * count;
                    float thresholdY = aboveRt.anchoredPosition.y - 5f;
                    for (int i = 0; i < parent.childCount; i++)
                    {
                        Transform child = parent.GetChild(i);
                        if (child == anchorAbove) continue;
                        bool isInserted = false;
                        for (int k = 0; k < count; k++)
                        {
                            if (child == insertedList[k]) { isInserted = true; break; }
                        }
                        if (isInserted) continue;

                        RectTransform childRt = child as RectTransform;
                        if (childRt == null) continue;
                        if (childRt.anchoredPosition.y < thresholdY)
                        {
                            Vector2 p = childRt.anchoredPosition;
                            childRt.anchoredPosition = new Vector2(p.x, p.y - shiftAmount);
                        }
                    }
                }
            }

            Vector2 abovePos = aboveRt.anchoredPosition;
            int baseSibling = anchorAbove.GetSiblingIndex();
            for (int k = 0; k < count; k++)
            {
                Transform tr = insertedList[k];
                if (tr == null) continue;
                RectTransform insRt = tr as RectTransform;
                if (insRt != null)
                {
                    insRt.anchoredPosition = new Vector2(abovePos.x, abovePos.y - step * (k + 1));
                }
                tr.SetSiblingIndex(baseSibling + 1 + k);
            }
        }
    }
}

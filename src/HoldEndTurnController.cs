using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FtkQuickSL
{
    public static class HoldEndTurnController
    {
        private const float ProgressRevealDelay = 0.12f;

        private static bool _holding;
        private static bool _triggeredThisPress;
        private static float _holdElapsed;
        private static Vector2 _pressPos;

        public static void Tick()
        {
            try
            {
                if (FtkQuickSLPlugin.Instance == null || !FtkQuickSLPlugin.Instance.EnableHoldEndTurn.Value)
                {
                    CancelHold();
                    return;
                }

                int btn = Mathf.Clamp(FtkQuickSLPlugin.Instance.HoldMouseButton.Value, 0, 2);
                float required = Mathf.Max(0.15f, FtkQuickSLPlugin.Instance.HoldDurationSeconds.Value);
                float maxDrag = Mathf.Max(2f, FtkQuickSLPlugin.Instance.MaxDragPixels.Value);

                if (_holding)
                {
                    if (!Input.GetMouseButton(btn) || _triggeredThisPress)
                    {
                        CancelHold();
                        return;
                    }

                    if (!CanHoldEndTurnNow() || IsPointerOverUi())
                    {
                        CancelHold();
                        return;
                    }

                    Vector2 curPos = Input.mousePosition;
                    if (Vector2.Distance(curPos, _pressPos) > maxDrag)
                    {
                        CancelHold();
                        return;
                    }

                    _holdElapsed += Time.unscaledDeltaTime;

                    if (FtkQuickSLPlugin.Instance.ShowHoldProgress.Value && _holdElapsed >= ProgressRevealDelay)
                    {
                        float fade = Mathf.Clamp01((_holdElapsed - ProgressRevealDelay) / 0.10f);
                        float progress = Mathf.Clamp01(_holdElapsed / required);
                        QuickSlOverlay.ShowHoldProgress(curPos, progress, fade);
                    }

                    if (_holdElapsed >= required)
                    {
                        _triggeredThisPress = true;
                        _holding = false;
                        QuickSlOverlay.HideHoldProgress();
                        ExecuteEndTurn();
                    }
                    return;
                }

                if (!Input.GetMouseButton(btn))
                {
                    _triggeredThisPress = false;
                    return;
                }

                if (Input.GetMouseButtonDown(btn) && !_triggeredThisPress)
                {
                    if (CanHoldEndTurnNow() && !IsPointerOverUi())
                    {
                        _holding = true;
                        _holdElapsed = 0f;
                        _pressPos = Input.mousePosition;
                    }
                }
            }
            catch (Exception ex)
            {
                CancelHold();
                FtkQuickSLPlugin.LogSource.LogDebug("HoldEndTurnController skipped: " + ex.Message);
            }
        }

        private static bool CanHoldEndTurnNow()
        {
            if (QuickReloadController.IsBusy) return false;
            if (uiStartGame.Instance == null || !uiStartGame.Instance.m_GameStarted) return false;
            if (FTKHub.Instance == null) return false;
            if (uiOptionsMenu.Instance != null && uiOptionsMenu.Instance.m_Showing) return false;
            if (uiSystemDialog.Instance != null && uiSystemDialog.Instance.gameObject.activeInHierarchy) return false;
            if (uiChatBox.Instance != null && uiChatBox.Instance.IsTextInputInFocus()) return false;
            if (EncounterSession.Instance != null && EncounterSession.Instance.m_IsInCombat) return false;
            if (FTKUI.Instance != null &&
                FTKUI.Instance.m_EncounterMenu != null &&
                FTKUI.Instance.m_EncounterMenu.m_MainPanel != null &&
                FTKUI.Instance.m_EncounterMenu.m_MainPanel.activeInHierarchy)
                return false;
            if (OverworldCamera.Instance == null || OverworldCamera.Instance.m_Camera == null ||
                !OverworldCamera.Instance.m_Camera.enabled)
                return false;
            if (uiEndTurnButton.Instance == null ||
                !uiEndTurnButton.Instance.gameObject.activeInHierarchy ||
                !uiEndTurnButton.Instance.interactable)
                return false;

            return true;
        }

        private static bool IsPointerOverUi()
        {
            return EventSystem.current != null && FTKUI.IsMouseOverUI();
        }

        private static void ExecuteEndTurn()
        {
            try
            {
                if (uiEndTurnButton.Instance == null || !uiEndTurnButton.Instance.interactable)
                    return;

                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.MainMenuButtonClick();
                }

                uiEndTurnButton.Instance.OnEndTurn();
                FtkQuickSLPlugin.LogSource.LogInfo("Hold-to-end-turn triggered Overworld EndTurn.");
            }
            catch (Exception ex)
            {
                FtkQuickSLPlugin.LogSource.LogError("ExecuteEndTurn failed: " + ex);
            }
        }

        public static void CancelHold()
        {
            _holding = false;
            _holdElapsed = 0f;
            QuickSlOverlay.HideHoldProgress();
        }
    }
}

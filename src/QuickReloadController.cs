using System;
using System.IO;
using HarmonyLib;
using StartGameFE;
using Steamworks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FtkQuickSL
{
    public enum ReloadStage
    {
        Idle,
        FadingOut,
        WaitingForMainScreen,
        WaitingForEnterFahrul,
        WaitingForGameStarted
    }

    public static class QuickReloadController
    {
        private const float StageTimeoutSeconds = 20f;
        private const float EnterFahrulSettleDelay = 0.08f;
        private const float GameStartedFallbackRevealSeconds = 3.0f;
        private const float AcceleratedCameraSpeed = 50f;

        public static bool BlockSaves { get; private set; }
        public static ReloadStage Stage { get; private set; }

        public static bool IsBusy
        {
            get { return Stage != ReloadStage.Idle; }
        }

        private static string _targetSavePath = string.Empty;
        private static float _stageElapsed;
        private static float _enterFahrulReadyElapsed;
        private static float _gameStartedElapsed;
        private static Animator _acceleratedAnimator;

        public static bool CanQuickSave(out string reason)
        {
            reason = string.Empty;

            if (IsBusy)
            {
                reason = NativeUiInjector.IsChineseLocale()
                    ? "正在重载存档，无法保存。"
                    : "Cannot save while reloading.";
                return false;
            }

            if (uiStartGame.Instance == null || !uiStartGame.Instance.m_GameStarted || GameLogic.Instance == null)
            {
                reason = NativeUiInjector.IsChineseLocale()
                    ? "仅可在对局进行中使用快速保存。"
                    : "Quick Save is only available during an active run.";
                return false;
            }

            if (GameLogic.Instance.IsMultiplayer())
            {
                reason = NativeUiInjector.IsChineseLocale()
                    ? "联机模式不支持快捷单人保存。"
                    : "Quick Save is not supported in online multiplayer.";
                return false;
            }

            if (!GameLogic.Instance.CanSave())
            {
                reason = NativeUiInjector.IsChineseLocale()
                    ? "当前状态不允许存档（仅限大地图非移动待机或地牢探索界面）。"
                    : "Cannot save right now (only allowed while idle on the overworld or dungeon menu).";
                return false;
            }

            return true;
        }

        public static void RequestQuickSave()
        {
            string reason;
            if (!CanQuickSave(out reason))
            {
                ShowNoticeDialog(
                    NativeUiInjector.IsChineseLocale() ? "无法快速保存" : "Quick Save Unavailable",
                    reason);
                return;
            }

            try
            {
                HoldEndTurnController.CancelHold();
                ClosePauseMenusSafely();

                Traverse.Create(GameLogic.Instance)
                    .Method("Save", new Type[] { typeof(bool), typeof(bool) }, new object[] { false, false })
                    .GetValue();

                if (FTKUI.Instance != null)
                {
                    FTKUI.Instance.ShowAutoSave();
                }

                NativeUiInjector.NotifyQuickSaveSuccess();
                FtkQuickSLPlugin.LogSource.LogInfo("Quick Save completed -> " + ResolveTargetSavePath());
            }
            catch (Exception ex)
            {
                FtkQuickSLPlugin.LogSource.LogError("RequestQuickSave failed: " + ex);
                ShowNoticeDialog(
                    NativeUiInjector.IsChineseLocale() ? "快速保存失败" : "Quick Save Failed",
                    ex.Message);
            }
        }

        public static bool CanQuickReload(out string reason)
        {
            reason = string.Empty;

            if (IsBusy)
            {
                reason = NativeUiInjector.IsChineseLocale()
                    ? "正在重载存档，请稍候..."
                    : "Already reloading save...";
                return false;
            }

            if (uiStartGame.Instance == null || !uiStartGame.Instance.m_GameStarted || GameLogic.Instance == null)
            {
                reason = NativeUiInjector.IsChineseLocale()
                    ? "仅可在对局进行中使用快捷读档。"
                    : "Quick SL is only available during an active run.";
                return false;
            }

            if (GameLogic.Instance.IsMultiplayer())
            {
                reason = NativeUiInjector.IsChineseLocale()
                    ? "联机模式不支持直接快捷读档（需房主重开房间载入）。"
                    : "Quick SL is not supported in online multiplayer.";
                return false;
            }

            string savePath = ResolveTargetSavePath();
            if (string.IsNullOrEmpty(savePath))
            {
                reason = NativeUiInjector.IsChineseLocale()
                    ? "当前对局尚未生成存档，请先点击 S 保存或结束一次回合。"
                    : "No save file exists for this run yet. Click S to save or end a turn once.";
                return false;
            }

            return true;
        }

        public static string ResolveTargetSavePath()
        {
            try
            {
                if (uiStartGame.Instance != null)
                {
                    string resumePath = uiStartGame.Instance.GetResumeFilename();
                    if (!string.IsNullOrEmpty(resumePath))
                    {
                        return File.Exists(resumePath) ? resumePath : string.Empty;
                    }

                    if (uiStartGame.Instance.m_IsResuming)
                    {
                        string loadFile = string.Empty;
                        try
                        {
                            loadFile = Traverse.Create(uiStartGame.Instance).Field("m_LoadFileName").GetValue<string>();
                        }
                        catch { }
                        if (!string.IsNullOrEmpty(loadFile) && File.Exists(loadFile))
                        {
                            return loadFile;
                        }
                    }
                }
            }
            catch { }

            return string.Empty;
        }

        public static void RequestQuickReload()
        {
            if (IsBusy) return;

            string reason;
            if (!CanQuickReload(out reason))
            {
                ShowNoticeDialog(
                    NativeUiInjector.IsChineseLocale() ? "无法快捷读档" : "Quick SL Unavailable",
                    reason);
                return;
            }

            string savePath = ResolveTargetSavePath();
            if (string.IsNullOrEmpty(savePath))
            {
                ShowNoticeDialog(
                    NativeUiInjector.IsChineseLocale() ? "无法快捷读档" : "Quick SL Unavailable",
                    NativeUiInjector.IsChineseLocale()
                        ? "未找到本局的存档文件。"
                        : "Could not find a save file for this run.");
                return;
            }

            bool confirm = FtkQuickSLPlugin.Instance != null && FtkQuickSLPlugin.Instance.ConfirmDialog.Value;
            if (confirm && uiSystemDialog.Instance != null)
            {
                string title = NativeUiInjector.IsChineseLocale() ? "重载上一存档" : "Reload Last Save";
                string msg = NativeUiInjector.IsChineseLocale()
                    ? "确定放弃当前未保存进度，直接回到上一个存档节点吗？"
                    : "Discard unsaved progress and reload the last save point?";
                string warn = Path.GetFileName(savePath);
                string yesText = FTKHub.Localized<Google2u.TextMenu>("STR_buttonYes");
                string noText = FTKHub.Localized<Google2u.TextMenu>("STR_buttonNo");

                if (string.IsNullOrEmpty(yesText)) yesText = NativeUiInjector.IsChineseLocale() ? "确定" : "Yes";
                if (string.IsNullOrEmpty(noText)) noText = NativeUiInjector.IsChineseLocale() ? "取消" : "No";

                uiSystemDialog.Instance.Show(
                    title,
                    msg,
                    warn,
                    yesText,
                    noText,
                    new ContinueFSM((Action)(delegate { ExecuteCleanReload(savePath); }), ContinueFSM.WaitClients.Self),
                    new ContinueFSM((Action)(delegate { }), ContinueFSM.WaitClients.Self),
                    true,
                    null);
                return;
            }

            ExecuteCleanReload(savePath);
        }

        private static void ShowNoticeDialog(string title, string message)
        {
            FtkQuickSLPlugin.LogSource.LogWarning(title + ": " + message);
            if (uiSystemDialog.Instance != null)
            {
                string okText = FTKHub.Localized<Google2u.TextMenu>("STR_buttonYes");
                if (string.IsNullOrEmpty(okText))
                {
                    okText = NativeUiInjector.IsChineseLocale() ? "确定" : "OK";
                }
                uiSystemDialog.Instance.Show(
                    title,
                    message,
                    string.Empty,
                    okText,
                    new ContinueFSM((Action)(delegate { }), ContinueFSM.WaitClients.Self),
                    null);
            }
        }

        private static void ExecuteCleanReload(string savePath)
        {
            try
            {
                HoldEndTurnController.CancelHold();
                BlockSaves = true;
                _targetSavePath = savePath;
                _enterFahrulReadyElapsed = 0f;
                _gameStartedElapsed = 0f;
                SetStage(ReloadStage.FadingOut);

                FtkQuickSLPlugin.LogSource.LogInfo("Starting Quick SL reload -> " + savePath);

                ClosePauseMenusSafely();

                bool useOverlay = FtkQuickSLPlugin.Instance == null || FtkQuickSLPlugin.Instance.SeamlessReloadOverlay.Value;
                if (useOverlay)
                {
                    string status = NativeUiInjector.IsChineseLocale() ? "正在重载存档..." : "Reloading save...";
                    QuickSlOverlay.FadeInAndRun(0.12f, status, new Action(FinishUnloadAndLoadMainScene));
                }
                else if (FTKUI.Instance != null)
                {
                    FTKUI.ScreenFadeInfo[] fade = new FTKUI.ScreenFadeInfo[]
                    {
                        new FTKUI.ScreenFadeInfo(
                            0f,
                            1f,
                            0.15f,
                            new ContinueFSM(new Action(FinishUnloadAndLoadMainScene), ContinueFSM.WaitClients.Self))
                    };
                    FTKUI.Instance.ScreenFadeRealtime(Color.black, fade, false);
                }
                else
                {
                    FinishUnloadAndLoadMainScene();
                }
            }
            catch (Exception ex)
            {
                FtkQuickSLPlugin.LogSource.LogError("ExecuteCleanReload failed: " + ex);
                ResetState(true);
            }
        }

        private static void ClosePauseMenusSafely()
        {
            try
            {
                if (uiOptionsMenu.Instance != null)
                {
                    if (uiOptionsMenu.Instance.m_SaveOptions != null)
                        uiOptionsMenu.Instance.m_SaveOptions.gameObject.SetActive(false);
                    if (uiOptionsMenu.Instance.m_MainOptions != null)
                        uiOptionsMenu.Instance.m_MainOptions.gameObject.SetActive(false);
                    uiOptionsMenu.Instance.PauseGame(false);
                }
            }
            catch { }

            Time.timeScale = 1f;
        }

        private static void FinishUnloadAndLoadMainScene()
        {
            try
            {
                if (!string.IsNullOrEmpty(_targetSavePath))
                {
                    PlayerPrefs.SetString("LastSave", _targetSavePath);
                    PlayerPrefs.Save();
                }

                GameLogic.gAction = string.Empty;

                if (FTKGameStats.Inst != null)
                {
                    try { FTKGameStats.Inst.SendEndSession(null); } catch { }
                }

                if (uiStartGame.Instance != null && uiStartGame.Instance.m_SteamLobbyID != 0UL)
                {
                    try
                    {
                        SteamMatchmaking.LeaveLobby(new CSteamID(uiStartGame.Instance.m_SteamLobbyID));
                    }
                    catch { }
                }

                ClosePauseMenusSafely();

                if (AudioManager.Instance != null)
                {
                    try { AudioManager.Instance.AudioEvent("Mute_All_Exploration_Audio", null); } catch { }
                    try { AudioManager.Instance.StopAllSound(); } catch { }
                }

                if (FTKHub.Instance != null)
                {
                    try { FTKHub.Instance.ReleaseAllPhotonView(); } catch { }
                }

                try { PhotonNetwork.Disconnect(); } catch { }

                QuickSlOverlay.SetStatus(NativeUiInjector.IsChineseLocale() ? "正在读取存档..." : "Loading save data...");
                SetStage(ReloadStage.WaitingForMainScreen);
                SceneManager.LoadScene("FTK_main");
            }
            catch (Exception ex)
            {
                FtkQuickSLPlugin.LogSource.LogError("FinishUnloadAndLoadMainScene failed: " + ex);
                ResetState(true);
            }
        }

        public static void OnMainScreenFocus(MainScreen mainScreen)
        {
            if (Stage != ReloadStage.WaitingForMainScreen || mainScreen == null)
                return;

            if (FtkQuickSLPlugin.Instance != null)
            {
                FtkQuickSLPlugin.Instance.ScheduleMainScreenResume(mainScreen);
            }
            else
            {
                TriggerMainScreenResume(mainScreen);
            }
        }

        public static void TriggerMainScreenResume(MainScreen mainScreen)
        {
            if (Stage != ReloadStage.WaitingForMainScreen)
                return;

            try
            {
                if (mainScreen == null || string.IsNullOrEmpty(_targetSavePath) || !File.Exists(_targetSavePath))
                {
                    FtkQuickSLPlugin.LogSource.LogWarning("Target save missing during MainScreen resume: " + _targetSavePath);
                    ResetState(true);
                    return;
                }

                PlayerPrefs.SetString("LastSave", _targetSavePath);
                PlayerPrefs.Save();

                FtkQuickSLPlugin.LogSource.LogInfo("Invoking MainScreen.OnResume for: " + _targetSavePath);
                QuickSlOverlay.SetStatus(NativeUiInjector.IsChineseLocale() ? "正在恢复地图..." : "Restoring world...");
                SetStage(ReloadStage.WaitingForEnterFahrul);
                _enterFahrulReadyElapsed = 0f;
                mainScreen.OnResume();
            }
            catch (Exception ex)
            {
                FtkQuickSLPlugin.LogSource.LogError("TriggerMainScreenResume failed: " + ex);
                ResetState(true);
            }
        }

        public static void Tick()
        {
            if (Stage == ReloadStage.Idle)
                return;

            _stageElapsed += Time.unscaledDeltaTime;
            if (_stageElapsed > StageTimeoutSeconds)
            {
                FtkQuickSLPlugin.LogSource.LogWarning("Quick SL timed out at stage " + Stage + "; resetting lock.");
                ResetState(true);
                return;
            }

            if (Stage == ReloadStage.WaitingForEnterFahrul)
            {
                AccelerateCameraIfEnabled();
                TickWaitingForEnterFahrul();
            }
            else if (Stage == ReloadStage.WaitingForGameStarted)
            {
                if (uiStartGame.Instance != null)
                {
                    if (uiStartGame.Instance.m_FahrulEntered)
                    {
                        FtkQuickSLPlugin.LogSource.LogInfo("Quick SL completed (m_FahrulEntered); revealing game.");
                        QuickSlOverlay.FadeOut(0.25f);
                        ResetState(false);
                        return;
                    }

                    if (uiStartGame.Instance.m_GameStarted)
                    {
                        _gameStartedElapsed += Time.unscaledDeltaTime;
                        if (_gameStartedElapsed >= GameStartedFallbackRevealSeconds)
                        {
                            FtkQuickSLPlugin.LogSource.LogInfo("Quick SL completed (fallback timer); revealing game.");
                            QuickSlOverlay.FadeOut(0.25f);
                            ResetState(false);
                        }
                    }
                }
            }
        }

        private static void AccelerateCameraIfEnabled()
        {
            bool accelerate = FtkQuickSLPlugin.Instance == null || FtkQuickSLPlugin.Instance.AccelerateReloadCamera.Value;
            if (!accelerate) return;

            try
            {
                if (SelectScreenCamera.Instance != null)
                {
                    Animator anim = SelectScreenCamera.Instance.GetComponent<Animator>();
                    if (anim != null)
                    {
                        _acceleratedAnimator = anim;
                        if (anim.speed < AcceleratedCameraSpeed)
                        {
                            anim.speed = AcceleratedCameraSpeed;
                        }
                    }
                }
            }
            catch { }
        }

        private static void RestoreCameraSpeed()
        {
            try
            {
                if (_acceleratedAnimator != null)
                {
                    _acceleratedAnimator.speed = 1f;
                }
            }
            catch { }
            _acceleratedAnimator = null;
        }

        private static void TickWaitingForEnterFahrul()
        {
            try
            {
                if (uiStartGame.Instance == null) return;
                if (uiStartGame.Instance.m_GameStarted)
                {
                    RestoreCameraSpeed();
                    SetStage(ReloadStage.WaitingForGameStarted);
                    _gameStartedElapsed = 0f;
                    return;
                }

                bool mapReady = false;
                try
                {
                    mapReady = Traverse.Create(uiStartGame.Instance).Field("m_MapReady").GetValue<bool>();
                }
                catch { }

                if (!mapReady) return;

                if (GameLogic.Instance == null) return;

                if (GameLogic.Instance.IsLocalMultiplayer())
                {
                    FtkQuickSLPlugin.LogSource.LogInfo("Local multiplayer save loaded; revealing slot selection.");
                    ResetState(true);
                    return;
                }

                if (!GameLogic.Instance.IsSinglePlayer())
                {
                    ResetState(true);
                    return;
                }

                bool autoEnter = FtkQuickSLPlugin.Instance == null || FtkQuickSLPlugin.Instance.AutoEnterFahrul.Value;
                if (!autoEnter)
                {
                    ResetState(true);
                    return;
                }

                if (uiStartGame.Instance.m_CreateUIs == null ||
                    uiStartGame.Instance.m_CreateUIs.Count == 0 ||
                    uiStartGame.Instance.m_CreateUIs.Count < uiStartGame.Instance.m_ActualMaxCharCount ||
                    !uiStartGame.Instance.GetAllPlayersReady())
                {
                    return;
                }

                _enterFahrulReadyElapsed += Time.unscaledDeltaTime;
                if (_enterFahrulReadyElapsed < EnterFahrulSettleDelay)
                    return;

                RestoreCameraSpeed();
                QuickSlOverlay.SetStatus(NativeUiInjector.IsChineseLocale() ? "正在进入法汝..." : "Entering Fahrul...");
                FtkQuickSLPlugin.LogSource.LogInfo("Map and player UIs ready; calling EnterFahrul().");
                SetStage(ReloadStage.WaitingForGameStarted);
                _gameStartedElapsed = 0f;
                uiStartGame.Instance.EnterFahrul();
            }
            catch (Exception ex)
            {
                FtkQuickSLPlugin.LogSource.LogError("TickWaitingForEnterFahrul failed: " + ex);
                ResetState(true);
            }
        }

        private static void SetStage(ReloadStage next)
        {
            Stage = next;
            _stageElapsed = 0f;
        }

        public static void ResetState(bool fadeOutOverlay)
        {
            RestoreCameraSpeed();
            BlockSaves = false;
            Stage = ReloadStage.Idle;
            _targetSavePath = string.Empty;
            _stageElapsed = 0f;
            _enterFahrulReadyElapsed = 0f;
            _gameStartedElapsed = 0f;
            if (fadeOutOverlay)
            {
                QuickSlOverlay.FadeOut(0.20f);
            }
        }
    }
}

using System;
using System.Collections;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using StartGameFE;
using UnityEngine;

namespace FtkQuickSL
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class FtkQuickSLPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.ftk.quicksl";
        public const string PluginName = "FTK Quick SL";
        public const string PluginVersion = "1.1.3";

        public static FtkQuickSLPlugin Instance { get; private set; }
        public static ManualLogSource LogSource { get; private set; }

        public ConfigEntry<bool> ShowHudButton { get; private set; }
        public ConfigEntry<bool> ConfirmDialog { get; private set; }
        public ConfigEntry<bool> AutoEnterFahrul { get; private set; }
        public ConfigEntry<bool> SeamlessReloadOverlay { get; private set; }
        public ConfigEntry<bool> AccelerateReloadCamera { get; private set; }
        public ConfigEntry<string> SaveButtonLabel { get; private set; }
        public ConfigEntry<string> LoadButtonLabel { get; private set; }

        public ConfigEntry<KeyCode> ReloadKey { get; private set; }
        public ConfigEntry<KeyCode> SaveKey { get; private set; }

        public ConfigEntry<bool> HudUseCustomPosition { get; private set; }
        public ConfigEntry<float> HudOffsetX { get; private set; }
        public ConfigEntry<float> HudOffsetY { get; private set; }

        public ConfigEntry<bool> EnableHoldEndTurn { get; private set; }
        public ConfigEntry<int> HoldMouseButton { get; private set; }
        public ConfigEntry<float> HoldDurationSeconds { get; private set; }
        public ConfigEntry<float> MaxDragPixels { get; private set; }
        public ConfigEntry<bool> ShowHoldProgress { get; private set; }

        private Harmony _harmony;

        private void Awake()
        {
            Instance = this;
            LogSource = Logger;

            ShowHudButton = Config.Bind("General", "ShowHudButton", true,
                "Show native-style S / L quick buttons on the top HUD bar during gameplay.");
            ConfirmDialog = Config.Bind("General", "ConfirmDialog", false,
                "Show a confirmation dialog before reloading the last autosave (default false = one-click SL).");
            AutoEnterFahrul = Config.Bind("General", "AutoEnterFahrul", true,
                "Automatically enter Fahrul after loading a single-player save so one click returns straight to gameplay.");
            SeamlessReloadOverlay = Config.Bind("General", "SeamlessReloadOverlay", true,
                "Keep a seamless black loading overlay across scene reload to hide the main menu flash.");
            AccelerateReloadCamera = Config.Bind("General", "AccelerateReloadCamera", true,
                "Speed up the character-select camera pan animation during Quick SL reload.");
            SaveButtonLabel = Config.Bind("General", "SaveButtonLabel", "S",
                "Text displayed on the quick-save HUD button.");
            LoadButtonLabel = Config.Bind("General", "LoadButtonLabel", "L",
                "Text displayed on the quick-load HUD button.");

            ReloadKey = Config.Bind("Hotkey", "ReloadKey", KeyCode.F10,
                "Keyboard hotkey to trigger Quick Load (L) during gameplay. Set to None to disable.");
            SaveKey = Config.Bind("Hotkey", "SaveKey", KeyCode.F11,
                "Keyboard hotkey to trigger Quick Save (S) during gameplay. Set to None to disable.");

            HudUseCustomPosition = Config.Bind("HudPosition", "HudUseCustomPosition", false,
                "When false, automatically docks [S][L] into the top black bar between Day and Lore. Automatically becomes true after right-click dragging.");
            HudOffsetX = Config.Bind("HudPosition", "HudOffsetX", -150f,
                "Custom horizontal offset from the top-right corner of the main HUD canvas.");
            HudOffsetY = Config.Bind("HudPosition", "HudOffsetY", -4f,
                "Custom vertical offset from the top-right corner of the main HUD canvas.");

            EnableHoldEndTurn = Config.Bind("EndTurnHold", "EnableHoldEndTurn", true,
                "Hold the configured mouse button on empty overworld area to end turn.");
            HoldMouseButton = Config.Bind("EndTurnHold", "HoldMouseButton", 0,
                "Mouse button index for hold-to-end-turn (0 = Left, 1 = Right, 2 = Middle).");
            HoldDurationSeconds = Config.Bind("EndTurnHold", "HoldDurationSeconds", 0.45f,
                "Seconds the mouse button must remain stationary and held to end turn.");
            MaxDragPixels = Config.Bind("EndTurnHold", "MaxDragPixels", 14f,
                "Maximum pointer movement in pixels before hold-to-end-turn cancels.");
            ShowHoldProgress = Config.Bind("EndTurnHold", "ShowHoldProgress", true,
                "Show a compact progress bar under the cursor while holding to end turn.");

            _harmony = new Harmony(PluginGuid);
            HarmonyPatches.Apply(_harmony);

            Logger.LogInfo(string.Format("{0} v{1} loaded. SaveKey={2}, LoadKey={3}, HoldEndTurn={4} (btn={5}, {6:0.00}s)",
                PluginName, PluginVersion, SaveKey.Value, ReloadKey.Value,
                EnableHoldEndTurn.Value, HoldMouseButton.Value, HoldDurationSeconds.Value));
        }

        private void OnDestroy()
        {
            try
            {
                HoldEndTurnController.CancelHold();
                NativeUiInjector.CleanupHudButton();
                QuickSlOverlay.Cleanup();
            }
            catch { }

            try
            {
                if (_harmony != null)
                {
                    _harmony.UnpatchSelf();
                }
            }
            catch { }
        }

        private void Update()
        {
            QuickSlOverlay.Tick();
            QuickReloadController.Tick();
            NativeUiInjector.TickHudButton();
            HoldEndTurnController.Tick();
            HandleHotkeys();
        }

        private void HandleHotkeys()
        {
            try
            {
                if (uiChatBox.Instance != null && uiChatBox.Instance.IsTextInputInFocus())
                    return;

                if (uiStartGame.Instance == null || !uiStartGame.Instance.m_GameStarted)
                    return;

                if (QuickReloadController.IsBusy)
                    return;

                KeyCode loadKey = ReloadKey.Value;
                if (loadKey != KeyCode.None && Input.GetKeyDown(loadKey))
                {
                    QuickReloadController.RequestQuickReload();
                    return;
                }

                KeyCode saveKey = SaveKey.Value;
                if (saveKey != KeyCode.None && Input.GetKeyDown(saveKey))
                {
                    QuickReloadController.RequestQuickSave();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("HandleHotkeys failed: " + ex);
            }
        }

        public void ScheduleMainScreenResume(MainScreen mainScreen)
        {
            StartCoroutine(MainScreenResumeCoroutine(mainScreen));
        }

        private IEnumerator MainScreenResumeCoroutine(MainScreen mainScreen)
        {
            yield return null;
            QuickReloadController.TriggerMainScreenResume(mainScreen);
        }

        public void SaveHudOffset(Vector2 anchoredPos)
        {
            HudUseCustomPosition.Value = true;
            HudOffsetX.Value = anchoredPos.x;
            HudOffsetY.Value = anchoredPos.y;
            Config.Save();
        }
    }
}

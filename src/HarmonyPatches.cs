using System;
using System.Reflection;
using HarmonyLib;
using StartGameFE;

namespace FtkQuickSL
{
    public static class HarmonyPatches
    {
        public static void Apply(Harmony harmony)
        {
            // 1. Save / Delete guards during active SL reload so autosave or wipe cleanup cannot clobber the target file.
            MethodInfo glAutoSave = AccessTools.Method(typeof(GameLogic), "AutoSave", Type.EmptyTypes);
            MethodInfo glSave = AccessTools.Method(typeof(GameLogic), "Save", new Type[] { typeof(bool), typeof(bool) });
            MethodInfo glSaveGame = AccessTools.Method(typeof(GameLogic), "SaveGame", new Type[] { typeof(string), typeof(bool), typeof(bool) });
            MethodInfo glDeleteSave = AccessTools.Method(typeof(GameLogic), "DeleteSaveFile", new Type[] { typeof(string) });

            MethodInfo blockSavePrefix = AccessTools.Method(typeof(HarmonyPatches), "BlockDuringReloadPrefix");

            PatchPrefix(harmony, glAutoSave, blockSavePrefix, "GameLogic.AutoSave");
            PatchPrefix(harmony, glSave, blockSavePrefix, "GameLogic.Save");
            PatchPrefix(harmony, glSaveGame, blockSavePrefix, "GameLogic.SaveGame");
            PatchPrefix(harmony, glDeleteSave, blockSavePrefix, "GameLogic.DeleteSaveFile");

            // 2. MainScreen auto-resume hook after FTK_main finishes reloading.
            MethodInfo mainOnSetFocus = AccessTools.Method(typeof(MainScreen), "OnSetFocus", Type.EmptyTypes);
            MethodInfo mainOnSetFocusPostfix = AccessTools.Method(typeof(HarmonyPatches), "MainScreenOnSetFocusPostfix");
            PatchPostfix(harmony, mainOnSetFocus, mainOnSetFocusPostfix, "MainScreen.OnSetFocus");

            // 3. Inject native-style Quick SL buttons into ESC options menus.
            MethodInfo optMenuShow = AccessTools.Method(typeof(uiOptionsMenu), "Show", Type.EmptyTypes);
            MethodInfo optMenuShowPostfix = AccessTools.Method(typeof(HarmonyPatches), "OptionsMenuShowPostfix");
            PatchPostfix(harmony, optMenuShow, optMenuShowPostfix, "uiOptionsMenu.Show");

            MethodInfo optSaveFocus = AccessTools.Method(typeof(uiOptionsSave), "OnSetFocus", Type.EmptyTypes);
            MethodInfo optSaveFocusPostfix = AccessTools.Method(typeof(HarmonyPatches), "OptionsSaveOnSetFocusPostfix");
            PatchPostfix(harmony, optSaveFocus, optSaveFocusPostfix, "uiOptionsSave.OnSetFocus");

            MethodInfo optSaveClose = AccessTools.Method(typeof(uiOptionsSave), "OnClose", Type.EmptyTypes);
            MethodInfo optSaveClosePostfix = AccessTools.Method(typeof(HarmonyPatches), "OptionsSaveOnClosePostfix");
            PatchPostfix(harmony, optSaveClose, optSaveClosePostfix, "uiOptionsSave.OnClose");

            MethodInfo optMainFocus = AccessTools.Method(typeof(uiOptionsMain), "OnSetFocus", Type.EmptyTypes);
            MethodInfo optMainFocusPostfix = AccessTools.Method(typeof(HarmonyPatches), "OptionsMainOnSetFocusPostfix");
            PatchPostfix(harmony, optMainFocus, optMainFocusPostfix, "uiOptionsMain.OnSetFocus");
        }

        private static void PatchPrefix(Harmony harmony, MethodInfo target, MethodInfo prefix, string label)
        {
            if (target == null || prefix == null)
            {
                FtkQuickSLPlugin.LogSource.LogWarning("Harmony prefix target missing: " + label);
                return;
            }
            harmony.Patch(target, new HarmonyMethod(prefix), null);
        }

        private static void PatchPostfix(Harmony harmony, MethodInfo target, MethodInfo postfix, string label)
        {
            if (target == null || postfix == null)
            {
                FtkQuickSLPlugin.LogSource.LogWarning("Harmony postfix target missing: " + label);
                return;
            }
            harmony.Patch(target, null, new HarmonyMethod(postfix));
        }

        private static bool BlockDuringReloadPrefix()
        {
            if (QuickReloadController.BlockSaves)
            {
                FtkQuickSLPlugin.LogSource.LogInfo("Blocked Save/Delete call during Quick SL transition.");
                return false;
            }
            return true;
        }

        private static void MainScreenOnSetFocusPostfix(MainScreen __instance)
        {
            try
            {
                QuickReloadController.OnMainScreenFocus(__instance);
            }
            catch (Exception ex)
            {
                FtkQuickSLPlugin.LogSource.LogError("MainScreenOnSetFocusPostfix error: " + ex);
            }
        }

        private static void OptionsMenuShowPostfix(uiOptionsMenu __instance)
        {
            try
            {
                NativeUiInjector.InjectIntoOptionsMain(__instance);
            }
            catch (Exception ex)
            {
                FtkQuickSLPlugin.LogSource.LogError("OptionsMenuShowPostfix error: " + ex);
            }
        }

        private static void OptionsSaveOnSetFocusPostfix(uiOptionsSave __instance)
        {
            try
            {
                NativeUiInjector.InjectIntoOptionsSave(__instance);
            }
            catch (Exception ex)
            {
                FtkQuickSLPlugin.LogSource.LogError("OptionsSaveOnSetFocusPostfix error: " + ex);
            }
        }

        private static void OptionsSaveOnClosePostfix(uiOptionsSave __instance)
        {
            try
            {
                NativeUiInjector.OnOptionsSaveClosed(__instance);
            }
            catch (Exception ex)
            {
                FtkQuickSLPlugin.LogSource.LogError("OptionsSaveOnClosePostfix error: " + ex);
            }
        }

        private static void OptionsMainOnSetFocusPostfix(uiOptionsMain __instance)
        {
            try
            {
                NativeUiInjector.OnOptionsMainFocused(__instance);
            }
            catch (Exception ex)
            {
                FtkQuickSLPlugin.LogSource.LogError("OptionsMainOnSetFocusPostfix error: " + ex);
            }
        }
    }
}

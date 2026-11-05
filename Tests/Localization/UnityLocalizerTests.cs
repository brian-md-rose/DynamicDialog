using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.TestTools;

namespace DynamicDialog.Localization.Tests
{
    /// <summary>Looks lines up in a real Unity Localization string table, in Play Mode.</summary>
    public class UnityLocalizerTests
    {
        const string Folder = "Assets/DynamicDialogLocalizationTemp";
        const string Collection = "DynamicDialogTestLines";

        [UnityTest]
        public IEnumerator Localizer_ReadsTheSelectedLocalesStringTable()
        {
            // Set up on disk first: locals don't survive EnterPlayMode's domain reload.
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "DynamicDialogLocalizationTemp");

            var addressables = AddressableAssetSettingsDefaultObject.GetSettings(true);
            addressables.ActivePlayModeDataBuilderIndex = addressables.DataBuilders.FindIndex(b => b is BuildScriptFastMode);

            var settings = LocalizationEditorSettings.ActiveLocalizationSettings;
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<LocalizationSettings>();
                AssetDatabase.CreateAsset(settings, Folder + "/LocalizationSettings.asset");
                LocalizationEditorSettings.ActiveLocalizationSettings = settings;
            }

            var french = Locale.CreateLocale(new LocaleIdentifier("fr"));
            AssetDatabase.CreateAsset(french, Folder + "/French (fr).asset");
            LocalizationEditorSettings.AddLocale(french);
            // French is the only locale, so the default selectors (system language, then English) would find none and log an error.
            settings.GetStartupLocaleSelectors().Insert(0, new SpecificLocaleSelector { LocaleId = french.Identifier });
            EditorUtility.SetDirty(settings);

            var collection = LocalizationEditorSettings.CreateStringTableCollection(Collection, Folder);
            var table = (StringTable)collection.GetTable(french.Identifier);
            table.AddEntry("GREET_1", "Bonjour !");
            EditorUtility.SetDirty(table);
            EditorUtility.SetDirty(table.SharedData);
            AssetDatabase.SaveAssets();

            yield return new EnterPlayMode();

            var init = LocalizationSettings.InitializationOperation;
            while (!init.IsDone) yield return null;
            LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.GetLocale(new LocaleIdentifier("fr"));
            var tableLoad = LocalizationSettings.StringDatabase.GetTableAsync(Collection);
            while (!tableLoad.IsDone) yield return null;

            var go = new GameObject("Localizer");
            var localizer = go.AddComponent<DialogUnityLocalizer>();
            localizer.StringTable = Collection;

            Assert.AreEqual("Bonjour !", localizer.GetText("GREET_1", null));
            Assert.IsNull(localizer.GetText("NOT_TRANSLATED", null), "missing entries fall back to authored text");
            Assert.IsNull(localizer.GetClip("GREET_1", null), "no audio table set");

            localizer.StringTable = "";
            Assert.IsNull(localizer.GetText("GREET_1", null), "an empty table name turns lookups off");

            Object.Destroy(go);
            yield return new ExitPlayMode();
        }

        // Runs before and after, so a failed run can't leave settings behind that break later Play Mode tests.
        [SetUp, TearDown]
        public void CleanUp()
        {
            if (EditorApplication.isPlaying) return;
            var active = LocalizationEditorSettings.ActiveLocalizationSettings;
            if (active != null)
            {
                active.GetStartupLocaleSelectors().RemoveAll(s => s is SpecificLocaleSelector sl && sl.LocaleId.Code == "fr");
                EditorUtility.SetDirty(active);
            }
            var loc = LocalizationEditorSettings.GetLocale(new LocaleIdentifier("fr"));
            if (loc != null) LocalizationEditorSettings.RemoveLocale(loc);
            // Deleting the folder below removes the table collection along with its assets.
            if (LocalizationEditorSettings.ActiveLocalizationSettings != null &&
                AssetDatabase.GetAssetPath(LocalizationEditorSettings.ActiveLocalizationSettings).StartsWith(Folder))
                LocalizationEditorSettings.ActiveLocalizationSettings = null;
            AssetDatabase.DeleteAsset(Folder);
            AssetDatabase.SaveAssets();
        }
    }
}

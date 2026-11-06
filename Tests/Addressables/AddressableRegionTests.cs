using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.TestTools;

namespace DynamicDialog.Addressables.Tests
{
    /// <summary>Streams a region's rules through Addressables in Play Mode (using the Asset Database play mode, so no build is needed).</summary>
    public class AddressableRegionTests
    {
        const string Folder = "Assets/DynamicDialogAddressablesTemp";
        const string AssetPath = Folder + "/SwampRules.asset";

        [UnityTest]
        public IEnumerator Region_StreamsRulesInAndOut()
        {
            // Locals don't survive the domain reload in EnterPlayMode, so set up on disk first.
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "DynamicDialogAddressablesTemp");
            var db = ScriptableObject.CreateInstance<DialogRuleDatabase>();
            var rule = new RuleDefinition { name = "swampLine", who = "Nick", concept = "Talk" };
            rule.response.lines.Add(new DialogLineDefinition { text = "Swamp.", duration = 0.01f });
            db.rules.Add(rule);
            AssetDatabase.CreateAsset(db, AssetPath);

            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(AssetPath), settings.DefaultGroup);
            int fastMode = settings.DataBuilders.FindIndex(b => b is BuildScriptFastMode);
            Assert.GreaterOrEqual(fastMode, 0, "the 'Use Asset Database' play mode script exists");
            settings.ActivePlayModeDataBuilderIndex = fastMode;
            AssetDatabase.SaveAssets();

            yield return new EnterPlayMode();

            string guid = AssetDatabase.AssetPathToGUID(AssetPath);
            var director = new GameObject("Director").AddComponent<DialogDirector>();
            var nick = new GameObject("Nick").AddComponent<DialogSpeaker>();
            var regionGo = new GameObject("Swamp");
            regionGo.SetActive(false);
            var region = regionGo.AddComponent<DialogAddressableRegion>();
            region.SetDatabase(new AssetReferenceT<DialogRuleDatabase>(guid));
            region.Facts.regionName = "swamp";
            yield return null;

            Assert.IsNull(nick.Speak("Talk"), "nothing to say before the region loads");

            regionGo.SetActive(true);
            float end = Time.realtimeSinceStartup + 10f;
            while (!region.IsLoaded && Time.realtimeSinceStartup < end) yield return null;
            Assert.IsTrue(region.IsLoaded, "the database streamed in");
            Assert.IsTrue(director.IsLoaded(region.LoadedDatabase));

            var line = nick.Speak("Talk");
            Assert.AreEqual("swampLine", line?.Rule.Name);
            Assert.AreEqual("swamp", director.Recorder.Latest(nick.Who).Query.GetSymbol(WellKnownFacts.Region).Name);
            nick.StopLine();

            var loadedDb = region.LoadedDatabase;
            regionGo.SetActive(false);
            Assert.IsFalse(region.IsLoaded);
            Assert.IsFalse(director.IsLoaded(loadedDb), "leaving the region unloads its rules");
            Assert.IsNull(nick.Speak("Talk"));

            // Disabling while a load is still in flight must not leave the rules loaded.
            regionGo.SetActive(true);
            regionGo.SetActive(false);
            yield return null;
            yield return null;
            Assert.IsFalse(region.IsLoaded);
            Assert.IsNull(nick.Speak("Talk"));

            Object.Destroy(regionGo);
            Object.Destroy(nick.gameObject);
            Object.Destroy(director.gameObject);
            yield return new ExitPlayMode();

            var s = AddressableAssetSettingsDefaultObject.Settings;
            if (s != null) s.RemoveAssetEntry(AssetDatabase.AssetPathToGUID(AssetPath));
            AssetDatabase.DeleteAsset(Folder);
        }
    }
}

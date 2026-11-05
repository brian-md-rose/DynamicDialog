using UnityEditor;

namespace DynamicDialog.Editor
{
    /// <summary>Assets > Create menu entries for new dialog files, each with a worked example.</summary>
    internal static class DialogCreateMenu
    {
        internal const string ScriptTemplate =
@"# Dynamic Dialog rules. Each rule: when its conditions all hold, the speaker says one of its lines.
# The rule with the most conditions wins, so add specific rules on top of general ones.
# Statements: who, concept, if, say, select, remember, forget, then, once, cooldown, weight, bonus.

rule GreetGeneric
  concept Greet
  say ""Hey.""
  say ""Hi there.""

rule GreetFirstTime
  concept Greet
  if greeted missing
  say ""Oh! Hello. I haven't seen you before.""
  remember greeted = 1
  then any GreetReply after 0.5s

rule ReplyToGreeting
  concept GreetReply
  say ""Hello yourself.""
";

        internal const string SpreadsheetTemplate =
            "Rule,Who,Concept,If,Text,Audio,Remember,Then,Once,Notes\n" +
            "GreetGeneric,,Greet,,Hey.,,,,,One row per line; extra rows add variations\n" +
            ",,,,Hi there.,,,,,\n" +
            "GreetFirstTime,,Greet,greeted missing,Oh! Hello. I haven't seen you before.,,greeted = 1,any GreetReply after 0.5s,,\n" +
            "ReplyToGreeting,,GreetReply,,Hello yourself.,,,,,\n";

        [MenuItem("Assets/Create/Dynamic Dialog/Dialog Script (.dlg)", priority = 81)]
        static void CreateScript() => CreateFile("NewDialog.dlg", ScriptTemplate);

        [MenuItem("Assets/Create/Dynamic Dialog/Dialog Spreadsheet (.dlgcsv)", priority = 82)]
        static void CreateSpreadsheet() => CreateFile("NewDialog.dlgcsv", SpreadsheetTemplate);

        /// <summary>
        /// Writes the file into the folder selected in the Project window and selects it.
        /// (ProjectWindowUtil.CreateAssetWithContent is obsolete in Unity 6.5 and its replacement
        /// doesn't exist in older versions, so this works the same everywhere.)
        /// </summary>
        static void CreateFile(string fileName, string content)
        {
            string folder = "Assets";
            foreach (var obj in Selection.GetFiltered<UnityEngine.Object>(SelectionMode.Assets))
            {
                string p = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(p)) continue;
                folder = AssetDatabase.IsValidFolder(p) ? p : System.IO.Path.GetDirectoryName(p).Replace('\\', '/');
                break;
            }
            string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + fileName);
            System.IO.File.WriteAllText(path, content);
            AssetDatabase.ImportAsset(path);
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }
    }
}

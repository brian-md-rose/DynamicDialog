using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace DynamicDialog.Editor
{
    /// <summary>Shared work for the .dlg and spreadsheet importers: build the database asset and find audio clips.</summary>
    internal static class DialogImportUtility
    {
        static readonly string[] AudioExtensions = { "", ".wav", ".ogg", ".mp3", ".aif", ".aiff", ".flac" };

        public static DialogRuleDatabase Import(AssetImportContext ctx, DialogSource source, string audioFolder, bool searchWholeProject)
        {
            var db = ScriptableObject.CreateInstance<DialogRuleDatabase>();
            db.name = Path.GetFileNameWithoutExtension(ctx.assetPath);
            db.sourceAssetPath = ctx.assetPath;
            db.partitionKeys = new List<string>(source.PartitionKeys);

            string folder = string.IsNullOrWhiteSpace(audioFolder)
                ? Path.GetDirectoryName(ctx.assetPath)?.Replace('\\', '/')
                : audioFolder.Trim().TrimEnd('/');
            var resolver = new AudioResolver(ctx, folder, searchWholeProject);

            foreach (var rule in source.Rules)
            {
                resolver.CurrentRule = rule;
                db.rules.Add(rule.ToDefinition(resolver.Resolve));
            }

            foreach (var m in source.Messages)
            {
                if (m.Severity == MessageSeverity.Error) ctx.LogImportError(m.ToString(), db);
                else if (m.Severity == MessageSeverity.Warning) ctx.LogImportWarning(m.ToString(), db);
            }

            ctx.AddObjectToAsset("rules", db);
            ctx.SetMainObject(db);
            return db;
        }

        sealed class AudioResolver
        {
            readonly AssetImportContext ctx;
            readonly string folder;
            readonly bool searchWholeProject;
            public RuleSource CurrentRule;

            public AudioResolver(AssetImportContext ctx, string folder, bool searchWholeProject)
            {
                this.ctx = ctx;
                this.folder = folder;
                this.searchWholeProject = searchWholeProject;
            }

            /// <summary>
            /// Looks for a clip by path (relative to the audio folder, or from Assets/ or Packages/),
            /// then by file name in the audio folder, then anywhere in the project.
            /// </summary>
            public AudioClip Resolve(string reference)
            {
                string name = reference.Replace('\\', '/');
                bool rooted = name.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) || name.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase);
                string basePath = rooted || string.IsNullOrEmpty(folder) ? name : folder + "/" + name;

                foreach (var ext in AudioExtensions)
                {
                    var clip = LoadAt(basePath + ext);
                    if (clip != null) return clip;
                }

                string fileName = Path.GetFileNameWithoutExtension(name);
                var clipByName = FindByName(fileName, string.IsNullOrEmpty(folder) || !AssetDatabase.IsValidFolder(folder) ? null : new[] { folder });
                if (clipByName == null && searchWholeProject) clipByName = FindByName(fileName, null);
                if (clipByName != null) return clipByName;

                ctx.LogImportWarning($"{ctx.assetPath}({CurrentRule?.SourceLine}): warning: [{CurrentRule?.Name}] Audio '{reference}' not found " +
                    $"(looked in {(string.IsNullOrEmpty(folder) ? "the project" : folder)}{(searchWholeProject ? " and the whole project" : "")}). " +
                    "The line will show its text only. Reimport this file after adding the clip.");
                return null;
            }

            AudioClip FindByName(string fileName, string[] folders)
            {
                string[] guids = folders == null
                    ? AssetDatabase.FindAssets($"t:AudioClip {fileName}")
                    : AssetDatabase.FindAssets($"t:AudioClip {fileName}", folders);
                foreach (var guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.Equals(Path.GetFileNameWithoutExtension(path), fileName, StringComparison.OrdinalIgnoreCase))
                    {
                        var clip = LoadAt(path);
                        if (clip != null) return clip;
                    }
                }
                return null;
            }

            AudioClip LoadAt(string path)
            {
                if (!File.Exists(path)) return null;
                // Reimport this file if the clip changes, and make sure the clip is imported first.
                ctx.DependsOnArtifact(path);
                return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            }
        }
    }
}

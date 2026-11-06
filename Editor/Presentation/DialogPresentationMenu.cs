using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace DynamicDialog.Editor
{
    /// <summary>GameObject menu items that add the built-in dialog presenters to the open scene.</summary>
    static class DialogPresentationMenu
    {
        internal const string Folder = "Packages/com.dynamicdialog/Runtime/Presentation";
        internal const string BubblePrefabPath = Folder + "/Prefabs/Dialog Speech Bubble.prefab";
        internal const string BubblesPrefabPath = Folder + "/Prefabs/Dialog Speech Bubbles.prefab";
        internal const string SubtitleBarPrefabPath = Folder + "/Prefabs/Dialog Subtitle Bar.prefab";

        [MenuItem("GameObject/Dynamic Dialog/Speech Bubbles", false, 10)]
        static void AddSpeechBubbles(MenuCommand command) => Add(BubblesPrefabPath, command);

        [MenuItem("GameObject/Dynamic Dialog/Subtitle Bar", false, 11)]
        static void AddSubtitleBar(MenuCommand command) => Add(SubtitleBarPrefabPath, command);

        static void Add(string path, MenuCommand command)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogError($"Dynamic Dialog: can't find {path}. Reimport the package.");
                return;
            }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (command.context is GameObject parent) GameObjectUtility.SetParentAndAlign(go, parent);
            Undo.RegisterCreatedObjectUndo(go, "Add " + prefab.name);
            Selection.activeGameObject = go;
        }
    }

    /// <summary>
    /// Builds the package's presentation sprites and prefabs. For package maintainers, after changing the
    /// default look: the menu item only works when the package is a local checkout (a <c>file:</c> or embedded
    /// package), since an installed package's folder is read-only.
    /// </summary>
    static class DialogPresentationAssets
    {
        const string RoundedSpritePath = DialogPresentationMenu.Folder + "/Sprites/DialogRounded.png";
        const string TailSpritePath = DialogPresentationMenu.Folder + "/Sprites/DialogTail.png";
        const string RebuildMenu = "Tools/Dynamic Dialog/Package Maintenance/Rebuild Presentation Prefabs";

        [MenuItem(RebuildMenu, true)]
        static bool CanRebuild()
        {
            var info = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(DialogPresentationAssets).Assembly);
            return info != null && (info.source == UnityEditor.PackageManager.PackageSource.Local ||
                                    info.source == UnityEditor.PackageManager.PackageSource.Embedded);
        }

        [MenuItem(RebuildMenu, false, 1000)]
        internal static void BuildAll()
        {
            WriteSprite(RoundedSpritePath, RoundedRect(64, 22), new Vector4(24, 24, 24, 24));
            WriteSprite(TailSpritePath, Tail(32, 22), Vector4.zero);
            var rounded = AssetDatabase.LoadAssetAtPath<Sprite>(RoundedSpritePath);
            var tail = AssetDatabase.LoadAssetAtPath<Sprite>(TailSpritePath);

            var bubble = BuildBubble(rounded, tail);
            BuildBubbles(bubble);
            BuildSubtitleBar(rounded);
            AssetDatabase.SaveAssets();
        }

        static DialogSpeechBubble BuildBubble(Sprite rounded, Sprite tailSprite)
        {
            var root = UiObject("Dialog Speech Bubble", null);
            var rt = (RectTransform)root.transform;
            rt.sizeDelta = new Vector2(200f, 80f);
            var bg = root.AddComponent<Image>();
            bg.sprite = rounded;
            bg.type = Image.Type.Sliced;
            bg.color = Color.white;
            bg.raycastTarget = false;
            var shadow = root.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.22f);
            shadow.effectDistance = new Vector2(0f, -3f);
            root.AddComponent<CanvasGroup>();

            var tail = UiObject("Tail", root.transform);
            var tailRt = (RectTransform)tail.transform;
            tailRt.pivot = new Vector2(0.5f, 1f);
            tailRt.sizeDelta = new Vector2(26f, 18f);
            var tailImage = tail.AddComponent<Image>();
            tailImage.sprite = tailSprite;
            tailImage.color = Color.white;
            tailImage.raycastTarget = false;

            var name = Text("Name", root.transform, 17f, new Color(0.2f, 0.45f, 0.75f), FontStyles.Bold, wrap: false);
            var body = Text("Text", root.transform, 22f, new Color(0.12f, 0.12f, 0.15f), FontStyles.Normal, wrap: true);

            var bubble = root.AddComponent<DialogSpeechBubble>();
            bubble.body = body;
            bubble.nameLabel = name;
            bubble.tail = tailRt;

            var saved = PrefabUtility.SaveAsPrefabAsset(root, DialogPresentationMenu.BubblePrefabPath);
            Object.DestroyImmediate(root);
            return saved.GetComponent<DialogSpeechBubble>();
        }

        static void BuildBubbles(DialogSpeechBubble bubblePrefab)
        {
            var root = CanvasRoot("Dialog Speech Bubbles", 50);
            var bubbles = root.AddComponent<DialogSpeechBubbles>();
            bubbles.bubblePrefab = bubblePrefab;
            PrefabUtility.SaveAsPrefabAsset(root, DialogPresentationMenu.BubblesPrefabPath);
            Object.DestroyImmediate(root);
        }

        static void BuildSubtitleBar(Sprite rounded)
        {
            var root = CanvasRoot("Dialog Subtitle Bar", 60);

            var rows = UiObject("Rows", root.transform);
            var rowsRt = (RectTransform)rows.transform;
            rowsRt.anchorMin = rowsRt.anchorMax = new Vector2(0.5f, 0f);
            rowsRt.pivot = new Vector2(0.5f, 0f);
            rowsRt.anchoredPosition = new Vector2(0f, 48f);
            rowsRt.sizeDelta = new Vector2(1100f, 0f);
            var layout = rows.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.LowerCenter;
            layout.spacing = 6f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            rows.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var row = UiObject("Row Template", rows.transform);
            var rowImage = row.AddComponent<Image>();
            rowImage.sprite = rounded;
            rowImage.type = Image.Type.Sliced;
            rowImage.color = new Color(0f, 0f, 0f, 0.62f);
            rowImage.raycastTarget = false;
            var rowLayout = row.AddComponent<VerticalLayoutGroup>();
            rowLayout.padding = new RectOffset(24, 24, 10, 12);
            rowLayout.childControlWidth = rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = true;
            rowLayout.childForceExpandHeight = false;
            row.AddComponent<CanvasGroup>();
            var text = Text("Text", row.transform, 32f, Color.white, FontStyles.Normal, wrap: true);
            text.alignment = TextAlignmentOptions.Center;
            row.SetActive(false);

            var bar = root.AddComponent<DialogSubtitleBar>();
            bar.rowContainer = rowsRt;
            bar.rowTemplate = row;
            PrefabUtility.SaveAsPrefabAsset(root, DialogPresentationMenu.SubtitleBarPrefabPath);
            Object.DestroyImmediate(root);
        }

        static GameObject CanvasRoot(string name, int sortingOrder)
        {
            var root = UiObject(name, null);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return root;
        }

        static GameObject UiObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            if (parent != null) go.transform.SetParent(parent, false);
            return go;
        }

        static TextMeshProUGUI Text(string name, Transform parent, float size, Color color, FontStyles style, bool wrap)
        {
            var go = UiObject(name, parent);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.fontSize = size;
            t.color = color;
            t.fontStyle = style;
            t.alignment = TextAlignmentOptions.TopLeft;
#if UNITY_2023_2_OR_NEWER
            t.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
#else
            t.enableWordWrapping = wrap;
#endif
            t.raycastTarget = false;
            t.text = name;
            // TextMeshPro assigns its default font (LiberationSans SDF from TMP Essential Resources, which has the
            // same GUID in every project). Users pick their own font in a prefab variant.
            return t;
        }

        static void WriteSprite(string assetPath, Texture2D texture, Vector4 border)
        {
            var physical = FileUtil.GetPhysicalPath(assetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(physical));
            File.WriteAllBytes(physical, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = border;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }

        // White rounded square with anti-aliased corners, for 9-slicing.
        static Texture2D RoundedRect(int size, float radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float px = x + 0.5f, py = y + 0.5f;
                float cx = Mathf.Clamp(px, radius, size - radius), cy = Mathf.Clamp(py, radius, size - radius);
                float d = Vector2.Distance(new Vector2(px, py), new Vector2(cx, cy));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(radius - d + 0.5f)));
            }
            tex.Apply();
            return tex;
        }

        // White triangle pointing down, full width along the top row.
        static Texture2D Tail(int width, int height)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float t = (y + 0.5f) / height;          // 0 at the tip (bottom), 1 at the top
                float half = t * width * 0.5f;
                float dx = Mathf.Abs(x + 0.5f - width * 0.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(half - dx + 0.5f)));
            }
            tex.Apply();
            return tex;
        }
    }
}

using UnityEngine;

namespace DynamicDialog.Samples
{
    /// <summary>
    /// Builds the Robot Banter room at runtime: a director with the sample's rules, two wandering
    /// robots that see, remember and talk, some objects to talk about, and the subtitle and debug
    /// overlays. Everything the robots say is in RobotBanter.dlg next to this script.
    /// </summary>
    public class RobotBanterSetup : MonoBehaviour
    {
        [Tooltip("The sample's rules. In the editor, RobotBanter.dlg next to this script is used when this is empty.")]
        [SerializeField] DialogRuleDatabase rules;

        [Tooltip("Speeds the room up, for tests and the impatient.")]
        [SerializeField, Min(0.1f)] float timeScale = 1f;

        public DialogDirector Director { get; private set; }
        public DialogSpeaker Bolt { get; private set; }
        public DialogSpeaker Sprocket { get; private set; }

        void Awake()
        {
            Time.timeScale = timeScale;
            if (rules == null) rules = FindSampleRules();
            if (rules == null)
            {
                Debug.LogError("[RobotBanter] Assign the RobotBanter rule database to RobotBanterSetup.", this);
                enabled = false;
                return;
            }

            BuildRoom();

            var directorGo = new GameObject("Dialog Director");
            directorGo.SetActive(false);
            Director = directorGo.AddComponent<DialogDirector>();
            directorGo.AddComponent<DialogSubtitleOverlay>();
            directorGo.AddComponent<DialogDebugOverlay>();
            directorGo.SetActive(true);
            Director.AddDatabase(rules);

            var hazard = Prop("toxic_barrel", PrimitiveType.Cylinder, new Vector3(3.5f, 0.6f, -3f), new Vector3(0.8f, 0.6f, 0.8f), new Color(0.4f, 1f, 0.2f));
            Prop("barrel", PrimitiveType.Cylinder, new Vector3(-4f, 0.6f, 3f), new Vector3(0.8f, 0.6f, 0.8f), new Color(0.55f, 0.35f, 0.2f));
            Prop("barrel", PrimitiveType.Cylinder, new Vector3(4f, 0.6f, 4f), new Vector3(0.8f, 0.6f, 0.8f), new Color(0.55f, 0.35f, 0.2f));
            Prop("soda_can", PrimitiveType.Cylinder, new Vector3(0f, 0.2f, 0f), new Vector3(0.25f, 0.2f, 0.25f), new Color(0.9f, 0.1f, 0.15f));
            Prop("radiator", PrimitiveType.Cube, new Vector3(-5.5f, 0.5f, -4f), new Vector3(0.3f, 1f, 2f), new Color(0.8f, 0.8f, 0.85f));

            Bolt = Robot("Bolt", new Vector3(-2f, 0f, -1f), new Color(0.25f, 0.5f, 1f), hazard.transform);
            Sprocket = Robot("Sprocket", new Vector3(2f, 0f, 1f), new Color(1f, 0.55f, 0.1f), hazard.transform);
        }

        void OnDestroy()
        {
            if (Mathf.Approximately(Time.timeScale, timeScale)) Time.timeScale = 1f;
        }

        DialogSpeaker Robot(string robotName, Vector3 position, Color color, Transform hazard)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = robotName;
            go.transform.position = position + Vector3.up;
            Tint(go, color);

            var eye = GameObject.CreatePrimitive(PrimitiveType.Cube);
            eye.name = "Visor";
            Destroy(eye.GetComponent<Collider>());
            eye.transform.SetParent(go.transform, false);
            eye.transform.localPosition = new Vector3(0f, 0.45f, 0.4f);
            eye.transform.localScale = new Vector3(0.6f, 0.15f, 0.2f);
            Tint(eye, Color.black);

            go.SetActive(false); // configure everything before the speaker registers
            var speaker = go.AddComponent<DialogSpeaker>();
            go.AddComponent<NearbySpeakerFacts>();
            go.AddComponent<HazardFacts>().hazard = hazard;
            go.AddComponent<DialogVision>();
            go.AddComponent<RobotWander>();
            go.SetActive(true);
            return speaker;
        }

        static GameObject Prop(string tag, PrimitiveType shape, Vector3 position, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(shape);
            go.name = tag;
            go.transform.position = position;
            go.transform.localScale = scale;
            Tint(go, color);
            go.AddComponent<DialogTag>();
            return go;
        }

        static void BuildRoom()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.localScale = new Vector3(1.4f, 1f, 1.4f);
            Tint(floor, new Color(0.35f, 0.37f, 0.4f));

            if (Camera.main == null)
            {
                var cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
                cam.transform.position = new Vector3(0f, 11f, -11f);
                cam.transform.LookAt(Vector3.zero);
            }
            if (Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Length == 0)
            {
                var light = new GameObject("Light", typeof(Light)).GetComponent<Light>();
                light.type = LightType.Directional;
                light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }
        }

        static void Tint(GameObject go, Color color)
        {
            var r = go.GetComponent<Renderer>();
            if (r != null) r.material.color = color;
        }

        DialogRuleDatabase FindSampleRules()
        {
#if UNITY_EDITOR
            var script = UnityEditor.MonoScript.FromMonoBehaviour(this);
            string folder = System.IO.Path.GetDirectoryName(UnityEditor.AssetDatabase.GetAssetPath(script))?.Replace('\\', '/');
            return folder == null ? null : UnityEditor.AssetDatabase.LoadAssetAtPath<DialogRuleDatabase>(folder + "/RobotBanter.dlg");
#else
            return null;
#endif
        }
    }
}

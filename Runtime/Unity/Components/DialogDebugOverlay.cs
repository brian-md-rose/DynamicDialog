using UnityEngine;

namespace DynamicDialog
{
    /// <summary>
    /// <b>Debug tool</b> (IMGUI; not meant to ship). Draws a label over each speaker in the Game view: its name, the line it's saying, and its
    /// latest lookup (concept and winning rule, or "no match"). Needs lookup recording on the
    /// director. Toggle <see cref="visible"/> from your own debug key or menu.
    /// </summary>
    [AddComponentMenu("Dynamic Dialog/Debug/Debug Overlay")]
    public class DialogDebugOverlay : MonoBehaviour
    {
        /// <summary>Whether labels are drawn. Toggle it from your own debug key or menu.</summary>
        public bool visible = true;

        [Tooltip("Camera to project speaker positions with. Empty uses Camera.main.")]
        [SerializeField] Camera viewCamera;

        [Tooltip("Height above the speaker's pivot to draw the label at.")]
        [SerializeField] float heightOffset = 2.2f;

        [Tooltip("How long a lookup stays on screen.")]
        [SerializeField, Min(0.1f)] float showSeconds = 4f;

        [Tooltip("Only show lookups that found nothing (useful when hunting for missing lines).")]
        [SerializeField] bool onlyMisses;

        [SerializeField, Min(8)] int fontSize = 12;

        GUIStyle style;

        void OnGUI()
        {
            if (!visible || Event.current.type != EventType.Repaint) return;
            var director = DialogDirector.Instance;
            var cam = viewCamera != null ? viewCamera : Camera.main;
            if (director == null || director.Conductor == null || cam == null) return;

            if (style == null)
                style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, richText = true, wordWrap = false };
            style.fontSize = fontSize;

            var recorder = director.Recorder;
            double now = Time.timeAsDouble;

            foreach (var s in director.Conductor.Speakers)
            {
                if (!(s is DialogSpeaker speaker) || speaker == null) continue;
                Vector3 screen = cam.WorldToScreenPoint(speaker.transform.position + Vector3.up * heightOffset);
                if (screen.z <= 0f) continue;

                var sb = new System.Text.StringBuilder();
                sb.Append("<b>").Append(speaker.Who).Append("</b>");

                if (speaker.IsSpeaking)
                    sb.Append("\n<color=#7CFC00>speaking:</color> ").Append(speaker.CurrentLine.Rule.Name)
                      .Append(" <color=#aaaaaa>(").Append(speaker.CurrentLine.Concept).Append(")</color>");

                var record = recorder?.Latest(speaker.Who);
                bool showRecord = record != null && now - record.Time <= showSeconds && (!onlyMisses || !record.Matched);
                if (showRecord)
                {
                    string color = !record.Matched ? "#ff8080" : record.Started ? "#7CFC00" : "#ffd75e";
                    string result = !record.Matched ? "no match" : record.Winner.Name + (record.Started ? "" : " (not played)");
                    sb.Append("\n<color=#aaaaaa>").Append(record.Concept).Append(" →</color> <color=").Append(color).Append('>')
                      .Append(result).Append("</color>");
                }
                else if (onlyMisses && !speaker.IsSpeaking)
                {
                    continue;
                }

                var content = new GUIContent(sb.ToString());
                Vector2 size = style.CalcSize(content);
                var rect = new Rect(screen.x - size.x * 0.5f, Screen.height - screen.y - size.y, size.x, size.y);
                GUI.Box(rect, content, style);
            }
        }
    }
}

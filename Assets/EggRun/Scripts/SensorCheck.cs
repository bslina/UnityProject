using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

// Lists every sensor Unity can see on this phone, with live values.
// Creates itself when the game starts (no scene setup needed). Tap "Hide sensors" to close it.
public class SensorCheck : MonoBehaviour
{
    bool visible = true;
    float nextEnableTime;
    readonly StringBuilder sb = new StringBuilder();
    GUIStyle textStyle;
    GUIStyle buttonStyle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if (FindFirstObjectByType<SensorCheck>() != null) return;
        var go = new GameObject("SensorCheck");
        DontDestroyOnLoad(go);
        go.AddComponent<SensorCheck>();
    }

    void Update()
    {
        if (!visible || Time.unscaledTime < nextEnableTime) return;
        nextEnableTime = Time.unscaledTime + 1f;

        // Sensors are off by default; turn on every one so its values show up.
        foreach (var device in InputSystem.devices)
            if (device is Sensor && !device.enabled)
                InputSystem.EnableDevice(device);
    }

    void OnGUI()
    {
        if (!visible) return;

        float scale = Screen.height / 1080f;
        if (textStyle == null)
        {
            textStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(26 * scale), wordWrap = true };
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(30 * scale) };
        }

        sb.Clear();
        int count = 0;
        foreach (var device in InputSystem.devices)
        {
            if (!(device is Sensor)) continue;
            count++;
            sb.Append("<b>").Append(device.layout).Append("</b>");
            foreach (var control in device.children)
                sb.Append("  ").Append(control.name).Append(" = ").Append(Format(control.ReadValueAsObject()));
            sb.Append('\n');
        }
        if (count == 0) sb.Append("No sensors found (normal in the Editor without Unity Remote).");

        var area = new Rect(Screen.width * 0.42f, 10f * scale, Screen.width * 0.58f - 10f * scale, Screen.height * 0.7f);
        GUI.Box(area, GUIContent.none);
        GUILayout.BeginArea(area);
        textStyle.richText = true;
        GUILayout.Label($"<b>Sensors found: {count}</b>\n" + sb, textStyle);
        if (GUILayout.Button("Hide sensors", buttonStyle, GUILayout.Height(70f * scale)))
            visible = false;
        GUILayout.EndArea();
    }

    static string Format(object value)
    {
        switch (value)
        {
            case Vector3 v: return v.ToString("F2");
            case Quaternion q: return q.eulerAngles.ToString("F0");
            case float f: return f.ToString("F2");
            default: return value != null ? value.ToString() : "-";
        }
    }
}

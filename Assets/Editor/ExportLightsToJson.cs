using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;

// Редакторский скрипт: собирает все лампы внутри выделенного объекта (например, hall)
// и копирует готовый блок "lights" для JSON комнаты в буфер обмена.
// Положить в папку Assets/Editor (папку Editor создать, если её нет).
public static class ExportLightsToJson
{
    static string F(float v)
    {
        // InvariantCulture: чтобы в JSON была точка, а не запятая (на русской Windows будет запятая)
        return v.ToString("0.###", CultureInfo.InvariantCulture);
    }

    [MenuItem("Museum/Export Lights To JSON")]
    static void Export()
    {
        Transform root = Selection.activeTransform;
        if (root == null)
        {
            EditorUtility.DisplayDialog("Museum", "Выдели в Hierarchy объект hall (с лампами внутри) и запусти снова.", "OK");
            return;
        }

        Light[] lights = root.GetComponentsInChildren<Light>(true);
        List<string> entries = new List<string>();

        foreach (Light l in lights)
        {
            string type;
            switch (l.type)
            {
                case LightType.Point: type = "point"; break;
                case LightType.Spot: type = "spot"; break;
                case LightType.Directional: type = "directional"; break;
                default:
                    Debug.LogWarning("Лампа '" + l.name + "' типа " + l.type + " не поддерживается, пропущена.");
                    continue;
            }

            // Координаты относительно выделенного объекта (его Transform считается началом координат)
            Vector3 p = root.InverseTransformPoint(l.transform.position);
            Vector3 e = (Quaternion.Inverse(root.rotation) * l.transform.rotation).eulerAngles;

            string entry =
                "    {\n" +
                "      \"type\": \"" + type + "\",\n" +
                "      \"pos\": [" + F(p.x) + ", " + F(p.y) + ", " + F(p.z) + "],\n" +
                "      \"rot\": [" + F(e.x) + ", " + F(e.y) + ", " + F(e.z) + "],\n" +
                "      \"color\": \"#" + ColorUtility.ToHtmlStringRGB(l.color) + "\",\n" +
                "      \"intensity\": " + F(l.intensity) + ",\n" +
                "      \"range\": " + F(l.range) + ",\n" +
                "      \"spotAngle\": " + F(l.spotAngle) + ",\n" +
                "      \"shadows\": " + (l.shadows != LightShadows.None ? "true" : "false") + "\n" +
                "    }";
            entries.Add(entry);
        }

        string json = "  \"lights\": [\n" + string.Join(",\n", entries) + "\n  ],";
        GUIUtility.systemCopyBuffer = json;
        Debug.Log("Экспортировано ламп: " + entries.Count + ". Блок скопирован в буфер обмена, вставь его в JSON комнаты:\n" + json);
    }
}

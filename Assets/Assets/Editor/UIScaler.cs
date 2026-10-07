using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public static class UIScaler
{
    // Change this before running: 1.3 = 30% bigger, 1.5 = 50% bigger, 2.0 = double.
    private const float FACTOR = 1.35f;

    [MenuItem("Tools/Scale Up Font Sizes")]
    public static void ScaleFonts()
    {
        int count = 0;
        var allTexts = Object.FindObjectsByType<TMP_Text>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (var t in allTexts)
        {
            Undo.RecordObject(t, "Scale Font Size");
            t.fontSize = Mathf.RoundToInt(t.fontSize * FACTOR);
            EditorUtility.SetDirty(t);
            count++;
        }

        MarkScenesDirty();
        EditorUtility.DisplayDialog("UIScaler",
            $"Scaled font size on {count} text objects by {FACTOR}x.\n\nSave the scene to keep changes.", "OK");
    }

    [MenuItem("Tools/Scale Up Button Sizes")]
    public static void ScaleButtons()
    {
        int count = 0;
        var allButtons = Object.FindObjectsByType<Button>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (var b in allButtons)
        {
            var rt = b.GetComponent<RectTransform>();
            if (rt == null) continue;

            Undo.RecordObject(rt, "Scale Button Size");
            rt.sizeDelta = new Vector2(rt.sizeDelta.x * FACTOR, rt.sizeDelta.y * FACTOR);
            EditorUtility.SetDirty(rt);
            count++;
        }

        MarkScenesDirty();
        EditorUtility.DisplayDialog("UIScaler",
            $"Scaled {count} buttons by {FACTOR}x.\n\nSave the scene to keep changes.", "OK");
    }

    private static void MarkScenesDirty()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (scene.isLoaded && !string.IsNullOrEmpty(scene.path))
                EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
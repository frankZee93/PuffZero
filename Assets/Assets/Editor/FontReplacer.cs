using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using UnityEngine.SceneManagement;

public static class FontReplacer
{
    [MenuItem("Tools/Replace TMP Font in Open Scenes")]
    public static void ReplaceFont()
    {
        TMP_FontAsset newFont = Selection.activeObject as TMP_FontAsset;
        if (newFont == null)
        {
            EditorUtility.DisplayDialog(
                "Font Replacer",
                "Select a TMP Font Asset in the Project window first, then run this menu item again.",
                "OK");
            return;
        }

        int replaced = 0;
        var allTexts = Object.FindObjectsByType<TMP_Text>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (var t in allTexts)
        {
            if (t.font != newFont)
            {
                Undo.RecordObject(t, "Replace TMP Font");
                t.font = newFont;
                EditorUtility.SetDirty(t);
                replaced++;
            }
        }

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (scene.isLoaded && !string.IsNullOrEmpty(scene.path))
                EditorSceneManager.MarkSceneDirty(scene);
        }

        EditorUtility.DisplayDialog(
            "Font Replacer",
            $"Replaced font on {replaced} text objects.\n\nSave your scene(s) to keep the change.",
            "OK");
    }
}
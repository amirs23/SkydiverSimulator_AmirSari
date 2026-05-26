using UnityEngine;
using UnityEditor;
using System.IO;

// Converts all materials under Assets/Samples/Xsens to URP/Lit so they render
// correctly on Android (Quest 2). Run once via the menu below.
public static class FixXsensMaterials
{
    [MenuItem("Tools/Fix Xsens Materials for URP")]
    static void Fix()
    {
        string folder = "Assets/Samples/Xsens";
        string[] guids = AssetDatabase.FindAssets("t:Material", new[] { folder });

        if (guids.Length == 0)
        {
            Debug.LogWarning("[FixXsensMaterials] No materials found under " + folder);
            return;
        }

        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null)
        {
            Debug.LogError("[FixXsensMaterials] URP/Lit shader not found — make sure URP is installed.");
            return;
        }

        int count = 0;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) continue;

            Color albedo = mat.HasProperty("_Color")
                ? mat.GetColor("_Color")
                : Color.white;

            mat.shader = urpLit;

            // Restore the base color on the URP material
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", albedo);

            EditorUtility.SetDirty(mat);
            count++;
            Debug.Log($"[FixXsensMaterials] Fixed: {Path.GetFileName(path)}");
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[FixXsensMaterials] Done — {count} materials updated to URP/Lit.");
    }
}

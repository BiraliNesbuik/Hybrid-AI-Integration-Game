using UnityEditor;
using UnityEngine;

public static class FixModelMaterialLocation
{
    [MenuItem("Tools/Fix Imported Model Materials")]
    private static void Fix()
    {
        string[] guids = AssetDatabase.FindAssets("t:Model", new[] { "Assets/BrokenVector/LowPolyDungeon" });
        int fixedCount = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;

            if (importer == null)
            {
                continue;
            }

            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            importer.SaveAndReimport();
            fixedCount++;
        }

        Debug.Log($"Fixed material location on {fixedCount} models.");
    }
}

using UnityEditor;
using UnityEngine;

public class CreateMaterialPreset
{
    [MenuItem("Assets/Create/Materials/Glass Preset")]
    public static void CreateGlass()
    {
        CreatePreset(MaterialType.Glass, "GlassPreset");
    }

    [MenuItem("Assets/Create/Materials/Wood Preset")]
    public static void CreateWood()
    {
        CreatePreset(MaterialType.Wood, "WoodPreset");
    }

    [MenuItem("Assets/Create/Materials/Metal Preset")]
    public static void CreateMetal()
    {
        CreatePreset(MaterialType.Metal, "MetalPreset");
    }

    [MenuItem("Assets/Create/Materials/Stone Preset")]
    public static void CreateStone()
    {
        CreatePreset(MaterialType.Stone, "StonePreset");
    }

    [MenuItem("Assets/Create/Materials/Rubber Preset")]
    public static void CreateRubber()
    {
        CreatePreset(MaterialType.Rubber, "RubberPreset");
    }

    [MenuItem("Assets/Create/Materials/Plastic Preset")]
    public static void CreatePlastic()
    {
        CreatePreset(MaterialType.Plastic, "PlasticPreset");
    }

    private static void CreatePreset(MaterialType type, string defaultName)
    {
        var preset = ScriptableObject.CreateInstance<MaterialPreset>();
        preset.SetType(type);

        string path = EditorUtility.SaveFilePanelInProject("Save Material Preset", defaultName, "asset", "Choose location to save preset");
        if (!string.IsNullOrEmpty(path))
        {
            AssetDatabase.CreateAsset(preset, path);
            AssetDatabase.SaveAssets();
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = preset;
        }
    }
}

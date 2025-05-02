using UnityEngine;

// [CreateAssetMenu(fileName = "MaterialPreset", menuName = "Materials/Material Preset")]
public class MaterialPreset : ScriptableObject
{
    [SerializeField, HideInInspector]
    private MaterialType type;

    // Unity
    public MaterialType Type => type;
    public Material material;
    public float bounciness;
    public float friction;

    public void SetType(MaterialType newType)
    {
        type = newType;
    }

    public MaterialPreset(MaterialType t, float b, float f)
    {
        bounciness = b;
        friction = f;
        type = t;
    }

    public MaterialPreset(float b, float f)
    {
        type = MaterialType.Custom;
        bounciness = b;
        friction = f;
    }

    public MaterialPreset()
    {
        type = MaterialType.Custom;
        bounciness = 0.5f;
        friction = 0.0f;
    }
}
using UnityEngine;

[ExecuteAlways] // Allows OnValidate to run in edit mode
public class MaterialManager : MonoBehaviour
{
    public MaterialProperties materialProperties = new MaterialProperties();

    private void OnValidate()
    {
        SetDefaultsForMaterial(materialProperties.materialType);
    }

    public MaterialProperties GetMaterialProperties()
    {
        return materialProperties;
    }

    private void SetDefaultsForMaterial(MaterialType type)
    {
        switch (type)
        {
            case MaterialType.Wood:
                materialProperties.bounciness = 0.2f;
                materialProperties.friction = 0.7f;
                break;
            case MaterialType.Glass:
                materialProperties.bounciness = 0.3f;
                materialProperties.friction = 0.2f;
                break;
            case MaterialType.Metal:
                materialProperties.bounciness = 0.1f;
                materialProperties.friction = 0.1f;
                break;
            case MaterialType.Rubber:
                materialProperties.bounciness = 0.8f;
                materialProperties.friction = 0.4f;
                break;
            case MaterialType.Stone:
                materialProperties.bounciness = 0.0f;
                materialProperties.friction = 0.9f;
                break;
            case MaterialType.Custom:
                break;
        }
    }
}
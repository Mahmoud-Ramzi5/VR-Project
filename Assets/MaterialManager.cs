using UnityEngine;

public enum MaterialType
{
    Glass,
    Wood,
    Stone,
    Metal,
    Rubber,
    Plastic,
    Custom
}

[ExecuteAlways] // Allows OnValidate to run in edit mode
public class MaterialManager : MonoBehaviour
{
    public MaterialType materialType;
    public MaterialDatabase materialDatabase;
    private Renderer objectRenderer;

    private void Start()
    {
        // Get the Renderer component
        objectRenderer = GetComponent<Renderer>();
        ApplyMaterial(materialType);
    }
    private void OnValidate()
    {
        if (objectRenderer == null)
            objectRenderer = GetComponent<Renderer>();

        if (objectRenderer != null)
            ApplyMaterial(materialType);
    }

    private void ApplyMaterial(MaterialType type)
    {
        var preset = materialDatabase.GetPreset(type);
        Debug.LogWarning($"Preset {preset}");

        if (preset != null && objectRenderer != null)
        {
            objectRenderer.sharedMaterial = preset.material;
        }
    }

    public MaterialPreset GetMaterialProperties()
    {
        return materialDatabase.GetPreset(materialType);
    }

    /*
    private void OnValidate()
    {
        SetDefaultsForMaterial(materialProperties.materialType);

        // Only apply in editor mode when values change
        if (objectRenderer == null)
            objectRenderer = GetComponent<Renderer>();

        if (objectRenderer != null)
            ApplySelectedMaterial(materialProperties.materialType);
    }

    public MaterialProperties GetMaterialProperties()
    {
        return materialProperties;
    }

    private void SetDefaultsForMaterial(MaterialType type)
    {
        switch (type)
        {
            case MaterialType.Glass:
                materialProperties.bounciness = 0.3f;
                materialProperties.friction = 0.2f;
                break;
            case MaterialType.Wood:
                materialProperties.bounciness = 0.2f;
                materialProperties.friction = 0.7f;
                break;
            case MaterialType.Metal:
                materialProperties.bounciness = 0.1f;
                materialProperties.friction = 0.1f;
                break;
            case MaterialType.Stone:
                materialProperties.bounciness = 0.0f;
                materialProperties.friction = 0.9f;
                break;
            case MaterialType.Rubber:
                materialProperties.bounciness = 0.8f;
                materialProperties.friction = 0.4f;
                break;
            case MaterialType.Plastic:
                materialProperties.bounciness = 0.4f;
                materialProperties.friction = 0.3f;
                break;
            case MaterialType.Custom:
                break;
        }
    }

    private void ApplySelectedMaterial(MaterialType type)
    {
        switch (type)
        {
            case MaterialType.Glass:
                if (MaterialReferences.GlassMaterial != null) objectRenderer.material = MaterialReferences.GlassMaterial;
                break;
            case MaterialType.Wood:
                if (MaterialReferences.WoodMaterial != null) objectRenderer.material = MaterialReferences.WoodMaterial;
                break;
            case MaterialType.Stone:
                if (MaterialReferences.StoneMaterial != null) objectRenderer.material = MaterialReferences.StoneMaterial;
                break;
            case MaterialType.Metal:
                if (MaterialReferences.MetalMaterial != null) objectRenderer.material = MaterialReferences.MetalMaterial;
                break;
            case MaterialType.Rubber:
                if (MaterialReferences.RubberMaterial != null) objectRenderer.material = MaterialReferences.RubberMaterial;
                break;
            case MaterialType.Plastic:
                if (MaterialReferences.PlasticMaterial != null) objectRenderer.material = MaterialReferences.PlasticMaterial;
                break;
            case MaterialType.Custom:
                if (MaterialReferences.CustomMaterial != null) 
                    objectRenderer.material = MaterialReferences.CustomMaterial;
                else
                    objectRenderer.material = null;
                break;
        }
    } */
}
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
    private GameObject currentObject;

    private void Start()
    {
        currentObject = gameObject;
        ApplyMaterial(currentObject, materialType);
    }

    private void OnValidate()
    {
        ApplyMaterial(currentObject, materialType);
    }

    private void ApplyMaterial(GameObject parent, MaterialType type)
    {
        var preset = materialDatabase.GetPreset(type);
        Debug.LogWarning($"Preset {preset}");

        if (preset != null && parent != null)
        {
            // ProcessChildren
            if (parent.transform.childCount > 0)
            {
                Debug.LogWarning("Children found in " + parent.name);
                foreach (Transform child in parent.transform)
                {
                    // Process each child
                    var renderer = child.GetComponent<Renderer>();
                    if (preset != null && renderer != null)
                        renderer.sharedMaterial = preset.material;
                }
            } 
            else
            {
                Debug.LogWarning("No children found in " + parent.name);
                var renderer = parent.GetComponent<Renderer>();
                if (preset != null && renderer != null)
                    renderer.sharedMaterial = preset.material;
            }
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
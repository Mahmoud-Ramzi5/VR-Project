using UnityEngine;

public enum MaterialType
{
    Glass,
    Wood,
    Stone,
    Metal,
    Rubber,
    Custom
}

[System.Serializable]
public class MaterialProperties
{
    public MaterialType materialType;
    public float bounciness = 0f; // How much it bounces (0 = no bounce, 1 = max bounce)
    public float friction = 0.5f; // How much friction (0 = no friction, 1 = max friction)
}

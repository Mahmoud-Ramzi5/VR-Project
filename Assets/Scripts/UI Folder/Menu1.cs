using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Menu1 : MenuDataBinderBase
{
    public TMP_InputField X_distribution;
    public TMP_InputField Y_distribution;
    public TMP_InputField Z_distribution;

    void Start()
    {
        OctreeSpringFiller springFiller = FindObjectOfType<OctreeSpringFiller>();
        if (springFiller != null)
        {
            Initializethings(springFiller);
        }
    }

    // Initialize toggle states based on target values
    public void Initializethings(OctreeSpringFiller target)
    {
        if (target == null) return;

        X_distribution.text = target.minNodeSize.x.ToString("F2"); // "F2" = 2 decimal places
        Y_distribution.text = target.minNodeSize.y.ToString("F2");
        Z_distribution.text = target.minNodeSize.z.ToString("F2");
    }

    public override void ApplyTo(OctreeSpringFiller target)
    {
        float x = 1.0f;
        float y = 1.0f;
        float z = 1.0f;
        if (X_distribution != null && !string.IsNullOrWhiteSpace(X_distribution.text))
            float.TryParse(X_distribution.text, out x);
        if (Y_distribution != null && !string.IsNullOrWhiteSpace(X_distribution.text))
            float.TryParse(Y_distribution.text, out y);
        if (Z_distribution != null && !string.IsNullOrWhiteSpace(X_distribution.text))
            float.TryParse(Z_distribution.text, out z);

        target.minNodeSize = new Vector3(x, y, z);
        target.PointSpacing = new Vector3(x, y, z);
    }
}

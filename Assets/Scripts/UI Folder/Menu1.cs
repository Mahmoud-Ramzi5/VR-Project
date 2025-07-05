using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Menu1 : MenuDataBinderBase
{
    public TMP_InputField X_distribution;
    public TMP_InputField Y_distribution;
    public TMP_InputField Z_distribution;

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

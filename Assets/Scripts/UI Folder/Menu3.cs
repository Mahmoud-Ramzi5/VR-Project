using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Menu3 : MenuDataBinderBase
{
    public TMP_InputField Layer1_spring_constant;
    public TMP_InputField Layer1_spring_damper;
    public TMP_InputField Layer1_connection_radius;
    public TMP_InputField Layer1_max_rest_length;
    
    public override void ApplyTo(OctreeSpringFiller target)
    {
        if (Layer1_spring_constant != null && !string.IsNullOrWhiteSpace(Layer1_spring_constant.text))
            float.TryParse(Layer1_spring_constant.text, out target.springConstantL1);

        if (Layer1_spring_damper != null && !string.IsNullOrWhiteSpace(Layer1_spring_damper.text))
            float.TryParse(Layer1_spring_damper.text, out target.damperConstantL1);
        
        if (Layer1_connection_radius != null && !string.IsNullOrWhiteSpace(Layer1_connection_radius.text))
            float.TryParse(Layer1_connection_radius.text, out target.connectionRadiusL1);
        
        if (Layer1_max_rest_length != null && !string.IsNullOrWhiteSpace(Layer1_max_rest_length.text))
            float.TryParse(Layer1_max_rest_length.text, out target.maxRestLengthL1);
    }
}

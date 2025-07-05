using UnityEngine;
using UnityEngine.UI;

public class Menu2 : MenuDataBinderBase
{
    public Toggle Apply_gravity;
    public Toggle Show_points;
    public Toggle Show_connections;
    public Toggle Is_rigid;
    public Toggle Is_solid;

    public override void ApplyTo(OctreeSpringFiller target)
    {
        target.applyGravity = Apply_gravity.isOn;
        target.visualizeSpringPoints = Show_points.isOn;
        target.visualizeSpringConnections = Show_connections.isOn;
        target.isRigid = Is_rigid.isOn;
        target.isSolid = Is_solid.isOn;
    }
}

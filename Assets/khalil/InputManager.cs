using UnityEngine;

public class InputManager : MonoBehaviour
{
    private Transform selectedObject;
    private float objectZCoord;

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            OnClick();
        }
        else if (Input.GetMouseButton(0) && selectedObject != null)
        {
            OnDrag();
        }
        else if (Input.GetMouseButtonUp(0))
        {
            selectedObject = null;
        }
    }

    void OnClick()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit))
        {
            selectedObject = hit.transform;
            // Calculate distance from camera to object to maintain depth while dragging
            objectZCoord = Camera.main.WorldToScreenPoint(selectedObject.position).z;
            Debug.Log("Selected object: " + selectedObject.name);
        }
    }

    void OnDrag()
    {
        // Get the mouse position in screen space, including the depth (z)
        Vector3 mousePoint = new Vector3(Input.mousePosition.x, Input.mousePosition.y, objectZCoord);

        // Convert screen space mouse position to world space
        Vector3 worldPosition = Camera.main.ScreenToWorldPoint(mousePoint);

        // Update the selected object's position
        selectedObject.position = worldPosition;
    }
}

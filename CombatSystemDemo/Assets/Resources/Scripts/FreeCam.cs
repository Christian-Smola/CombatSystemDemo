using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FreeCam : MonoBehaviour
{
    private Vector2 CameraStartPosition;

    // Update is called once per frame
    void Update()
    {
        if (Input.GetMouseButtonDown(2))
            CameraStartPosition = Input.mousePosition;

        if (Input.GetMouseButton(2))
        {
            float x = (CameraStartPosition.x - Input.mousePosition.x) / 20f;
            float y = (CameraStartPosition.y - Input.mousePosition.y) / 20f;

            Camera.main.transform.localPosition += Camera.main.gameObject.transform.right * x;
            Camera.main.transform.localPosition += Camera.main.gameObject.transform.up * y;

            CameraStartPosition = Input.mousePosition;
        }

        if ((Input.GetAxis("Mouse ScrollWheel") > 0 && Camera.main.gameObject.transform.position.y > 5) || Input.GetAxis("Mouse ScrollWheel") < 0 && Camera.main.gameObject.transform.position.y < 100)
            Camera.main.gameObject.transform.localPosition += (Camera.main.gameObject.transform.forward * Input.GetAxis("Mouse ScrollWheel") * 15f);
    }
}

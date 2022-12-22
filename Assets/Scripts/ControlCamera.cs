using System.Collections;
using System.Collections.Generic;
using UnityEditor.UIElements;
using UnityEngine;

public class ControlCamera : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKey(KeyCode.RightArrow))
        {
            gameObject.transform.position += new Vector3(-10.0f, 0.0f, 0.0f) * Time.deltaTime;
        }

        if (Input.GetKey(KeyCode.LeftArrow))
        {
            gameObject.transform.position += new Vector3(10.0f, 0.0f, 0.0f) * Time.deltaTime;
        }

        if (Input.GetKey(KeyCode.UpArrow))
        {
            gameObject.transform.position += new Vector3(0.0f, 0.0f, -15.0f) * Time.deltaTime;
        }

        if (Input.GetKey(KeyCode.DownArrow))
        {
            gameObject.transform.position += new Vector3(0.0f, 0.0f, 15.0f) * Time.deltaTime;
        }

        if (Input.GetKeyDown(KeyCode.D))
        {
            gameObject.GetComponent<Camera>().targetTexture = null;
        }

        if (Input.GetKey(KeyCode.F))
        {
            gameObject.GetComponent<Camera>().fieldOfView += 1;
        }

        if (Input.GetKey(KeyCode.G))
        {
            gameObject.GetComponent<Camera>().fieldOfView -= 1;
        }
    }
}

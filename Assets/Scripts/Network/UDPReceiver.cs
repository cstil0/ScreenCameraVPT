using UnityEngine;
using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Globalization;
using System.Collections;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine.EventSystems;

// this scripts listens to UDP messages and triggers the corresponding actions when one is received
public class UDPReceiver : MonoBehaviour
{
    public static UDPReceiver instance;

    [Header ("Thread properties")]
    // udpclient object
    UdpClient client;
    public int serverPort;
    Thread receiveThread;
    Thread evaluateThread;

    [Header ("Movement parameters")]
    Vector3 startPos;
    Vector3 remoteStartPos;
    Vector3 currRemotePos;

    bool resetStart;
    int currActiveCamera;

    Quaternion startRot;
    Quaternion remoteStartRot;
    Quaternion currRemoteRot;

    // the difference between original and start is that original is used to fully reset the position, not only from the last time that the camera was moved
    [SerializeField] Vector3 originalCameraPos;
    [SerializeField] Quaternion originalCameraRot;

    Quaternion remoteRotDiff = new Quaternion();
    Quaternion newRotation = new Quaternion();
    Quaternion newSceneRotation = new Quaternion();

    [Header ("Cameras")]
    [SerializeField] Transform camerasContainer;
    public Camera ScreenCamera;
    public RenderTexture screenTexture;

    [Header ("Message states")]
    bool newRotationParsed = true;
    bool newSceneRotationParsed = true;
    string lastReceivedMessage = "";
    bool isMessageParsed = true;

    [Header ("Movement types")]
    [SerializeField] eCameraModes cameraMode;
    [SerializeField] eParallaxType parallaxType;
    public float wallDistance = 0;

    enum Messages
    {
        CAMERA_INFO,
        SCENE_ROTATION,
        SEND_NDI,
        SEND_DISPLAY,
        CHANGE_CAMERA,
        CHANGE_SCREEN_DISTANCE,
        RESET_POSROT
    }

    enum eCameraModes
    {
        FOLLOW,
        INVERTED
    }

    enum eParallaxType
    {
        BASIC,
        MODELED
    }

    private void Awake()
    {
        // If there is an instance, and it's not me, delete myself.

        if (UDPReceiver.instance != null && UDPReceiver.instance != this)
        {
            Destroy(this);
        }
        else
        {
            UDPReceiver.instance = this;
        }
    }

    void OnDisable()
    {
        // stop thread when object is disabled
        if (receiveThread != null)
            receiveThread.Abort();

        if (evaluateThread != null)
            evaluateThread.Abort();

        client.Close();
    }

    void Start()
    {
        // set computation modes by default
        cameraMode = eCameraModes.INVERTED;
        parallaxType = eParallaxType.BASIC;
        resetStart = false;

        // Start thread to listen UDP messages and set it as background
        receiveThread = new Thread(ReceiveUDP);
        receiveThread.IsBackground = true;
        receiveThread.Start();

        startPos = ScreenCamera.transform.position;
        startRot = ScreenCamera.transform.rotation;

        originalCameraPos = ScreenCamera.transform.position;
        originalCameraRot = ScreenCamera.transform.rotation;

        newRotation = startRot;
    }

    // main thread that listens to UDP messages through a defined port
    void ReceiveUDP()
    {
        // create client and set the port
        client = new UdpClient(serverPort);
        // loop needed to keep listening
        while (true)
        {
            try
            {
                // recieve messages through the end point
                IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, serverPort);
                byte[] receiveBytes = client.Receive(ref remoteEndPoint);
                // once the message is recieved, encode it as ASCII
                lastReceivedMessage = Encoding.ASCII.GetString(receiveBytes);
                isMessageParsed = false;
            }
            catch (Exception e) { }
        }
    }

    // executed when main camera changes
    void computeChangeCamera()
    {
        // eliminate target texture from the current main camera
        ScreenCamera.targetTexture = null;
        // change main camera reference
        GameObject screenCameraGO = camerasContainer.GetChild(currActiveCamera - 1).gameObject;
        ScreenCamera = screenCameraGO.GetComponent<Camera>();
        // start new main camera to render into the screen texture
        ScreenCamera.targetTexture = screenTexture;

        // set its corresponding position
        startPos = ScreenCamera.transform.position;
        startRot = ScreenCamera.transform.rotation;
        originalCameraPos = ScreenCamera.transform.position;
        originalCameraRot = ScreenCamera.transform.rotation;

        // enable / disable camera component
        foreach (Transform child in camerasContainer.transform)
        {
            if(ScreenCamera.gameObject != child.gameObject)
                child.GetComponent<Camera>().enabled = false;
            else
                child.GetComponent<Camera>().enabled = true;
        }
    }

    // executed when new position is received
    void changePos()
    {
        // if reset start is set, restart the initial position of the camera according to the current one
        if (resetStart)
        {
            remoteStartPos = new Vector3(currRemotePos.x, currRemotePos.y, currRemotePos.z);
            startPos = ScreenCamera.transform.position;
        }

        // compute difference depending on the movement mode
        Vector3 remotePosDiff = new Vector3();
        if (cameraMode == eCameraModes.FOLLOW)
           remotePosDiff  = remoteStartPos - currRemotePos;
        else if (cameraMode == eCameraModes.INVERTED)
            remotePosDiff = currRemotePos - remoteStartPos;

        // the higher distance, the slower the camera moves (in an approximate way)
        ScreenCamera.transform.position = (remotePosDiff * (1 - wallDistance/100)) + startPos;
    }

    // executed when new rotation is received
    void changeRot()
    {
        // if reset start is set, restart the initial rotation of the camera according to the current one
        if (resetStart)
        {
            remoteStartRot = new Quaternion(currRemoteRot.x, currRemoteRot.y, currRemoteRot.z, currRemoteRot.w);
            startRot = ScreenCamera.transform.rotation;
        }

        // compute difference depending on the movement mode
        if (cameraMode == eCameraModes.FOLLOW)
            remoteRotDiff = Quaternion.Inverse(currRemoteRot) * remoteStartRot;
        else if (cameraMode == eCameraModes.INVERTED)
            remoteRotDiff = Quaternion.Inverse(remoteStartRot) * currRemoteRot;

        // compute basic motion parallax mode
        if (parallaxType == eParallaxType.BASIC)
        {
            float speedFactor = (1 - wallDistance / 100);
            // compute maximum movement that will be applied by summing the difference of rotation received to the current initial one
            Quaternion totalRotation = startRot * remoteRotDiff;
            // interpolate between the current start position and the one with the difference applied to change the movement's speed
            newRotation = Quaternion.Slerp(startRot, totalRotation, speedFactor);
        }

        // compute modeled motion parallax mode
        else if (parallaxType == eParallaxType.MODELED)
        {
            float maxDist = 100f;
            float factor = (maxDist - wallDistance) / maxDist;

            Quaternion alpha = startRot * remoteRotDiff;
            // interpolate between the current start position and the one with the difference applied to change the movement's speed
            Quaternion theta = Quaternion.Slerp(startRot, alpha, factor);
            newRotation = theta;
        }

        newRotationParsed = false;
    }

    // rotate all cameras when the scene is rotated
    void rotateScene(float rotationAngle)
    {
        Vector3 rotation = new Vector3(0.0f, -rotationAngle, 0.0f);
        // save the new value to be applied in late update
        newSceneRotation = Quaternion.Euler(rotation + camerasContainer.transform.rotation.eulerAngles);
        newSceneRotationParsed = false;
    }

    // change from HDMI to NDI output
    void changeToNDI()
    {
        GameObject.Find("NDI Sender").SetActive(true);
        ScreenCamera.targetTexture = screenTexture;
    }

    // change from NDI to HDMI output
    void changeToDisplay()
    {
        GameObject.Find("NDI Sender").SetActive(false);
        ScreenCamera.targetTexture = null;
        ScreenCamera.targetDisplay = 0;
    }

    // reset all initial positions and rotations
    void resetPosRot()
    {
        ScreenCamera.transform.position = originalCameraPos;
        ScreenCamera.transform.rotation = originalCameraRot;

        startPos = originalCameraPos;
        startRot = originalCameraRot;

        remoteStartPos = currRemotePos;
        remoteStartRot = currRemoteRot;
    }

    private void Update()
    {
        // check if there is a message waiting to be parsed and evaluate its corresponding action
        if (!isMessageParsed)
        {
            string[] splittedMessage = lastReceivedMessage.Split(":".ToCharArray());
            Messages message_enum = (Messages)Enum.Parse(typeof(Messages), splittedMessage[0]);

            // switch action according to message
            switch (message_enum)
            {
                // parse the received position and rotation coordinates
                case Messages.CAMERA_INFO:
                    string[] splittedInfo = splittedMessage[1].Split("#".ToCharArray());
                    resetStart = bool.Parse(splittedInfo[0]);
                    string remotePosRaw = splittedInfo[1];
                    string remoteRotRaw = splittedInfo[2];

                    int parenthesisIndex = remotePosRaw.IndexOf(")");
                    string filteredMessage = remotePosRaw.Substring(1, parenthesisIndex - 1);
                    string[] splittedPos = filteredMessage.Split(", ".ToCharArray());
                    currRemotePos = new Vector3(float.Parse(splittedPos[0], CultureInfo.InvariantCulture), float.Parse(splittedPos[2], CultureInfo.InvariantCulture), float.Parse(splittedPos[4], CultureInfo.InvariantCulture));

                    parenthesisIndex = remoteRotRaw.IndexOf(")");
                    filteredMessage = remoteRotRaw.Substring(1, parenthesisIndex - 1);
                    string[] splittedRot = filteredMessage.Split(", ".ToCharArray());
                    currRemoteRot = new Quaternion(float.Parse(splittedRot[0], CultureInfo.InvariantCulture), float.Parse(splittedRot[2], CultureInfo.InvariantCulture), float.Parse(splittedRot[4], CultureInfo.InvariantCulture), float.Parse(splittedRot[6], CultureInfo.InvariantCulture));
                    changePos();
                    changeRot();
                    break;

                case Messages.SCENE_ROTATION:
                    float sceneRotationAngle = float.Parse(splittedMessage[1]);
                    rotateScene(sceneRotationAngle);
                    break;

                case Messages.SEND_NDI:
                    changeToNDI();
                    break;

                case Messages.SEND_DISPLAY:
                    changeToDisplay();
                    break;

                case Messages.CHANGE_CAMERA:
                    currActiveCamera = int.Parse(splittedMessage[1]);
                    computeChangeCamera();
                    break;

                case Messages.CHANGE_SCREEN_DISTANCE:
                    wallDistance = float.Parse(splittedMessage[1], CultureInfo.InvariantCulture);
                    break;

                case Messages.RESET_POSROT:
                    resetPosRot();
                    break;

            }
            
            isMessageParsed = true;
        }

        // shortcuts to change properties at run-time
        if (Input.GetKeyDown(KeyCode.A))
        {
            float tempDist = wallDistance - 10f;
            if (tempDist <= 1.0f)
                wallDistance = tempDist;
        }
        if (Input.GetKeyDown(KeyCode.S))
        {
            float tempDist = wallDistance + 10f;
            if (tempDist >= 0.0f)
                wallDistance = tempDist;
        }
        
        // change computation modes
        if (Input.GetKeyDown(KeyCode.C))
        {
            cameraMode = eCameraModes.FOLLOW;
        }
        if (Input.GetKeyDown(KeyCode.V))
        {
            cameraMode = eCameraModes.INVERTED;
        }

        if (Input.GetKeyDown(KeyCode.B))
        {
            parallaxType = eParallaxType.BASIC;
        }

        if (Input.GetKeyDown(KeyCode.N))
        {
            parallaxType = eParallaxType.MODELED;
        }
    }

    // screen camera and scene rotations are computed at update, but applied at late update to ensure that they are only applied once after all computations are done
    private void LateUpdate()
    {
        if (!newRotationParsed)
        {
            ScreenCamera.transform.rotation = newRotation;
            newRotationParsed = true;
        }
        
        if (!newSceneRotationParsed)
        { 
            camerasContainer.transform.rotation = newSceneRotation;
            newSceneRotationParsed = true;
        }
    }
}
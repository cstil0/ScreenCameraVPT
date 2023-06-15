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
using UnityEditor.VersionControl;

public class UDPReceiver : MonoBehaviour
{
    public static UDPReceiver instance;
    // udpclient object
    UdpClient client;
    public int serverPort;
    Thread receiveThread;
    Thread evaluateThread;
    string lastReceivedMessage = "";
    bool isMessageParsed = true;

    [SerializeField] Transform camerasContainer;
    public Camera ScreenCamera;

    Vector3 startPos;
    Vector3 remoteStartPos;
    Vector3 currRemotePos;

    bool resetStart;
    int currActiveCamera;

    Quaternion startRot;
    Quaternion remoteStartRot;
    Quaternion currRemoteRot;

    // the difference between original and start is that original is used to fully reset the position while start from the last time the camera was moved
    [SerializeField] Vector3 originalCameraPos;
    [SerializeField] Quaternion originalCameraRot;

    Quaternion remoteRotDiff = new Quaternion();
    Quaternion newRotation = new Quaternion();
    Quaternion newSceneRotation = new Quaternion();

    bool newRotationParsed = true;
    bool newSceneRotationParsed = true;

    public RenderTexture screenTexture;

    public float wallDistance = 0;

    [SerializeField] eCameraModes cameraMode;
    [SerializeField] eParallaxType parallaxType;
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

    // BORRAR!!!!!!!!!!!!!!!!!!!!
    int lastCount = 1;
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

    void computeChangeCamera()
    {
        Debug.Log("CHANGE CAMERA RECEIVED");
        ScreenCamera.targetTexture = null;
        GameObject screenCameraGO = camerasContainer.GetChild(currActiveCamera - 1).gameObject;
        ScreenCamera = screenCameraGO.GetComponent<Camera>();
        ScreenCamera.targetTexture = screenTexture;

        startPos = ScreenCamera.transform.position;
        startRot = ScreenCamera.transform.rotation;
        originalCameraPos = ScreenCamera.transform.position;
        originalCameraRot = ScreenCamera.transform.rotation;

        foreach (Transform child in camerasContainer.transform)
        {
            if(ScreenCamera.gameObject != child.gameObject)
                child.GetComponent<Camera>().enabled = false;
            else
                child.GetComponent<Camera>().enabled = true;
        }
    }

    void changePos()
    {
        if (resetStart)
        {
            Debug.Log("RESETING START POS");
            remoteStartPos = new Vector3(currRemotePos.x, currRemotePos.y, currRemotePos.z);
            startPos = ScreenCamera.transform.position;
        }

        Vector3 remotePosDiff = new Vector3();
        if (cameraMode == eCameraModes.FOLLOW)
           remotePosDiff  = remoteStartPos - currRemotePos;
        else if (cameraMode == eCameraModes.INVERTED)
            remotePosDiff = currRemotePos - remoteStartPos;

        // the higher distance, the slower the camera moves (in an approximate way)
        ScreenCamera.transform.position = (remotePosDiff * (1 - wallDistance/100)) + startPos;
    }

    void changeRot()
    {
        if (resetStart)
        {
            remoteStartRot = new Quaternion(currRemoteRot.x, currRemoteRot.y, currRemoteRot.z, currRemoteRot.w);
            startRot = ScreenCamera.transform.rotation;
            Debug.Log("RESETING START ROT: " + remoteStartRot.eulerAngles);
        }

        // compute difference depending on the movement mode
        if (cameraMode == eCameraModes.FOLLOW)
            remoteRotDiff = remoteStartRot * Quaternion.Inverse(currRemoteRot);
        else if (cameraMode == eCameraModes.INVERTED)
        {
            //Vector3 remoteStartRotVec = remoteStartRot.eulerAngles;
            Vector3 remoteStartRotVec = originalCameraRot.eulerAngles;
            Vector3 currRemoteRotVec = currRemoteRot.eulerAngles;
            Debug.Log("REMOTE CURR ROT: " + currRemoteRot.eulerAngles);
            remoteRotDiff = Quaternion.Inverse(remoteStartRot) * currRemoteRot;

            //remoteRotDiff = Quaternion.Euler(currRemoteRotVec - remoteStartRotVec);
            //remoteRotDiff = Quaternion.Euler(remoteStartRotVec.x - currRemoteRotVec.x, currRemoteRotVec.y - remoteStartRotVec.y, currRemoteRotVec.z - remoteStartRotVec.z);
            Debug.Log("REMOTE DIFF: " + remoteRotDiff.eulerAngles);
        }

        //// compute basic motion parallax mode
        if (parallaxType == eParallaxType.BASIC)
        {
            float speedFactor = (1 - wallDistance / 100);
            newRotation = Quaternion.Slerp(startRot, remoteRotDiff, speedFactor);
            //newRotation = startRot * new Quaternion(remoteRotDiff.x * speedFactor, remoteRotDiff.y * speedFactor, remoteRotDiff.z * speedFactor, remoteRotDiff.w * speedFactor);
            //newRotationVec = (remoteRotDiff.eulerAngles * (1 - wallDistance / 100)) + startRot.eulerAngles;
            //ScreenCamera.transform.rotation = Quaternion.Euler(newRotationVec);
        }

        // compute modeled motion parallax mode
        else if (parallaxType == eParallaxType.MODELED)
        {
            if(wallDistance == 0) {
                ScreenCamera.transform.rotation = remoteRotDiff * startRot;
            }
            else
            {
                float maxDist = 100f;
                float factor = (maxDist - wallDistance) / maxDist;

                Vector3 alpha = remoteRotDiff.eulerAngles;
                Quaternion theta = Quaternion.Euler(factor * alpha.x, factor * alpha.y, factor * alpha.z);
                newRotation = theta;
            }
        }

        newRotationParsed = false;
    }

    void rotateScene(float rotationAngle)
    {
        Vector3 rotation = new Vector3(0.0f, -rotationAngle, 0.0f);
        //Quaternion rotation = Quaternion.Euler(0.0f, rotationAngle, 0.0f);
        newSceneRotation = Quaternion.Euler(rotation + camerasContainer.transform.rotation.eulerAngles);
        newSceneRotationParsed = false;
    }

    void changeToNDI()
    {
        GameObject.Find("NDI Sender").SetActive(true);
        ScreenCamera.targetTexture = screenTexture;
    }

    void changeToDisplay()
    {
        GameObject.Find("NDI Sender").SetActive(false);
        ScreenCamera.targetTexture = null;
        ScreenCamera.targetDisplay = 0;
    }

    void resetPosRot()
    {
        ScreenCamera.transform.position = originalCameraPos;
        ScreenCamera.transform.rotation = originalCameraRot;

        startPos = originalCameraPos;
        startRot = originalCameraRot;

        remoteStartPos = currRemotePos;
        remoteStartRot = currRemoteRot;
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
    // Start is called before the first frame update
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

    private void Update()
    {
        if (!isMessageParsed)
        {
            string[] splittedMessage = lastReceivedMessage.Split(":".ToCharArray());
            Messages message_enum = (Messages)Enum.Parse(typeof(Messages), splittedMessage[0]);

            // switch action according to message
            switch (message_enum)
            {
                case Messages.CAMERA_INFO:
                    string[] splittedInfo = splittedMessage[1].Split("#".ToCharArray());
                    resetStart = bool.Parse(splittedInfo[0]);
                    if (resetStart)
                        Debug.Log("MATCH RESET: " + resetStart);
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
                    Debug.Log("ROTATION MATCH: " + currRemoteRot.eulerAngles);
                    changePos();
                    changeRot();

                    int count = int.Parse(splittedInfo[3]);
                    if (count != lastCount + 1)
                        Debug.Log("COUNT NOT MATCHING! LAST: " + lastCount + ". RECEIVED: " + count);
                    else
                        Debug.Log("MATCH ALL FINE: " + count);

                    lastCount = count;
                    break;

                case Messages.SCENE_ROTATION:
                    float sceneRotationAngle = float.Parse(splittedMessage[1]);
                    //parenthesisIndex = sceneRotationRaw.IndexOf(")");
                    //filteredMessage = sceneRotationRaw.Substring(1, parenthesisIndex - 1);
                    //string[] splittedSceneRot = filteredMessage.Split(", ".ToCharArray());
                    //currSceneRot = new Quaternion(float.Parse(splittedSceneRot[0], CultureInfo.InvariantCulture), float.Parse(splittedSceneRot[2], CultureInfo.InvariantCulture), float.Parse(splittedSceneRot[4], CultureInfo.InvariantCulture), float.Parse(splittedSceneRot[6], CultureInfo.InvariantCulture));

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
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

public class UDPReceiver : MonoBehaviour
{
    public static UDPReceiver instance;
    // udpclient object
    UdpClient client;
    public int serverPort;
    Thread receiveThread;
    Thread evaluateThread;
    String receivedMessage;
    Queue messagesToEvaluate;

    Messages lastMessageType;

    [SerializeField] Transform camerasContainer;
    public Camera ScreenCamera;

    Vector3 startPos;
    Vector3 remoteStartPos;
    Vector3 currRemotePos;

    Vector3 lastRemotePos;
    Quaternion lastRemoteRot;

    bool resetStart;
    bool changeCamera;
    int currActiveCamera;

    //Vector3 startRot;
    Quaternion startRot;
    //Vector3 remoteStartRot;
    Quaternion remoteStartRot;
    //Vector3 currRemoteRot;
    Quaternion currRemoteRot;
    Quaternion currSceneRot;

    Vector3 lastRemoteRotDiff;

    // the difference between original and start is that original is used to fully reset the position while start from the last time the camera was moved
    [SerializeField] Vector3 originalCameraPos;
    [SerializeField] Quaternion originalCameraRot;

    public RenderTexture screenTexture;

    public float wallDistance = 0;

    [SerializeField] eCameraModes cameraMode;
    [SerializeField] eParallaxType parallaxType;
    enum Messages
    {
        ALREADY_EVALUATED,
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
                receivedMessage = Encoding.ASCII.GetString(receiveBytes);

                Messages message_enum = 0;
                string message = "";

                if (receivedMessage.Contains(":"))
                {
                    string[] splittedMessageType = receivedMessage.Split(":".ToCharArray());
                    // convert string to correct enum type
                    message_enum = (Messages)Enum.Parse(typeof(Messages), splittedMessageType[0]);
                    message = splittedMessageType[1];
                }
                else
                {
                    message_enum = (Messages)Enum.Parse(typeof(Messages), receivedMessage);
                    message = receivedMessage;
                }

                // switch action according to message
                switch (message_enum)
                {
                    case Messages.CAMERA_INFO:
                        resetStart = bool.Parse(message);

                        Debug.Log("RESET START: " + resetStart);
                        // Receive positition
                        receiveBytes = client.Receive(ref remoteEndPoint);
                        // once the message is recieved, encode it as ASCII
                        receivedMessage = Encoding.ASCII.GetString(receiveBytes);

                        int parenthesisIndex = receivedMessage.IndexOf(")");
                        string filteredMessage = receivedMessage.Substring(1, parenthesisIndex - 1);
                        //string filteredMessage = receivedMessage.Substring(1, -2);
                        string[] splittedMessage = filteredMessage.Split(", ".ToCharArray());
                        currRemotePos = new Vector3(float.Parse(splittedMessage[0], CultureInfo.InvariantCulture), float.Parse(splittedMessage[2], CultureInfo.InvariantCulture), float.Parse(splittedMessage[4], CultureInfo.InvariantCulture));
                        Debug.Log("POS RECEIVED: " + currRemotePos);

                        // Receive rotation
                        receiveBytes = client.Receive(ref remoteEndPoint);
                        // once the message is recieved, encode it as ASCII
                        receivedMessage = Encoding.ASCII.GetString(receiveBytes);

                        //splittedMessage = receivedMessage.Split(" ");
                        parenthesisIndex = receivedMessage.IndexOf(")");
                        filteredMessage = receivedMessage.Substring(1, parenthesisIndex - 1);
                        splittedMessage = filteredMessage.Split(", ".ToCharArray());

                        currRemoteRot = new Quaternion(float.Parse(splittedMessage[0], CultureInfo.InvariantCulture), float.Parse(splittedMessage[2], CultureInfo.InvariantCulture), float.Parse(splittedMessage[4], CultureInfo.InvariantCulture), float.Parse(splittedMessage[6], CultureInfo.InvariantCulture));
                        Debug.Log("ROT RECEIVED: " + currRemoteRot);

                        lastMessageType = Messages.CAMERA_INFO;
                        break;

                    case Messages.SCENE_ROTATION:
                        parenthesisIndex = message.IndexOf(")");
                        filteredMessage = message.Substring(1, parenthesisIndex - 1);
                        splittedMessage = filteredMessage.Split(", ".ToCharArray());

                        currSceneRot = new Quaternion(float.Parse(splittedMessage[0], CultureInfo.InvariantCulture), float.Parse(splittedMessage[2], CultureInfo.InvariantCulture), float.Parse(splittedMessage[4], CultureInfo.InvariantCulture), float.Parse(splittedMessage[6], CultureInfo.InvariantCulture));

                        lastMessageType = Messages.SCENE_ROTATION;
                        break;

                    case Messages.SEND_NDI:
                        lastMessageType = Messages.SEND_NDI;
                        break;

                    case Messages.SEND_DISPLAY:
                        lastMessageType = Messages.SEND_DISPLAY;
                        break;

                    case Messages.CHANGE_CAMERA:
                        //changeCamera = true;
                        lastMessageType= Messages.CHANGE_CAMERA;
                        currActiveCamera = int.Parse(message);
                        Debug.Log("CHANGE OF CAMERA RECEIVED: " + currActiveCamera);
                        break;

                    case Messages.CHANGE_SCREEN_DISTANCE:
                        wallDistance = float.Parse(message, CultureInfo.InvariantCulture);
                        break;

                    case Messages.RESET_POSROT:
                        lastMessageType = Messages.RESET_POSROT;
                        break;
                }
            }
            catch (Exception e)
            {
                print("Error: " + e.Message);
            }
        }
    }

    void computeChangeCamera()
    {
        ScreenCamera.targetTexture = null;
        GameObject screenCameraGO = camerasContainer.GetChild(currActiveCamera - 1).gameObject;
        ScreenCamera = screenCameraGO.GetComponent<Camera>();
        ScreenCamera.targetTexture = screenTexture;

        startPos = ScreenCamera.transform.position;
        startRot = ScreenCamera.transform.rotation;
        originalCameraPos = ScreenCamera.transform.position;
        originalCameraRot = ScreenCamera.transform.rotation;
        //changeCamera = false;
    }

    void changePos()
    {
        if (resetStart)
        {
            remoteStartPos = new Vector3(currRemotePos.x, currRemotePos.y, currRemotePos.z);
            startPos = ScreenCamera.transform.position;
        }

        Vector3 remotePosDiff = new Vector3();
        if (cameraMode == eCameraModes.FOLLOW)
           remotePosDiff  = remoteStartPos - currRemotePos;
        else if (cameraMode == eCameraModes.INVERTED)
            remotePosDiff = remoteStartPos - currRemotePos;

        //Vector3 remotePosDiff = currPos - remoteStartPos;
        // just a trick to make camera move slower with higher distance in an approximate way
        ScreenCamera.transform.position = (remotePosDiff * (1 - wallDistance/100)) + startPos;
        }
    void changeRot()
    {
        if (resetStart)
        {
            remoteStartRot = new Quaternion(currRemoteRot.x, currRemoteRot.y, currRemoteRot.z, currRemoteRot.w);
            startRot = ScreenCamera.transform.rotation;
        }

        //Vector3 remoteRotDiff = new Vector3();
        Quaternion remoteRotDiff = new Quaternion();
        if (cameraMode == eCameraModes.FOLLOW)
            remoteRotDiff = remoteStartRot * Quaternion.Inverse(currRemoteRot);
        //remoteRotDiff = remoteStartRot.eulerAngles - currRemoteRot.eulerAngles;
        else if (cameraMode == eCameraModes.INVERTED)
            remoteRotDiff = currRemoteRot * Quaternion.Inverse(remoteStartRot);

        //remoteRotDiff = currRemoteRot.eulerAngles - remoteStartRot.eulerAngles;

        //if (lastRemoteRot == new Quaternion(0.0f, 0.0f, 0.0f, 0.0f)|| resetStart)
        //    lastRemoteRotDiff = remoteRotDiff;

        //Vector3 lastCurrRotDiff = lastRemoteRotDiff - remoteRotDiff;

        if (parallaxType == eParallaxType.BASIC)
        {
            Vector3 newRotationVec = (remoteRotDiff.eulerAngles * (1 - wallDistance / 100)) + startRot.eulerAngles;
            ScreenCamera.transform.rotation = Quaternion.Euler(newRotationVec);
            //Quaternion newRotation = Quaternion.Slerp(Quaternion.identity, remoteRotDiff, factor);
            //float factor = (1 - wallDistance / 100);
            //ScreenCamera.transform.rotation = newRotation * startRot;
        }

        else if (parallaxType == eParallaxType.MODELED)
        {
            if(wallDistance == 0) {
                ScreenCamera.transform.rotation = remoteRotDiff * startRot;
                //ScreenCamera.transform.rotation = Quaternion.Euler(remoteRotDiff) * startRot;
            }
            else
            {
                // apply parallax model
                //float focalLength = ScreenCamera.GetComponent<Camera>().focalLength / 100;
                //Vector3 theta = (wallDistance - focalLength) * remoteRotDiff / wallDistance;
                float maxDist = 100f;
                //Vector3 theta = new Vector3();
                float theta = (maxDist - wallDistance) / maxDist;
                //Vector3 newRot = new Vector3(0, theta, 0);
                Quaternion newRotation = Quaternion.Slerp(Quaternion.identity, remoteRotDiff, theta);

                //ScreenCamera.transform.rotation = Quaternion.Euler(theta.x, theta.y - 180, theta.z);
                Debug.Log("THETA: " + theta);
                Debug.Log("");
            }
        }


        //Quaternion remoteRotDiff = currRemoteRot * Quaternion.Inverse(remoteStartRot);
        //Quaternion parallaxDiff = new Quaternion(remoteRotDiff.x * wallDistance, remoteRotDiff.y * wallDistance, remoteRotDiff.z * wallDistance, remoteRotDiff.w * wallDistance);
        //ScreenCamera.transform.rotation = parallaxDiff * startRot;

        lastMessageType = Messages.ALREADY_EVALUATED;
    }

    void rotateScene()
    {
        Quaternion sceneRotDiff = remoteStartRot * Quaternion.Inverse(currSceneRot);
        ScreenCamera.transform.rotation = sceneRotDiff * startRot;
        lastMessageType = Messages.ALREADY_EVALUATED;
    }

    void changeToNDI()
    {
        GameObject.Find("NDI Sender").SetActive(true);
        ScreenCamera.targetTexture = screenTexture;
        lastMessageType = Messages.ALREADY_EVALUATED;

    }

    void changeToDisplay()
    {
        GameObject.Find("NDI Sender").SetActive(false);
        ScreenCamera.targetTexture = null;
        ScreenCamera.targetDisplay = 0;
        lastMessageType = Messages.ALREADY_EVALUATED;
    }

    void resetPosRot()
    {
        Debug.Log("RESET POS ROT");
        ScreenCamera.transform.position = originalCameraPos;
        ScreenCamera.transform.rotation = originalCameraRot;

        startPos = originalCameraPos;
        startRot = originalCameraRot;

        remoteStartPos = currRemotePos;
        remoteStartRot = currRemoteRot;
    }

    //void evaluateMessage(String messageToEvaluate)
    //{
    //    Debug.Log(messageToEvaluate);
    //}

    //void readMessagesToEvaluate()
    //{
    //    while (true)
    //    {
    //        try
    //        {
    //           // get the first message that was added, evaluate it and eliminate it from the queue
    //           evaluateMessage(messagesToEvaluate.Dequeue().ToString());
    //        }
    //        catch (Exception e) {}
    //    }
    //} 

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
        cameraMode = eCameraModes.FOLLOW;
        parallaxType = eParallaxType.BASIC;
        //messagesToEvaluate = new Queue();
        lastMessageType = Messages.ALREADY_EVALUATED;
        resetStart = false;
        changeCamera = false;

        // Start thread to listen UDP messages and set it as background
        receiveThread = new Thread(ReceiveUDP);
        receiveThread.IsBackground = true;
        receiveThread.Start();

        // Start thread to read and evaluate the messages and set it as background
        //evaluateThread = new Thread(readMessagesToEvaluate);
        //evaluateThread.IsBackground = true;
        //evaluateThread.Start();

        startPos = ScreenCamera.transform.position;
        //startRot = ScreenCamera.transform.rotation.eulerAngles;
        startRot = ScreenCamera.transform.rotation;

        originalCameraPos = ScreenCamera.transform.position;
        originalCameraRot = ScreenCamera.transform.rotation;
    }

    private void Update()
    {
        switch (lastMessageType)
        {
            case Messages.CAMERA_INFO:
                changePos();
                changeRot();
                break;
            case Messages.SCENE_ROTATION:
                rotateScene();
                break;
            case Messages.SEND_NDI:
                changeToNDI();
                break;
            case Messages.SEND_DISPLAY:
                changeToDisplay();
                break;
            case Messages.RESET_POSROT:
                resetPosRot();
                break;
            case Messages.CHANGE_CAMERA:
                computeChangeCamera();
                break;
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
}
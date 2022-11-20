using UnityEngine;
using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Globalization;
using System.Collections;

public class UDPReceiver : MonoBehaviour
{
    // udpclient object
    UdpClient client;
    public int serverPort;
    Thread receiveThread;
    Thread evaluateThread;
    String receivedMessage;
    Queue messagesToEvaluate;

    Messages lastMessageType;

    public Camera ScreenCamera;
    Vector3 startPos;
    Vector3 remoteStartPos;
    Vector3 currPos;

    bool resetStart;
    //Vector3 startRot;
    Quaternion startRot;
    //Vector3 remoteStartRot;
    Quaternion remoteStartRot;
    //Vector3 currRot;
    Quaternion currRot;
    Quaternion currSceneRot;

    public RenderTexture screenTexture; 

    enum Messages
    {
        ALREADY_EVALUATED,
        CAMERA_INFO,
        SCENE_ROTATION,
        SEND_NDI,
        SEND_DISPLAY
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

                        // Receive positition
                        receiveBytes = client.Receive(ref remoteEndPoint);
                        // once the message is recieved, encode it as ASCII
                        receivedMessage = Encoding.ASCII.GetString(receiveBytes);

                        int parenthesisIndex = receivedMessage.IndexOf(")");
                        string filteredMessage = receivedMessage.Substring(1, parenthesisIndex - 1);
                        //string filteredMessage = receivedMessage.Substring(1, -2);
                        string[] splittedMessage = filteredMessage.Split(", ".ToCharArray());
                        currPos = new Vector3(float.Parse(splittedMessage[0], CultureInfo.InvariantCulture), float.Parse(splittedMessage[2], CultureInfo.InvariantCulture), float.Parse(splittedMessage[4], CultureInfo.InvariantCulture));

                        // Receive rotation
                        receiveBytes = client.Receive(ref remoteEndPoint);
                        // once the message is recieved, encode it as ASCII
                        receivedMessage = Encoding.ASCII.GetString(receiveBytes);

                        //splittedMessage = receivedMessage.Split(" ");
                        parenthesisIndex = receivedMessage.IndexOf(")");
                        filteredMessage = receivedMessage.Substring(1, parenthesisIndex - 1);
                        splittedMessage = filteredMessage.Split(", ".ToCharArray());

                        currRot = new Quaternion(float.Parse(splittedMessage[0], CultureInfo.InvariantCulture), float.Parse(splittedMessage[2], CultureInfo.InvariantCulture), float.Parse(splittedMessage[4], CultureInfo.InvariantCulture), float.Parse(splittedMessage[6], CultureInfo.InvariantCulture));

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

                }
            }
            catch (Exception e)
            {
                print("Error: " + e.Message);
            }
        }
    }


    void changePos()
    {
        if (resetStart)
            remoteStartPos = new Vector3(currPos.x, currPos.y, currPos.z);

        Vector3 remotePosDiff = remoteStartPos - currPos;
        //Vector3 remotePosDiff = currPos - remoteStartPos;
        ScreenCamera.transform.position = remotePosDiff + startPos;

    }
    void changeRot()
    {
        if (resetStart)
            remoteStartRot = new Quaternion(currRot.x, currRot.y, currRot.z, currRot.w);

        Quaternion remoteRotDiff = currRot * Quaternion.Inverse(remoteStartRot);
        //Quaternion remoteRotDiff = remoteStartRot * Quaternion.Inverse(currRot);
        ScreenCamera.transform.rotation = remoteRotDiff * startRot;

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
        //messagesToEvaluate = new Queue();
        lastMessageType = Messages.ALREADY_EVALUATED;
        resetStart = false;

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
        }
    }
}
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine.UI;
using System.IO;
using System.Globalization;

public class UDPReceiver : MonoBehaviour
{
    // udpclient object
    UdpClient client;
    public int serverPort;
    Thread receiveThread;
    String receivedMessage;

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

    enum Messages
    {
        RESET_START,
        CAMERA_ROT,
        CAMERA_POS,
        SCENE_ROTATION
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
                evaluateMessage(receivedMessage);
            }
            catch (Exception e)
            {
                print("Error: " + e.Message);
            }
        }
    }

    void evaluateMessage(String receivedMessage)
    {
        string[] splittedMessageType = receivedMessage.Split(":".ToCharArray());
        // convert string to correct enum type
        Messages message_enum = (Messages)Enum.Parse(typeof(Messages), splittedMessageType[0]);
        string message = splittedMessageType[1];

        // switch action according to message
        switch (message_enum)
        {
            case Messages.RESET_START:
                resetStart = bool.Parse(message);
                break;

            case Messages.CAMERA_POS:
                int parenthesisIndex = message.IndexOf(")");
                string filteredMessage = message.Substring(1, parenthesisIndex - 1);
                //string filteredMessage = receivedMessage.Substring(1, -2);
                string[] splittedMessage = filteredMessage.Split(", ".ToCharArray());
                currPos = new Vector3(float.Parse(splittedMessage[0], CultureInfo.InvariantCulture), float.Parse(splittedMessage[2], CultureInfo.InvariantCulture), float.Parse(splittedMessage[4], CultureInfo.InvariantCulture));

                if (resetStart)
                    remoteStartPos = new Vector3(currPos.x, currPos.y, currPos.z);

                Vector3 remotePosDiff = currPos - remoteStartPos;
                ScreenCamera.transform.position = remotePosDiff + startPos;

                break;

            case Messages.CAMERA_ROT:
                //splittedMessage = receivedMessage.Split(" ");
                parenthesisIndex = message.IndexOf(")");
                filteredMessage = message.Substring(1, parenthesisIndex - 1);
                splittedMessage = filteredMessage.Split(", ".ToCharArray());

                currRot = new Quaternion(float.Parse(splittedMessage[0], CultureInfo.InvariantCulture), float.Parse(splittedMessage[2], CultureInfo.InvariantCulture), float.Parse(splittedMessage[4], CultureInfo.InvariantCulture), float.Parse(splittedMessage[6], CultureInfo.InvariantCulture));

                if (resetStart)
                    remoteStartRot = new Quaternion(currRot.x, currRot.y, currRot.z, currRot.w);

                Quaternion remoteRotDiff = remoteStartRot * Quaternion.Inverse(currRot);
                ScreenCamera.transform.rotation = remoteRotDiff * startRot;

                break;

            case Messages.SCENE_ROTATION:
                parenthesisIndex = message.IndexOf(")");
                filteredMessage = message.Substring(1, parenthesisIndex - 1);
                splittedMessage = filteredMessage.Split(", ".ToCharArray());

                currRot = new Quaternion(float.Parse(splittedMessage[0], CultureInfo.InvariantCulture), float.Parse(splittedMessage[2], CultureInfo.InvariantCulture), float.Parse(splittedMessage[4], CultureInfo.InvariantCulture), float.Parse(splittedMessage[6], CultureInfo.InvariantCulture));

                Quaternion sceneRotDiff = remoteStartRot * Quaternion.Inverse(currRot);
                ScreenCamera.transform.rotation = sceneRotDiff * startRot;

                break;
        }
    }

    void OnDisable()
    {
        // stop thread when object is disabled
        if (receiveThread != null)
            receiveThread.Abort();
        client.Close();
    }
    // Start is called before the first frame update
    void Start()
    {
        resetStart = false;
        // Start thread to listen UDP messages and set it as background
        receiveThread = new Thread(ReceiveUDP);
        receiveThread.IsBackground = true;
        receiveThread.Start();

        startPos = ScreenCamera.transform.position;
        //startRot = ScreenCamera.transform.rotation.eulerAngles;
        startRot = ScreenCamera.transform.rotation;
    }

    // Update is called once per frame
    void Update()
    {

    }
}
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

    //Vector3 startRot;
    Quaternion startRot;
    //Vector3 remoteStartRot;
    Quaternion remoteStartRot;
    //Vector3 currRot;
    Quaternion currRot;

    // main thread that listens to UDP messages through a defined port
    void ReceiveUDP()
    {
        // create client and set the port
        UdpClient client = new UdpClient(serverPort);
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
                bool resetStart = Convert.ToBoolean(receivedMessage);

                receiveBytes = client.Receive(ref remoteEndPoint);
                // once the message is recieved, encode it as ASCII
                receivedMessage = Encoding.ASCII.GetString(receiveBytes);
                Debug.Log("Position: " + receivedMessage);

                // since it is a vector we do not want the parenthesis
                string filteredMessage = receivedMessage.Substring(1, -2);
                string[] splittedMessage = filteredMessage.Split(",".ToCharArray());
                currPos = new Vector3(float.Parse(splittedMessage[0], CultureInfo.InvariantCulture), float.Parse(splittedMessage[1], CultureInfo.InvariantCulture), float.Parse(splittedMessage[2], CultureInfo.InvariantCulture));

                if (resetStart)
                {
                    remoteStartPos = new Vector3(currPos.x, currPos.y, currPos.z);
                }

                receiveBytes = client.Receive(ref remoteEndPoint);
                // once the message is recieved, encode it as ASCII
                receivedMessage = Encoding.ASCII.GetString(receiveBytes);
                Debug.Log("Rotation: " + receivedMessage);

                //splittedMessage = receivedMessage.Split(" ");
                filteredMessage = receivedMessage.Substring(1, -2);
                splittedMessage = filteredMessage.Split(",".ToCharArray());
                
                currRot = new Quaternion(float.Parse(splittedMessage[0], CultureInfo.InvariantCulture), float.Parse(splittedMessage[1], CultureInfo.InvariantCulture), float.Parse(splittedMessage[2], CultureInfo.InvariantCulture), float.Parse(splittedMessage[3], CultureInfo.InvariantCulture));

                if (resetStart)
                {
                    remoteStartRot = new Quaternion(currRot.x, currRot.y, currRot.z, currRot.w);
                }
            }
            catch (Exception e)
            {
                print("Error: " + e.Message);
            }
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
        if (receivedMessage != null)
        {
            Vector3 remotePosDiff = currPos - remoteStartPos;
            ScreenCamera.transform.position = remotePosDiff + startPos;

            // S'HAURIA DE MIRAR COM FER OPERACIONS AMB QUATERNIONS
            //Vector3 remoteRotDiff = remoteStartRot - currRot;
            Quaternion remoteRotDiff = remoteStartRot * Quaternion.Inverse(currRot);
            //ScreenCamera.transform.rotation = Quaternion.Euler(remoteRotDiff + startRot);
            ScreenCamera.transform.rotation = remoteRotDiff * startRot;
        }
    }
}
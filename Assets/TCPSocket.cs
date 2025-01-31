using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using Newtonsoft.Json;

public class TCPIPServerAsync : MonoBehaviour
{
    Thread SocketThread;
    volatile bool keepReading = false;
    Socket listener;
    Socket handler;
    public FloorGenerator floorGenerator;

    // Buffer for received JSON messages
    private StringBuilder receivedDataBuffer = new StringBuilder();
    private readonly Queue<Action> mainThreadActions = new Queue<Action>();

    void Start()
    {
        Application.runInBackground = true;
        floorGenerator = GetComponent<FloorGenerator>();
        if (floorGenerator == null)
        {
            Debug.LogError("FloorGenerator component not found on this GameObject!");
            return;
        }
        StartServer();
    }

    void StartServer()
    {
        SocketThread = new Thread(NetworkCode);
        SocketThread.IsBackground = true;
        SocketThread.Start();
    }

    void NetworkCode()
    {
        byte[] bytes = new byte[4096]; // Buffer size
        IPAddress ipAddress = IPAddress.Parse("127.0.0.1");
        IPEndPoint localEndPoint = new IPEndPoint(ipAddress, 1102);
        listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        try
        {
            listener.Bind(localEndPoint);
            listener.Listen(10);
            Debug.Log("Waiting for connection...");

            while (true)
            {
                handler = listener.Accept();
                Debug.Log("Client Connected");
                keepReading = true;

                while (keepReading)
                {
                    try
                    {
                        int bytesRec = handler.Receive(bytes);
                        if (bytesRec > 0)
                        {
                            string receivedChunk = Encoding.UTF8.GetString(bytes, 0, bytesRec);
                            receivedDataBuffer.Append(receivedChunk);

                            while (receivedDataBuffer.ToString().Contains("\n"))
                            {
                                int newLineIndex = receivedDataBuffer.ToString().IndexOf("\n");
                                string completeMessage = receivedDataBuffer.ToString().Substring(0, newLineIndex).Trim();
                                receivedDataBuffer.Remove(0, newLineIndex + 1); // Remove processed message

                                ProcessJSONMessage(completeMessage);
                            }
                        }
                        else
                        {
                            Debug.Log("Client disconnected.");
                            keepReading = false;
                        }
                    }
                    catch (SocketException ex)
                    {
                        Debug.LogWarning("Socket exception: " + ex.Message);
                        keepReading = false;
                    }
                }

                // Close the handler socket after the client disconnects
                handler.Shutdown(SocketShutdown.Both);
                handler.Close();
            }
        }
        catch (Exception e)
        {
            Debug.LogError("Socket Error: " + e.Message);
        }
    }

    public int[,] ConvertToIntegerMatrix(float[,] floatMatrix)
    {
        int rows = floatMatrix.GetLength(0);
        int cols = floatMatrix.GetLength(1);
        int[,] intMatrix = new int[rows, cols];

        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                intMatrix[i, j] = Mathf.RoundToInt(floatMatrix[i, j]); // Convert float to int
            }
        }

        return intMatrix;
    }


    void ProcessJSONMessage(string jsonData)
    {
        try
        {
            Debug.Log("Received JSON: " + jsonData);
            ReceivedData received = JsonConvert.DeserializeObject<ReceivedData>(jsonData);

            if (received.matrix != null)
            {
                lock (mainThreadActions)
                {
                    mainThreadActions.Enqueue(() =>
                    {
                        int[,] intMatrix = ConvertToIntegerMatrix(received.matrix);
                        floorGenerator.BuildCityFromMatrix(intMatrix);
                    });
                }
            }

            if (received.AgentPosition != null && received.AgentPosition.Count == 2)
            {
                Vector2Int agentPos = new Vector2Int(received.AgentPosition[0], received.AgentPosition[1]);

                lock (mainThreadActions)
                {
                    mainThreadActions.Enqueue(() =>
                    {
                        // Call SpawnWalker (will update position if walker exists)
                        floorGenerator.SpawnWalker(agentPos);
                    });
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError("Error parsing JSON: " + e.Message);
        }
    }

    void Update()
    {
        lock (mainThreadActions)
        {
            while (mainThreadActions.Count > 0)
            {
                mainThreadActions.Dequeue().Invoke();
            }
        }
    }

    void OnDisable()
    {
        GracefulShutdown();
    }

    void OnApplicationQuit()
    {
        GracefulShutdown();
    }

    private void GracefulShutdown()
    {
        try
        {
            keepReading = false;

            if (handler != null)
            {
                handler.Shutdown(SocketShutdown.Both);
                handler.Close();
                Debug.Log("Client handler socket closed.");
            }

            if (listener != null)
            {
                listener.Close();
                Debug.Log("Listener socket closed.");
            }

            if (SocketThread != null)
            {
                SocketThread.Abort();
                Debug.Log("Socket thread aborted.");
            }
        }
        catch (Exception e)
        {
            Debug.LogError("Error during shutdown: " + e.Message);
        }
    }

    public class ReceivedData
    {
        public int step { get; set; }
        public string message { get; set; }
        public float[,] matrix { get; set; }
        public List<int> AgentPosition { get; set; }
        public List<List<int>> AgentPath { get; set; }
    }
}

using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Networking.Types;
using UnityEngine.Networking.Match;
using System.Collections.Generic;

public class DirectSetup : MonoBehaviour
{
    // Matchmaker related
    List<MatchInfoSnapshot> m_MatchList = new List<MatchInfoSnapshot>();
    bool m_MatchCreated;
    bool m_MatchJoined;
    MatchInfo m_MatchInfo;
    string m_MatchName = "NewRoom";
    NetworkMatch m_NetworkMatch;

    // Connection/communication related
    int m_HostId = -1;
    // On the server there will be multiple connections, on the client this will only contain one ID
    public List<int> m_ConnectionIds = new List<int>();
    byte[] m_ReceiveBuffer;
    string m_NetworkMessage = "Hello world";
    string m_LastReceivedMessage = "";
    NetworkWriter m_Writer;
    NetworkReader m_Reader;
    bool m_ConnectionEstablished;
    [HideInInspector]
    public bool localIsServer = false;
    [HideInInspector]
    public int localNodeId;
    const int k_ServerPort = 25000;
    const int k_MaxMessageSize = 65535;
    public static DirectSetup instance;
    void Awake()
    {
        m_NetworkMatch = gameObject.AddComponent<NetworkMatch>();
        instance = this;
    }

    void Start()
    {
        m_NetworkMatch.baseUri = new System.Uri("https://mm.unet.unity3d.com/");
        m_ReceiveBuffer = new byte[k_MaxMessageSize];
        m_Writer = new NetworkWriter();
        // While testing with multiple standalone players on one machine this will need to be enabled
        Application.runInBackground = true;
    }

    void OnApplicationQuit()
    {
        NetworkTransport.Shutdown();
    }

    void OnGUI()
    {
        if (GUILayout.Button("Clear Log"))
        {
            GameManager.instance.Log("");
        }
        if (GUILayout.Button("Show Current Players Info"))
        {
            GameManager.instance.LogPlayersInfo();
        }
        if (string.IsNullOrEmpty(Application.cloudProjectId))
            GUILayout.Label("You must set up the project first. See the Multiplayer tab in the Service Window");
        else
            GUILayout.Label("Cloud Project ID: " + Application.cloudProjectId);

        if (m_MatchJoined)
            GUILayout.Label("Match joined '" + m_MatchName + "' on Matchmaker server");
        else if (m_MatchCreated)
            GUILayout.Label("Match '" + m_MatchName + "' created on Matchmaker server");

        GUILayout.Label("Connection Established: " + m_ConnectionEstablished);

        if (m_MatchCreated || m_MatchJoined)
        {
            GUILayout.Label("Relay Server: " + m_MatchInfo.address + ":" + m_MatchInfo.port);
            GUILayout.Label("NetworkID: " + m_MatchInfo.networkId + " NodeID: " + m_MatchInfo.nodeId);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Outgoing message:");
            m_NetworkMessage = GUILayout.TextField(m_NetworkMessage);
            GUILayout.EndHorizontal();
            GUILayout.Label("Last incoming message: " + m_LastReceivedMessage);

            if (m_ConnectionEstablished && GUILayout.Button("Send message"))
            {
                m_Writer.SeekZero();
                m_Writer.Write("chat;" + m_NetworkMessage);
                byte error;
                for (int i = 0; i < m_ConnectionIds.Count; ++i)
                {
                    NetworkTransport.Send(m_HostId,
                        m_ConnectionIds[i], 0, m_Writer.AsArray(), m_Writer.Position, out error);
                    if ((NetworkError)error != NetworkError.Ok)
                        Debug.LogError("Failed to send message: " + (NetworkError)error);
                }
            }

            if (GUILayout.Button("Shutdown"))
            {
                m_NetworkMatch.DropConnection(m_MatchInfo.networkId,
                    m_MatchInfo.nodeId, 0, OnConnectionDropped);
            }
        }
        else
        {
            if (GUILayout.Button("Create Room"))
            {
                m_NetworkMatch.CreateMatch(m_MatchName, 4, true, "", "", "", 0, 0, OnMatchCreate);
            }

            if (GUILayout.Button("Join first found match"))
            {
                m_NetworkMatch.ListMatches(0, 1, "", true, 0, 0, (success, info, matches) =>
                {
                    if (success && matches.Count > 0)
                        m_NetworkMatch.JoinMatch(matches[0].networkId, "", "", "", 0, 0, OnMatchJoined);
                });
            }

            if (GUILayout.Button("List rooms"))
            {
                m_NetworkMatch.ListMatches(0, 20, "", true, 0, 0, OnMatchList);
            }

            if (m_MatchList.Count > 0)
            {
                GUILayout.Label("Current rooms:");
            }
            foreach (var match in m_MatchList)
            {
                if (GUILayout.Button(match.name))
                {
                    m_NetworkMatch.JoinMatch(match.networkId, "", "", "", 0, 0, OnMatchJoined);
                }
            }
        }
    }

    public void OnConnectionDropped(bool success, string extendedInfo)
    {
        GameManager.instance.Log("Connection has been dropped on matchmaker server");
        NetworkTransport.Shutdown();
        m_HostId = -1;
        m_ConnectionIds.Clear();
        m_MatchInfo = null;
        m_MatchCreated = false;
        m_MatchJoined = false;
        m_ConnectionEstablished = false;
        GameManager.instance.EndGame();
    }

    public virtual void OnMatchCreate(bool success, string extendedInfo, MatchInfo matchInfo)
    {
        if (success)
        {
            GameManager.instance.Log("Create match succeeded");
            Utility.SetAccessTokenForNetwork(matchInfo.networkId, matchInfo.accessToken);
            m_MatchCreated = true;
            m_MatchInfo = matchInfo;
            StartServer(matchInfo.address, matchInfo.port, matchInfo.networkId, matchInfo.nodeId);
        }
        else
        {
            Debug.LogError("Create match failed: " + extendedInfo);
        }
    }

    public void OnMatchList(bool success, string extendedInfo, List<MatchInfoSnapshot> matches)
    {
        if (success && matches != null)
        {
            m_MatchList = matches;
        }
        else if (!success)
        {
            Debug.LogError("List match failed: " + extendedInfo);
        }
    }

    // When we've joined a match we connect to the server/host
    public virtual void OnMatchJoined(bool success, string extendedInfo, MatchInfo matchInfo)
    {
        if (success)
        {
            GameManager.instance.Log("Join match succeeded");
            Utility.SetAccessTokenForNetwork(matchInfo.networkId, matchInfo.accessToken);
            m_MatchJoined = true;
            m_MatchInfo = matchInfo;
            GameManager.instance.Log("Connecting to Address:" + matchInfo.address +
                " Port:" + matchInfo.port +
                " NetworKID: " + matchInfo.networkId +
                " NodeID: " + matchInfo.nodeId);
            ConnectThroughRelay(matchInfo.address, matchInfo.port, matchInfo.networkId,
                matchInfo.nodeId);
        }
        else
        {
            Debug.LogError("Join match failed: " + extendedInfo);
        }
    }

    void SetupHost(bool isServer)
    {
        GameManager.instance.Log("SetupHost, Initializing network transport");
        NetworkTransport.Init();
        var config = new ConnectionConfig();
        config.AddChannel(QosType.Reliable);
        config.AddChannel(QosType.Unreliable);
        var topology = new HostTopology(config, GameManager.instance.playerNum);
        if (isServer)
            m_HostId = NetworkTransport.AddHost(topology, k_ServerPort);
        else
            m_HostId = NetworkTransport.AddHost(topology);
    }

    void StartServer(string relayIp, int relayPort, NetworkID networkId, NodeID nodeId)
    {
        GameManager.instance.Log("StartServer nodeId=" + nodeId.ToString());
        this.localIsServer = true;
        this.localNodeId = (int)nodeId;
        SetupHost(true);
        byte error;
        NetworkTransport.ConnectAsNetworkHost(
            m_HostId, relayIp, relayPort, networkId, Utility.GetSourceID(), nodeId, out error);
    }

    void ConnectThroughRelay(string relayIp, int relayPort, NetworkID networkId, NodeID nodeId)
    {
        this.localIsServer = false;
        this.localNodeId = (int)nodeId;
        SetupHost(false);
        byte error;
        NetworkTransport.ConnectToNetworkPeer(
            m_HostId, relayIp, relayPort, 0, 0, networkId, Utility.GetSourceID(), nodeId, out error);
    }

    void Update()
    {
        if (m_HostId == -1)
            return;
        var networkEvent = NetworkEventType.Nothing;
        int connectionId;
        int channelId;
        int receivedSize;
        byte error;

        // Get events from the relay connection
        networkEvent = NetworkTransport.ReceiveRelayEventFromHost(m_HostId, out error);
        if (networkEvent == NetworkEventType.ConnectEvent)
            GameManager.instance.Log("Relay server connected");
        if (networkEvent == NetworkEventType.DisconnectEvent)
            GameManager.instance.Log("Relay server disconnected");

        do
        {
            // Get events from the server/client game connection
            networkEvent = NetworkTransport.ReceiveFromHost(m_HostId, out connectionId, out channelId,
                m_ReceiveBuffer, (int)m_ReceiveBuffer.Length, out receivedSize, out error);
            if ((NetworkError)error != NetworkError.Ok)
            {
                Debug.LogError("Error while receiveing network message: " + (NetworkError)error);
            }

            switch (networkEvent)
            {
                case NetworkEventType.ConnectEvent:
                    {
                        GameManager.instance.Log("Connected through relay, ConnectionID:" + connectionId +
                            " ChannelID:" + channelId);
                        m_ConnectionEstablished = true;
                        m_ConnectionIds.Add(connectionId);

                        PlayerInitializationData data = new PlayerInitializationData();
                        data.isServer = localIsServer;
                        data.playerID = localNodeId;
                        GameManager.instance.ReceivePlayerInitializationData(data, true);
                        //after join match send a init event to server
                        m_Writer.SeekZero();
                        m_Writer.Write("init;" + (data.isServer ? "t;" : "n;") + data.playerID.ToString());
                        byte error1;
                        if (m_ConnectionIds.Count == 1)
                        {
                            NetworkTransport.Send(m_HostId,
                                m_ConnectionIds[0], 0, m_Writer.AsArray(), m_Writer.Position, out error1);
                            if ((NetworkError)error1 != NetworkError.Ok)
                                Debug.LogError("Failed to send message: " + (NetworkError)error1);
                        }
                        else
                        {
                            GameManager.instance.Log("ConnectThroughRelay m_ConnectionIds.Count != 1 " + m_ConnectionIds.Count);
                        }
                        break;
                    }
                case NetworkEventType.DataEvent:
                    {
                        /*GameManager.instance.Log("Data event, ConnectionID:" + connectionId +
                            " ChannelID: " + channelId +
                            " Received Size: " + receivedSize);*/
                        m_Reader = new NetworkReader(m_ReceiveBuffer);
                        string allString = m_Reader.ReadString();
                        //GameManager.instance.Log("allString:" + allString);
                        string[] stringSeparators = new string[] { ";" };
                        string[] result = allString.Split(stringSeparators, System.StringSplitOptions.None);
                        if (result.Length > 0)
                        {
                            switch (result[0])
                            {
                                case "chat":
                                    m_LastReceivedMessage = result[1];
                                    break;
                                case "init":
                                    //m_Writer.Write("init;"+(data.isServer?"t;":"n;")+data.playerID.ToString();
                                    PlayerInitializationData data = new PlayerInitializationData();
                                    data.isServer = result[1] == "t" ? true : false;
                                    data.playerID = int.Parse(result[2]);
                                    GameManager.instance.ReceivePlayerInitializationData(data);
                                    if (localIsServer && GameManager.instance.CanStartGame())
                                    {
                                        GameManager.instance.Log("send StartGame");
                                        m_Writer.SeekZero();
                                        string s = "start";
                                        foreach (PlayerDataBuffer pPlayerDataBuffer in GameManager.instance.allPlayersDataBuffer)
                                        {
                                            s += ";" + pPlayerDataBuffer.player.playerID.ToString() + ";" + (pPlayerDataBuffer.player.isServer ? "t" : "f") ;
                                        }
                                        m_Writer.Write(s);
                                        byte error2;
                                        for (int i = 0; i < m_ConnectionIds.Count; i++)
                                        {
                                            NetworkTransport.Send(m_HostId,
                                                m_ConnectionIds[i], 0, m_Writer.AsArray(), m_Writer.Position, out error2);
                                            if ((NetworkError)error2 != NetworkError.Ok)
                                                Debug.LogError("Failed to send message: " + (NetworkError)error2);
                                        }
                                        GameManager.instance.StartGame();
                                    }
                                    break;
                                case "start":
                                    if (result.Length > 1)
                                    {
                                        int extraInitNum = (result.Length - 1) / 2;
                                        for (int i = 0; i < extraInitNum; i++)
                                        {
                                            PlayerInitializationData extraInitData = new PlayerInitializationData();
                                            extraInitData.isServer = result[i * 2 + 2] == "t" ? true : false;
                                            extraInitData.playerID = int.Parse(result[i * 2 + 1]);
                                            GameManager.instance.ReceivePlayerInitializationData(extraInitData);
                                        }
                                    }
                                    GameManager.instance.StartGame();
                                    break;
                                case "game":
                                    //from one client to server
                                    if (localIsServer)
                                    {
                                        m_Writer.SeekZero();
                                        m_Writer.Write(allString);
                                        byte error3;
                                        //send this to other clients except the emitter
                                        for (int i = 0; i < m_ConnectionIds.Count; ++i)
                                        {
                                            if (m_ConnectionIds[i] == connectionId)
                                                continue;
                                            //GameManager.instance.Log("host resend game to m_ConnectionIds[i]");
                                            NetworkTransport.Send(m_HostId,
                                                m_ConnectionIds[i], 1, m_Writer.AsArray(), m_Writer.Position, out error3);
                                        }
                                    }
                                    int inputDataLength = (result.Length - 2) / 3;
                                    if (inputDataLength > 0)
                                    {
                                        PlayerInputDataPack pack = new PlayerInputDataPack();
                                        pack.playerID = int.Parse(result[1]);
                                        pack.myInputData = new List<PlayerInputData>();
                                        for (int i = 0; i < inputDataLength; i++)
                                        {
                                            PlayerInputData playerInputData = new PlayerInputData();
                                            playerInputData.turn = int.Parse(result[i * 3 + 2]);
                                            playerInputData.X = int.Parse(result[i * 3 + 3]);
                                            playerInputData.Y = int.Parse(result[i * 3 + 4]);
                                            pack.myInputData.Add(playerInputData);
                                        }
                                        GameManager.instance.ReceiveInputDataPack(pack);
                                    }
                                    break;
                                case "scoreserver":
                                    //this comes from server
                                    //GameManager.instance.Log("scoreserver");
                                    print("scoreserver"+allString);
                                    if (!localIsServer)
                                    {
                                        if (result.Length > 1)
                                        {
                                            List<PlayerScoreData> scores = new List<PlayerScoreData>();
                                            for (int i = 0; i < (result.Length - 1) / 2; i++)
                                            {
                                                PlayerScoreData score = new PlayerScoreData();
                                                score.ID = int.Parse(result[i * 2 + 1]);
                                                score.score = int.Parse(result[i * 2 + 2]);
                                                scores.Add(score);
                                            }
                                            GameManager.instance.ShowScore(scores);
                                        }
                                    }
                                    break;
                                case "scoreclient":
                                    //this comes from client
                                    //GameManager.instance.Log("scoreclient");
                                    if (localIsServer)
                                    {
                                        GameManager.instance.ModifyAndSendScore(int.Parse(result[1]), int.Parse(result[2]));
                                        SendScoreEventServer();
                                        GameManager.instance.ShowScore(GameManager.instance.scores);
                                    }
                                    break;
                                case "ball":
                                    //this comes from server
                                    if (!localIsServer)
                                    {
                                        GameManager.instance.RegisteBall(int.Parse(result[1]), int.Parse(result[2]), int.Parse(result[3]));
                                    }
                                    break;
                                default:
                                    break;
                            }
                        }
                        break;
                    }
                case NetworkEventType.DisconnectEvent:
                    {
                        GameManager.instance.Log("Connection disconnected, ConnectionID:" + connectionId);
                        GameManager.instance.EndGame();
                        break;
                    }
                case NetworkEventType.Nothing:
                    break;
            }
        } while (networkEvent != NetworkEventType.Nothing);
    }

    public void SendDataPack(PlayerInputDataPack data)
    {
        if (data.myInputData == null || data.myInputData.Count == 0)
        {
            //GameManager.instance.Log("invalid SendDataPack!");
            return;
        }
        m_Writer.SeekZero();
        string content = "game;" + data.playerID.ToString();
        foreach (PlayerInputData inputData in data.myInputData)
        {
            content += ";"+inputData.turn + ";" + inputData.X + ";" + inputData.Y;
        }
        m_Writer.Write(content);
        byte error;
        //use unreliable channel
        NetworkTransport.Send(m_HostId,
                m_ConnectionIds[0], 1, m_Writer.AsArray(), m_Writer.Position, out error);
    }

    public void SendScoreEventServer()
    {
        //broatcast scores to all clients, and also refresh direcly local client;
        m_Writer.SeekZero();
        string s = "scoreserver";
        foreach(PlayerScoreData score in GameManager.instance.scores)
        {
            s += ";"+score.ID + ";" + score.score;
        }
        m_Writer.Write(s);
        byte error;
        for (int i = 0; i < m_ConnectionIds.Count; ++i)
        {
            NetworkTransport.Send(m_HostId,
                m_ConnectionIds[i], 0, m_Writer.AsArray(), m_Writer.Position, out error);
            if ((NetworkError)error != NetworkError.Ok)
                Debug.LogError("Failed to send message: " + (NetworkError)error);
        }
    }

    public void SendScoreEventClient(int score)
    {
        //call the server to update score and do the SendScoreEventServer function
        m_Writer.SeekZero();
        m_Writer.Write("scoreclient;" + localNodeId + ";" + score);
        byte error;
        NetworkTransport.Send(m_HostId,
                m_ConnectionIds[0], 0, m_Writer.AsArray(), m_Writer.Position, out error);
        if ((NetworkError)error != NetworkError.Ok)
            Debug.LogError("Failed to send message: " + (NetworkError)error);
    }

    public void SendLureBall(int turn, int x, int y)
    {
        //only in server
        if (localIsServer)
        {
            GameManager.instance.Log("SendLureBall ");
            m_Writer.SeekZero();
            m_Writer.Write("ball;"+ turn+";"+x+";"+y);
            byte error;
            //send this to other clients except the emitter
            for (int i = 0; i < m_ConnectionIds.Count; ++i)
            {
                NetworkTransport.Send(m_HostId,
                    m_ConnectionIds[i], 0, m_Writer.AsArray(), m_Writer.Position, out error);
                if ((NetworkError)error != NetworkError.Ok)
                    Debug.LogError("Failed to send message: " + (NetworkError)error);
            }
            GameManager.instance.RegisteBall(turn, x, y);
        }
    }
}
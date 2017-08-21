using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
[System.Serializable]
public class PlayerScoreData
{
    public int ID;
    public int score;
}

public class PlayerInputData
{
    public int turn;
    public int X;
    public int Y;
}

public class PlayerInputDataPack
{
    public int playerID;
    public List<PlayerInputData> myInputData;//data of 3 turns
}

public class PlayerInitializationData
{
    public int playerID;
    public bool isServer;
    public int colorIndex;
}

public class PlayerDataBuffer
{
    public PlayerInitializationData player;
    public List<PlayerInputData> myData;
}

public class GameManager : MonoBehaviour {

    [SerializeField]
    private GameObject ClickFeedbackPrefab;
    [SerializeField]
    private GameObject BallPrefab;
    public int playerNum = 4;
    public static GameManager instance;
    [SerializeField]
    private Text turnText;
    [SerializeField]
    private Text startTimeText;
    [SerializeField]
    private Text logText;
    [SerializeField]
    private Text ScoreText;
    [SerializeField]
    private Color[] myColors;
    private double currentTurnStartTime;
    [SerializeField]
    private double turnTime = 0.1;//100ms
    private int currentTurn=-1;
    private bool gameStarted = false;
    private List<PlayerInputData> localPlayerInputData;
    [HideInInspector]
    public List<PlayerDataBuffer> allPlayersDataBuffer;
    private List<PlayerInputData> ballFromServerBuffer;
    [SerializeField]//debug 
    private List<BallTarget> balls;
    [HideInInspector]
    public List<PlayerScoreData> scores = null;//only in server
    private void Awake()
    {
        instance = this;
    }

    private void Start()
    {
        localPlayerInputData = new List<PlayerInputData>();
        allPlayersDataBuffer = new List<PlayerDataBuffer>();
        ballFromServerBuffer = new List<PlayerInputData>();
        balls = new List<BallTarget>();
    }

    private void Update () {
		if(gameStarted && Network.time - currentTurnStartTime > turnTime)
        {
            ProcessTurn();
        }
	}

    private void ProcessTurn()
    {
        currentTurnStartTime += turnTime;

        //process balls
        List<PlayerInputData> removeBallList = new List<PlayerInputData>();
        foreach (PlayerInputData tpBuffer in ballFromServerBuffer)
        {
            if (tpBuffer.turn <= currentTurn - 2)
            {
                float x = (float)Screen.width * (float)tpBuffer.X / 100f;
                float y = (float)Screen.height * (float)tpBuffer.Y / 100f;
                Vector3 pos = InputManager.instance.ScreenPosToGamePos(new Vector2(x, y));
                GameObject go = (GameObject)Instantiate(BallPrefab, pos, Quaternion.identity);
                balls.Add(go.GetComponent<BallTarget>());
                removeBallList.Add(tpBuffer);
            }
        }
        foreach (PlayerInputData tpRemoveBallData in removeBallList)
        {
            ballFromServerBuffer.Remove(tpRemoveBallData);
        }

        //read local and remote data
        //remove useless data
        foreach (PlayerDataBuffer tpPlayerDatabuffer in allPlayersDataBuffer)
        {
            List<PlayerInputData> removeList = new List<PlayerInputData>();
            foreach (PlayerInputData tpData in tpPlayerDatabuffer.myData)
            {
                if (tpData.turn >= 0 && tpData.turn == currentTurn - 2)
                {
                    SetText(startTimeText,"last play "+tpPlayerDatabuffer.player.playerID+", turn:"+ tpData.turn+" at turn:"+ currentTurn);
                    float x = (float)Screen.width * (float)tpData.X / 100f;
                    float y = (float)Screen.height * (float)tpData.Y / 100f;
                    Vector3 pos = InputManager.instance.ScreenPosToGamePos(new Vector2(x, y));
                    GameObject go = (GameObject)Instantiate(ClickFeedbackPrefab, pos, Quaternion.identity);
                    ParticleSystem.MainModule main = go.GetComponent<ParticleSystem>().main;
                    main.startColor = myColors[tpPlayerDatabuffer.player.colorIndex];
                    //set time past for (currentTurn-1-tpData.turn) turns after

                    //check collision
                    for(int i= balls.Count-1; i>=0; i--)
                    {
                        BallTarget ball = balls[i];
                        if (ball != null)
                        {
                            Vector3 distance = ball.transform.position - pos;
                            distance.y = 0;
                            if (distance.magnitude < 3)
                                ball.OnAttack(tpPlayerDatabuffer.player.playerID == DirectSetup.instance.localNodeId);
                        }
                    }
                }
                if (tpData.turn < currentTurn - 2)
                {
                    removeList.Add(tpData);
                }
            }
            foreach (PlayerInputData tpRemoveData in removeList)
            {
                tpPlayerDatabuffer.myData.Remove(tpRemoveData);
            }
            removeList.Clear();
        }
        //sent pack to remote
        PlayerInputDataPack pack = CreateLocalInputDataPack();
        DirectSetup.instance.SendDataPack(pack);
        //call the server to call all client to create a ball some time
        if (DirectSetup.instance.localIsServer)
        {
            if (Random.value < 0.06f)
            {
                int x = Random.Range(5, 98);
                int y = Random.Range(2, 98);
                DirectSetup.instance.SendLureBall(currentTurn, x, y);
            }
        }
        SetText(turnText, "Turn: " + currentTurn.ToString());
        currentTurn++;
    }

    public void RegisteBall(int turn, int x, int y)
    {
        PlayerInputData data = new PlayerInputData();
        data.turn = turn;
        data.X = x;
        data.Y = y;
        ballFromServerBuffer.Add(data);
    }

    public void DestoryBall(BallTarget ball)
    {
        //add score for player
        //parameter ball now is useless, will later use its score
        balls.Remove(ball);
        if (DirectSetup.instance.localIsServer)
        {
            ModifyAndSendScore(DirectSetup.instance.localNodeId, 8);
            DirectSetup.instance.SendScoreEventServer();
            GameManager.instance.ShowScore(scores);
        }
        else
            DirectSetup.instance.SendScoreEventClient(8);
    }

    public void ShowScore(List<PlayerScoreData> pScores)
    {
        string s = "";
        foreach (PlayerScoreData score in pScores)
        {
            s += score.ID + ": " + score.score + "\n";
        }
        ScoreText.text = s;
    }

    public void ModifyAndSendScore(int ID, int modifier)
    {
        foreach(PlayerScoreData score in scores)
        {
            if (score.ID == ID)
            {
                score.score += modifier;
                break;
            }
        }
        DirectSetup.instance.SendScoreEventServer();
    }

    public bool CanStartGame()
    {
        return allPlayersDataBuffer.Count == playerNum;
    }

    public void StartGame()
    {
        //init allPlayersDataCache!
        //enable the controller
        InputManager.instance.ReceiveInput = true;
        //synchronize turn and timers
        currentTurn = 0;
        currentTurnStartTime = Network.time;
        gameStarted = true;
        //allocate colors for players
        //init scores for players
        allPlayersDataBuffer.Sort((x, y) => x.player.playerID.CompareTo(y.player.playerID));
        for(int i =0; i< allPlayersDataBuffer.Count; i++)
        {
            allPlayersDataBuffer[i].player.colorIndex = i;
            if (scores != null){
                PlayerScoreData pPlayerScoreData = new PlayerScoreData();
                pPlayerScoreData.ID = allPlayersDataBuffer[i].player.playerID;
                pPlayerScoreData.score = 0;
                scores.Add(pPlayerScoreData);
            }
        }
        SetText(startTimeText, "start game at " + currentTurnStartTime.ToString());
    }

    public void EndGame()
    {
        InputManager.instance.ReceiveInput = false;
        currentTurn = -1;
        gameStarted = false;
        SetText(startTimeText, "EndGame at " + Network.time.ToString());
        SetText(turnText, "Turn: ");
        localPlayerInputData = new List<PlayerInputData>();
        allPlayersDataBuffer = new List<PlayerDataBuffer>();
}

    public void ReceiveInputDataPack(PlayerInputDataPack pack)
    {
        //for both local and remote
        //insert data inside into allPlayersDataCache
        Log("ReceiveInputDataPack from node ID: "+ pack.playerID);
        foreach (PlayerDataBuffer tpPlayerDataBuffer in allPlayersDataBuffer)
        {
            if (tpPlayerDataBuffer.player.playerID == pack.playerID)
            {
                foreach (PlayerInputData tpReceivedData in pack.myInputData)
                {
                    if (tpReceivedData == null)
                        continue;
                    int turn = tpReceivedData.turn;
                    bool isDuplicate = false;
                    foreach (PlayerInputData tpDataBuffer in tpPlayerDataBuffer.myData)
                    {
                        if (tpDataBuffer == null || tpDataBuffer.turn == turn)
                        {
                            isDuplicate = true;
                            break;
                        }
                    }
                    if (!isDuplicate)
                    {
                        Log("Player "+tpPlayerDataBuffer.player.playerID+" add data at Turn:" + tpReceivedData.turn + ", " + tpReceivedData.X + ", " + tpReceivedData.Y);
                        tpPlayerDataBuffer.myData.Add(tpReceivedData);
                    }
                }
            }
        }
    }

    public void ReceivePlayerInitializationData(PlayerInitializationData data, bool isLocal = false)
    {
        //For both local and remote, if all player is ready, start game
        Log("ReceivePlayerInitializationData");
        foreach (PlayerDataBuffer tpPlayerDataCache in allPlayersDataBuffer)
        {
            if (tpPlayerDataCache.player.playerID == data.playerID)
            {
                Log("allPlayersDataBuffer already had nodeID = " + data.playerID);
                return;
            }
        }
        if (isLocal)
        {
            //set local player
            if (data.isServer)
            {
                //set server only things
                scores = new List<PlayerScoreData>();
            }
        }
        PlayerInitializationData newPlayerInitializationData = new PlayerInitializationData();
        newPlayerInitializationData.isServer = data.isServer;
        newPlayerInitializationData.playerID = data.playerID;
        PlayerDataBuffer newPlayerDataBuffer = new PlayerDataBuffer();
        newPlayerDataBuffer.player = newPlayerInitializationData;
        newPlayerDataBuffer.myData = new List<PlayerInputData>();
        allPlayersDataBuffer.Add(newPlayerDataBuffer);
    }

    private PlayerInputDataPack CreateLocalInputDataPack()
    {
        //make a local InputDataPack, and remove useless PlayerInputData from localPlayerInputData
        PlayerInputDataPack pack = new PlayerInputDataPack();
        pack.playerID = DirectSetup.instance.localNodeId;
        List<PlayerInputData> removeLocalPlayerInputData = new List<PlayerInputData>();
        //int lowestTurn = currentTurn;
        foreach (PlayerInputData localData in localPlayerInputData)
        {
            if (localData.turn > currentTurn || localData.turn < currentTurn - 2)
                removeLocalPlayerInputData.Add(localData);
        }
        foreach (PlayerInputData tpData in removeLocalPlayerInputData)
        {
            localPlayerInputData.Remove(tpData);
        }
        removeLocalPlayerInputData.Clear();
        if (localPlayerInputData.Count > 0)
        {
            pack.myInputData = localPlayerInputData.GetRange(0, localPlayerInputData.Count);
        }
        else
        {
            pack.myInputData = new List<PlayerInputData>();
        }
        return pack;
    }

    public void CreateAndAddLocalPlayerInputDataToCurrentTurn(int ratioX, int ratioY)
    {
        PlayerInputData data = new PlayerInputData();
        data.turn = currentTurn;
        data.X = ratioX;
        data.Y = ratioY;
        //each turn CAN have multiple input
        localPlayerInputData.Add(data);
        foreach (PlayerDataBuffer tpPlayerDataCache in allPlayersDataBuffer)
        {
            if (tpPlayerDataCache.player.playerID == DirectSetup.instance.localNodeId)
            {
                tpPlayerDataCache.myData.Add(data);
            }
        }
    }

    private void SetText(Text txt, string str)
    {
        txt.text = str;
    }

    public void Log(string str)
    {
        if (str == "")
        {
            logText.text = "";
        }
        else
        {
            if(logText.text.Length>500)
                logText.text = str;
            else
                logText.text += "\n" + str;
        }
    }

    public void LogPlayersInfo()
    {
        string s = "local:"+ DirectSetup.instance.localNodeId + "\n";
        foreach(PlayerDataBuffer buffer in allPlayersDataBuffer)
        {
            s += "Pid:" + buffer.player.playerID + " isS:" + buffer.player.isServer + " col:" + buffer.player.colorIndex+" data:";
            foreach (PlayerInputData data in buffer.myData)
            {
                s += " t:" + data.turn + " x:" + data.X + " y:" + data.Y;
            }
            s += "\n";
        }
        logText.text = s;
    }
}

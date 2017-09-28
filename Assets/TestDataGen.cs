﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;
using UnityStandardAssets.Vehicles.Car;
using System.Net.Sockets;
using System.Threading;
using System.Net;
using System.Text;


public class TestDataGen : MonoBehaviour
{

    //const string ROAD_NAME = "road";
    public const int ROAD_COUNT = 500;

    public const float ROAD_SCALE_X = 8;
    public const float ROAD_SCALE_Y = 0.5F;
    public const float ROAD_SCALE_Z = 1.25F;
    public const float START_POS_X = ROAD_SCALE_X / 2 + 250;
    public const float START_POS_Y = 0;
    public const float START_POS_Z = ROAD_SCALE_Z / 2 + 250;
    public const float START_ROT_X = 0;
    public const float START_ROT_Y = 0;
    public const float START_ROT_Z = 0;
    public const float ROT = 4.9f;
    public const float OVERLAP = 0.5F;
    public const int MAP_SCALE = 500;
    public const int ROLLBACK = 50;
    public const bool SAVE_ROAD = true;
    public int ROAD_DIFFICULTY = 3; // (1:Easy 2:Normal 3:Difficult)

    public bool isGenFinished = false;

    const int ROAD_OCCUPIED_TABLE_SIZE = 100 * (int)(ROAD_SCALE_Z * 50);

    bool[,] ROAD_OCCUPIED_TABLE;

    bool isGen = false;
    int screanShotCnt = 0;

    List<List<Vector2>> ROAD_OCCUPIED_LIST;

    Vector3 NEXT_POS, NEXT_ROT;

    List<Vector2> RandList;

    
    private int width = Screen.width;
    private int height = Screen.height;
    private TestData[] testData, sendTestData;
    public int testDataCnt;

    static Socket listener;
    static string LocalHost = "192.168.1.35";
    static int port = 8787;
    static string data = null;
    bool isFinish = false;

    Texture2D texture2d;

    AutoDrive autoDrive;
    CarController carController;

    private class TestData
    {
        public TestData(byte[] screenShot, string direction, float speed)
        {
            this.screenShot = screenShot;
            this.direction = direction;
            this.speed = speed;
        }

        public byte[] screenShot;
        public string direction;
        public float speed;
    }



    // Use this for initialization
    void Start()
    {
        (new Thread(ServerListening)).Start();
        autoDrive = GameObject.Find("Car").GetComponent<AutoDrive>();
        carController = GameObject.Find("Car").GetComponent<CarController>();
    }

    // Update is called once per frame
    void Update()
    {
        if(Input.GetKeyDown(KeyCode.R))
        {
            ClickBtn();
            screanShotCnt = 1;
        }
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            isFinish = true;
            Application.Quit();
        }
        if (!isGen)
        {
            ClickBtn();
            isGen = true;
        }
        if(screanShotCnt % 6 == 0)
            StartCoroutine(ScreenShot());
        screanShotCnt++;
    }
    
    public void ResetTestData()
    {
        screanShotCnt = 0;
        testDataCnt = 0;
        testData = new TestData[400];
        testData[testDataCnt] = null;
    }

    public void SendTestData()
    {
        sendTestData = testData;
        ResetTestData();
    }

    IEnumerator ScreenShot()
    {
        texture2d = new Texture2D(width, height, TextureFormat.RGB24, false);
        yield return new WaitForEndOfFrame();
        texture2d.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        texture2d.Apply();
        Texture2D newTexture2d = new Texture2D(50, 50, TextureFormat.RGB24, false);
        for (int i = 0; i < 50; i++)
            for (int j = 0; j < 50; j++)
                newTexture2d.SetPixel(i, j, texture2d.GetPixel(i * width / 50, j * height / 50));
        newTexture2d.Apply();
        Destroy(texture2d);
        texture2d = null;
        testData[testDataCnt++] = new TestData(newTexture2d.EncodeToPNG(),
                                                GameObject.Find("Car").GetComponent<AutoDrive>().direction,
                                                GameObject.Find("Car").GetComponent<CarController>().CurrentSpeed);
        Destroy(newTexture2d);
        newTexture2d = null;
        System.GC.Collect();
        testData[testDataCnt] = null;
    }

    private void ServerListening()
    {
        sendTestData = null;
        while (true)
        {
            listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                listener.Bind(new IPEndPoint(IPAddress.Parse(LocalHost), port));
                listener.Listen(100);
                while (true)
                {
                    Debug.Log("Waiting for client connect.");
                    Socket handler = listener.Accept();
                    byte[] bytes = new byte[1024];
                    int count = handler.Receive(bytes);
                    data = Encoding.ASCII.GetString(bytes, 0, count);
                    if (data == "CNN_CarDriving_Client")
                    {
                        Debug.Log("Connect!");
                        while (true)
                        {
                            if (isFinish)
                            {
                                handler.Send(new byte[2]);
                                return;
                            }
                            if (sendTestData != null)
                            {
                                for (int i = 0; i < 400; i++)
                                {
                                    if (sendTestData[i] == null)
                                    {
                                        // Using for online training.
                                        handler.Send(new byte[1]);
                                        autoDrive.training = true;
                                        Debug.Log("Waiting for training.");
                                        handler.Receive(bytes);
                                        autoDrive.training = false;
                                        Debug.Log("Finish.");
                                        // --------------------------
                                        break;
                                    }
                                    handler.Send(sendTestData[i].screenShot);
                                    sendTestData[i].screenShot = null;
                                    handler.Receive(bytes);
                                    handler.Send(Encoding.ASCII.GetBytes(sendTestData[i].speed + " " + sendTestData[i].direction));
                                    handler.Receive(bytes);
                                    sendTestData[i] = null;
                                }
                                sendTestData = null;
                                System.GC.Collect();
                            }
                        }
                    }
                    handler.Shutdown(SocketShutdown.Both);
                    handler.Close();
                }
            }
            catch (System.Exception e)
            {
                listener.Shutdown(SocketShutdown.Both);
                listener.Close();
                Debug.Log(e.Message.ToString());
            }
        }
    }

    public void ClickBtn()
    {
        ROAD_DIFFICULTY = Random.Range(1, 3);
        isGenFinished = false;

        initVar();

        RoadGen();
        GameObject.Find("Car").GetComponent<AutoDrive>().ResetCar();
        GameObject.Find("Car").GetComponent<AutoDrive>().ResetSpeed();

        ResetTestData();

        isGenFinished = true;
    }

    void RoadGen()
    {
        GameObject road, plane;
        Vector2 rand = new Vector2();

        int RollBackCount = 1, LastRollBackIndex = 0, randIndex = 0;
        road = GameObject.Find("road (1)");
        plane = GameObject.Find("plane (1)");
        for (int i = 1; i < ROAD_COUNT;)
        {
            rand = RandList[randIndex++];
            int cnt = 0;
            while (!nextRoadAvailable(road, rand) && cnt < 3)
            {
                rand.x = (rand.x == 2 ? 0 : rand.x + 1);
                cnt++;
            }
            if (cnt == 3)
            {  // Road Not Available => RollBack
                int index = RollBack(road, RollBackCount++);
                randIndex = getRandIndex(index + 1);
                i = index + 1;
                LastRollBackIndex = getRandIndex(getRoadNum(road));
                road = GameObject.Find("road (" + (index + 1).ToString() + ")");
                plane = GameObject.Find("plane (" + (index + 1).ToString() + ")");
            }
            else
            {
                if (getRandIndex(getRoadNum(road)) > LastRollBackIndex)
                    RollBackCount = 1;
                for (int k = 0; k < rand.y; k++)
                {
                    if (i + k > ROAD_COUNT) break;
                    NEXT_ROT.y = (rand.x == 0 ? NEXT_ROT.y : (rand.x == 1 ? NEXT_ROT.y + ROT : NEXT_ROT.y - ROT));
                    NEXT_POS = new Vector3(NEXT_POS.x + OVERLAP * Mathf.Sin(NEXT_ROT.y * Mathf.PI / 180), NEXT_POS.y, NEXT_POS.z + OVERLAP * Mathf.Sin((90 - NEXT_ROT.y) * Mathf.PI / 180));
                    road = GameObject.Find("road (" + (i + k).ToString() + ")");
                    plane = GameObject.Find("plane (" + (i + k).ToString() + ")");
                    road.transform.localScale = new Vector3(ROAD_SCALE_X, ROAD_SCALE_Y, ROAD_SCALE_Z);
                    road.transform.position = NEXT_POS;
                    road.transform.eulerAngles = NEXT_ROT;
                    plane.transform.position = NEXT_POS;
                    plane.transform.eulerAngles = new Vector3(90, NEXT_ROT.y, 0);
                    MarkTable(road);
                }
                i += (int)rand.y;
                road = GameObject.Find("road (" + i.ToString() + ")");
                plane = GameObject.Find("plane (" + i.ToString() + ")");
            }
        }

        for (int i = 0; i < 20; i++)
        {
            road = GameObject.Find("road (" + (ROAD_COUNT + i).ToString() + ")");
            plane = GameObject.Find("plane (" + (ROAD_COUNT + i).ToString() + ")");
            road.transform.position = new Vector3(0, 0, 0);
            plane.transform.position = new Vector3(0, 0, 0);
        }
    }

    int RollBack(GameObject road, int RollBackCount)
    {
        int RoadCount = 0;
        for (int i = 0; i < RollBackCount; i++)
            RoadCount += (int)RandList[getRandIndex(getRoadNum(road) - 1) - i].y;

        updateRand(getRandIndex(getRoadNum(road) - 1) - RollBackCount);

        //if((getRoadNum(road) - RoadCount) < 0)
        //{
        //    Debug.Log("RollBackCount:" + RollBackCount.ToString());
        //    Debug.Log("getRoadNum(road):" + getRoadNum(road).ToString());
        //    Debug.Log("(getRoadNum(road) - RoadCount):" + (getRoadNum(road) - RoadCount).ToString());
        //}

        for (int i = getRoadNum(road); i > (getRoadNum(road) - RoadCount); i--)
        {
            for (int k = 0; k < ROAD_OCCUPIED_LIST[i].Count; k++)
                ROAD_OCCUPIED_TABLE[(int)ROAD_OCCUPIED_LIST[i][k].x, (int)ROAD_OCCUPIED_LIST[i][k].y] = false;
            ROAD_OCCUPIED_LIST[i].Clear();
        }

        road = GameObject.Find("road (" + (getRoadNum(road) - RoadCount).ToString() + ")");
        NEXT_POS = road.transform.position;
        NEXT_ROT = road.transform.eulerAngles;

        return getRoadNum(road);
    }

    void MarkTable(GameObject road)
    {
        float min_x, max_x, min_z, max_z;
        min_x = (road.transform.position.x - (Mathf.Abs(ROAD_SCALE_Z * Mathf.Sin(NEXT_ROT.y * Mathf.PI / 180)) + Mathf.Abs(ROAD_SCALE_X * Mathf.Cos(NEXT_ROT.y * Mathf.PI / 180))) / 2) / ROAD_SCALE_Z;
        max_x = (road.transform.position.x + (Mathf.Abs(ROAD_SCALE_Z * Mathf.Sin(NEXT_ROT.y * Mathf.PI / 180)) + Mathf.Abs(ROAD_SCALE_X * Mathf.Cos(NEXT_ROT.y * Mathf.PI / 180))) / 2) / ROAD_SCALE_Z;
        min_z = (road.transform.position.z - (Mathf.Abs(ROAD_SCALE_Z * Mathf.Cos(NEXT_ROT.y * Mathf.PI / 180)) + Mathf.Abs(ROAD_SCALE_X * Mathf.Sin(NEXT_ROT.y * Mathf.PI / 180))) / 2) / ROAD_SCALE_Z;
        max_z = (road.transform.position.z + (Mathf.Abs(ROAD_SCALE_Z * Mathf.Cos(NEXT_ROT.y * Mathf.PI / 180)) + Mathf.Abs(ROAD_SCALE_X * Mathf.Sin(NEXT_ROT.y * Mathf.PI / 180))) / 2) / ROAD_SCALE_Z;

        min_x = (min_x % 1 == 0) ? (min_x <= 0 ? 0 : (int)(min_x - 1)) : (int)min_x;
        max_x = (max_x % 1 == 0) ? (max_x >= ROAD_OCCUPIED_TABLE_SIZE ? ROAD_OCCUPIED_TABLE_SIZE : (int)(max_x + 1)) : (int)max_x;
        min_z = (min_z % 1 == 0) ? (min_z <= 0 ? 0 : (int)(min_z - 1)) : (int)min_z;
        max_z = (max_z % 1 == 0) ? (max_z >= ROAD_OCCUPIED_TABLE_SIZE ? ROAD_OCCUPIED_TABLE_SIZE : (int)(max_z + 1)) : (int)max_z;


        for (int i = (int)min_x; i <= (int)max_x; i++)
        {
            for (int j = (int)min_z; j <= (int)max_z; j++)
            {
                ROAD_OCCUPIED_TABLE[i, j] = true;
                ROAD_OCCUPIED_LIST[getRoadNum(road)].Add(new Vector2(i, j));
            }
        }
    }

    bool nextRoadAvailable(GameObject road, Vector2 rand)
    {
        float min_x, max_x, min_z, max_z;
        Vector3 ORIG_POS = NEXT_POS, ORIG_ROT = NEXT_ROT;
        int roadNum = getRoadNum(road);
        for (int k = 0; k < rand.y; k++)
        {
            switch ((int)rand.x)
            {
                case 0:
                    NEXT_POS = new Vector3(NEXT_POS.x + OVERLAP * Mathf.Sin(NEXT_ROT.y * Mathf.PI / 180), NEXT_POS.y, NEXT_POS.z + OVERLAP * Mathf.Sin((90 - NEXT_ROT.y) * Mathf.PI / 180));
                    break;
                case 1:
                    NEXT_ROT.y += ROT;
                    NEXT_POS = new Vector3(NEXT_POS.x + OVERLAP * Mathf.Sin(NEXT_ROT.y * Mathf.PI / 180), NEXT_POS.y, NEXT_POS.z + OVERLAP * Mathf.Sin((90 - NEXT_ROT.y) * Mathf.PI / 180));
                    break;
                case 2:
                    NEXT_ROT.y -= ROT;
                    NEXT_POS = new Vector3(NEXT_POS.x + OVERLAP * Mathf.Sin(NEXT_ROT.y * Mathf.PI / 180), NEXT_POS.y, NEXT_POS.z + OVERLAP * Mathf.Sin((90 - NEXT_ROT.y) * Mathf.PI / 180));
                    break;
            }
            min_x = NEXT_POS.x - (Mathf.Abs(ROAD_SCALE_Z * Mathf.Sin(NEXT_ROT.y * Mathf.PI / 180)) + Mathf.Abs(ROAD_SCALE_X * Mathf.Cos(NEXT_ROT.y * Mathf.PI / 180))) / 2;
            max_x = NEXT_POS.x + (Mathf.Abs(ROAD_SCALE_Z * Mathf.Sin(NEXT_ROT.y * Mathf.PI / 180)) + Mathf.Abs(ROAD_SCALE_X * Mathf.Cos(NEXT_ROT.y * Mathf.PI / 180))) / 2;
            min_z = NEXT_POS.z - (Mathf.Abs(ROAD_SCALE_Z * Mathf.Cos(NEXT_ROT.y * Mathf.PI / 180)) + Mathf.Abs(ROAD_SCALE_X * Mathf.Sin(NEXT_ROT.y * Mathf.PI / 180))) / 2;
            max_z = NEXT_POS.z + (Mathf.Abs(ROAD_SCALE_Z * Mathf.Cos(NEXT_ROT.y * Mathf.PI / 180)) + Mathf.Abs(ROAD_SCALE_X * Mathf.Sin(NEXT_ROT.y * Mathf.PI / 180))) / 2;

            if (min_x < 0 || min_x >= MAP_SCALE || max_x < 0 || max_x >= MAP_SCALE || min_z < 0 || min_z >= MAP_SCALE || max_z < 0 || max_z >= MAP_SCALE)
            {
                return false;
            }
            else
            {
                min_x /= ROAD_SCALE_Z;
                max_x /= ROAD_SCALE_Z;
                min_z /= ROAD_SCALE_Z;
                max_z /= ROAD_SCALE_Z;

                min_x = (min_x % 1 == 0) ? (min_x == 0 ? min_x : (int)(min_x - 1)) : (int)min_x;
                max_x = (max_x % 1 == 0) ? (max_x == ROAD_OCCUPIED_TABLE_SIZE ? max_x : (int)(max_x + 1)) : (int)max_x;
                min_z = (min_z % 1 == 0) ? (min_z == 0 ? min_z : (int)(min_z - 1)) : (int)min_z;
                max_z = (max_z % 1 == 0) ? (max_z == ROAD_OCCUPIED_TABLE_SIZE ? max_z : (int)(max_z + 1)) : (int)max_z;

                for (int i = (int)min_x; i <= (int)max_x; i++)
                {
                    for (int j = (int)min_z; j <= (int)max_z; j++)
                    {
                        if (ROAD_OCCUPIED_TABLE[i, j])
                        {
                            bool isContain = false;
                            for (int cnt = 0; cnt < (roadNum > (180 / ROT) ? 180 / ROT : roadNum); cnt++)
                            {
                                try
                                {
                                    if (ROAD_OCCUPIED_LIST[roadNum - cnt].Contains(new Vector2(i, j)))
                                        isContain = true;
                                }
                                catch
                                {
                                    Debug.Log((roadNum - cnt).ToString());
                                }
                            }
                            if (!isContain)
                            {
                                NEXT_POS = ORIG_POS;
                                NEXT_ROT = ORIG_ROT;
                                return false;
                            }
                        }
                    }
                }
            }
            roadNum++;
        }
        NEXT_POS = ORIG_POS;
        NEXT_ROT = ORIG_ROT;
        return true;
    }

    int getRoadNum(GameObject road)
    {
        string[] token = road.name.Split("(".ToCharArray(), road.name.Length);
        return System.Convert.ToInt32(token[1].Split(")".ToCharArray(), token[1].Length)[0]);
    }

    Vector2 myRand(int roadCount)
    {
        return RandList[getRandIndex(roadCount)];
    }

    int getRandIndex(int roadCount)
    {
        int i, cnt = 0;
        for (i = 0; i < RandList.Count; i++)
        {
            cnt += (int)RandList[i].y;
            if (cnt >= roadCount)
                return i;
        }
        return 0;
    }

    void updateRand(int randIndex)
    {
        List<Vector2> NewRandList = new List<Vector2>();
        int direction, count, j = 0;
        int dRate = ROAD_DIFFICULTY == 3 ? 5 : (ROAD_DIFFICULTY == 2 ? 7 : 9);
        for (int i = 0; i < randIndex; i++)
        {
            j += (int)RandList[i].y;
            NewRandList.Add(RandList[i]);
        }
        if (randIndex == 0)
        {
            NewRandList.Add(new Vector2(0, 10));
            j += 10;
        }
        for (; j < ROAD_COUNT;)
        {
            direction = Random.Range(0, dRate);
            count = Random.Range(5, 20);
            if (direction > 1)
            {
                j += count;
                if (j >= ROAD_COUNT)
                {
                    count = count - j + ROAD_COUNT - 1;
                    j = ROAD_COUNT;
                }
                NewRandList.Add(new Vector2(0, count));
            }
            else
            {
                direction++;
                if (NewRandList.Count > 0)
                    if (NewRandList[NewRandList.Count - 1].x == direction)
                    {
                        if (Random.Range(0, 1) == 1)
                            direction = direction == 1 ? 2 : 1;
                        else
                            direction = 0;
                    }
                j += count;
                if (j >= ROAD_COUNT)
                {
                    count = count - j + ROAD_COUNT - 1;
                    j = ROAD_COUNT;
                }
                NewRandList.Add(new Vector2(direction, count));
            }
        }
        RandList = NewRandList;
    }

    void initVar()
    {
        for (int i = 1; i < GameObject.Find("Car").GetComponent<point>().num_plane; i++)
            GameObject.Find("Car").GetComponent<point>().plane_check[i] = false;
        GameObject.Find("Car").GetComponent<point>().point1 = 0;
        GameObject.Find("Car").GetComponent<point>().point2 = 0;

        updateRand(0);

        ROAD_OCCUPIED_LIST = new List<List<Vector2>>();
        for (int i = 0; i <= ROAD_COUNT + 20; i++)
        {
            ROAD_OCCUPIED_LIST.Add(new List<Vector2>());
        }

        ROAD_OCCUPIED_TABLE = new bool[ROAD_OCCUPIED_TABLE_SIZE, ROAD_OCCUPIED_TABLE_SIZE];

        NEXT_POS = new Vector3(START_POS_X, START_POS_Y, START_POS_Z);
        NEXT_ROT = new Vector3(START_ROT_X, START_ROT_Y, START_ROT_Z);
    }

    public Vector3 getScale()
    {
        return new Vector3(ROAD_SCALE_X, ROAD_SCALE_Y, ROAD_SCALE_Z);
    }
}
﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;
using System.Threading;

public class TestDataGen : MonoBehaviour
{

    //const string ROAD_NAME = "road";
    const int ROAD_COUNT = 500;

    const float ROAD_SCALE_X = 8;
    const float ROAD_SCALE_Y = 0.5F;
    const float ROAD_SCALE_Z = 1.25F;
    const float START_POS_X = ROAD_SCALE_X / 2 + 250;
    const float START_POS_Y = 0;
    const float START_POS_Z = ROAD_SCALE_Z / 2 + 250;
    const float START_ROT_X = 0;
    const float START_ROT_Y = 0;
    const float START_ROT_Z = 0;
    const float ROT = 5;
    const float OVERLAP = 0.5F;
    const int MAP_SCALE = 500;
    const int ROLLBACK = 50;

    const int ROAD_OCCUPIED_TABLE_SIZE = 100 * (int)(ROAD_SCALE_Z * 50);

    bool[,] ROAD_OCCUPIED_TABLE;
    bool screenShotFinish = true;
    List<List<Vector2>> ROAD_OCCUPIED_LIST;

    Vector3 NEXT_POS, NEXT_ROT;

    List<Vector2> RandList;

    int screenShotCnt = 60008;



    // Use this for initialization
    void Start()
    {
        Button btn = GetComponent<Button>();
        btn.onClick.AddListener(delegate {
            ClickBtn();
        });
    }

    // Update is called once per frame
    void Update()
    {
        if (screenShotFinish)
        {
            screenShotFinish = false;
            initVar();
            RoadGen();
        }
    }

    void ClickBtn()
    {
        GameObject.Find("Car").transform.position = new Vector3(START_POS_X, 0.25F, START_POS_Z + 1.25F);
        
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

        screenShotFinish = false;
        StartCoroutine(screenShot());
    }

    IEnumerator screenShot()
    {
        GameObject car = GameObject.Find("Car");
        GameObject road;
        for (int i = 5; i < ROAD_COUNT-30; i+=5)
        {
            Debug.Log("ScreenShot");
            road = GameObject.Find("road (" + i.ToString() + ")");
            car.transform.position = new Vector3(road.transform.position.x, 0.24f, road.transform.position.z);
            car.transform.rotation = road.transform.rotation;
            yield return new WaitForSeconds(0.5f);
            Application.CaptureScreenshot("C:\\Users\\Jack-PC\\Desktop\\CNN\\ScreenShot_" + (screenShotCnt++).ToString() + ".png");
        }
        screenShotFinish = true;
        yield return 0;
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

    void initRand()
    {
        int direction, count;
        RandList = new List<Vector2>();
        for (int i = 0; i < ROAD_COUNT;)
        {
            direction = Random.Range(0, 8);
            count = Random.Range(1, 20);
            if (direction < 6)
            {
                i += count;
                if (i >= ROAD_COUNT)
                {
                    count = count - i + ROAD_COUNT - 1;
                    i = ROAD_COUNT;
                }
                RandList.Add(new Vector2(0, count));
            }
            else
            {
                i += count;
                if (i >= ROAD_COUNT)
                {
                    count = count - i + ROAD_COUNT - 1;
                    i = ROAD_COUNT;
                }
                RandList.Add(new Vector2(direction - 5, count));
            }
        }
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
        for (int i = 0; i < randIndex; i++)
        {
            j += (int)RandList[i].y;
            NewRandList.Add(RandList[i]);
        }
        for (; j < ROAD_COUNT;)
        {
            direction = Random.Range(0, 8);
            count = Random.Range(1, 20);
            if (direction < 6)
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
                j += count;
                if (j >= ROAD_COUNT)
                {
                    count = count - j + ROAD_COUNT - 1;
                    j = ROAD_COUNT;
                }
                NewRandList.Add(new Vector2(direction - 5, count));
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
        GameObject.Find("Canvas/Text").GetComponent<Text>().text = "Score : 0";

        initRand();

        ROAD_OCCUPIED_LIST = new List<List<Vector2>>();
        for (int i = 0; i <= ROAD_COUNT; i++)
        {
            ROAD_OCCUPIED_LIST.Add(new List<Vector2>());
        }

        ROAD_OCCUPIED_TABLE = new bool[ROAD_OCCUPIED_TABLE_SIZE, ROAD_OCCUPIED_TABLE_SIZE];

        NEXT_POS = new Vector3(START_POS_X, START_POS_Y, START_POS_Z);
        NEXT_ROT = new Vector3(START_ROT_X, START_ROT_Y, START_ROT_Z);
    }
}

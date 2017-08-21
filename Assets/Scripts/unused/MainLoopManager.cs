using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public delegate void FrameUpdateFunc();
public class FrameUpdate
{
    public FrameUpdateFunc func;
    public GameObject ower;
    public CustomBehaviour behaviour;
}

public class MainLoopManager : MonoBehaviour
{
    public static MainLoopManager instance { get; private set; }
    private bool m_start;
    private int m_logicFrameDelta;//逻辑帧更新时间
    private int m_logicFrameAdd;//累积时间

    private static List<FrameUpdate> m_frameUpdateList;
    private static List<FrameUpdate> m_frameLateUpdateList;

    private int m_serverFrameDelta;//毫秒
    private int m_curFrameIndex;
    private int m_fillFrameNum;
    private int m_serverRandomSeed;
    public int serverRandomSeed
    {
        get { return m_serverRandomSeed; }
    }
    public int curFrameIndex
    {
        get { return m_curFrameIndex; }
    }
    public static int curFrameTime
    {
        get
        {
            return MainLoopManager.instance.m_curFrameIndex * MainLoopManager.instance.m_serverFrameDelta / (1 + MainLoopManager.instance.m_fillFrameNum);
        }
    }
    public static int deltaFrameTime
    {
        get
        {
            return MainLoopManager.instance.m_serverFrameDelta / (1 + MainLoopManager.instance.m_fillFrameNum);
        }
    }

    private void Awake()
    {
        instance = this;
    }


    public void RegisterFrameUpdate(FrameUpdateFunc func, GameObject owner)
    {

    }

    public void UnRegisterFrameUpdate(FrameUpdateFunc func, GameObject owner)
    {

    }
    public void RegisterFrameLateUpdate(FrameUpdateFunc func, GameObject owner)
    {

    }
    public void UnRegisterFrameLateUpdate(FrameUpdateFunc func, GameObject owner)
    {

    }
    void Loop()
    {
        //......遍历所有脚本
        //先遍历m_frameUpdateList
        //再遍历m_frameLateUpdateList
    }

    void Update()
    {
        if (!m_start)
            return;

        if (m_logicFrameAdd < m_logicFrameDelta)
        {
            m_logicFrameAdd += (int)(Time.deltaTime * 1000);
        }
        else
        {
            int frameNum = 0;
            while (CanUpdateNextFrame() || IsFillFrame())
            {
                Loop();//主循环
                frameNum++;
                if (frameNum > 10)
                {
                    //最多连续播放10帧
                    break;
                }
            }
            m_logicFrameAdd = 0;
        }
    }

    private bool CanUpdateNextFrame()
    {
        //是否可以更新至下一关键帧
        return true;
    }
    private bool IsFillFrame()
    {
        //当前逻辑帧是否为填充帧
        return true;
    }

}
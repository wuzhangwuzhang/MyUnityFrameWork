﻿using UnityEngine;
using System.Collections;
using System;
using System.Collections.Generic;

public class MemoryManager
{
    /// <summary>
    /// 是否允许动态加载
    /// </summary>
    public static bool s_allowDynamicLoad = true;

    /// <summary>
    /// 最大允许的内存使用量
    /// </summary>
    public static int s_MaxMemoryUse = 170;

    /// <summary>
    /// 最大允许的堆内存使用量
    /// </summary>
    public static int s_MaxHeapMemoryUse = 50;

    public static void Init()
    {
        ApplicationManager.s_OnApplicationUpdate += Update;

        if(ApplicationManager.AppMode != AppMode.Release)
            ApplicationManager.s_OnApplicationOnGUI += GUI;
    }

    static void Update()
    {
        //资源加载
        LoadResources();

        //内存管理
        MonitorMemorySize();
    }

    static void GUI()
    {
        GUILayout.TextField("总内存：" + ByteToM(Profiler.GetTotalAllocatedMemory() ) + "M");
        GUILayout.TextField("堆内存：" + ByteToM(Profiler.GetMonoUsedSize() ) + "M");
    }

    /// <summary>
    /// 释放内存
    /// </summary>
    public static void FreeMemory()
    {
        GlobalEvent.DispatchEvent(MemoryEvent.FreeMemory);

        //清空对象池
        GameObjectManager.CleanPool();

        //清空缓存的UI
        UIManager.DestroyAllHideUI();

        FreeHeapMemory();

        //GC
        //GC.Collect();
    }

    /// <summary>
    /// 释放堆内存
    /// </summary>
    public static void FreeHeapMemory()
    {
        GlobalEvent.DispatchEvent(MemoryEvent.FreeHeapMemory);

        DataManager.CleanCatch();
        ConfigManager.CleanCatch();
        RecordManager.CleanCatch();
    }

    public static void LoadRes(List<string> resList,LoadProgressCallBack callBack)
    {
        if(ResourceManager.m_gameLoadType == ResLoadType.Resource)
        {
            callBack(LoadState.CompleteState);
        }
        else
        {
            s_loadCallBack += callBack;
            s_LoadList.AddRange(resList);
            s_loadCount += resList.Count;
        }
    }

    public static void UnLoadRes(List<string> resList)
    {
        if (ResourceManager.m_gameLoadType != ResLoadType.Resource)
        {
            for (int i = 0; i < resList.Count; i++)
            {
                AssetsBundleManager.UnLoadBundle(resList[i]);
            }
        }
    }

    #region 资源加载

    static int s_loadCount = 0;
    static bool isLoading = false;
    static List<string> s_LoadList = new List<string>();
    static LoadProgressCallBack s_loadCallBack;

    static LoadState s_loadStatus = new LoadState();

    static void LoadResources()
    {
        if (!isLoading)
        {
            if (s_LoadList.Count == 0)
            {
                if (s_loadCallBack != null)
                {
                    try
                    {
                        s_loadCallBack(LoadState.CompleteState);
                    }
                    catch(Exception e)
                    {
                        Debug.LogError("Load Finsih CallBack Error : " + e.ToString());
                    }
                    s_loadCallBack = null;
                    s_loadCount = 0;
                }
            }
            else
            {
                AssetsBundleManager.LoadBundleAsync(s_LoadList[0], LoadResourcesFinishCallBack);
                s_LoadList.RemoveAt(0);
                isLoading = true;

                s_loadStatus.isDone = false;
                s_loadStatus.progress = (1- (s_LoadList.Count / s_loadCount));

                try
                {
                    s_loadCallBack(s_loadStatus);
                }
                catch (Exception e)
                {
                    Debug.LogError("Load Finsih CallBack Error : " + e.ToString());
                }
                
            }
        }
    }

    static void LoadResourcesFinishCallBack(LoadState state, Bundle bundlle)
    {
        if (state.isDone == true)
            isLoading = false;
    }

    #endregion

    #region 内存监控

    // 字节到兆
    //const float ByteToM = 0.000001f;

    static bool s_isFreeMemory = false;
    static bool s_isFreeMemory2 = false;

    static bool s_isFreeHeapMemory = false;
    static bool s_isFreeHeapMemory2 = false;

    /// <summary>
    /// 用于监控内存
    /// </summary>
    /// <param name="tag"></param>
    static void MonitorMemorySize()
    {
        if(ByteToM(Profiler.GetTotalReservedMemory() ) > s_MaxMemoryUse * 0.7f)
        {
            if (!s_isFreeMemory)
            {
                s_isFreeMemory = true;
                FreeMemory();
            }

            if (ByteToM(Profiler.GetMonoHeapSize()) > s_MaxMemoryUse)
            {
                if (!s_isFreeMemory2)
                {
                    s_isFreeMemory2 = true;
                    FreeMemory();
                    Debug.LogError("总内存超标告警 ！当前总内存使用量： " + ByteToM(Profiler.GetTotalAllocatedMemory()) + "M");
                }
            }
            else
            {
                s_isFreeMemory2 = false;
            }
        }
        else
        {
            s_isFreeMemory = false;
        }

        if (ByteToM(Profiler.GetMonoUsedSize() ) > s_MaxHeapMemoryUse * 0.7f)
        {
            if (!s_isFreeHeapMemory)
            {
                s_isFreeHeapMemory = true;
            }

            if (ByteToM(Profiler.GetMonoUsedSize()) > s_MaxHeapMemoryUse)
            {
                if (!s_isFreeHeapMemory2)
                {
                    s_isFreeHeapMemory2 = true;
                    Debug.LogError("堆内存超标告警 ！当前堆内存使用量： " + ByteToM(Profiler.GetMonoUsedSize()) + "M");
                }
            }
            else
            {
                s_isFreeHeapMemory2 = false;
            }
        }
        else
        {
            s_isFreeHeapMemory = false;
        }
    }

    #endregion

    static float ByteToM(uint byteCount)
    {
        return (float)(byteCount / (1024.0f * 1024.0f));
    }
}

public delegate void LoadProgressCallBack(LoadState loadState);

public enum MemoryEvent
{
    FreeHeapMemory, //释放堆内存
    FreeMemory,     //释放全部内存
}
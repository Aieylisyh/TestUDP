using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class CustomBehaviour : MonoBehaviour
{
    public class FixedFloat
    {
        private static int digits = 1000;
        public static float FixFloat(float v)
        {
            int _value = (int)(v * digits);
            return (float)((float)_value / digits);
        }

        public static int ToInt(float v)
        {
            float fixedFloat = FixFloat(v);
            return (int)(fixedFloat * digits);
        }
    }

    protected class Time
    {
        public static float time
        {
            get { return FixedFloat.FixFloat(MainLoopManager.curFrameTime / 1000); }
        }

        public static float deltaTime
        {
            get { return FixedFloat.FixFloat(MainLoopManager.deltaFrameTime / 1000); }
        }
    }

    protected class Random
    {
        public static float Range(float min, float max)
        {
            float diff = max - min;
            float seed = FixedFloat.FixFloat(MainLoopManager.instance.serverRandomSeed);
            return FixedFloat.FixFloat(min + diff * seed);
        }
    }

    private bool m_isDestroy = false;

    public bool IsDestroy
    {
        get
        {
            return m_isDestroy;
        }
        private set
        {
            m_isDestroy = value;
        }
    }

    protected virtual void DoDestroy() {
        //logics here, unregister
    }

    public void CustomDestroy(UnityEngine.Object obj)
    {
        if (obj.GetType() == typeof(GameObject))
        {
            GameObject go = (GameObject)obj;
            CustomBehaviour[] behaviours = go.GetComponentsInChildren<CustomBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                behaviours[i].IsDestroy = true;
                behaviours[i].DoDestroy();
            }
        }
        else if (obj.GetType() == typeof(CustomBehaviour))
        {
            CustomBehaviour behaviour = (CustomBehaviour)obj;
            behaviour.IsDestroy = true;
            behaviour.DoDestroy();
        }
        UnityEngine.Object.Destroy(obj);
    }
}
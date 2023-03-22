using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PMRP
{
    public class SRPRenderSettings : MonoBehaviour
    {
        public T GetSettingComponent<T>() where T : BaseRenderSetting
        {
            T setting = gameObject.GetComponent<T>();
            if (setting == null)
            {
                gameObject.AddComponent<T>();
            }
            return setting;
        }
    }
}
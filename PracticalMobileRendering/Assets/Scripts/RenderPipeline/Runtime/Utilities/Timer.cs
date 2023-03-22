using System;
using UnityEngine;

namespace PMRP
{
    public struct Timer
    {
        private double m_start;

        public void Reset()
        {
#if UNITY_5
            m_start = Time.realtimeSinceStartup;
#else
            m_start = Time.realtimeSinceStartupAsDouble;
#endif
        }

        public double Elapsed()
        {
#if UNITY_5
            double now = Time.realtimeSinceStartup;
#else
            double now = Time.realtimeSinceStartupAsDouble;
#endif
            double duration = now - m_start;
            return duration;
        }
    }

    public struct ScopedTimer : IDisposable
    {
        readonly string m_msg;
        Timer           m_timer;

        public ScopedTimer(string msg)
        {
            m_msg = msg;

            m_timer = new Timer();
            m_timer.Reset();
        }

        public void Dispose()
        {
            Dispose(true);
        }

        private void Dispose(bool disposing)
        {
            Debug.LogFormat("{0} took {1}s", m_msg, m_timer.Elapsed());
        }
    }
}

using System;
using UnityEngine.Rendering;

namespace PMRP
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public struct ProfilingSample :IDisposable
    {
        readonly string m_marker;
        readonly CommandBuffer m_cmd;

        bool m_disposed;

        public ProfilingSample(CommandBuffer cmd, string marker)
        {
            m_disposed = false;
            m_marker = marker;
            m_cmd = cmd;
            m_cmd.BeginSample(m_marker);
        }

        public void Dispose()
        {
            Dispose(true);
        }

        private void Dispose(bool disposing)
        {
            if (m_disposed)
                return;

            if (disposing)
            {
                m_cmd.EndSample(m_marker);
            }

            m_disposed = true;
        }
    }
#else
    public struct ProfilingSample : IDisposable
    {
        public ProfilingSample(CommandBuffer cmd, string marker)
        {
        }

        public void Dispose()
        {
        }
    }
#endif
}


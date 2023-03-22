using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace PMRP
{
    public class IRenderPass : IDisposable
    {
        protected bool m_disposed = false;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (m_disposed)
                return;

            if (disposing)
            {
                // dispose managed state (managed objects).
            }

            // free unmanaged resources (unmanaged objects) and override a finalizer below.
            // set large fields to null.

            m_disposed = true;
        }

        ~IRenderPass()
        {
            Debug.LogErrorFormat("Call Dispose on {0} explicitly!!!", this.GetType().Name);
            Dispose(false);
        }
    }
}

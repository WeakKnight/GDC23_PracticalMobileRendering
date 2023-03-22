using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace PMRP
{
    public interface ILightLoop : IDisposable
    {
        void Init();
        void Build(WorldData worldData, SRPCamera viewCamera);
        void Setup(CommandBuffer cmd, SRPCamera viewCamera);
    }

    public sealed class LightLoop : IRenderPass
    {
        private ILightLoop m_impl = null;

        protected override void Dispose(bool disposing)
        {
            if (m_disposed)
                return;

            m_impl.Dispose();
            base.Dispose(disposing);
        }

        public void Init()
        {
            if ((int)ShaderOptions.LightLoopImpl == (int)YALightLoop.Simple)
            {
                m_impl = new SimpleLightLoop();
            }

            m_impl.Init();
        }

        public void Build(WorldData worldData, SRPCamera viewCamera)
        {
            m_impl.Build(worldData, viewCamera);
        }

        public void Setup(CommandBuffer cmd, SRPCamera viewCamera)
        {
            m_impl.Setup(cmd, viewCamera);
        }
    }
}

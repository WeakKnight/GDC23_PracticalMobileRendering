using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PMRP
{
    public enum DepthTextureFormat
    {
        D16 = 0,    // at least 16 bit Z buffer and no stencil buffer
        D24S8,      // at least 24 bit Z buffer, and a stencil buffer. (according to Unity doc : When requesting 24 bit Z Unity will prefer 32 bit floating point Z buffer if available on the platform)
    }

    public sealed class FBO : IDisposable
    {
        public class Desc
        {
            private struct TargetDesc
            {
                public RenderTextureFormat format;
                public bool                useMipmap;
                public bool                allowUav;
                public bool                sRGB;
                public FilterMode          filterMode;
                public int                 ArraySize;
            };

            private struct DepthTargetDesc
            {
                public DepthTextureFormat format;
                public bool               allowUav;
            };

            public Desc SetColorTarget(uint                rtIndex,
                                       RenderTextureFormat format,
                                       FilterMode          filterMode = FilterMode.Bilinear,
                                       bool                sRGB       = false,
                                       bool                useMipmap  = false,
                                       bool                allowUav   = false,
                                       int                 arraySize  = 1)
            {
                colorTargets[rtIndex].format     = format;
                colorTargets[rtIndex].useMipmap  = useMipmap;
                colorTargets[rtIndex].filterMode = filterMode;
                colorTargets[rtIndex].allowUav   = allowUav;
                colorTargets[rtIndex].sRGB       = sRGB;
                colorTargets[rtIndex].ArraySize  = arraySize;
                numOfColorTargets                = Math.Max(numOfColorTargets, rtIndex + 1);

                return this;
            }

            public Desc SetDepthStencilTarget(DepthTextureFormat format, bool allowUav = false)
            {
                hasDepthStencilTarget       = true;
                depthStencilTarget.format   = format;
                depthStencilTarget.allowUav = allowUav;
                return this;
            }

            public RenderTextureFormat GetFormatOfColorTarget(uint idx)
            {
                Debug.Assert(idx < numOfColorTargets);
                return colorTargets[idx].format;
            }

            public bool GetAllowColorTargetAsUAV(uint idx)
            {
                return colorTargets[idx].allowUav;
            }

            public bool GetColorTargetUseMipmap(uint idx)
            {
                return colorTargets[idx].useMipmap;
            }

            public FilterMode GetColorTargetFilterMode(uint idx)
            {
                return colorTargets[idx].filterMode;
            }

            public bool GetColorTargetIsSrgb(uint idx)
            {
                return colorTargets[idx].sRGB;
            }

            public int GetColorTargetArraySize(uint idx)
            {
                return colorTargets[idx].ArraySize;
            }

            public DepthTextureFormat GetFormatOfDepthStencilTarget()
            {
                Debug.Assert(hasDepthStencilTarget);
                return depthStencilTarget.format;
            }

            public bool GetAllowDepthStencilAsUAV()
            {
                return depthStencilTarget.allowUav;
            }

            public uint GetNumOfColorTargets()
            {
                return numOfColorTargets;
            }

            public bool HasDepthStencilTarget()
            {
                return hasDepthStencilTarget;
            }

            public void SetMultiSamples(int samples)
            {
                multiSamples = Math.Max(samples, 1);
            }

            public int GetMultiSamples()
            {
                return multiSamples;
            }

            private uint            numOfColorTargets     = 0;
            private bool            hasDepthStencilTarget = false;
            private TargetDesc[]    colorTargets          = new TargetDesc[8];
            private DepthTargetDesc depthStencilTarget;
            private int             multiSamples = 1;
        };

        private class RenderTextureWrapper
        {
            public RenderTexture rt;
            public bool          owner;
        };

        private Desc                       m_desc;
        private List<RenderTextureWrapper> m_colorRTs;
        private RenderTextureWrapper       m_depthRT;

        private int m_width  = 0;
        private int m_height = 0;

        private bool m_disposed = false;

        public string name = "FBO";


        // Create a dummy FBO
        public static FBO Create()
        {
            FBO fbo = new FBO();
            fbo.m_width  = 0;
            fbo.m_height = 0;
            return fbo;
        }

        public static FBO Create(Desc d, int width_ = 0, int height_ = 0)
        {
            FBO fbo = new FBO();
            fbo.m_desc   = d;
            fbo.m_width  = 0;
            fbo.m_height = 0;
            fbo.Resize(width_, height_);
            return fbo;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public bool Resize(int w, int h)
        {
            //if (name == "FBO")
            //{
            //    int foo = 0;
            //}

            if (w == m_width && h == m_height)
                return false;

            Cleanup();

            m_width  = w;
            m_height = h;
            if (m_desc != null && m_width > 0 && m_height > 0)
            {
                m_colorRTs = new List<RenderTextureWrapper>((int)m_desc.GetNumOfColorTargets());
                for (uint i = 0; i < m_desc.GetNumOfColorTargets(); ++i)
                {
                    RenderTextureReadWrite readWrite = RenderTextureReadWrite.Linear;
                    if (m_desc.GetColorTargetIsSrgb(i))
                        readWrite = RenderTextureReadWrite.sRGB;

                    int arraySize = m_desc.GetColorTargetArraySize(i);

                    RenderTexture rt = new RenderTexture(w, h, 0,
                                                         m_desc.GetFormatOfColorTarget(i),
                                                         readWrite);
                    rt.enableRandomWrite = m_desc.GetAllowColorTargetAsUAV(i);
                    rt.useMipMap         = m_desc.GetColorTargetUseMipmap(i);
                    rt.autoGenerateMips  = false;
                    rt.filterMode        = m_desc.GetColorTargetFilterMode(i);
                    rt.volumeDepth       = arraySize;
                    if (arraySize > 1)
                        rt.dimension = TextureDimension.Tex2DArray;
                    rt.anisoLevel   = 0;
                    rt.antiAliasing = m_desc.GetMultiSamples();
                    rt.Create();

                    if (!string.IsNullOrEmpty(name))
                        rt.name = string.Format("{0}_color{1}", name, i);

                    m_colorRTs.Add(new RenderTextureWrapper { rt = rt, owner = true });
                }

                if (m_desc.HasDepthStencilTarget())
                {
                    int depth = m_desc.GetFormatOfDepthStencilTarget() == DepthTextureFormat.D24S8 ? 24 : 16;
                    RenderTexture rt = new RenderTexture(w, h, depth,
                                                         RenderTextureFormat.Depth,
                                                         RenderTextureReadWrite.Default);
                    rt.enableRandomWrite = m_desc.GetAllowDepthStencilAsUAV();
                    rt.antiAliasing      = m_desc.GetMultiSamples();
#if UNITY_2020_1_OR_NEWER
                    if (m_desc.GetMultiSamples() > 1)
                        rt.bindTextureMS = true;
#endif
                    rt.Create();

                    if (!string.IsNullOrEmpty(name))
                        rt.name = string.Format("{0}_depth", name);

                    m_depthRT = new RenderTextureWrapper { rt = rt, owner = true };
                }

                return true;
            }

            return false;
        }

        public int GetWidth()
        {
            return m_width;
        }

        public int GetHeight()
        {
            return m_height;
        }

        public Vector4 GetTexelSize()
        {
            return new Vector4(1.0f/m_width, 1.0f/m_height, m_width, m_height);
        }

        public int GetMultiSamples()
        {
            return m_desc.GetMultiSamples();
        }

        public int GetNumColorTargets()
        {
            return m_colorRTs.Count;
        }

        public RenderTexture GetColorTarget(int i = 0)
        {
            if (m_colorRTs != null && m_colorRTs[i] != null)
                return m_colorRTs[i].rt;
            return null;
        }

        public RenderTexture GetDepthStencilTarget()
        {
            if (m_depthRT != null)
                return m_depthRT.rt;
            return null;
        }

        #region private functions

        ~FBO()
        {
            Debug.LogErrorFormat(string.Format( "Call Dispose on FBO {0} explicitly!!!", name));
            Dispose(false);
        }

        private void Dispose(bool disposing)
        {
            if (m_disposed)
                return;

            Cleanup();

            m_disposed = true;
        }

        private void Cleanup()
        {
            if (m_colorRTs != null)
            {
                foreach (var rt in m_colorRTs)
                {
                    if (rt.owner && rt.rt != null)
                    {
                        rt.rt.Release();
                        rt.rt = null;
                    }
                }

                m_colorRTs = null;
            }

            if (m_depthRT != null)
            {
                if (m_depthRT.owner && m_depthRT.rt != null)
                {
                    m_depthRT.rt.Release();
                    m_depthRT.rt = null;
                }
                m_depthRT = null;
            }
        }

#endregion
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PMRP
{
    [ExecuteInEditMode]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SphereCollider))]
    public class SRPShadowSplitBoundingSphere : MonoBehaviour
    {
        private SphereCollider m_sphereCollider;

        public BoundingSphere BoundingSphere
        {
            get
            {
                return new BoundingSphere(m_sphereCollider.bounds.center,
                                          m_sphereCollider.transform.lossyScale.MaxComponent() * m_sphereCollider.radius);
            }
        }

        void Awake()
        {
            m_sphereCollider = GetComponent<SphereCollider>();
            m_sphereCollider.isTrigger = true;
        }
    }
}

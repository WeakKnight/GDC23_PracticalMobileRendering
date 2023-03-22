using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EmissiveSync : MonoBehaviour
{
    MeshRenderer meshRenderer;
    int propId = Shader.PropertyToID("_EmissiveColor");

    public void Sync(float intensity)
    {
        if (meshRenderer == null)
        {
            meshRenderer = GetComponent<MeshRenderer>();

            if (meshRenderer == null)
            {
                return;
            }
        }

        Color hdrColor = meshRenderer.sharedMaterial.GetColor(propId);

        var propBlock = new MaterialPropertyBlock();
        meshRenderer.GetPropertyBlock(propBlock);
        propBlock.SetColor(propId, intensity * hdrColor);
        meshRenderer.SetPropertyBlock(propBlock);
    }
}

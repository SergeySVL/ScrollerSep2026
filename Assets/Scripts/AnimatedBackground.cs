using Unity.Collections.Tests.CoreCLR.TestJobs;
using UnityEngine;

public enum BackgroundType { Blue, Brown, Grey, Green, Pink, Purple, Yellow }

public class AnimatedBackground : MonoBehaviour
{
    [SerializeField] private Vector2 movementDirection;
    private MeshRenderer mesh;

    [Header("Color")]
    [SerializeField] private BackgroundType backgroundType;
    [SerializeField] private Texture2D[] textures;

    private void Awake()
    {
        mesh = GetComponent<MeshRenderer>();
        UpdateBackgroundTexture();
    }

    private void Update()
    {
        mesh.material.mainTextureOffset += movementDirection * Time.deltaTime;
    }

    [ContextMenu("Update Background")]
    private void UpdateBackgroundTexture()
    {
        if (mesh == null)
            mesh = GetComponent<MeshRenderer>();
        mesh.material.mainTexture = textures[(int)backgroundType];
    }


}

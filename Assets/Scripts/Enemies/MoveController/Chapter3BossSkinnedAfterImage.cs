using System;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.U2D;
using UnityEngine.U2D.Animation;

/// <summary>
/// Chapter3BossのSpriteSkin変形後ポーズを静的Meshへ焼き付ける残像です。
/// AfterImageEffect2DはSpriteSkinの頂点変形を複製しないため、このクラスで補完します。
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public sealed class Chapter3BossSkinnedAfterImage : MonoBehaviour
{
    private static readonly int MainTextureId = Shader.PropertyToID("_MainTex");

    private MeshFilter _meshFilter;
    private MeshRenderer _meshRenderer;
    private Mesh _mesh;
    private Color32[] _vertexColors;
    private Color _baseColor;
    private float _holdTime;
    private float _fadeTime;
    private float _elapsedTime;
    private Action<Chapter3BossSkinnedAfterImage> _releaseAction;

    public void Initialize(
        SpriteRenderer sourceRenderer,
        Material material,
        Color color,
        float holdTime,
        float fadeTime,
        Action<Chapter3BossSkinnedAfterImage> releaseAction
    )
    {
        EnsureComponents();

        Sprite sprite = sourceRenderer.sprite;
        SpriteSkin spriteSkin = sourceRenderer.GetComponent<SpriteSkin>();
        BakeCurrentPose(sourceRenderer, spriteSkin, sprite);

        _meshRenderer.sharedMaterial = material != null ? material : sourceRenderer.sharedMaterial;
        _meshRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
        _meshRenderer.sortingOrder = sourceRenderer.sortingOrder - 1;
        _meshRenderer.enabled = sourceRenderer.enabled;

        var propertyBlock = new MaterialPropertyBlock();
        _meshRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetTexture(MainTextureId, sprite.texture);
        _meshRenderer.SetPropertyBlock(propertyBlock);

        _baseColor = color;
        _holdTime = Mathf.Max(0f, holdTime);
        _fadeTime = Mathf.Max(0.001f, fadeTime);
        _elapsedTime = 0f;
        _releaseAction = releaseAction;
        ApplyColor(_baseColor);
    }

    private void EnsureComponents()
    {
        if (_meshFilter == null)
            _meshFilter = GetComponent<MeshFilter>();
        if (_meshFilter == null)
            _meshFilter = gameObject.AddComponent<MeshFilter>();

        if (_meshRenderer == null)
            _meshRenderer = GetComponent<MeshRenderer>();
        if (_meshRenderer == null)
            _meshRenderer = gameObject.AddComponent<MeshRenderer>();

        if (_mesh == null)
        {
            _mesh = new Mesh { name = "Chapter3BossSkinnedAfterImageMesh" };
            _mesh.MarkDynamic();
            _meshFilter.sharedMesh = _mesh;
        }
    }

    private void BakeCurrentPose(
        SpriteRenderer sourceRenderer,
        SpriteSkin spriteSkin,
        Sprite sprite
    )
    {
        NativeSlice<Vector3> sourceVertices = sprite.GetVertexAttribute<Vector3>(
            VertexAttribute.Position
        );
        NativeSlice<Vector2> sourceUvs = sprite.GetVertexAttribute<Vector2>(
            VertexAttribute.TexCoord0
        );
        NativeArray<ushort> sourceIndices = sprite.GetIndices();
        var vertices = new Vector3[sourceVertices.Length];
        var uvs = new Vector2[sourceUvs.Length];
        var triangles = new int[sourceIndices.Length];

        bool canSkin =
            spriteSkin != null
            && spriteSkin.isActiveAndEnabled
            && spriteSkin.boneTransforms != null
            && spriteSkin.boneTransforms.Length > 0;

        NativeSlice<BoneWeight> boneWeights = default;
        NativeArray<Matrix4x4> bindPoses = default;
        Transform[] bones = null;

        if (canSkin)
        {
            boneWeights = sprite.GetVertexAttribute<BoneWeight>(
                VertexAttribute.BlendWeight
            );
            bindPoses = sprite.GetBindPoses();
            bones = spriteSkin.boneTransforms;
            canSkin =
                boneWeights.Length == sourceVertices.Length
                && bindPoses.Length == bones.Length;
        }

        if (canSkin)
        {
            for (int i = 0; i < sourceVertices.Length; i++)
            {
                BoneWeight weight = boneWeights[i];
                Vector3 vertex = sourceVertices[i];
                Vector3 worldVertex = Vector3.zero;
                worldVertex += TransformByBone(vertex, weight.boneIndex0, weight.weight0, bones, bindPoses);
                worldVertex += TransformByBone(vertex, weight.boneIndex1, weight.weight1, bones, bindPoses);
                worldVertex += TransformByBone(vertex, weight.boneIndex2, weight.weight2, bones, bindPoses);
                worldVertex += TransformByBone(vertex, weight.boneIndex3, weight.weight3, bones, bindPoses);
                vertices[i] = worldVertex;
            }
        }
        else
        {
            for (int i = 0; i < sourceVertices.Length; i++)
                vertices[i] = sourceRenderer.transform.TransformPoint(sourceVertices[i]);
        }

        for (int i = 0; i < sourceUvs.Length; i++)
            uvs[i] = sourceUvs[i];
        for (int i = 0; i < sourceIndices.Length; i++)
            triangles[i] = sourceIndices[i];

        transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        transform.localScale = Vector3.one;
        _mesh.Clear();
        _mesh.vertices = vertices;
        _mesh.uv = uvs;
        _mesh.triangles = triangles;
        _mesh.RecalculateBounds();
    }

    private static Vector3 TransformByBone(
        Vector3 vertex,
        int boneIndex,
        float weight,
        Transform[] bones,
        NativeArray<Matrix4x4> bindPoses
    )
    {
        if (
            weight <= 0f
            || boneIndex < 0
            || boneIndex >= bones.Length
            || boneIndex >= bindPoses.Length
            || bones[boneIndex] == null
        )
            return Vector3.zero;

        return weight * (bones[boneIndex].localToWorldMatrix * bindPoses[boneIndex]).MultiplyPoint3x4(vertex);
    }

    private void LateUpdate()
    {
        _elapsedTime += Time.deltaTime;
        if (_elapsedTime <= _holdTime)
            return;

        float fadeProgress = (_elapsedTime - _holdTime) / _fadeTime;
        if (fadeProgress >= 1f)
        {
            _releaseAction?.Invoke(this);
            return;
        }

        Color color = _baseColor;
        color.a *= 1f - fadeProgress;
        ApplyColor(color);
    }

    private void ApplyColor(Color color)
    {
        int vertexCount = _mesh.vertexCount;
        if (_vertexColors == null || _vertexColors.Length != vertexCount)
            _vertexColors = new Color32[vertexCount];

        Color32 color32 = color;
        for (int i = 0; i < _vertexColors.Length; i++)
            _vertexColors[i] = color32;
        _mesh.colors32 = _vertexColors;
    }

    private void OnDestroy()
    {
        if (_mesh != null)
            Destroy(_mesh);
    }
}

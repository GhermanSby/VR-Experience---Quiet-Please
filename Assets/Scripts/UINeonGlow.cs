using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[AddComponentMenu("UI/Effects/UI Neon Glow")]
[RequireComponent(typeof(Graphic))]
public sealed class UINeonGlow : BaseMeshEffect
{
    [SerializeField] private Color glowColor = new Color(0f, 0.65f, 1f, 1f);
    [SerializeField, Range(8, 24)] private int directionCount = 16;
    [SerializeField, Range(6, 20)] private int radialSteps = 14;
    [SerializeField, Min(1f)] private float glowRadius = 18f;
    [SerializeField, Range(0f, 2f)] private float glowOpacity = 0.75f;
    [SerializeField, Range(0.5f, 4f)] private float falloff = 2.2f;

    private readonly List<UIVertex> source = new List<UIVertex>();
    private readonly List<UIVertex> output = new List<UIVertex>();

    public override void ModifyMesh(VertexHelper vertexHelper)
    {
        if (!IsActive() || vertexHelper.currentVertCount == 0)
            return;

        source.Clear();
        output.Clear();
        vertexHelper.GetUIVertexStream(source);
        int steps = Mathf.Max(2, radialSteps);
        for (int step = steps; step >= 1; step--)
        {
            float normalizedRadius = step / (float)steps;
            float radius = glowRadius * normalizedRadius;
            float alpha = glowOpacity * Mathf.Pow(1f - normalizedRadius, falloff)
                          / Mathf.Max(1, directionCount);
            AddRing(radius, alpha);
        }
        output.AddRange(source);
        vertexHelper.Clear();
        vertexHelper.AddUIVertexTriangleStream(output);
    }

    private void AddRing(float radius, float alpha)
    {
        int directions = Mathf.Max(4, directionCount);
        for (int direction = 0; direction < directions; direction++)
        {
            float angle = direction * Mathf.PI * 2f / directions;
            Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
            for (int i = 0; i < source.Count; i++)
            {
                UIVertex vertex = source[i];
                vertex.position += offset;
                Color color = glowColor;
                color.a = alpha * (vertex.color.a / 255f);
                vertex.color = color;
                output.Add(vertex);
            }
        }
    }

    private void OnValidate()
    {
        if (graphic != null)
            graphic.SetVerticesDirty();
    }
}

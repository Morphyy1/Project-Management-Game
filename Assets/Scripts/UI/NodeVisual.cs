using UnityEngine;
using TMPro;

public class NodeVisual : MonoBehaviour
{
    public GraphNode node;
    public SourceInfo source;
    
    [Header("Визуальные элементы")]
    public Renderer nodeRenderer;
    public TextMeshPro nameLabel;      // 3D-подпись (не требует Canvas)
    public TextMeshPro valueLabel;
    public GameObject sourceIndicator;
    
    [Header("Цвета")]
    public Color defaultColor = Color.white;
    public Color sourceColor = Color.yellow;
    public Color reachableColor = Color.green;
    public Color unreachableColor = Color.red;
    public Color visitedColor = Color.gray;
    public Color selectedColor = Color.cyan;
    
    public void Initialize(GraphNode node, SourceInfo source = null)
    {
        this.node = node;
        this.source = source;
        
        if (nameLabel != null)
        {
            nameLabel.text = node.name;
        }
        
        UpdateVisual();
    }
    
    public void UpdateVisual()
    {
        if (nodeRenderer == null) return;
        
        Color color = defaultColor;
        
        if (node.isSource)
        {
            color = sourceColor;
            
            if (source != null)
            {
                if (source.visited)
                {
                    color = visitedColor;
                }
                else if (source.reachable)
                {
                    color = reachableColor;
                }
                else
                {
                    color = unreachableColor;
                }
                
                if (valueLabel != null)
                {
                    valueLabel.text = $"u:{source.usefulness:F2} d:{source.reliability:F2}";
                }
            }
        }
        
        nodeRenderer.material.color = color;
        
        if (sourceIndicator != null)
        {
            sourceIndicator.SetActive(node.isSource);
        }
    }
    
    public void Highlight(bool highlight)
    {
        if (nodeRenderer != null)
        {
            nodeRenderer.material.color = highlight ? selectedColor : 
                (node.isSource ? sourceColor : defaultColor);
        }
    }
}
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class GraphEdge
{
    public GraphNode from;
    public GraphNode to;
    public float baseDistance; // базовое расстояние
    public TerrainType terrain;
    public float obstacleMultiplier = 1.0f; // множитель препятствия
    public string obstacleName;
    
    public GraphEdge(GraphNode from, GraphNode to, float distance, TerrainType terrain)
    {
        this.from = from;
        this.to = to;
        this.baseDistance = distance;
        this.terrain = terrain;
    }
}

public enum TerrainType
{
    Road,      
    Forest,    
    Swamp      
}
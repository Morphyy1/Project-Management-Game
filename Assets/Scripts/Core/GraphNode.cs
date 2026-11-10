using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[System.Serializable]
public class GraphNode
{
    public int id;
    public Vector3 position;
    public string name;
    public bool isSource;
    public float sourceValue; 
    public float visitTime; 
    public bool visited;
    
    public List<GraphEdge> connections = new List<GraphEdge>();
    
    public GraphNode(int id, Vector3 position, string name)
    {
        this.id = id;
        this.position = position;
        this.name = name;
    }
}
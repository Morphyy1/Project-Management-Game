using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SourceInfo
{
    public GraphNode node;
    public float usefulness; // полезность 0-1
    public float reliability; // достоверность 0-1
    public float visitTime; // время сбора в минутах
    public bool reachable;
    public bool visited;
    
    public float Value => usefulness * reliability;
    
    public SourceInfo(GraphNode node, float usefulness, float reliability, float visitTime)
    {
        this.node = node;
        this.usefulness = usefulness;
        this.reliability = reliability;
        this.visitTime = visitTime;
    }
}

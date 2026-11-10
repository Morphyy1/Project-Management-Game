using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[UnityEngine.DefaultExecutionOrder(-100)] // гарантия: GenerateGraph() в Start() выполнится раньше DemoController.Start()
public class CityGraph : MonoBehaviour
{
    public List<GraphNode> nodes = new List<GraphNode>();
    public List<GraphEdge> edges = new List<GraphEdge>();
    public List<SourceInfo> sources = new List<SourceInfo>();
    
    [Header("Параметры генерации")]
    public int nodeCount = 15;
    public float spawnRadius = 50f;
    public float minDistance = 10f;
    
    [Header("Параметры игрока")]
    public GraphNode playerStartNode;
    public float baseSpeed = 10f; // единиц в минуту
    public float availableTime = 60f; // доступное время в минутах
    
    [Header("Коэффициенты")]
    public float transportCoeff = 1.0f;
    public float weatherCoeff = 1.0f;
    
    void Start()
    {
        GenerateGraph();
    }
    
    public void GenerateGraph()
    {
        nodes.Clear();
        edges.Clear();
        sources.Clear();
        
        // Генерация вершин
        for (int i = 0; i < nodeCount; i++)
        {
            Vector3 pos = GetRandomPosition();
            GraphNode node = new GraphNode(i, pos, $"Локация {i + 1}");
            nodes.Add(node);
        }
        
        // Создание рёбер (соединяем ближайшие вершины)
        CreateEdges();
        
        // Выбор стартовой позиции
        playerStartNode = nodes[0];
        
        // Генерация источников
        GenerateSources();
    }
    
    Vector3 GetRandomPosition()
    {
        Vector2 randomPos = Random.insideUnitCircle * spawnRadius;
        return new Vector3(randomPos.x, 0, randomPos.y);
    }
    
    void CreateEdges()
    {
        // Соединяем каждую вершину с 2-3 ближайшими
        foreach (GraphNode node in nodes)
        {
            var nearest = nodes
                .Where(n => n != node)
                .OrderBy(n => Vector3.Distance(node.position, n.position))
                .Take(3);
            
            foreach (GraphNode nearestNode in nearest)
            {
                if (!edges.Any(e => (e.from == node && e.to == nearestNode) || 
                                   (e.from == nearestNode && e.to == node)))
                {
                    float distance = Vector3.Distance(node.position, nearestNode.position);
                    TerrainType terrain = GetRandomTerrain();
                    
                    GraphEdge edge = new GraphEdge(node, nearestNode, distance, terrain);
                    edges.Add(edge);
                    
                    node.connections.Add(edge);
                    nearestNode.connections.Add(edge);
                }
            }
        }
    }
    
    TerrainType GetRandomTerrain()
    {
        float rand = Random.value;
        if (rand < 0.6f) return TerrainType.Road;
        if (rand < 0.9f) return TerrainType.Forest;
        return TerrainType.Swamp;
    }
    
    void GenerateSources()
    {
        // Выбираем 5 случайных вершин как источники
        var sourceNodes = nodes.OrderBy(n => Random.value).Take(5).ToList();
        
        foreach (GraphNode node in sourceNodes)
        {
            node.isSource = true;
            float usefulness = Random.Range(0.5f, 1.0f);
            float reliability = Random.Range(0.6f, 1.0f);
            float visitTime = Random.Range(5f, 15f);
            
            SourceInfo source = new SourceInfo(node, usefulness, reliability, visitTime);
            sources.Add(source);
        }
    }
    
    public float GetTerrainMultiplier(TerrainType terrain)
    {
        switch (terrain)
        {
            case TerrainType.Road: return 1.0f;
            case TerrainType.Forest: return 0.7f;
            case TerrainType.Swamp: return 0.5f;
            default: return 1.0f;
        }
    }
}
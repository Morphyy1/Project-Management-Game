using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PathfindingAlgorithm : MonoBehaviour
{
    public CityGraph cityGraph;
    
    public class PathResult
    {
        public List<GraphNode> path;
        public float totalTime;
        public float totalDistance;
    }
    
    // Алгоритм Дейкстры для поиска кратчайшего пути
    public PathResult FindShortestPath(GraphNode start, GraphNode end, 
                                       float weatherCoeff, float transportCoeff)
    {
        Dictionary<GraphNode, float> distances = new Dictionary<GraphNode, float>();
        Dictionary<GraphNode, GraphNode> previous = new Dictionary<GraphNode, GraphNode>();
        List<GraphNode> unvisited = new List<GraphNode>();
        
        // Инициализация
        foreach (GraphNode node in cityGraph.nodes)
        {
            distances[node] = float.MaxValue;
            unvisited.Add(node);
        }
        
        distances[start] = 0;
        
        while (unvisited.Count > 0)
        {
            // Найти вершину с минимальным расстоянием
            GraphNode current = unvisited.OrderBy(n => distances[n]).First();
            unvisited.Remove(current);
            
            // Если достигли цели
            if (current == end) break;
            
            // Обновить расстояния для соседей
            foreach (GraphEdge edge in current.connections)
            {
                GraphNode neighbor = edge.from == current ? edge.to : edge.from;
                
                if (!unvisited.Contains(neighbor)) continue;
                
                float terrainMultiplier = cityGraph.GetTerrainMultiplier(edge.terrain);
                float travelTime = CalculateTravelTime(edge.baseDistance, terrainMultiplier, 
                                                       weatherCoeff, transportCoeff, 
                                                       edge.obstacleMultiplier);
                
                float newDistance = distances[current] + travelTime;
                
                if (newDistance < distances[neighbor])
                {
                    distances[neighbor] = newDistance;
                    previous[neighbor] = current;
                }
            }
        }
        
        // Восстановить путь
        List<GraphNode> path = new List<GraphNode>();
        GraphNode current_node = end;
        
        while (previous.ContainsKey(current_node))
        {
            path.Add(current_node);
            current_node = previous[current_node];
        }
        path.Add(start);
        path.Reverse();
        
        return new PathResult
        {
            path = path,
            totalTime = distances[end],
            totalDistance = path.Count > 1 ? 
                Vector3.Distance(path.First().position, path.Last().position) : 0
        };
    }
    
    public float CalculateTravelTime(float distance, float terrainMultiplier, 
                                     float weatherCoeff, float transportCoeff, 
                                     float obstacleMultiplier)
    {
        // Формула: T = R / (V_base × K_транспорт × K_местность × K_погода × K_событие)
        return distance / (cityGraph.baseSpeed * transportCoeff * terrainMultiplier * 
                          weatherCoeff * obstacleMultiplier);
    }
    
    // Жадный алгоритм для расчёта минимального времени обхода всех источников
    public float CalculateMinTourTime(List<SourceInfo> sources, GraphNode start, 
                                      float weatherCoeff, float transportCoeff)
    {
        List<SourceInfo> remaining = new List<SourceInfo>(sources);
        GraphNode current = start;
        float totalTime = 0;
        
        while (remaining.Count > 0)
        {
            // Найти ближайший источник
            SourceInfo nearest = null;
            float minTime = float.MaxValue;
            
            foreach (SourceInfo source in remaining)
            {
                PathResult path = FindShortestPath(current, source.node, 
                                                   weatherCoeff, transportCoeff);
                
                if (path.totalTime < minTime)
                {
                    minTime = path.totalTime;
                    nearest = source;
                }
            }
            
            // Добавить время пути и сбора
            totalTime += minTime + nearest.visitTime;
            current = nearest.node;
            remaining.Remove(nearest);
        }
        
        return totalTime;
    }
}
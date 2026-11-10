using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class DemoController : MonoBehaviour
{
    [Header("Компоненты")]
    public CityGraph cityGraph;
    public PathfindingAlgorithm pathfinding;
    public SpawnValidator spawnValidator;
    public TimeCalculator timeCalculator;
    public UIManager uiManager;
    
    [Header("Визуализация")]
    public GameObject nodePrefab;
    public GameObject edgePrefab;
    public GameObject playerPrefab;
    public LineRenderer pathLineRenderer;
    
    [Header("Параметры демонстрации")]
    public float demoSpeed = 2f; // скорость анимации
    public bool autoDemo = false;
    
    private GameObject playerObject;
    private GraphNode currentNode;
    private List<GraphNode> visitedPath = new List<GraphNode>();
    private float currentAlpha = 0;
    
    void Start()
    {
        InitializeDemo();
    }
    
    void InitializeDemo()
    {
        // Создать игрока
        if (playerPrefab != null && cityGraph.playerStartNode != null)
        {
            playerObject = Instantiate(playerPrefab, cityGraph.playerStartNode.position, 
                                       Quaternion.identity);
            currentNode = cityGraph.playerStartNode;
        }
        
        // Визуализировать граф
        VisualizeGraph();
        
        // Обновить UI
        UpdateUI();
    }
    
    void VisualizeGraph()
    {
        // Создать визуальные узлы
        foreach (GraphNode node in cityGraph.nodes)
        {
            GameObject nodeObj = Instantiate(nodePrefab, node.position, Quaternion.identity);
            NodeVisual nodeVisual = nodeObj.GetComponent<NodeVisual>();
            
            SourceInfo source = cityGraph.sources.FirstOrDefault(s => s.node == node);
            nodeVisual.Initialize(node, source);
        }
        
        // Создать визуальные рёбра
        foreach (GraphEdge edge in cityGraph.edges)
        {
            CreateEdgeVisual(edge);
        }
    }
    
    void CreateEdgeVisual(GraphEdge edge)
    {
        GameObject edgeObj = Instantiate(edgePrefab);
        LineRenderer lineRenderer = edgeObj.GetComponent<LineRenderer>();
        
        if (lineRenderer != null)
        {
            lineRenderer.positionCount = 2;
            lineRenderer.SetPosition(0, edge.from.position);
            lineRenderer.SetPosition(1, edge.to.position);
            
            // Цвет в зависимости от типа местности
            Color color = Color.gray;
            switch (edge.terrain)
            {
                case TerrainType.Road: color = Color.gray; break;
                case TerrainType.Forest: color = Color.green; break;
                case TerrainType.Swamp: color = Color.green; break;
            }
            lineRenderer.startColor = color;
            lineRenderer.endColor = color;
        }
    }
    
    public void OnGenerateClicked()
    {
        cityGraph.GenerateGraph();
        InitializeDemo();
        uiManager.UpdateStatus("Граф сгенерирован");
    }
    
    public void OnValidateClicked()
    {
        StartCoroutine(ValidateAndShowResult());
    }
    
    IEnumerator ValidateAndShowResult()
    {
        uiManager.UpdateStatus("Проверка справедливости спавна...");
        yield return new WaitForSeconds(1f);
        
        SpawnValidator.ValidationResult result = spawnValidator.ValidateSpawn(
            timeCalculator.weatherCoeff, timeCalculator.transportCoeff);
        
        if (result.isValid)
        {
            currentAlpha = result.alpha;
            uiManager.UpdateAlphaDisplay(currentAlpha);
            uiManager.UpdateStatus($"✓ Спавн валиден! α = {currentAlpha:F2}");
            uiManager.ShowWarning($"Коэффициент достижимости: {currentAlpha:F2}");
            
            // Подсветить достижимые источники
            HighlightReachableSources(result.reachableSources);
        }
        else
        {
            uiManager.UpdateStatus($"✗ {result.rejectionReason}");
            uiManager.ShowWarning($"Спавн невалиден: {result.rejectionReason}");
        }
        
        yield return new WaitForSeconds(2f);
    }
    
    void HighlightReachableSources(List<SourceInfo> reachableSources)
    {
        foreach (SourceInfo source in cityGraph.sources)
        {
            GameObject nodeObj = FindNodeObject(source.node);
            if (nodeObj != null)
            {
                NodeVisual nodeVisual = nodeObj.GetComponent<NodeVisual>();
                nodeVisual.UpdateVisual();
            }
        }
    }
    
    public void OnNodeClicked(GraphNode node)
    {
        if (currentNode == null) return;
        
        // Найти ребро между текущей и выбранной вершиной
        GraphEdge edge = currentNode.connections.FirstOrDefault(
            e => e.from == node || e.to == node);
        
        if (edge == null)
        {
            uiManager.ShowWarning("Нет прямого пути к этой вершине!");
            return;
        }
        
        // Проверить возможность перехода
        if (!timeCalculator.CanTravel(edge))
        {
            uiManager.ShowWarning("Недостаточно времени для перехода!");
            return;
        }
        
        StartCoroutine(MoveToNode(node, edge));
    }
    
    IEnumerator MoveToNode(GraphNode targetNode, GraphEdge edge)
    {
        uiManager.UpdateStatus($"Перемещение к {targetNode.name}...");
        
        // Рассчитать время перехода
        float travelTime = timeCalculator.CalculateTravelTime(edge);
        
        // Анимация перемещения
        float elapsed = 0;
        Vector3 startPos = playerObject.transform.position;
        Vector3 endPos = targetNode.position;
        
        while (elapsed < travelTime / demoSpeed)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / (travelTime / demoSpeed));
            playerObject.transform.position = Vector3.Lerp(startPos, endPos, t);
            
            yield return null;
        }
        
        // Обновить состояние
        timeCalculator.SpendTime(travelTime);
        currentNode = targetNode;
        visitedPath.Add(targetNode);
        
        // Проверить, является ли вершина источником
        SourceInfo source = cityGraph.sources.FirstOrDefault(s => s.node == targetNode);
        if (source != null && !source.visited)
        {
            if (timeCalculator.CanVisitSource(source))
            {
                yield return StartCoroutine(CollectSource(source));
            }
            else
            {
                uiManager.ShowWarning("Недостаточно времени для сбора информации!");
            }
        }
        
        // Проверить риски
        CheckRisks();
        
        // Обновить UI
        UpdateUI();
        
        uiManager.UpdateStatus($"Достигнута вершина: {targetNode.name}");
    }
    
    IEnumerator CollectSource(SourceInfo source)
    {
        uiManager.UpdateStatus($"Сбор информации из {source.node.name}...");
        
        // Анимация сбора
        yield return new WaitForSeconds(source.visitTime / demoSpeed);
        
        source.visited = true;
        timeCalculator.SpendTime(source.visitTime);
        
        uiManager.ShowWarning($"Собрана информация: ценность = {source.Value:F2}");
        
        // Обновить визуализацию
        GameObject nodeObj = FindNodeObject(source.node);
        if (nodeObj != null)
        {
            NodeVisual nodeVisual = nodeObj.GetComponent<NodeVisual>();
            nodeVisual.UpdateVisual();
        }
    }
    
    void CheckRisks()
    {
        // Пересчитать кратчайший путь до оставшихся источников
        List<SourceInfo> unvisitedSources = cityGraph.sources
            .Where(s => !s.visited && s.reachable)
            .ToList();
        
        if (unvisitedSources.Count > 0)
        {
            float minTimeToSources = float.MaxValue;
            
            foreach (SourceInfo source in unvisitedSources)
            {
                PathfindingAlgorithm.PathResult path = pathfinding.FindShortestPath(
                    currentNode, source.node, timeCalculator.weatherCoeff, 
                    timeCalculator.transportCoeff);
                
                float totalTime = path.totalTime + source.visitTime;
                if (totalTime < minTimeToSources)
                {
                    minTimeToSources = totalTime;
                }
            }
            
            if (minTimeToSources > timeCalculator.remainingTime)
            {
                uiManager.ShowWarning("⚠ Внимание: не хватит времени на оставшиеся источники!");
            }
        }
    }
    
    public void OnFinishClicked()
    {
        CalculateFinalScore();
    }
    
    void CalculateFinalScore()
    {
        float factValue = cityGraph.sources.Where(s => s.visited).Sum(s => s.Value);
        float maxValue = cityGraph.sources.Sum(s => s.Value);
        
        float score = maxValue > 0 ? (factValue / maxValue) * 100f : 0;
        score = Mathf.Min(score, 100f);
        
        uiManager.UpdateStatus($"Итоговая успеваемость: {score:F1}%");
        uiManager.ShowWarning($"Результат: {score:F1}% (Факт: {factValue:F2} / Макс: {maxValue:F2})");
    }
    
    void UpdateUI()
    {
        uiManager.UpdateTimeDisplay(timeCalculator.remainingTime, timeCalculator.currentTime);
        uiManager.UpdateWeatherDisplay(timeCalculator.weatherCoeff);
        uiManager.UpdateSourceList(cityGraph.sources);
    }
    
    GameObject FindNodeObject(GraphNode node)
    {
        // Найти визуальный объект узла
        foreach (GameObject obj in FindObjectsOfType<GameObject>())
        {
            NodeVisual nodeVisual = obj.GetComponent<NodeVisual>();
            if (nodeVisual != null && nodeVisual.node == node)
            {
                return obj;
            }
        }
        return null;
    }
    
    void Update()
    {
        // Обработка кликов по узлам
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            
            if (Physics.Raycast(ray, out hit))
            {
                NodeVisual nodeVisual = hit.collider.GetComponent<NodeVisual>();
                if (nodeVisual != null)
                {
                    OnNodeClicked(nodeVisual.node);
                }
            }
        }
    }
}
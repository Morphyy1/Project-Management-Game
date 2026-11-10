using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using static PathfindingAlgorithm;

public class SpawnValidator : MonoBehaviour
{
    public CityGraph cityGraph;
    public PathfindingAlgorithm pathfinding;
    
    [Header("Параметры валидации")]
    public float pRequired = 0.7f; // минимальная доля достижимых источников
    public float alphaMin = 0.7f; // минимальный коэффициент достижимости
    public float timeReservePercent = 0.15f; // резерв времени 15%
    public int maxAttempts = 5;
    
    public class ValidationResult
    {
        public bool isValid;
        public float alpha;
        public float minTourTime;
        public List<SourceInfo> reachableSources;
        public string rejectionReason;
    }
    
    public ValidationResult ValidateSpawn(float weatherCoeff, float transportCoeff)
    {
        float availableTime = cityGraph.availableTime;
        float timeReserve = availableTime * timeReservePercent;
        
        // Шаг 1: Проверка индивидуальной достижимости
        List<SourceInfo> reachableSources = new List<SourceInfo>();
        
        foreach (SourceInfo source in cityGraph.sources)
        {
            PathResult pathToSource = pathfinding.FindShortestPath(
                cityGraph.playerStartNode, source.node, weatherCoeff, transportCoeff);
            
            // Время туда + время сбора + время обратно
            float totalTime = pathToSource.totalTime * 2 + source.visitTime;
            
            if (totalTime <= availableTime)
            {
                source.reachable = true;
                reachableSources.Add(source);
            }
            else
            {
                source.reachable = false;
            }
        }
        
        // Шаг 2: Проверка достаточности
        float requiredCount = pRequired * cityGraph.sources.Count;
        if (reachableSources.Count < requiredCount)
        {
            return new ValidationResult
            {
                isValid = false,
                rejectionReason = $"Недостаточно достижимых источников: {reachableSources.Count} из {requiredCount}"
            };
        }
        
        // Шаг 3: Расчёт Tmin
        float minTourTime = pathfinding.CalculateMinTourTime(
            reachableSources, cityGraph.playerStartNode, weatherCoeff, transportCoeff);
        
        // Шаг 4: Расчёт коэффициента α
        float totalValue = cityGraph.sources.Sum(s => s.Value);
        float reachableValue = reachableSources.Sum(s => s.Value);
        float alpha = totalValue > 0 ? reachableValue / totalValue : 0;
        
        // Шаг 5: Проверка запаса времени
        if (minTourTime + timeReserve > availableTime)
        {
            return new ValidationResult
            {
                isValid = false,
                rejectionReason = $"Недостаточный запас времени: Tmin={minTourTime:F1} мин, доступно={availableTime} мин"
            };
        }
        
        // Шаг 6: Проверка α
        if (alpha < alphaMin)
        {
            return new ValidationResult
            {
                isValid = false,
                rejectionReason = $"Коэффициент достижимости слишком низкий: α={alpha:F2}"
            };
        }
        
        return new ValidationResult
        {
            isValid = true,
            alpha = alpha,
            minTourTime = minTourTime,
            reachableSources = reachableSources
        };
    }
}
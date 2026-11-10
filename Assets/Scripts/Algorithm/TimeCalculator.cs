using UnityEngine;

public class TimeCalculator : MonoBehaviour
{
    public CityGraph cityGraph;
    
    [Header("Текущее состояние")]
    public float currentTime = 0;
    public float remainingTime;
    public float weatherCoeff = 1.0f;
    public float transportCoeff = 1.0f;
    public float stateCoeff = 1.0f;
    public float dayEventCoeff = 1.0f;
    
    void Start()
    {
        ResetTime();
    }
    
    public void ResetTime()
    {
        currentTime = 0;
        remainingTime = cityGraph != null ? cityGraph.availableTime : remainingTime;
    }
    
    public float CalculateWorkability()
    {
        // Работоспособность = КоэфПогоды × КоэфСостояния × КоэфСобытияДня
        return weatherCoeff * stateCoeff * dayEventCoeff;
    }
    
    public float CalculateTravelTime(GraphEdge edge)
    {
        float terrainMultiplier = cityGraph.GetTerrainMultiplier(edge.terrain);
        float workability = CalculateWorkability();
        
        // T = R / (V_base × K_транспорт × K_местность × K_погода × K_событие)
        return edge.baseDistance / (cityGraph.baseSpeed * transportCoeff * 
                                   terrainMultiplier * weatherCoeff * 
                                   edge.obstacleMultiplier * workability);
    }
    
    public bool CanTravel(GraphEdge edge)
    {
        float travelTime = CalculateTravelTime(edge);
        return travelTime <= remainingTime;
    }
    
    public bool CanVisitSource(SourceInfo source)
    {
        return source.visitTime <= remainingTime;
    }
    
    public void SpendTime(float time)
    {
        remainingTime -= time;
        currentTime += time;
        
        if (remainingTime < 0) remainingTime = 0;
    }
    
    public float CalculateMaxWorkAmount(float productivity, float workerCoeff, 
                                        float toolCoeff, float workability)
    {
        // МаксВлезет = ОсталосьЧасов × Произв × КоэфР × КоэфИ × Работоспособность
        float remainingHours = remainingTime / 60f;
        return remainingHours * productivity * workerCoeff * toolCoeff * workability;
    }
}
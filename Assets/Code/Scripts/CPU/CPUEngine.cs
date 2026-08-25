using System;
using UnityEngine;

public class CPUEngine : MonoBehaviour
{
    [SerializeField] private CPUDataDefinition r_Definition;
    [SerializeField] private CPUData m_CPUData;

    [SerializeField] private float retryTimer = 0;

    private void OnEnable() {
        if(m_CPUData == null) return;

        if (m_CPUData.Consumed == false) { 
            m_CPUData = new CPUData(r_Definition);
        }
    }

    private void Update() {
        retryTimer += Time.deltaTime;

        if (retryTimer > m_CPUData.RetryInterval) {
            RetryPressing(m_CPUData.PressChance);
        }
    }



    private void RetryPressing(float pressChance) {
        // Retry pressing math

        retryTimer = 0;
    }



}




[Serializable]
public class CPUData : ICPUDataLike {

    private CPUDataDefinition m_Definition;
    public bool Consumed;

    [SerializeField] private float m_CurrentPressChance;
    [SerializeField] private float m_CurrentRetryInterval;

    public float PressChance => m_CurrentPressChance;
    public float RetryInterval => m_CurrentRetryInterval;

    public CPUData(CPUDataDefinition def) {
        m_Definition = def;
        ConsumeInitialDefinition();
    }


    private void ConsumeInitialDefinition() {
        if(Consumed) return;
        Consumed = true;


        m_CurrentPressChance = m_Definition.PressChance;
        m_CurrentRetryInterval = m_Definition.RetryInterval;
    }


}

public interface ICPUDataLike {
    float PressChance { get; }
    float RetryInterval { get; }
}

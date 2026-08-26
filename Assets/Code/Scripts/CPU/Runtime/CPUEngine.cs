using System;
using UnityEngine;

public class CPUEngine : MonoBehaviour {

    [SerializeField] private TrustSequenceManager r_TrustSequenceManager;
    [SerializeField] private CPUDataDefinition r_DataDefinition;
    [SerializeField] private CPURuntimeData m_CPURuntimeData;

    [SerializeField] private CPUBehaviourDefinition m_CPUBehaviourDefinition;
    [SerializeField] private CPURuntimeBehaviour m_RuntimeBehaviour;

    [SerializeField] private float retryTimer = 0;

    private void OnEnable() {
        if (m_CPURuntimeData == null || !m_CPURuntimeData.Consumed)
        {
            m_CPURuntimeData = new CPURuntimeData(r_DataDefinition);
        }

        if (m_CPUBehaviourDefinition == null) return;

        m_RuntimeBehaviour = m_CPUBehaviourDefinition.CreateRuntimeBehaviour();
        // Check for CPURuntimeBehaviour 
        Debug.Log(m_RuntimeBehaviour);
        Debug.Log(m_CPURuntimeData);
    }

    private void Update() {


        m_RuntimeBehaviour.OnEngineUpdate(m_CPURuntimeData);

        retryTimer += Time.deltaTime;

        if (retryTimer > m_CPURuntimeData.RetryInterval)
        {
            retryTimer = 0;

            bool didPress = RetryPressing(m_CPURuntimeData.PressChance);

            if (didPress)
            {
                Debug.Log("CPU butona bastı!");
                // r_TrustSequenceManager üzerinden ilgili aksiyon burada tetiklenebilir
            }
        }
    }



    private bool RetryPressing(float pressChance)
    {
        float roll = UnityEngine.Random.value; 

        bool success = roll < pressChance;

        Debug.Log($"Retry pressing - roll: {roll:F3}, chance: {pressChance:F3}, success: {success}");

        return success;
    }

}




[Serializable]
public class CPURuntimeData : ICPUDataLike {

    private CPUDataDefinition m_Definition;
    public bool Consumed;

    [SerializeField] private float m_CurrentPressChance;
    [SerializeField] private float m_CurrentRetryInterval;


    public float PressChance { get => m_CurrentPressChance; set => m_CurrentPressChance = value; }
    public float RetryInterval { get => m_CurrentRetryInterval; set => m_CurrentRetryInterval = value; }

    public CPURuntimeData(CPUDataDefinition def) {
        m_Definition = def;
        ConsumeInitialDefinition();
    }


    private void ConsumeInitialDefinition() {
        if (Consumed) return;
        Consumed = true;


        m_CurrentPressChance = m_Definition.PressChance;
        m_CurrentRetryInterval = m_Definition.RetryInterval;
    }


}

public interface ICPUDataLike {
    float PressChance { get; set; }
    float RetryInterval { get; set; }
}

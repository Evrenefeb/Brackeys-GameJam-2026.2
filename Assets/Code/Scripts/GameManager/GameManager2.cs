using Ami.BroAudio;
using Lean.Gui;
using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class GameManager2 : MonoBehaviour {
    public List<TrustSequenceObject> r_TrustSequenceList = new();
    public ShopSequenceObject r_UpgradeSequenceObject;

    public SceneManager r_SceneManager;



    [SerializeField] private LeanButton r_PlayerButton;
    [SerializeField] private InventoryManager r_PlayerInventoryManager;


    [SerializeField] private GameData p_GameData;
    [SerializeField] private ItemDatabase r_ItemDatabase;
    [SerializeField] private PlayerInventoryData r_CommonPlayerInventoryData;
    [SerializeField] private EnvironmentManager r_EnvironmentManager;
    public float FlowInvokeDelay = 0.75f;


    private List<SequenceObject> m_AllSequenceObjects;

    public float m_CurrentQuota;
    public float m_CurrentUsableQuota;
    [SerializeField][ReadOnly] private float m_RequiredQuota;


    public LeanButton PlayerButton => r_PlayerButton;
    public ItemDatabase ItemDatabase => r_ItemDatabase;
    public PlayerInventoryData PlayerInventoryData => r_CommonPlayerInventoryData;
    public GameData GameData => p_GameData;

    [SerializeField] private TMP_Text txt_Glasses;
    public TMP_Text TXT_Glasses => txt_Glasses;
    private float infoTimer = 0;
    private float maxInfoTimer = 5f;


    [Space(20)]
    [SerializeField] private CampaignFlow p_CampaignFlow;
    [SerializeField][ReadOnly] private int m_CampaignFlowIndex;
    [SerializeField][ReadOnly] private TrustSequenceManager _currentTrustSequenceManager;

    public GameObject Hand;

    float displayPressChance;
    float displayRetryInterval;

    private void Start() {
        ChangeCurrentUsableQuota(GameData.p_StartMoney, QuotaChangeMode.OVERRRIDE);
    }

    private void Update() {


        if (Hand) {
            if (Hand.gameObject.activeSelf) {
                Invoke(nameof(HandDisable), 2.5f);
            }
        }
        

        if (txt_Glasses.gameObject.activeSelf) {
            infoTimer += Time.deltaTime;

            displayPressChance = _currentTrustSequenceManager.CPUEngine.CPURuntimeData.PressChance;
            displayRetryInterval = _currentTrustSequenceManager.CPUEngine.CPURuntimeData.RetryInterval;



            if (_currentTrustSequenceManager.CPUEngine.CPURuntimeData.PressChance <= 0){
                displayPressChance = 0f;
            }

            if (_currentTrustSequenceManager.CPUEngine.CPURuntimeData.RetryInterval <= 0) {
                displayRetryInterval = 0f;
            }

            string info = $"Press Chance: " +
                $"{(displayPressChance * 100f).ToString("F1")}%" +
                $"\nRetry Interval: {displayRetryInterval} s";

            txt_Glasses.text = info;

            if (infoTimer > maxInfoTimer) {
                txt_Glasses.gameObject.SetActive(false);
                infoTimer = 9999999f;
            }
        }
    }

    private void HandDisable() {
        Hand.SetActive(false);
    }

    private void OnEnable() {

        // Reset Owned Items
        r_CommonPlayerInventoryData.Reset();

        // Disable PlayerButton
        PlayerButton.enabled = false;

        Hand.SetActive(false );

        txt_Glasses.gameObject.SetActive(false);

        // Setup Sequence Object List
        SetupSequenceObjects();

        // Setup Campaign Flow
        m_CampaignFlowIndex = 0;
        Flow();

        // Setup Quotas
        m_CurrentQuota = 0;
        m_RequiredQuota = p_GameData.p_RequiredQuota;




    }

    private void SetupSequenceObjects() {
        m_AllSequenceObjects = new List<SequenceObject>();

        foreach (var sequenceObject in r_TrustSequenceList) {
            m_AllSequenceObjects.Add(sequenceObject);
        }

        m_AllSequenceObjects.Add(r_UpgradeSequenceObject);
    }

    public void ChangeTrustSequence(int index) {

        if (index >= r_TrustSequenceList.Count || index < 0) return;

        CloseAllSequenceObjects();

        r_TrustSequenceList[index].gameObject.SetActive(true);

    }

    public void ChangeToUpgradeSequence() {
        CloseAllSequenceObjects();

        r_UpgradeSequenceObject.gameObject.SetActive(true);
    }

    private void CloseAllSequenceObjects() {
        foreach (SequenceObject seq in m_AllSequenceObjects) {
            seq.gameObject.SetActive(false);
        }
    }

    public void ChangeCurrentQuota(float amount, QuotaChangeMode changeMode) {
        switch (changeMode) {
            case QuotaChangeMode.ADD:
                m_CurrentQuota += amount;
                break;
            case QuotaChangeMode.SUBTRACT:
                m_CurrentQuota -= amount;
                break;
            case QuotaChangeMode.OVERRRIDE:
                m_CurrentQuota = amount;
                break;
            case QuotaChangeMode.MULTIPLY:
                m_CurrentQuota *= amount;
                break;
        }
    }

    public void ChangeCurrentUsableQuota(float amount, QuotaChangeMode changeMode) {
        switch (changeMode) {
            case QuotaChangeMode.ADD:
                m_CurrentUsableQuota += amount;
                break;
            case QuotaChangeMode.SUBTRACT:
                m_CurrentUsableQuota -= amount;
                break;
            case QuotaChangeMode.OVERRRIDE:
                m_CurrentUsableQuota = amount;
                break;
            case QuotaChangeMode.MULTIPLY:
                m_CurrentUsableQuota *= amount;
                break;
        }
    }

    public void Flow() {
        if (m_CampaignFlowIndex >= p_CampaignFlow.SequenceQueue.Count) {
            EndGame();
            return;
        }

        BroAudio.Stop(BroAudioType.All, 0.2f);

        CloseAllSequenceObjects();

        SequenceObject sequenceObject = p_CampaignFlow.SequenceQueue[m_CampaignFlowIndex];

        if (sequenceObject.GetType() == typeof(TrustSequenceObject)) {
            //Debug.Log("Trust Sequence");

            _currentTrustSequenceManager = sequenceObject.GetComponentInChildren<TrustSequenceManager>();
            _currentTrustSequenceManager.OnSequenceStarted += CurrentTrustSequenceManager_SequenceStarted;
        }
        else {
            //Debug.Log("Upgrade Sequence");
            if (_currentTrustSequenceManager != null) {
                _currentTrustSequenceManager.OnSequenceStarted -= CurrentTrustSequenceManager_SequenceStarted;
                _currentTrustSequenceManager = null;
            }

            r_PlayerInventoryManager.DemolishInventoryUI();
            PlayerButton.enabled = false;
        }

        sequenceObject.gameObject.SetActive(true);
        m_CampaignFlowIndex++;
    }

    private void CurrentTrustSequenceManager_SequenceStarted() {
        //Debug.Log("CurrentTrustSequenceManager_SequenceStarted");

        r_PlayerInventoryManager.BuildInventoryUI(_currentTrustSequenceManager);
        PlayerButton.enabled = true;
    }

    public void SequenceFinished() {
        r_EnvironmentManager.HandleTransition(FlowInvokeDelay, Flow);
    }


    private const string p_WinSceneName = "ESCENE_Win";
    private const string p_LoseSceneName = "ESCENE_Lose";

    private void EndGame() {

        ChangeCurrentQuota(0 , QuotaChangeMode.OVERRRIDE);
        ChangeCurrentUsableQuota(0 , QuotaChangeMode.OVERRRIDE);

        if (m_CurrentQuota >= m_RequiredQuota) {
            Debug.Log("Win");
            r_SceneManager.ChangeScene(p_WinSceneName);
        }
        else {
            Debug.Log("Lose");
            r_SceneManager.ChangeScene(p_LoseSceneName);
        }
    }

}

[Serializable]
public class CampaignFlow {
    public List<SequenceObject> SequenceQueue = new();
}

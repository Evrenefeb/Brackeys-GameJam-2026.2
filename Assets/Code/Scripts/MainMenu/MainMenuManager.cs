using Lean.Gui;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuManager : MonoBehaviour {


    [SerializeField] private TMP_Text txt_GameName;
    [SerializeField] private TMP_Text txt_GameVersion;


    [SerializeField] private LeanButton btn_Play;
    [SerializeField] private LeanButton btn_Credits;
    [SerializeField] private LeanButton btn_Exit;

    [SerializeField] private SceneManager r_SceneManager;

    private const string SCENE_Tut = nameof(SCENE_Tut);



    #region Unity Methods

    // Unity Methods


    void Start() {
        
    }

    private void OnEnable() {
        Initialize();
    }

    #endregion




    #region Initialization

    private void Initialize() {
        btn_Play.OnClick.AddListener(btn_Play_OnClick);
        btn_Credits.OnClick.AddListener(btn_Credits_OnClick);
        btn_Exit.OnClick.AddListener(btn_Exit_OnClick);

        txt_GameName.text = Application.productName;
        txt_GameVersion.text = $"v{Application.version}";


    }

    #endregion

    #region UI Methods

    // UI Methods
    private void btn_Play_OnClick() {
        //Debug.Log("btn_Play_OnClick");

        r_SceneManager.ChangeScene(SCENE_Tut);

    }

    private void btn_Credits_OnClick() {
        //Debug.Log("btn_Credits_OnClick");
        r_SceneManager.ChangeScene("SCENE_Credits");
    }

    private void btn_Exit_OnClick() {
        //Debug.Log("btn_Exit_OnClick");
        Application.Quit();
    }

    #endregion

}

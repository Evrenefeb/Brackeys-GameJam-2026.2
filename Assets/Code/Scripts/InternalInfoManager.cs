using UnityEngine;

public class InternalInfoManager : MonoBehaviour {
    public bool Toggle = false;

    [SerializeField] private GameObject r_Graphy;
    [SerializeField] private GameObject r_Console;

    private void OnValidate() {
        if (Toggle) {
            ToggleInfo();
            Toggle = false;
        }
    }

    private void Update() {
        if (Input.GetKeyDown(KeyCode.F3)) {
            ToggleInfo();
        }
    }

    private void ToggleInfo() {
        r_Graphy.SetActive(!r_Graphy.activeSelf);
        r_Console.SetActive(!r_Console.activeSelf);
    }
}

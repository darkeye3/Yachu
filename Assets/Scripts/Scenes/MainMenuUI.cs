using UnityEngine;
using UnityEngine.UI;

public class MainMenuUI : MonoBehaviour
{
    [SerializeField] private Button btnStart;
    [SerializeField] private Button btnQuit;

    void Start()
    {
        Debug.Log($"[MainMenuUI] Start — btnStart={btnStart != null}, btnQuit={btnQuit != null}, GameManager={GameManager.Instance != null}");

        btnStart?.onClick.AddListener(() =>
        {
            Debug.Log("[MainMenuUI] 시작 버튼 클릭 → LobbySetup 씬 로드");
            GameManager.Instance.LoadScene(GameManager.SCENE_LOBBY);
        });
        btnQuit?.onClick.AddListener(() =>
        {
            Debug.Log("[MainMenuUI] 종료 버튼 클릭");
            Application.Quit();
        });

        if (btnStart == null) Debug.LogWarning("[MainMenuUI] btnStart가 Inspector에 연결되지 않았습니다!");
        if (btnQuit  == null) Debug.LogWarning("[MainMenuUI] btnQuit이 Inspector에 연결되지 않았습니다!");

        AudioManager.Instance?.PlayLobbyBGM();
    }
}

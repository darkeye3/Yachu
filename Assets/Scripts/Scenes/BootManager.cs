using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 00_Boot 씬: GameManager 등 싱글턴 초기화 후 MainMenu로 이동
/// </summary>
public class BootManager : MonoBehaviour
{
    [SerializeField] private float delay = 0.5f;

    IEnumerator Start()
    {
        // GameManager가 없으면 프리팹에서 생성
        if (GameManager.Instance == null)
        {
            var prefab = Resources.Load<GameObject>("GameManager");
            if (prefab) Instantiate(prefab);
        }

        yield return new WaitForSeconds(delay);
        SceneManager.LoadScene(GameManager.SCENE_MAIN);
    }
}

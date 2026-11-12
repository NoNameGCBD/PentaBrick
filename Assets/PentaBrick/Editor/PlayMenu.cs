using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PentaBrick.EditorTools
{
    /// <summary>메뉴 PentaBrick > Play: 게임 씬을 열고 바로 플레이한다. 명령줄에서는 -executeMethod PentaBrick.EditorTools.PlayMenu.Play</summary>
    public static class PlayMenu
    {
        [MenuItem("PentaBrick/Play")]
        public static void Play()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            Debug.Log("PentaBrick: entering play mode");
            EditorApplication.delayCall += EditorApplication.EnterPlaymode; // 에디터 시작 직후에는 한 박자 늦춰야 한다
        }
    }
}

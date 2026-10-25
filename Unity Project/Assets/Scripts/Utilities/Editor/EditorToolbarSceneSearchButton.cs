using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.SceneManagement;
using UnityEditor.Toolbars;
using UnityEngine;

public class EditorToolbarSceneButton : MonoBehaviour
{
    [MainToolbarElement("Scenes Button", defaultDockPosition = MainToolbarDockPosition.Right)]
    public static MainToolbarElement GetScenesButton()
    {
        Texture2D assetIcon = EditorGUIUtility.IconContent("SceneAsset Icon").image as Texture2D;
        return new MainToolbarButton(new MainToolbarContent("Open Scene", assetIcon, "Search Scenes"), OnOpenSearchableScenesMenu);
    }

    private static void OnOpenSearchableScenesMenu()
    {
        SceneSearchableMenu searchableMenu = ScriptableObject.CreateInstance<SceneSearchableMenu>();
        SearchWindow.Open(new SearchWindowContext(GUIUtility.GUIToScreenPoint(Event.current.mousePosition)), searchableMenu);
    }

    private static void OnOpenScenesMenu()
    {
        string[] guids = AssetDatabase.FindAssets("t:SceneAsset");
        GenericMenu menu = new GenericMenu();

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            SceneAsset asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);

            if (AssetDatabase.IsOpenForEdit(asset))
                menu.AddItem(new GUIContent(asset.name), false, () => { OpenScene(path); });
        }
        menu.ShowAsContext();
    }
    
    private static void OpenScene(string scenePath)
    {
        if (EditorApplication.isPlaying)
        {
            if (EditorUtility.DisplayDialog("Stop Play Mode?", "You need to stop play mode to open a new scene. Do you want to stop play mode?", "Yes", "No"))
            {
                EditorApplication.isPlaying = false;
                EditorApplication.delayCall += () => EditorSceneManager.OpenScene(scenePath);
            }
        }
        else
        {
            EditorSceneManager.OpenScene(scenePath);
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.SceneManagement;
using UnityEngine;

public class SceneSearchableMenu : ScriptableObject, ISearchWindowProvider
{
    public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
    {
        List<SearchTreeEntry> tree = new List<SearchTreeEntry>();
        tree.Add(new SearchTreeGroupEntry(new GUIContent("Scene Assets"), 0));

        string[] guids = AssetDatabase.FindAssets("t:SceneAsset");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            SceneAsset asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);

            if (AssetDatabase.IsOpenForEdit(asset) == false) continue;

            SearchTreeEntry entry = new SearchTreeEntry(new GUIContent(asset.name, EditorGUIUtility.IconContent("SceneAsset Icon").image as Texture2D))
            {
                level = 1,
                userData = asset
            };
            tree.Add(entry);
        }
        
        return tree;
    }

    public bool OnSelectEntry(SearchTreeEntry SearchTreeEntry, SearchWindowContext context)
    {
        if (SearchTreeEntry.userData is SceneAsset scene)
        {
            // Ask user to save modified scenes first
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return false;

            string path = AssetDatabase.GetAssetPath(scene);

            // Open the selected scene
            EditorSceneManager.OpenScene(path);

            return true;
        }

        return false;
    }
}

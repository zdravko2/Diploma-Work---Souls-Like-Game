using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SceneDefinition))]
public class SceneDefinitionEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        SceneDefinition sceneDef = (SceneDefinition)target;

        GUILayout.Space(10);

        if (GUILayout.Button("Regenerate Scene ID"))
        {
            Undo.RecordObject(sceneDef, "Regenerate Scene ID");
            sceneDef.sceneId = System.Guid.NewGuid().ToString();
            EditorUtility.SetDirty(sceneDef);
        }
    }
}
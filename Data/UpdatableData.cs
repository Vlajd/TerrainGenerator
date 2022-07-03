using UnityEngine;
using UnityEditor;

public class UpdatableData : ScriptableObject
{
    public event System.Action OnValuesUpdated;
    public bool autoUpdate;

    public void NotifyUpdatedValues()
    {
        EditorApplication.update -= NotifyUpdatedValues;
        if (OnValuesUpdated != null)
            OnValuesUpdated();
    }

    private protected virtual void OnValidate()
    {
        if (autoUpdate)
            EditorApplication.update += NotifyUpdatedValues;
    }
}

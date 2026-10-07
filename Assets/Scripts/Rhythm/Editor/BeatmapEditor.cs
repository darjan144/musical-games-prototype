using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Beatmap))]
public class BeatmapEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var beatmap = (Beatmap)target;
        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(beatmap.clip == null))
        {
            if (GUILayout.Button("Analyze tempo from clip"))
            {
                if (BeatmapAnalyzer.EstimateTempo(beatmap.clip, out float bpm, out float offset))
                {
                    Undo.RecordObject(beatmap, "Analyze tempo");
                    beatmap.bpm = bpm;
                    beatmap.firstBeatOffset = offset;
                    EditorUtility.SetDirty(beatmap);
                }
            }
            if (GUILayout.Button("Halve tempo")) ScaleTempo(beatmap, 0.5f);
            if (GUILayout.Button("Double tempo")) ScaleTempo(beatmap, 2f);
        }
    }

    // Tempo detection can land on half or double the felt beat; hits are stored in beats, so rescale them too.
    static void ScaleTempo(Beatmap beatmap, float factor)
    {
        Undo.RecordObject(beatmap, "Scale tempo");
        beatmap.bpm *= factor;
        for (int i = 0; i < beatmap.hits.Count; i++)
        {
            var hit = beatmap.hits[i];
            hit.beat *= factor;
            beatmap.hits[i] = hit;
        }
        EditorUtility.SetDirty(beatmap);
    }
}

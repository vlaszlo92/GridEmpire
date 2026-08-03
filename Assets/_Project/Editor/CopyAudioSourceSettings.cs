#if UNITY_EDITOR
using GridEmpire.Gameplay;
using UnityEditor;
using UnityEngine;

namespace GridEmpire.EditorTools
{
    public class CopyAudioSourceSettings : MonoBehaviour
    {
        [MenuItem("Tools/Copy Selected AudioSource to All Combat Players")]
        public static void CopySettings()
        {
            // 1. Kijelölt objektum lekérése (lehet Scene objektum vagy Prefab asset is)
            GameObject selectedGo = Selection.activeGameObject;

            if (selectedGo == null || !selectedGo.TryGetComponent<AudioSource>(out var sourceAudio))
            {
                Debug.LogError("[AudioCopy] Hiba: Először jelöld ki azt az objektumot (Scene-ben vagy Prefabot), amin a beállított AudioSource van!");
                return;
            }

            // Beállítások kiolvasása
            float targetMin = sourceAudio.minDistance;
            float targetMax = sourceAudio.maxDistance;
            AudioRolloffMode targetRolloff = sourceAudio.rolloffMode;
            AnimationCurve targetCurve = sourceAudio.GetCustomCurve(AudioSourceCurveType.CustomRolloff);

            int updatedSceneCount = 0;
            int updatedPrefabCount = 0;

            // -------------------------------------------------------------
            // A) Frissítés a SCENE-ben lévő objektumokon
            // -------------------------------------------------------------
            var scenePlayers = FindObjectsByType<CombatAudioPlayer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var player in scenePlayers)
            {
                if (player.gameObject == selectedGo) continue;

                if (player.TryGetComponent<AudioSource>(out var targetAudio))
                {
                    Undo.RecordObject(targetAudio, "Copy AudioSource Settings");
                    ApplySettings(targetAudio, targetMin, targetMax, targetRolloff, targetCurve);
                    EditorUtility.SetDirty(targetAudio);
                    updatedSceneCount++;
                }
            }

            // -------------------------------------------------------------
            // B) Frissítés a PROJEKT MAPPÁBAN (Assets) lévő Prefabokon
            // -------------------------------------------------------------
            string[] guids = AssetDatabase.FindAssets("t:Prefab");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (prefab != null && prefab != selectedGo)
                {
                    // Megnézzük, van-e a Prefabon (vagy a gyerekein) CombatAudioPlayer
                    var players = prefab.GetComponentsInChildren<CombatAudioPlayer>(true);
                    foreach (var player in players)
                    {
                        if (player.TryGetComponent<AudioSource>(out var targetAudio))
                        {
                            ApplySettings(targetAudio, targetMin, targetMax, targetRolloff, targetCurve);
                            EditorUtility.SetDirty(prefab);
                            updatedPrefabCount++;
                        }
                    }
                }
            }

            // Módosított Prefabok elmentése a lemezre
            if (updatedPrefabCount > 0)
            {
                AssetDatabase.SaveAssets();
            }

            Debug.Log($"[AudioCopy] Kész! Átmásolva -> Scene objektumok: {updatedSceneCount} db | Prefab fájlok: {updatedPrefabCount} db!");
        }

        private static void ApplySettings(AudioSource audio, float min, float max, AudioRolloffMode mode, AnimationCurve curve)
        {
            audio.minDistance = min;
            audio.maxDistance = max;
            audio.rolloffMode = mode;

            if (mode == AudioRolloffMode.Custom && curve != null)
            {
                audio.SetCustomCurve(AudioSourceCurveType.CustomRolloff, curve);
            }
        }
    }
}
#endif
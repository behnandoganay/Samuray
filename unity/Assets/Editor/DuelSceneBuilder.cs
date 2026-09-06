using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using Samuray.Core;
using Samuray.Game;

namespace Samuray.EditorTools
{
    /// <summary>Duello sahnesini tek komutla kurar.
    ///
    /// Yapi tamamen idiomatik kaliyor - gercek GameObject'ler, bilesenler,
    /// Inspector'dan duzenlenebilir alanlar - ama ilk kurulumdaki onlarca
    /// surukle-birak otomatiklesiyor. Sahne olustuktan sonra her sey elle
    /// degistirilebilir; bu kurucu yalnizca baslangic durumunu yaratir.
    /// </summary>
    public static class DuelSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Duello.unity";
        const string PoseTablePath = "Assets/Settings/KamaePoseTable.asset";

        // Yandan profil, dikey ekran. Oyuncu solda, dusman sagda - sag taraf
        // bas parmagin dogal eristigi yer ve cizim dusmanin govdesine yapiliyor.
        const float OrthoSize = 6f;
        const float GroundY = -2.6f;
        const float FighterX = 1.7f;
        const float FighterScale = 1.3f;

        [MenuItem("Samuray/Düello Sahnesini Kur")]
        public static void Build()
        {
            if (Resources.Load<TextAsset>("rules") == null)
            {
                EditorUtility.DisplayDialog("Samuray",
                    "Assets/Resources/rules.json bulunamadi.\n\n" +
                    "Depo kokunde: python3 tools/sync_rules.py", "Tamam");
                return;
            }
            if (!EditorUtility.DisplayDialog("Samuray",
                    "Duello sahnesi yeniden kurulacak ve " + ScenePath + " olarak kaydedilecek.",
                    "Kur", "Vazgec"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var poses = LoadOrCreatePoseTable();

            // --- kamera ---
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = OrthoSize;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Art.Paper;
            camGo.transform.position = new Vector3(0f, 0f, -10f);
            var shake = camGo.AddComponent<CameraShake>();

            var root = new GameObject("Duello");

            // --- kagit ve zemin ---
            float worldH = OrthoSize * 2f;
            float worldW = worldH * 9f / 16f;
            var stage = DuelStage.Create(root.transform, worldW, worldH, GroundY);

            // --- savascilar: profilden karsi karsiya ---
            var meView = FighterView.Create(root.transform, "Fighter (Sen)",
                new Vector3(-FighterX, GroundY, 0f), +1, FighterScale, poses);
            var foeView = FighterView.Create(root.transform, "Fighter (Dusman)",
                new Vector3(FighterX, GroundY, 0f), -1, FighterScale, poses);

            // --- hat katmanlari ---
            // dusmanin uzerinde: savundugu hatlar
            var guard = LineOverlay.Create(root.transform, "GuardOverlay", foeView.Rig, false, 10);
            // senin uzerinde: gelen darbenin hayalet izi
            var telegraph = LineOverlay.Create(root.transform, "TelegraphOverlay", meView.Rig, true, 10);

            // --- cizim ---
            var trail = InkTrail.Create(root.transform);
            var input = StrokeInput.Create(root.transform, cam, trail,
                new UnityEngine.Rect(0f, 0.25f, 1f, 0.75f), foeView.Rig.transform);

            // --- vurus hissi ---
            var splatter = InkSplatter.Create(root.transform);
            var duelAudio = DuelAudio.Create(root.transform);
            var animator = BeatAnimator.Create(root.transform, shake, splatter, duelAudio, meView, foeView);

            // --- arayuz ---
            var hud = HudView.Create(root.transform);

            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            // Yeni Input System: StandaloneInputModule DEGIL.
            es.AddComponent<InputSystemUIInputModule>();

            // --- kontrolcu ---
            var ctrlGo = new GameObject("DuelController");
            ctrlGo.transform.SetParent(root.transform, false);
            var ctrl = ctrlGo.AddComponent<DuelController>();

            var so = new SerializedObject(ctrl);
            so.FindProperty("meView").objectReferenceValue = meView;
            so.FindProperty("foeView").objectReferenceValue = foeView;
            so.FindProperty("guardOverlay").objectReferenceValue = guard;
            so.FindProperty("telegraphOverlay").objectReferenceValue = telegraph;
            so.FindProperty("hud").objectReferenceValue = hud;
            so.FindProperty("input").objectReferenceValue = input;
            so.FindProperty("animator").objectReferenceValue = animator;
            so.FindProperty("stage").objectReferenceValue = stage;
            so.FindProperty("duelAudio").objectReferenceValue = duelAudio;
            so.ApplyModifiedPropertiesWithoutUndo();

            Directory.CreateDirectory("Assets/Scenes");
            AssetDatabase.Refresh();
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();

            Selection.activeGameObject = ctrlGo;
            Debug.Log("Samuray: duello sahnesi kuruldu -> " + ScenePath + "   Play'e basabilirsin.");
        }

        static KamaePoseTable LoadOrCreatePoseTable()
        {
            var existing = AssetDatabase.LoadAssetAtPath<KamaePoseTable>(PoseTablePath);
            if (existing != null)
            {
                // Eski surumde poz tablosunda sadece kilic konumu vardi; govde
                // durusu (egim, ayak acikligi, kalca yuksekligi) sonradan eklendi.
                // Eski bir asset'te bu alanlar sifir kalir ve figur yere coker -
                // o yuzden bayat veriyi tanip tazeliyoruz.
                if (existing.EnsurePopulated())
                {
                    EditorUtility.SetDirty(existing);
                    AssetDatabase.SaveAssets();
                    Debug.Log("Samuray: poz tablosu eksikti, varsayilanlarla dolduruldu.");
                }
                return existing;
            }

            Directory.CreateDirectory("Assets/Settings");
            AssetDatabase.Refresh();
            var t = KamaePoseTable.CreateDefault();
            t.EnsurePopulated();
            AssetDatabase.CreateAsset(t, PoseTablePath);
            AssetDatabase.SaveAssets();
            Debug.Log("Samuray: duruş poz tablosu olusturuldu -> " + PoseTablePath);
            return t;
        }

    }
}

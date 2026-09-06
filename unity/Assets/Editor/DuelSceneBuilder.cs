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
    /// Neden bir menu komutu? Yapi tamamen idiomatik kaliyor - gercek
    /// GameObject'ler, gercek bilesenler, Inspector'dan duzenlenebilir alanlar -
    /// ama ilk kurulumdaki onlarca surukle-birak islemi otomatiklesiyor.
    /// Sahne olustuktan sonra her seyi elle degistirebilirsin; bu kurucu
    /// yalnizca baslangic durumunu yaratir.
    /// </summary>
    public static class DuelSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Duello.unity";
        const string PoseTablePath = "Assets/Settings/KamaePoseTable.asset";

        [MenuItem("Samuray/Düello Sahnesini Kur")]
        public static void Build()
        {
            if (Resources.Load<TextAsset>("rules") == null)
            {
                EditorUtility.DisplayDialog("Samuray",
                    "Assets/Resources/rules.json bulunamadi.\n\n" +
                    "Depo kokunde su komutu calistir:\n    python3 tools/sync_rules.py", "Tamam");
                return;
            }

            if (!EditorUtility.DisplayDialog("Samuray",
                    "Yeni bir duello sahnesi kurulacak ve " + ScenePath + " olarak kaydedilecek.\n\n" +
                    "Acik sahnedeki kaydedilmemis degisiklikler sorulacak.", "Kur", "Vazgec"))
                return;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var poses = LoadOrCreatePoseTable();

            // --- kamera ---
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 10f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Art.Paper;
            cam.transform.position = new Vector3(0f, 0f, -10f);

            var root = new GameObject("Duello");

            // --- savascilar ve arena ---
            var foeView = FighterView.Create(root.transform, "Fighter (Dusman)",
                                             new Vector3(0f, 4.6f, 0f), -1, 1.5f, poses);
            var meView = FighterView.Create(root.transform, "Fighter (Sen)",
                                            new Vector3(0f, -6.2f, 0f), 1, 1.3f, poses);
            var arena = ArenaView.Create(root.transform, new Vector3(0f, 4.6f, 0f), 3.4f);

            // --- cizim ---
            var trail = InkTrail.Create(root.transform);
            // Cizim alani ekranin ust %68'i: alt kisim HUD butonlarinin.
            var input = StrokeInput.Create(root.transform, cam, trail,
                                           new UnityEngine.Rect(0f, 0.32f, 1f, 0.68f));

            // --- arayuz ---
            var hud = HudView.Create(root.transform);

            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            // Yeni Input System kullaniliyor: StandaloneInputModule DEGIL.
            // Yanlisi butonlarin hic calismamasina yol acar.
            es.AddComponent<InputSystemUIInputModule>();

            // --- kontrolcu ve referans baglama ---
            var ctrlGo = new GameObject("DuelController");
            ctrlGo.transform.SetParent(root.transform, false);
            var ctrl = ctrlGo.AddComponent<DuelController>();

            var so = new SerializedObject(ctrl);
            so.FindProperty("arena").objectReferenceValue = arena;
            so.FindProperty("meView").objectReferenceValue = meView;
            so.FindProperty("foeView").objectReferenceValue = foeView;
            so.FindProperty("hud").objectReferenceValue = hud;
            so.FindProperty("input").objectReferenceValue = input;
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
            if (existing != null) return existing;

            Directory.CreateDirectory("Assets/Settings");
            AssetDatabase.Refresh();
            var t = KamaePoseTable.CreateDefault();
            AssetDatabase.CreateAsset(t, PoseTablePath);
            AssetDatabase.SaveAssets();
            Debug.Log("Samuray: duruş poz tablosu olusturuldu -> " + PoseTablePath +
                      "   Kilic acilarini buradan ayarlayabilirsin.");
            return t;
        }
    }
}

//Copyright(c) 2024 SataniaShopping
//Released under the MIT license
//https://opensource.org/licenses/mit-license.php

//#define Overlay_Debug

#if UNITY_2021_2_OR_NEWER
using JetBrains.Annotations;
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine;
using UnityObject = UnityEngine.Object;

#nullable enable

namespace net.satania.screenshot_overlay
{
    [Overlay(typeof(SceneView), "Satania's ScreenShot Overlay", defaultDisplay = true)]
    public class ScreenShotOverlay : IMGUIOverlay
    {
        const int CAMERA_PRESET_LENGTH = 9;
        private bool[] cameraPresetHasKey = new bool[CAMERA_PRESET_LENGTH];

        const string MENU_PATH = "さたにあしょっぴんぐ/スクリーンショット オーバーレイ";
        const string PARAM_FOLDERPATH = MENU_PATH + "/FolderPath";
        const string PARAM_CUSTOM_SIZE = MENU_PATH + "UseCustomSize";
        const string PARAM_IS_TRANSPARENT = MENU_PATH + "UseTransparent";
        const string PARAM_CUSTOM_X = MENU_PATH + "/Custom_X";
        const string PARAM_CUSTOM_Y = MENU_PATH + "/Custom_Y";

        //Camera_Position_0.x;
        const string PARAM_CAMERA_POS = MENU_PATH + "/Camera_Position_{0}.{1}";
        const string PARAM_CAMERA_ROT = MENU_PATH + "/Camera_Rotation_{0}.{1}";

        private const int k_priority = 100;
        private static bool m_ctrlPressed = false;
        private static bool m_rPressed = false;

        /// <summary>
        /// 有効化/無効化の状態を保存する用
        /// </summary>
        private static ScreenShotOverlay? m_overlay;
        private SceneView? m_sceneView;

        public bool UseCustomSize
        {
            get => EditorPrefs.GetBool(PARAM_CUSTOM_SIZE, false);
            set => EditorPrefs.SetBool(PARAM_CUSTOM_SIZE, value);
        }

        public bool UseTransparent
        {
            get => EditorPrefs.GetBool(PARAM_IS_TRANSPARENT, true);
            set => EditorPrefs.SetBool(PARAM_IS_TRANSPARENT, value);
        }

        public int CustomWidth
        {
            get => EditorPrefs.GetInt(PARAM_CUSTOM_X, 1920);
            set => EditorPrefs.SetInt(PARAM_CUSTOM_X, value);
        }

        public int CustomHeight
        {
            get => EditorPrefs.GetInt(PARAM_CUSTOM_Y, 1080);
            set => EditorPrefs.SetInt(PARAM_CUSTOM_Y, value);
        }

        private string SaveFolderPath
        {
            get => EditorPrefs.GetString(PARAM_FOLDERPATH, Application.dataPath);
            set => EditorPrefs.SetString(PARAM_FOLDERPATH, value);
        }

        [PublicAPI]
        public void SetCustomSize(int x, int y)
        {
            CustomWidth = x;
            CustomHeight = y;
        }

        private void ScreenShot()
        {
            SceneView sceneView = SceneView.lastActiveSceneView;
            //string path = EditorUtility.SaveFilePanel("Save PNG", "Assets/", "ScreenShot", "png");

            //if (string.IsNullOrEmpty(path))
            //    return;

            //フォルダがなかった場合は作成
            if (!Directory.Exists(SaveFolderPath))
                Directory.CreateDirectory(SaveFolderPath);

            string path = Path.Combine(SaveFolderPath, DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss") + ".png");

            Camera cam = sceneView.camera;
            cam = UnityObject.Instantiate(cam);
            cam.gameObject.hideFlags = HideFlags.HideAndDontSave;
            var rect = sceneView.cameraViewport;

            int width = (int)(rect.width);
            int height = (int)(rect.height);

            if (UseCustomSize)
            {
                width = CustomWidth;
                height = CustomHeight;
            }

            RenderTexture beforeRT = RenderTexture.active;
            Texture2D screenShot = new Texture2D(width, height, TextureFormat.ARGB32, false);
            RenderTexture rt = new RenderTexture(width, height, 24);

            RenderTexture.active = rt;
            cam.targetTexture = rt;
            cam.clearFlags = UseTransparent ? CameraClearFlags.Nothing : CameraClearFlags.Skybox;
            cam.Render();

            screenShot.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            screenShot.Apply();

            byte[] bytes = screenShot.EncodeToPNG();
            File.WriteAllBytes(path, bytes);

            //Assets内なら更新
            if (path.Contains(Application.dataPath))
            {
                //Project
                string projectPath = Path.Combine(Application.dataPath, "..");
                string relativePath = Path.GetRelativePath(projectPath, path);
                AssetDatabase.ImportAsset(relativePath);
            }

            //EditorUtility.DisplayDialog("Satania's ScreenShot Overlay", $"保存完了\n{path}", "OK");

            RenderTexture.active = beforeRT;
            if (rt != null)
                UnityObject.DestroyImmediate(rt);

            UnityObject.DestroyImmediate(cam.gameObject);

            System.Diagnostics.Process.Start(SaveFolderPath);
        }

        private void SaveCamera(int index, SceneView sceneView)
        {
            if (sceneView.camera == null)
                throw new Exception("カメラが存在しません。");

            Vector3 position = sceneView.camera.transform.position;
            Quaternion rotation = sceneView.camera.transform.rotation;

            string[] xyzw = new string[] { "x", "y", "z", "w" };
            float[] p_xyz = new float[] { position.x, position.y, position.z };
            float[] r_xyzw = new float[] { rotation.x, rotation.y, rotation.z, rotation.w };
            for (int p = 0; p < 3; p++)
            {
                string key = string.Format(PARAM_CAMERA_POS, index, xyzw[p]);
                EditorPrefs.SetFloat(key, p_xyz[p]);
            }

            for (int r = 0; r < 4; r++)
            {
                string key = string.Format(PARAM_CAMERA_ROT, index, xyzw[r]);
                EditorPrefs.SetFloat(key, r_xyzw[r]);
            }
        }

        private void ResetCamera(int index)
        {
            string[] xyzw = new string[] { "x", "y", "z", "w" };
            for (int p = 0; p < 3; p++)
            {
                string key = string.Format(PARAM_CAMERA_POS, index, xyzw[p]);
                EditorPrefs.DeleteKey(key);
            }

            for (int r = 0; r < 4; r++)
            {
                string key = string.Format(PARAM_CAMERA_ROT, index, xyzw[r]);
                EditorPrefs.DeleteKey(key);
            }
        }

        private void LoadCamera(int index, SceneView sceneView)
        {
            string[] xyzw = new string[] { "x", "y", "z", "w" };
            Vector3 position = new Vector3();
            Quaternion rotation = new Quaternion();

            for (int p = 0; p < 3; p++)
            {
                string key = string.Format(PARAM_CAMERA_POS, index, xyzw[p]);
                position[p] = EditorPrefs.GetFloat(key, 0);
            }

            for (int r = 0; r < 4; r++)
            {
                string key = string.Format(PARAM_CAMERA_ROT, index, xyzw[r]);
                rotation[r] = EditorPrefs.GetFloat(key, 0);
            }

            //https://qiita.com/MARQUE/items/c41af003b9300f05e781#gameview-%E3%81%8B%E3%82%89-sceneview
            if (sceneView.camera != null)
            {
                sceneView.cameraSettings.fieldOfView = sceneView.cameraSettings.fieldOfView;
                sceneView.size = sceneView.size;
                sceneView.pivot = position + rotation * Vector3.forward * sceneView.cameraDistance;
                sceneView.rotation = rotation;
                //sceneView.LookAt(position, rotation, sceneView.size, sceneView.orthographic, true);
                sceneView.Repaint();
            }
        }

        private bool CheckHasKey(int index)
        {
            string[] xyzw = new string[] { "x", "y", "z", "w" };

            for (int p = 0; p < 3; p++)
            {
                string key = string.Format(PARAM_CAMERA_POS, index, xyzw[p]);
                if (!EditorPrefs.HasKey(key))
                    return false;
            }

            for (int r = 0; r < 4; r++)
            {
                string key = string.Format(PARAM_CAMERA_ROT, index, xyzw[r]);
                if (!EditorPrefs.HasKey(key))
                    return false;
            }

            return true;
        }

        private void CheckHasKeys()
        {
            for (int i = 0; i < CAMERA_PRESET_LENGTH; i++)
            {
                cameraPresetHasKey[i] = CheckHasKey(i);
            }
        }

        public override void OnCreated()
        {
            m_sceneView = SceneView.lastActiveSceneView;

            displayedChanged += OnDisplayChanged;
            m_overlay = this;

#if Overlay_Debug
        Debug.Log($"OnCreated()");
#endif
        }

        public void OnDisplayChanged(bool t)
        {

#if Overlay_Debug
        Debug.Log($"OnDisplayChanged : {t}, {displayed}");
#endif
        }

        //----------------------------------------------------------------------
        //MenuItemで切り替えできるように
        [MenuItem(MENU_PATH, true)]
        public static bool DrawToggleOverlayToMenuBar()
        {
            if (m_overlay != null)
                Menu.SetChecked(MENU_PATH, m_overlay.displayed);
            return true;
        }

        [MenuItem(MENU_PATH, priority = k_priority)]
        public static void ToggleOverlay()
        {
            if (m_overlay != null)
                m_overlay.displayed = !m_overlay.displayed;
        }
        //----------------------------------------------------------------------

        private static bool IsKeyCodeIsCtrlOrCommand(Event @event)
        {
            return @event.keyCode == KeyCode.LeftControl || @event.keyCode == KeyCode.RightControl ||
                    @event.keyCode == KeyCode.LeftCommand || @event.keyCode == KeyCode.RightCommand;
        }

        public override void OnGUI()
        {
            CheckHasKeys();

            m_sceneView = SceneView.lastActiveSceneView;
            //var newViewport = m_sceneView.cameraViewport;

            if (GUILayout.Button("ScreenShot"))
                ScreenShot();

            GUILayout.BeginHorizontal();
            GUILayout.Label("保存先: ", GUILayout.Width(45));

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField(
                SaveFolderPath.Length > 20 ? "..." + SaveFolderPath.Substring(SaveFolderPath.Length - 20, 20) : SaveFolderPath);
            EditorGUI.EndDisabledGroup();

            //保存先を選択
            if (GUILayout.Button("選択", GUILayout.Width(40)))
            {
                var newSavePath = EditorUtility.SaveFolderPanel("スクリーンショットを保存する場所を選んでください。", SaveFolderPath, "");

                //変更があった時だけ書き込み
                if (newSavePath != SaveFolderPath)
                    SaveFolderPath = newSavePath;
            }
            GUILayout.EndHorizontal();

            bool new_isTransparent = EditorGUILayout.ToggleLeft("透過 / Transparent", UseTransparent, GUILayout.Width(200));
            if (new_isTransparent != UseTransparent)
                UseTransparent = new_isTransparent;

            bool isCustomSize = EditorGUILayout.ToggleLeft("カスタムサイズ / Custom Resolution", UseCustomSize, GUILayout.Width(200));
            if (isCustomSize != UseCustomSize)
                UseCustomSize = isCustomSize;

            if (isCustomSize)
            {
                int newX = EditorGUILayout.IntField("横 / Width", CustomWidth);
                int newY = EditorGUILayout.IntField("縦 / Height", CustomHeight);

                if (newX != CustomWidth)
                {
                    CustomWidth = newX;
                }

                if (newY != CustomHeight)
                {
                    CustomHeight = newY;
                }

                using (new GUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("FHD", GUILayout.Width(35)))
                        SetCustomSize(1920, 1080);

                    if (GUILayout.Button("4K", GUILayout.Width(25)))
                        SetCustomSize(3840, 2160);

                    if (GUILayout.Button("8K", GUILayout.Width(25)))
                        SetCustomSize(7680, 4320);
                }
            }

            using (new GUILayout.VerticalScope(GUI.skin.box))
            {
                GUILayout.Label("カメラ位置のプリセット", EditorStyles.boldLabel);
                GUILayout.Label("Ctrl押しながら: 保存");
                GUILayout.Label("R押しながら: リセット");
                GUILayout.BeginHorizontal();

                for (int i = 0; i < CAMERA_PRESET_LENGTH; i++)
                {
                    bool hasKey = cameraPresetHasKey[i];
                    var buttonColor = GUI.color;

                    if (hasKey)
                    {
                        GUI.color = Color.green;
                    }

                    if (GUILayout.Button((i + 1).ToString(), GUILayout.Width(30)))
                    {
                        if (m_ctrlPressed)
                            SaveCamera(i, m_sceneView);
                        else if (m_rPressed)
                            ResetCamera(i);
                        else
                            LoadCamera(i, m_sceneView);
                    }

                    if (hasKey)
                    {
                        GUI.color = buttonColor;
                    }
                }
                GUILayout.EndHorizontal();
            }
        }

        [InitializeOnLoadMethod]
        private static void PressCheckMethodInitialize()
        {
            //https://qiita.com/r-ngtm/items/2802a3e53474762ef41b
            EditorApplication.CallbackFunction function = () =>
            {
                Event @event = Event.current;

                if (@event.type == EventType.KeyDown)
                {
                    if (!m_ctrlPressed && IsKeyCodeIsCtrlOrCommand(@event))
                        m_ctrlPressed = true;

                    if (!m_rPressed && @event.keyCode == KeyCode.R)
                        m_rPressed = true;
                }
                else if (@event.type == EventType.KeyUp)
                {
                    if (m_ctrlPressed && IsKeyCodeIsCtrlOrCommand(@event))
                        m_ctrlPressed = false;

                    if (m_rPressed && @event.keyCode == KeyCode.R)
                        m_rPressed = false;
                }
            };

            FieldInfo info = typeof(EditorApplication).GetField("globalEventHandler", BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic);
            EditorApplication.CallbackFunction functions = (EditorApplication.CallbackFunction)info.GetValue(null);
            functions += function;
            info.SetValue(null, (object)functions);
        }
    }
}
#endif
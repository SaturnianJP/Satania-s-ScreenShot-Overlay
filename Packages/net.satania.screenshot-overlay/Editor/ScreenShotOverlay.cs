//Copyright(c) 2024 SataniaShopping
//Released under the MIT license
//https://opensource.org/licenses/mit-license.php

//#define Overlay_Debug

#if UNITY_2021_2_OR_NEWER
using System.IO;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine;

[Overlay(typeof(SceneView), "Satania's ScreenShot Overlay", defaultDisplay = true)]
public class ScreenShotOverlay : IMGUIOverlay
{
    const string MENU_PATH = "さたにあしょっぴんぐ/スクリーンショット オーバーレイ";
    const string PATH_CUSTOM_X = MENU_PATH + "/Custom_X";
    const string PATH_CUSTOM_Y = MENU_PATH + "/Custom_Y";

    const int priority = 100;

    private bool isTransparent = false;
    private bool isCustomSize = false;

    private string resText = "";
    static ScreenShotOverlay overlay;
    private Rect beforeViewport;
    private SceneView sceneView;


    public int CustomWidth
    {
        get => EditorPrefs.GetInt(PATH_CUSTOM_X, 1920);
        set => EditorPrefs.SetInt(PATH_CUSTOM_X, value);
    }

    public int CustomHeight
    {
        get => EditorPrefs.GetInt(PATH_CUSTOM_Y, 1080);
        set => EditorPrefs.SetInt(PATH_CUSTOM_Y, value);
    }

    public void SetCustomSize(int x, int y)
    {
        CustomWidth = x;
        CustomHeight = y;
    }

    private void ScreenShot()
    {
        SceneView sceneView = SceneView.lastActiveSceneView;
        string path = EditorUtility.SaveFilePanel("Save PNG", "Assets/", "ScreenShot", "png");

        if (string.IsNullOrEmpty(path))
            return;

        Camera cam = sceneView.camera;
        cam = Object.Instantiate(cam);
        var rect = sceneView.cameraViewport;

        int width = (int)(rect.width);
        int height = (int)(rect.height);

        if (isCustomSize)
        {
            width = CustomWidth;
            height = CustomHeight;
        }

        RenderTexture currentRT = RenderTexture.active;
        Texture2D screenShot = new Texture2D(width, height, TextureFormat.ARGB32, false);
        RenderTexture rt = new RenderTexture(width, height, 24);

        RenderTexture.active = rt;
        cam.targetTexture = rt;
        cam.clearFlags = isTransparent ? CameraClearFlags.Nothing : CameraClearFlags.Skybox;
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

        EditorUtility.DisplayDialog("Satania's ScreenShot Overlay", $"保存完了\n{path}", "OK");

        RenderTexture.active = currentRT;
        if (rt != null)
            Object.DestroyImmediate(rt);

        Object.DestroyImmediate(cam.gameObject);
    }

    private void RefleshResLabel(float w, float h)
    {
        int width = (int)w;
        int height = (int)h;

        resText = $"{width} x {height}";
    }

    public override void OnCreated()
    {
        sceneView = SceneView.lastActiveSceneView;
        if (sceneView != null)
        {
            beforeViewport = sceneView.cameraViewport;
        }

        RefleshResLabel((int)beforeViewport.width, (int)beforeViewport.height);

        displayedChanged += OnDisplayChanged;
        overlay = this;

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
        if (overlay != null)
            Menu.SetChecked(MENU_PATH, overlay.displayed);
        return true;
    }

    [MenuItem(MENU_PATH, priority = priority)]
    public static void ToggleOverlay()
    {
        if (overlay != null)
            overlay.displayed = !overlay.displayed;
    }
    //----------------------------------------------------------------------

    public override void OnGUI()
    {
        sceneView = SceneView.lastActiveSceneView;
        var newViewport = sceneView.cameraViewport;

        if (GUILayout.Button("ScreenShot"))
            ScreenShot();

        isTransparent = EditorGUILayout.ToggleLeft("透過 / Transparent", isTransparent, GUILayout.Width(200));

        if (beforeViewport != newViewport)
        {
            //解像度のラベルを更新
            RefleshResLabel(newViewport.width, newViewport.height);
        }

        isCustomSize = EditorGUILayout.ToggleLeft("カスタムサイズ / Custom Resolution", isCustomSize, GUILayout.Width(200));
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

        if (!string.IsNullOrEmpty(resText))
        {
            GUILayout.Space(15);
            EditorGUILayout.LabelField(resText);
        }

        beforeViewport = sceneView.cameraViewport;
    }

}
#endif
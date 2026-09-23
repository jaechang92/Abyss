#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Abyss.Runtime.Stage;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Abyss.EditorTools
{
    /// <summary>통합본의 재현 가능한 빌드와 정적 화면 캡처. 플레이·청취 판정을 대신하지 않는다.</summary>
    public static class CoordinatorVerification
    {
        private const string OutputDirectory = "Builds/Wave2";

        public static void ApplyEnvironmentAndValidate()
        {
            bool isOk = FirstBossWiring.Validate()
                        && Stage1EnvironmentWiring.ApplyToRunScene()
                        && Stage1EnvironmentWiring.ValidateRunScene();
            if (!isOk)
            {
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            CaptureEnvironment();
        }

        public static void BuildWindows()
        {
            CaptureEnvironmentCore(false);
            Directory.CreateDirectory(OutputDirectory);
            var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            if (scenes.Length == 0) throw new InvalidOperationException("Enabled build scenes are missing.");
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = OutputDirectory + "/Abyss.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            File.WriteAllText(OutputDirectory + "/build-result.txt",
                $"result={report.summary.result}\nerrors={report.summary.totalErrors}\nwarnings={report.summary.totalWarnings}\n" +
                $"size={report.summary.totalSize}\ntime={report.summary.totalTime}\nunity={Application.unityVersion}\n" +
                "scenes=" + string.Join(",", scenes));
            if (Application.isBatchMode) EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        public static void CaptureEnvironment()
        {
            CaptureEnvironmentCore(true);
        }

        private static void CaptureEnvironmentCore(bool exitWhenDone)
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Run.unity", OpenSceneMode.Single);
            var presenter = UnityEngine.Object.FindAnyObjectByType<StageEnvironmentPresenter>();
            if (presenter == null) throw new InvalidOperationException("Stage1 environment is not wired.");
            var camera = UnityEngine.Object.FindObjectsByType<Camera>()
                .FirstOrDefault(candidate => candidate.orthographic);
            if (camera == null) throw new InvalidOperationException("Run camera is missing.");
            string directory = "Logs/environment-captures";
            Directory.CreateDirectory(directory);
            // 같은 카메라·좌표로 세 상태를 비교한다. 플레이어 입력이나 진행 상태는 실행하지 않는다.
            camera.transform.position = new Vector3(0f, 2f, -10f);
            foreach (StageEnvironmentLook look in new[] { StageEnvironmentLook.Field, StageEnvironmentLook.BossArena, StageEnvironmentLook.Hidden })
            {
                presenter.Apply(look);
                var target = new RenderTexture(1280, 720, 24);
                var previous = RenderTexture.active;
                var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                try
                {
                    camera.targetTexture = target;
                    // 새로 활성화한 SpriteRenderer의 렌더 데이터를 한 번 준비한 뒤 같은 상태를 캡처한다.
                    camera.Render();
                    camera.Render();
                    RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                    pixels.Apply();
                    File.WriteAllBytes(directory + "/" + look + ".png", pixels.EncodeToPNG());
                }
                finally
                {
                    camera.targetTexture = null;
                    RenderTexture.active = previous;
                    UnityEngine.Object.DestroyImmediate(pixels);
                    UnityEngine.Object.DestroyImmediate(target);
                }
            }
            // 캡처를 위해 바꾼 표시 상태와 카메라 위치는 씬에 저장하지 않는다.
            EditorSceneManager.OpenScene("Assets/Scenes/Run.unity", OpenSceneMode.Single);
            Debug.Log("[CoordinatorVerification] Static environment captures saved: " + directory);
            if (Application.isBatchMode && exitWhenDone) EditorApplication.Exit(0);
        }
    }
}
#endif

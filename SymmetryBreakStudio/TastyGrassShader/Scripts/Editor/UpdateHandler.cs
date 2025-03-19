using System;
using System.Collections;
using UnityEditor;
using UnityEngine.Networking;

namespace SymmetryBreakStudio.TastyGrassShader.Editor
{
    public static class UpdateHandler
    {
        const string UpdateUrl =
            "https://raw.githubusercontent.com/SymmetryBreakStudio/TastyGrassShader/refs/heads/main/version-unity.txt";

        public const string ThisVersion = "2.2.1";
        public static string NewVersionStr { get; private set; } = string.Empty;
        static bool _newVersionAvailable;
        static IEnumerator _downloader;
        static DownloadState _dlState = DownloadState.None;
        static string _downloadErrorMessage;

        [InitializeOnLoadMethod]
        static void CheckForUpdate()
        {
            EditorApplication.update += EditorUpdate;
        }

        static void EditorUpdate()
        {
            switch (_dlState)
            {
                case DownloadState.None:
                    _downloader = GetUpdateFile();
                    _dlState = DownloadState.Running;
                    break;
                case DownloadState.Running:
                    _downloader.MoveNext();
                    break;
                case DownloadState.FailedInternally:
                    // Unity bugs out a bit when running the web request just after loading,
                    // so we need to hammer it a few times...
                    _downloader = GetUpdateFile();
                    _dlState = DownloadState.Running;
                    break;
                case DownloadState.FailedConnection:
                    EditorApplication.update -= EditorUpdate;
                    break;
                case DownloadState.Success:
                    EditorApplication.update -= EditorUpdate;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public static void DisplayUpdateBox()
        {
            if (_newVersionAvailable)
            {
                EditorGUILayout.HelpBox(
                    $"A new version of Tasty Grass Shader is available.\n\nNew: {NewVersionStr}\nThis: {ThisVersion}\n\nPlease update via the Unity Package Manager.\nRemember to backup your project before updating and delete any previous installations.",
                    MessageType.Warning);
            }

            if (_dlState == DownloadState.FailedConnection)
            {
                EditorGUILayout.HelpBox(
                    $"Unable to check for updates.\nMake sure that you are connected to the internet and this is most recent version of Tasty Grass Shader.\n\nError: {_downloadErrorMessage}",
                    MessageType.Warning);
            }

            EditorGUILayout.Space();
        }

        static IEnumerator GetUpdateFile()
        {
            UnityWebRequest www = UnityWebRequest.Get(UpdateUrl);
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                _dlState = string.IsNullOrEmpty(www.error)
                    ? DownloadState.FailedInternally
                    : DownloadState.FailedConnection;
                _downloadErrorMessage = www.error;
            }
            else
            {
                NewVersionStr = www.downloadHandler.text.Trim();
                _newVersionAvailable = NewVersionStr != ThisVersion;
                _dlState = DownloadState.Success;
            }
        }

        enum DownloadState
        {
            None,
            Running,
            FailedInternally,
            FailedConnection,
            Success
        }
    }
}
using System.Linq;
using UnityEditor;
using UnityEngine;

// VRChat SDKのビルダーに残った状態を消して、パネルを開き直す。
// All Platforms のビルド後に状態が残ると、別のアバターを選んでも名前とサムネイルが切り替わらない。
public static class VRCBuilderReset
{
    private const string MpbKey = "VRCSdkControlPanel.MultiPlatformBuild";
    private const string MpbStateKey = "VRCSdkControlPanel" + MpbKey + ".State";
    private const string MpbProgressKey = "VRCSdkControlPanel" + MpbKey + ".Progress";
    private const string ClearMpbMenu = "VRChat SDK/Utilities/Clear Multi Platform Build State";
    private const string ShowPanelMenu = "VRChat SDK/Show Control Panel";

    // 新規アバターへ引き継がれる入力内容。公開範囲と対象プラットフォームは残す。
    private static readonly string[] AvatarInfoKeys =
    {
        "VRC.SK3A.Editor.Avatar.Name",
        "VRC.SK3A.Editor.Avatar.Desc",
        "VRC.SK3A.Editor.Avatar.Tags",
        "VRC.SK3A.Editor.Avatar.ThumbPath",
        "VRC.SK3A.Editor.Avatar.Style.Primary",
        "VRC.SK3A.Editor.Avatar.Style.Secondary",
    };

    [MenuItem("Tools/VRC Builder Reset")]
    private static void Run()
    {
        if (BuildPipeline.isBuildingPlayer || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("[VRCBuilderReset] ビルド中、コンパイル中、再生中は実行できません。");
            return;
        }

        var state = SessionState.GetInt(MpbStateKey, 0);
        var mpb = SessionState.GetBool(MpbKey, false);
        var progressId = SessionState.GetInt(MpbProgressKey, -1);
        var running = Progress.Exists(progressId) && Progress.GetStatus(progressId) == Progress.Status.Running;
        var savedName = SessionState.GetString(AvatarInfoKeys[0], "");
        Debug.Log($"[VRCBuilderReset] リセット前: MPB={mpb} State={state}(0=Idle,1=Building,2=Switching) 進行中={running} 引き継ぎ名='{savedName}'");

        if (running && !EditorUtility.DisplayDialog(
                "VRC Builder Reset",
                "マルチプラットフォームビルドが進行中の表示になっています。実際にアップロード中なら、リセットしないでください。\n\n止まっている場合だけリセットしますか？",
                "リセットする",
                "やめる"))
        {
            return;
        }

        if (!EditorApplication.ExecuteMenuItem(ClearMpbMenu))
        {
            Debug.LogError($"[VRCBuilderReset] メニューが見つかりません: {ClearMpbMenu}");
            return;
        }

        foreach (var key in AvatarInfoKeys)
        {
            SessionState.EraseString(key);
        }

        foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>().Where(w => w.GetType().Name == "VRCSdkControlPanel"))
        {
            window.Close();
        }

        EditorApplication.delayCall += () =>
        {
            EditorApplication.ExecuteMenuItem(ShowPanelMenu);
            Debug.Log("[VRCBuilderReset] リセットしました。Selected Avatar を選び直してください。");
        };
    }
}

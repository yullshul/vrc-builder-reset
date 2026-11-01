using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// VRChat SDKのビルダーに残った状態を消して、パネルを開き直す。
// All Platforms のビルド後に状態が残ると、別のアバターを選んでも名前とサムネイルが切り替わらない。
public static class VRCBuilderReset
{
    private const string MpbKey = "VRCSdkControlPanel.MultiPlatformBuild";
    private const string MpbStateKey = "VRCSdkControlPanel" + MpbKey + ".State";
    private const string MpbProgressKey = "VRCSdkControlPanel" + MpbKey + ".Progress";
    private const string ClearMpbMenu = "VRChat SDK/Utilities/Clear Multi Platform Build State";
    private const string ShowPanelMenu = "VRChat SDK/Show Control Panel";
    private const string DescriptorTypeName = "VRC.SDKBase.VRC_AvatarDescriptor";
    private const string BuilderTypeName = "VRC.SDK3A.Editor.VRCSdkControlPanelAvatarBuilder";
    private const string SelectedAvatarField = "_selectedAvatar";

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
            var target = SelectTargetAvatar();
            EditorApplication.ExecuteMenuItem(ShowPanelMenu);
            Debug.Log(target != null
                ? $"[VRCBuilderReset] リセットしました。選択アバター: {target.name}"
                : "[VRCBuilderReset] リセットしました。アバターは自動で選んでいません。");
        };
    }

    // Hierarchyで選択中のアバターを優先し、なければシーンで有効なアバターが1体だけのときに選ぶ。
    private static Component SelectTargetAvatar()
    {
        var descriptorType = FindType(DescriptorTypeName);
        var builderType = FindType(BuilderTypeName);
        var field = builderType?.GetField(SelectedAvatarField, BindingFlags.NonPublic | BindingFlags.Static);
        if (descriptorType == null || field == null)
        {
            Debug.LogWarning("[VRCBuilderReset] SDKの構成が想定と違うため、アバターを選択できません。");
            return null;
        }

        var selected = Selection.activeGameObject != null
            ? Selection.activeGameObject.GetComponentInParent(descriptorType, true)
            : null;
        if (selected != null && !selected.gameObject.activeInHierarchy)
        {
            Debug.LogWarning($"[VRCBuilderReset] {selected.name} は無効です。SDKは有効なアバターしか選べないので、Hierarchyで有効にしてからもう一度実行してください。");
            return null;
        }

        if (selected == null)
        {
            var active = Object.FindObjectsOfType(descriptorType, false).Cast<Component>().ToArray();
            if (active.Length != 1)
            {
                Debug.LogWarning($"[VRCBuilderReset] 有効なアバターが{active.Length}体あります。Hierarchyでアバターを選択してからもう一度実行してください。");
                return null;
            }
            selected = active[0];
        }

        field.SetValue(null, selected);
        return selected;
    }

    private static Type FindType(string fullName)
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(fullName))
            .FirstOrDefault(type => type != null);
    }
}

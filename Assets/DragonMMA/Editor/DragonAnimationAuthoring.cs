using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UI;

namespace DragonMMA.EditorTools
{
    public static class DragonAnimationAuthoring
    {
        public const string DirectoryPath = "Assets/DragonMMA/Animations";
        public const string HunterControllerPath = DirectoryPath + "/Hunter.controller";
        public static string DragonControllerPath(int kind) => DirectoryPath + "/Dragon" + kind + ".controller";
        public static void CreateAssets()
        {
            Directory.CreateDirectory(DirectoryPath);
            AssetDatabase.Refresh();
            CreateController(HunterControllerPath, "fighter", new[] { "idle", "walk", "punch", "kick", "hurt", "clinch", "takedown", "victory" });
            for (int i = 0; i < 5; i++) CreateController(DragonControllerPath(i), "dragon_" + i, new[] { "idle", "attack", "hurt" });
            AssetDatabase.SaveAssets();
        }
        private static void CreateController(string path, string prefix, string[] poses)
        {
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null) return; // Preserve designer edits on rerun.
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            var machine = controller.layers[0].stateMachine;
            for (int i = 0; i < poses.Length; i++)
            {
                string pose = poses[i], clipPath = DirectoryPath + "/" + prefix + "_" + pose + ".anim";
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                if (clip == null)
                {
                    clip = new AnimationClip { name = prefix + "_" + pose, frameRate = 7 };
                    var keys = new ObjectReferenceKeyframe[5];
                    for (int f = 0; f < keys.Length; f++)
                    {
                        var sprite = DragonArtCatalog.Load(prefix + "_" + pose + "_" + (f % 4));
                        if (sprite == null) throw new System.InvalidOperationException("Missing animation sprite: " + prefix + "_" + pose + "_" + (f % 4));
                        keys[f] = new ObjectReferenceKeyframe { time = f / 7f, value = sprite };
                    }
                    AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(Image), "m_Sprite"), keys);
                    var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = true;
                    AnimationUtility.SetAnimationClipSettings(clip, settings);
                    AssetDatabase.CreateAsset(clip, clipPath);
                }
                var state = machine.AddState(pose, new Vector3(i % 4 * 220, i / 4 * 90));
                state.motion = clip; state.writeDefaultValues = false;
                if (i == 0) machine.defaultState = state;
            }
        }
        public static void UpgradeHunterAssets()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(HunterControllerPath);
            if (controller == null) throw new System.InvalidOperationException("Missing authored Hunter controller.");
            var machine = controller.layers[0].stateMachine;
            Undo.RecordObject(controller, "Upgrade fighter animations");
            foreach (string pose in FighterCombatTiming.Poses)
            {
                string path = DirectoryPath + "/fighter_" + pose + ".anim";
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip == null) { clip = new AnimationClip { name = "fighter_" + pose }; AssetDatabase.CreateAsset(clip, path); }
                Undo.RecordObject(clip, "Upgrade fighter clip");
                clip.frameRate = 60;
                var keys = new ObjectReferenceKeyframe[FighterCombatTiming.Loops(pose) ? 9 : 8];
                for (int f = 0; f < keys.Length; f++)
                {
                    var sprite = DragonArtCatalog.Load("fighter_" + pose + "_" + (f % 8));
                    if (sprite == null) throw new System.InvalidOperationException("Missing upgraded sprite: " + pose + "_" + (f % 8));
                    keys[f] = new ObjectReferenceKeyframe { time = FighterCombatTiming.FrameTime(pose, f), value = sprite };
                }
                AnimationUtility.SetObjectReferenceCurve(clip, EditorCurveBinding.PPtrCurve("", typeof(Image), "m_Sprite"), keys);
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = FighterCombatTiming.Loops(pose);
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                EditorUtility.SetDirty(clip);
                AnimatorState state = null;
                foreach (var item in machine.states) if (item.state.name == pose) state = item.state;
                if (state == null) state = machine.AddState(pose);
                Undo.RecordObject(state, "Assign upgraded fighter clip");
                state.motion = clip; state.writeDefaultValues = false;
                if (pose == "idle") machine.defaultState = state;
            }
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
        }
    }
}


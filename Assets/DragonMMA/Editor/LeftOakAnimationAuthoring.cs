using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.UI;

namespace DragonMMA.EditorTools
{
    /// <summary>
    /// Imports the user-supplied five-frame tree sheet and applies a looping sway
    /// animation to the left oak in both authored overlay prefabs.
    /// </summary>
    public static class LeftOakAnimationAuthoring
    {
        public const string SheetPath = "Assets/DragonMMA/Resources/DragonMMA/Art/tree_sprite_sheet.png";
        public const string ClipPath = "Assets/DragonMMA/Animations/Environment/left_oak_sway.anim";
        public const string ControllerPath = "Assets/DragonMMA/Animations/Environment/LeftOakSway.controller";
        public const string DesktopPrefabPath = "Assets/DragonMMA/Prefabs/UI/OverlayRoot.prefab";
        public const string WebPrefabPath = "Assets/DragonMMA/Prefabs/UI/WebOverlayRoot.prefab";

        public static readonly string[] FrameNames =
        {
            "tree_sway_0", "tree_sway_1", "tree_sway_2", "tree_sway_3", "tree_sway_4"
        };

        // Source layout is three 512px cells on the top row and two on the bottom.
        // A common portrait crop preserves frame registration and matches the existing
        // 128x200 left-oak UI slot without stretching the tree.
        private static readonly Rect[] FrameRects =
        {
            new Rect(96, 512, 320, 512),
            new Rect(608, 512, 320, 512),
            new Rect(1120, 512, 320, 512),
            new Rect(96, 0, 320, 512),
            new Rect(608, 0, 320, 512)
        };

        private static readonly int[] PlaybackOrder = { 0, 1, 2, 3, 4, 3, 2, 1, 0 };

        [MenuItem("Dragon MMA/Environment/Apply Left Oak Animation")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before authoring the left oak animation.");

            ConfigureSpriteSheet();
            var frames = LoadFrames();
            var controller = CreateOrUpdateAnimation(frames);
            ConfigurePrefab(DesktopPrefabPath, frames[0], controller);
            ConfigurePrefab(WebPrefabPath, frames[0], controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ValidateOrThrow();
            Debug.Log("[Left Oak] Imported five registered frames and applied the looping sway animation to the desktop and WebGL left oak only.");
        }

        public static void ApplyBatch()
        {
            Apply();
        }

        public static void ValidateOrThrow()
        {
            var frames = LoadFrames();
            for (int i = 0; i < frames.Length; i++)
            {
                if (frames[i].rect != FrameRects[i])
                    throw new InvalidOperationException($"Unexpected rect for {FrameNames[i]}: {frames[i].rect}");
                if (Vector2.Distance(frames[i].pivot, new Vector2(frames[i].rect.width * .5f, 0)) > .01f)
                    throw new InvalidOperationException($"Unexpected pivot for {FrameNames[i]}: {frames[i].pivot}");
            }

            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (clip == null || controller == null)
                throw new InvalidOperationException("Left oak animation assets were not created.");

            var binding = EditorCurveBinding.PPtrCurve(string.Empty, typeof(Image), "m_Sprite");
            var keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
            if (keys == null || keys.Length != PlaybackOrder.Length)
                throw new InvalidOperationException("Left oak clip does not contain the expected sprite keys.");
            for (int i = 0; i < keys.Length; i++)
                if (keys[i].value != frames[PlaybackOrder[i]])
                    throw new InvalidOperationException($"Left oak clip key {i} uses the wrong sprite.");

            ValidatePrefab(DesktopPrefabPath, frames[0], controller);
            ValidatePrefab(WebPrefabPath, frames[0], controller);
        }

        private static void ConfigureSpriteSheet()
        {
            AssetDatabase.ImportAsset(SheetPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(SheetPath) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException("Missing tree sprite sheet at " + SheetPath);

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.isReadable = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;
            importer.spritePixelsPerUnit = 100;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            var old = provider.GetSpriteRects().ToDictionary(rect => rect.name);
            var rects = new List<SpriteRect>(FrameNames.Length);
            for (int i = 0; i < FrameNames.Length; i++)
            {
                rects.Add(new SpriteRect
                {
                    name = FrameNames[i],
                    rect = FrameRects[i],
                    pivot = new Vector2(.5f, 0),
                    alignment = SpriteAlignment.Custom,
                    spriteID = old.TryGetValue(FrameNames[i], out var existing) ? existing.spriteID : GUID.Generate()
                });
            }

            provider.SetSpriteRects(rects.ToArray());
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(
                rects.Select(rect => new SpriteNameFileIdPair(rect.name, rect.spriteID)));
            provider.Apply();
            importer.SaveAndReimport();
        }

        private static Sprite[] LoadFrames()
        {
            var byName = AssetDatabase.LoadAllAssetsAtPath(SheetPath)
                .OfType<Sprite>()
                .ToDictionary(sprite => sprite.name);
            return FrameNames.Select(name => byName.TryGetValue(name, out var sprite)
                    ? sprite
                    : throw new InvalidOperationException("Missing authored tree frame: " + name))
                .ToArray();
        }

        private static AnimatorController CreateOrUpdateAnimation(Sprite[] frames)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ClipPath));
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
            if (clip == null)
            {
                clip = new AnimationClip { name = "left_oak_sway" };
                AssetDatabase.CreateAsset(clip, ClipPath);
            }
            clip.frameRate = 4;
            var keys = new ObjectReferenceKeyframe[PlaybackOrder.Length];
            for (int i = 0; i < keys.Length; i++)
                keys[i] = new ObjectReferenceKeyframe { time = i / 4f, value = frames[PlaybackOrder[i]] };
            AnimationUtility.SetObjectReferenceCurve(
                clip,
                EditorCurveBinding.PPtrCurve(string.Empty, typeof(Image), "m_Sprite"),
                keys);
            var clipSettings = AnimationUtility.GetAnimationClipSettings(clip);
            clipSettings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, clipSettings);
            EditorUtility.SetDirty(clip);

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            var machine = controller.layers[0].stateMachine;
            var state = machine.states.Select(item => item.state).FirstOrDefault(item => item.name == "sway");
            if (state == null)
                state = machine.AddState("sway");
            state.motion = clip;
            state.writeDefaultValues = false;
            machine.defaultState = state;
            EditorUtility.SetDirty(state);
            EditorUtility.SetDirty(machine);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void ConfigurePrefab(string path, Sprite firstFrame, RuntimeAnimatorController controller)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var leftOak = FindUnique(root.transform, "Left oak", path);
                var image = leftOak.GetComponent<Image>();
                if (image == null)
                    throw new InvalidOperationException(path + " left oak is missing its Image component.");
                var animator = leftOak.GetComponent<Animator>();
                if (animator == null)
                    animator = leftOak.gameObject.AddComponent<Animator>();
                image.sprite = firstFrame;
                image.preserveAspect = true;
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.updateMode = AnimatorUpdateMode.Normal;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.speed = 1;
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ValidatePrefab(string path, Sprite firstFrame, RuntimeAnimatorController controller)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var leftOak = FindUnique(prefab.transform, "Left oak", path);
            var rightOak = FindUnique(prefab.transform, "Right oak", path);
            var image = leftOak.GetComponent<Image>();
            var animator = leftOak.GetComponent<Animator>();
            if (image == null || image.sprite != firstFrame || !image.preserveAspect)
                throw new InvalidOperationException(path + " left oak image is not configured with the first sway frame.");
            if (animator == null || animator.runtimeAnimatorController != controller || !animator.enabled)
                throw new InvalidOperationException(path + " left oak Animator is not configured.");
            var rightAnimator = rightOak.GetComponent<Animator>();
            if (rightAnimator != null && rightAnimator.runtimeAnimatorController == controller)
                throw new InvalidOperationException(path + " right oak was changed unexpectedly.");
        }

        private static Transform FindUnique(Transform root, string name, string context)
        {
            var matches = root.GetComponentsInChildren<Transform>(true)
                .Where(item => item.name == name)
                .ToArray();
            if (matches.Length != 1)
                throw new InvalidOperationException($"Expected one '{name}' in {context}; found {matches.Length}.");
            return matches[0];
        }
    }
}

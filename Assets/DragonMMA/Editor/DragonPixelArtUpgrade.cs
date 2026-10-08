using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DragonMMA.EditorTools
{
    /// <summary>Original, reproducible pixel art. Does not recreate scenes or modify player settings.</summary>
    public static class DragonPixelArtUpgrade
    {
        private const string ArtRoot = "Assets/DragonMMA/Resources/DragonMMA/Art";
        private static readonly Color32 Ink = Hex("22333E");
        private static readonly Color32 Ivory = Hex("F6E3B0");
        private static readonly Color32 Gold = Hex("EDB75B");
        private static readonly Color32 Skin = Hex("D58D60");
        private static readonly Color32 SkinLight = Hex("F2BF83");
        private static readonly Color32 SkinShade = Hex("A75E48");
        private static readonly Color32 Teal = Hex("387F82");
        private static readonly Color32 TealLight = Hex("64B0A2");
        private static readonly Color32 Coral = Hex("D55A4D");
        private static readonly Color32 CoralLight = Hex("F18C64");
        private static readonly Color32 Wood = Hex("8C6044");
        private static readonly Color32 WoodLight = Hex("C39460");

        [MenuItem("Dragon MMA/Upgrade Pixel Art")]
        public static void Generate()
        {
            if (!AssetDatabase.IsValidFolder(ArtRoot))
                throw new InvalidOperationException("Create the DragonMMA Art folder before upgrading its art.");

            var written = new List<string>();
            string[] fighterPoses = { "idle", "walk", "punch", "kick", "hurt", "clinch", "takedown", "victory" };
            // The reference-based atlas upgrade owns fighter assets once installed.
            if (!File.Exists(ArtRoot + "/fighter_idle_sheet.png") && !File.Exists(ArtRoot + "/fighter_cross_7.png"))
                foreach (string pose in fighterPoses)
                    for (int frame = 0; frame < 4; frame++)
                        Write("fighter_" + pose + "_" + frame, Fighter(pose, frame), written);

            string[] dragonPoses = { "idle", "attack", "hurt" };
            for (int kind = 0; kind < 5; kind++)
            {
                foreach (string pose in dragonPoses)
                    for (int frame = 0; frame < 4; frame++)
                        Write("dragon_" + kind + "_" + pose + "_" + frame, Dragon(kind, pose, frame), written);
                Write("dragon_" + kind, Dragon(kind, "idle", 0), written);
            }

            Write("forest_tile", ForestTile(), written);
            Write("shrub", Shrub(), written);
            Write("tree", Tree(), written);
            Write("cart", Cart(), written);
            Write("bag", Bag(), written);
            Write("hut", Hut(), written);
            Write("impact", Impact(), written);
            Write("coin", Coin(), written);

            // Import only this generated set. Existing .meta files retain their GUIDs.
            foreach (string path in written)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 20f;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.npotScale = TextureImporterNPOTScale.None;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.isReadable = false;
                importer.SaveAndReimport();
            }
            AssetDatabase.SaveAssets();
            ArtSpriteSheetMigration.Migrate();
            Debug.Log("[Dragon MMA] Upgraded " + written.Count + " original pixel assets and packed them into runtime sheets. Character motion canvas: 40 x 30 px.");
        }

        private static PixelCanvas Fighter(string pose, int frame)
        {
            var p = new PixelCanvas(40, 30);
            int bob = frame == 1 || frame == 3 ? 1 : 0;
            int lean = pose == "punch" ? (frame == 1 || frame == 2 ? 2 : 0) : 0;
            if (pose == "hurt") lean = -2 - (frame == 1 ? 1 : 0);
            if (pose == "clinch") lean = 2;
            if (pose == "takedown") { lean = 3; bob = -3 + (frame == 2 ? -1 : 0); }
            int x = 18 + lean;
            int y = 12 + bob;

            // Back arm and legs, then the torso: the silhouette reads as a guard, not a T-pose.
            if (pose == "walk")
            {
                int stride = new[] { -3, 0, 3, 0 }[frame];
                Limb(p, x - 1, y - 1, x - 4 + stride, 5, x - 4 + stride, 2, 4, SkinShade);
                Limb(p, x + 3, y - 1, x + 5 - stride, 6, x + 7 - stride, 2, 4, Skin);
                Boot(p, x - 5 + stride, 1, 6);
                Boot(p, x + 5 - stride, 1, 7);
            }
            else if (pose == "kick" && (frame == 1 || frame == 2))
            {
                Limb(p, x - 1, y, x - 4, 6, x - 6, 2, 4, SkinShade);
                Boot(p, x - 7, 1, 7);
                Limb(p, x + 3, y, x + 11, y + 2, 34, y + 7, 4, Skin);
                p.Line(33, y + 6, 36, y + 8, 3, Ink);
                p.Line(34, y + 7, 36, y + 8, 1, Ivory);
            }
            else if (pose == "takedown")
            {
                Limb(p, x - 2, y, x - 8, 6, x - 12, 2, 4, SkinShade);
                Limb(p, x + 2, y, x + 7, 4, x + 10, 2, 4, Skin);
                Boot(p, x - 13, 1, 7);
                Boot(p, x + 8, 1, 7);
            }
            else
            {
                Limb(p, x - 2, y, x - 5, 6, x - 7, 2, 4, SkinShade);
                Limb(p, x + 3, y, x + 5, 6, x + 7, 2, 4, Skin);
                Boot(p, x - 8, 1, 7);
                Boot(p, x + 5, 1, 7);
            }

            if (pose == "victory")
            {
                Limb(p, x - 2, y + 7, x - 7, y + 9, x - 9, y + 13, 4, SkinShade);
                Glove(p, x - 10, y + 13, false);
            }
            else
            {
                Limb(p, x - 2, y + 7, x - 6, y + 4, x - 3, y + 2, 4, SkinShade);
                Glove(p, x - 3, y + 3, false);
            }

            // Bare chest, wrapped wrists, teal kickboxing shorts and a small brass belt detail.
            p.Poly(Ink, x - 3, y + 10, x + 4, y + 10, x + 7, y + 6, x + 5, y - 1, x - 4, y - 1, x - 5, y + 6);
            p.Poly(Skin, x - 2, y + 9, x + 3, y + 9, x + 5, y + 6, x + 4, y, x - 3, y, x - 4, y + 6);
            p.Rect(x - 2, y + 5, 4, 4, SkinLight);
            p.Line(x + 3, y + 6, x + 3, y + 2, 1, SkinShade);
            p.Rect(x - 4, y - 2, 10, 5, Ink);
            p.Rect(x - 3, y - 1, 8, 3, Teal);
            p.Rect(x - 3, y + 1, 8, 1, Ivory);
            p.Rect(x + 2, y - 1, 2, 2, TealLight);
            p.Px(x, y + 1, Gold);

            // Head is slightly turned toward the opponent. One bright brow pixel keeps the eye readable.
            int hx = x + 1, hy = y + 9;
            p.Poly(Ink, hx - 4, hy, hx - 5, hy + 4, hx - 3, hy + 7, hx + 3, hy + 7, hx + 5, hy + 4, hx + 5, hy + 1, hx + 2, hy - 1);
            p.Poly(Skin, hx - 3, hy + 1, hx - 3, hy + 5, hx + 3, hy + 5, hx + 4, hy + 2, hx + 1, hy);
            p.Rect(hx - 2, hy + 3, 4, 2, SkinLight);
            p.Rect(hx - 3, hy + 5, 7, 2, Hex("493A37"));
            p.Rect(hx - 4, hy + 3, 2, 3, Hex("493A37"));
            p.Rect(hx - 3, hy + 5, 7, 1, Coral);
            p.Px(hx - 2, hy + 6, CoralLight);
            p.Rect(hx + 2, hy + 3, 2, 1, Ink);
            p.Px(hx + 2, hy + 4, Ivory);
            p.Px(hx + 3, hy + 1, SkinShade);
            p.Line(hx - 4, hy + 4, hx - 7, hy + 3 + frame % 2, 1, Coral);

            if (pose == "punch" && (frame == 1 || frame == 2))
            {
                Limb(p, x + 3, y + 7, x + 10, y + 8, 34, y + 8, 4, Skin);
                p.Line(x + 5, y + 8, 31, y + 9, 1, SkinLight);
                Glove(p, 35, y + 8, true);
            }
            else if (pose == "clinch")
            {
                Limb(p, x + 3, y + 7, x + 9, y + 10, x + 12, y + 8 + frame % 2, 4, Skin);
                Glove(p, x + 12, y + 8 + frame % 2, true);
            }
            else if (pose == "takedown")
            {
                Limb(p, x + 4, y + 7, x + 10, y + 5, x + 12, y + 2 + frame % 2, 4, Skin);
                Glove(p, x + 12, y + 2 + frame % 2, true);
            }
            else if (pose == "victory")
            {
                Limb(p, x + 3, y + 7, x + 8, y + 10, x + 9, y + 14 - frame % 2, 4, Skin);
                Glove(p, x + 9, y + 14 - frame % 2, true);
            }
            else if (pose == "hurt")
            {
                Limb(p, x + 3, y + 7, x + 7, y + 3, x + 10, y + 4, 4, Skin);
                Glove(p, x + 10, y + 4, true);
                p.Px(hx + 1, hy + 3, Ink);
                p.Px(hx + 3, hy + 3, Ink);
            }
            else
            {
                int guard = pose == "walk" ? new[] { -1, 0, 1, 0 }[frame] : frame % 2;
                Limb(p, x + 3, y + 7, x + 8, y + 5, x + 10, y + 9 + guard, 4, Skin);
                Glove(p, x + 10, y + 10 + guard, true);
            }
            return p;
        }

        private static void Boot(PixelCanvas p, int x, int y, int width)
        {
            p.Rect(x, y, width, 3, Ink);
            p.Rect(x + 1, y + 1, width - 2, 1, Ivory);
            p.Px(x + 1, y + 2, TealLight);
        }

        private static void Limb(PixelCanvas p, int ax, int ay, int bx, int by, int cx, int cy, int thickness, Color32 color)
        {
            p.Line(ax, ay, bx, by, thickness + 2, Ink);
            p.Line(bx, by, cx, cy, thickness + 2, Ink);
            p.Line(ax, ay, bx, by, thickness, color);
            p.Line(bx, by, cx, cy, thickness, color);
        }

        private static void Glove(PixelCanvas p, int x, int y, bool front)
        {
            p.Ellipse(x - 3, y - 2, 6, 5, Ink);
            p.Rect(x - 2, y - 1, 4, 3, front ? Coral : Hex("AE4945"));
            p.Rect(x - 1, y + 1, 3, 1, CoralLight);
            p.Rect(x - 3, y - 1, 1, 2, Ivory);
        }

        private static PixelCanvas Dragon(int kind, string pose, int frame)
        {
            var p = new PixelCanvas(40, 30);
            int dy = (frame == 1 || frame == 3 ? 1 : 0) - (pose == "hurt" ? 2 : 0);
            int dx = pose == "attack" && (frame == 1 || frame == 2) ? -2 : 0;
            p.OffsetX = dx;
            p.OffsetY = dy;
            Color32[] colors = { Hex("91BB91"), Hex("79A86D"), Hex("588975"), Hex("B58558"), Hex("BC6657") };
            Color32[] lights = { Hex("C8DEA3"), Hex("AED082"), Hex("89B49A"), Hex("D4AD76"), Hex("EBAB72") };
            Color32[] shades = { Hex("668977"), Hex("4C7D68"), Hex("375B57"), Hex("776A55"), Hex("7D4D4E") };
            Color32 body = colors[kind], light = lights[kind], shade = shades[kind];
            bool attacking = pose == "attack" && (frame == 1 || frame == 2);
            int foot = frame == 1 ? 1 : frame == 3 ? -1 : 0;

            // Silhouette-specific tails are drawn first, never recolored copies of the same creature.
            if (kind == 0)
            {
                p.Poly(Ink, 25, 8, 32, 9, 36, 14, 35, 6, 29, 5, 24, 6);
                p.Poly(body, 26, 8, 32, 8, 34, 11, 33, 7, 28, 6);
            }
            else if (kind == 2)
            {
                p.Line(26, 10, 32, 12, 5, Ink);
                p.Line(26, 10, 32, 12, 3, shade);
                p.Poly(Ink, 30, 7, 36, 6, 39, 10, 38, 18, 33, 20, 29, 16);
                p.Poly(Wood, 31, 8, 35, 7, 38, 11, 37, 17, 33, 18, 30, 15);
                p.Ellipse(33, 8, 5, 10, WoodLight);
                p.Line(35, 10, 36, 15, 1, Wood);
                p.Px(34, 15, Ivory);
                p.Line(31, 11, 32, 16, 1, Hex("5A5A45"));
            }
            else
            {
                p.Poly(Ink, 27, 9, 33, 9, 36, 14, 37, 15, 37, 7, 33, 5, 25, 5);
                p.Poly(shade, 28, 8, 33, 8, 35, 11, 35, 7, 32, 6, 27, 6);
            }

            if (kind == 4)
            {
                // Giant: hunched shoulder, tiny folded wing, heavy fists and upright profile.
                p.Poly(Ink, 22, 18, 27, 26, 29, 21, 33, 23, 32, 14, 25, 11);
                p.Poly(shade, 24, 17, 27, 23, 28, 18, 31, 20, 30, 15, 26, 13);
                p.Poly(Ink, 12, 5, 10, 11, 14, 22, 21, 25, 28, 21, 30, 12, 27, 5);
                p.Poly(body, 13, 6, 12, 11, 15, 21, 21, 23, 27, 20, 28, 12, 26, 6);
                p.Ellipse(14, 7, 11, 13, light);
                p.Line(17, 11, 22, 11, 1, body);
                p.Line(16, 14, 22, 14, 1, body);
                DragonFoot(p, 12 - foot, 2, 9, shade);
                DragonFoot(p, 24 + foot, 2, 9, shade);
                p.Line(26, 18, attacking ? 18 : 28, attacking ? 14 : 10, 7, Ink);
                p.Line(26, 18, attacking ? 18 : 28, attacking ? 14 : 10, 5, body);
                p.Ellipse(attacking ? 13 : 23, attacking ? 10 : 7, 8, 7, Ink);
                p.Ellipse(attacking ? 14 : 24, attacking ? 11 : 8, 6, 5, light);
                p.Poly(Ink, 7, 16, 6, 22, 10, 26, 18, 26, 23, 22, 22, 16, 16, 13);
                p.Poly(body, 8, 17, 8, 21, 11, 24, 18, 24, 21, 21, 20, 17, 15, 15);
                p.Rect(5, 16, 10, 6, Ink);
                p.Rect(6, 17, 9, 4, body);
                p.Rect(6, 20, 6, 1, light);
                Horn(p, 10, 24, 7, 28);
                Horn(p, 20, 24, 24, 28);
                p.Line(10, 22, 14, 21, 1, Ink);
                Eye(p, 10, 20, pose, frame, true);
                p.Rect(7, 16, 2, 2, Ivory);
                p.Px(6, 19, Ink);
            }
            else
            {
                int bx = kind == 0 ? 16 : 14;
                int bw = kind == 0 ? 15 : kind == 3 ? 20 : 18;
                int bh = kind == 0 ? 16 : kind == 3 ? 15 : 12;
                int by = 5;
                p.Ellipse(bx - 1, by, bw, bh, Ink);
                p.Ellipse(bx, by + 1, bw - 2, bh - 2, body);
                p.Ellipse(bx + 1, by + 2, bw - 6, bh - 6, light);
                p.Ellipse(bx + bw - 7, by + 4, 5, 6, shade);
                DragonFoot(p, bx + 1 - foot, 2, 6, shade);
                DragonFoot(p, bx + bw - 7 + foot, 2, 7, shade);

                if (kind == 0)
                {
                    p.Poly(Ink, 25, 12, 29, 20, 31, 17, 34, 17, 32, 12, 28, 10);
                    p.Poly(shade, 27, 13, 29, 17, 30, 15, 32, 15, 30, 12);
                    p.Poly(Ink, 6, 12, 5, 18, 8, 23, 15, 24, 21, 20, 22, 14, 16, 11);
                    p.Poly(body, 7, 13, 7, 18, 9, 21, 15, 22, 19, 19, 20, 15, 16, 13);
                    p.Rect(3, 12, 10, 6, Ink);
                    p.Rect(4, 13, 10, 4, body);
                    p.Rect(5, 16, 7, 1, light);
                    Horn(p, 15, 22, 17, 26);
                    p.Poly(Ink, 19, 20, 24, 23, 23, 18);
                    p.Poly(light, 20, 20, 22, 21, 22, 19);
                    Eye(p, 9, 18, pose, frame, false);
                    p.Px(4, 15, Ink);
                    p.Rect(7, 12, 4, 1, shade);
                }
                else if (kind == 1)
                {
                    p.Poly(Ink, 4, 11, 3, 18, 7, 24, 15, 24, 21, 19, 21, 12, 15, 9);
                    p.Poly(body, 5, 12, 5, 18, 8, 22, 15, 22, 19, 18, 19, 13, 14, 11);
                    p.Poly(shade, 6, 18, 9, 22, 14, 22, 16, 20, 10, 18);
                    p.Line(8, 22, 13, 23, 2, light);
                    Horn(p, 8, 22, 5, 28);
                    p.Rect(2, 10, 12, 7, Ink);
                    p.Rect(3, 11, 11, 5, body);
                    p.Rect(4, 15, 6, 1, light);
                    Eye(p, 8, 18, pose, frame, true);
                    p.Px(3, 14, Ink);
                    p.Line(4, 11, 8, 11, 1, shade);
                    p.Poly(Ink, 23, 18, 25, 22, 28, 17);
                    p.Poly(light, 24, 18, 25, 20, 26, 18);
                }
                else if (kind == 2)
                {
                    // Logtail: long snout, leaf crest, grain-marked club and horizontal crawler body.
                    p.Poly(Ink, 5, 11, 6, 18, 12, 22, 19, 21, 23, 17, 22, 10, 14, 8);
                    p.Poly(body, 7, 12, 8, 18, 13, 20, 19, 19, 21, 16, 20, 11, 14, 10);
                    p.Rect(2, 10, 14, 7, Ink);
                    p.Rect(3, 11, 13, 5, body);
                    p.Rect(4, 14, 8, 2, light);
                    p.Poly(Ink, 13, 21, 15, 26, 18, 23, 21, 25, 21, 19);
                    p.Poly(Hex("92A568"), 14, 21, 15, 24, 17, 21, 20, 23, 20, 20);
                    Eye(p, 9, 17, pose, frame, true);
                    p.Px(3, 13, Ink);
                    p.Rect(6, 10, 2, 1, Ivory);
                    p.Rect(14, 7, 8, 2, shade);
                    p.Px(23, 16, light);
                    p.Px(25, 14, light);
                }
                else
                {
                    // Stonehorn: wide armored shell, rocky segmented plates, stout legs and two horns.
                    p.Poly(Ink, 14, 16, 16, 23, 22, 27, 29, 25, 34, 19, 33, 10, 24, 8);
                    p.Poly(Hex("777E73"), 16, 16, 18, 22, 23, 25, 28, 23, 32, 18, 31, 12, 24, 10);
                    p.Poly(Hex("A8AD8B"), 18, 20, 23, 24, 27, 22, 25, 18, 20, 17);
                    p.Line(25, 18, 28, 13, 1, Ink);
                    p.Line(25, 18, 31, 19, 1, Ink);
                    p.Line(20, 17, 17, 13, 1, Ink);
                    p.Poly(Ink, 4, 10, 4, 17, 9, 21, 15, 20, 20, 16, 18, 10, 12, 8);
                    p.Poly(body, 6, 11, 6, 17, 10, 19, 15, 18, 18, 15, 16, 11, 12, 10);
                    p.Rect(2, 10, 10, 5, Ink);
                    p.Rect(3, 11, 9, 3, light);
                    Horn(p, 7, 19, 4, 25);
                    Horn(p, 13, 19, 15, 24);
                    Eye(p, 8, 16, pose, frame, true);
                    p.Px(3, 12, Ink);
                }

                // Small forward paw gives idle breathing, attack swipes and completed-training punches.
                int pawX = attacking ? 6 : 13;
                p.Line(18, 11, pawX, attacking ? 12 : 8, 4, Ink);
                p.Line(18, 11, pawX, attacking ? 12 : 8, 2, body);
                p.Rect(pawX - 2, attacking ? 10 : 6, 4, 3, shade);
                p.Px(pawX - 2, attacking ? 10 : 6, Ivory);
            }

            if (pose == "hurt")
            {
                p.Px(4 + frame * 2, 26, Gold);
                p.Px(5 + frame * 2, 27, Ivory);
            }
            return p;
        }

        private static void Eye(PixelCanvas p, int x, int y, string pose, int frame, bool fierce)
        {
            if (pose == "hurt")
            {
                p.Px(x, y + 1, Ink); p.Px(x + 1, y, Ink); p.Px(x, y - 1, Ink);
                return;
            }
            if (pose == "idle" && frame == 3)
                p.Rect(x, y, 3, 1, Ink);
            else
            {
                p.Rect(x, y, 3, 3, Ivory);
                p.Rect(x, y, 2, 2, Ink);
                p.Px(x + 1, y + 2, Hex("FFFFFF"));
                if (fierce) p.Line(x - 1, y + 3, x + 3, y + 2, 1, Ink);
            }
        }

        private static void Horn(PixelCanvas p, int x, int y, int tipX, int tipY)
        {
            p.Poly(Ink, x - 3, y - 1, x + 2, y - 1, tipX, tipY);
            p.Poly(Ivory, x - 1, y, x + 1, y, tipX, tipY - 1);
            p.Px(x, y, Gold);
        }

        private static void DragonFoot(PixelCanvas p, int x, int y, int width, Color32 color)
        {
            p.Rect(x, y, width, 5, Ink);
            p.Rect(x + 1, y + 1, width - 2, 4, color);
            p.Rect(x, y + 1, 2, 1, Ivory);
            p.Px(x + 3, y + 1, Ivory);
        }

        private static PixelCanvas ForestTile()
        {
            var p = new PixelCanvas(192, 72);
            Color32 soil = Hex("59483B"), soilDeep = Hex("3B3834"), soilLight = Hex("806247");
            Color32 moss = Hex("3F7155"), mossLight = Hex("759263"), path = Hex("B29A6A");
            // World surface sits at y=41. The upper third is alpha, broken only by grass and plants.
            p.Rect(0, 0, 192, 39, soilDeep);
            p.Rect(0, 7, 192, 31, soil);
            p.Rect(0, 31, 192, 11, moss);
            p.Rect(0, 34, 192, 6, mossLight);
            p.Rect(0, 35, 192, 3, Hex("90A773"));
            p.Rect(0, 27, 192, 7, path);
            p.Rect(0, 27, 192, 2, Hex("8C7955"));
            for (int x = 0; x < 192; x += 3)
            {
                int n = Hash(x, 3);
                p.Rect(x, 38, 2, 3 + n % 5, moss);
                p.Px(x + 1, 41 + n % 3, mossLight);
                if (n % 4 == 0) p.Line(x, 39, x - 2, 44, 1, Hex("829B62"));
                if (n % 5 == 0) p.Rect(x, 29 + n % 3, 2, 1, Hex("D1BA83"));
            }
            for (int i = 0; i < 70; i++)
            {
                int x = Hash(i, 71) % 192, y = 7 + Hash(i, 19) % 18;
                p.Rect(x, y, 2 + i % 3, 1 + i % 2, i % 3 == 0 ? soilDeep : soilLight);
            }
            for (int x = 16; x < 192; x += 49)
            {
                p.Line(x, 31, x + 2, 21, 2, soilDeep);
                p.Line(x + 2, 23, x + 8, 18, 1, soilDeep);
                p.Line(x + 2, 23, x - 3, 17, 1, soilDeep);
            }
            // Small mushrooms, stones and wild flowers make scrolling sections recognizable.
            p.Rect(42, 39, 2, 5, Ivory);
            p.Ellipse(38, 43, 9, 4, Coral);
            p.Px(40, 45, Ivory); p.Px(44, 44, Ivory);
            p.Rect(150, 40, 1, 6, moss);
            p.Rect(147, 45, 5, 2, Gold);
            p.Rect(149, 44, 2, 4, Gold);
            p.Px(149, 46, Ivory);
            p.Ellipse(99, 39, 10, 5, Hex("65776C"));
            p.Line(101, 42, 105, 42, 1, Hex("91A18C"));
            return p;
        }

        private static PixelCanvas Shrub()
        {
            var p = new PixelCanvas(48, 28);
            LeafCluster(p, 3, 3, 22, 17, Hex("284E46"), Hex("3E7156"), Hex("638C60"));
            LeafCluster(p, 17, 5, 23, 21, Hex("284E46"), Hex("3E7156"), Hex("638C60"));
            LeafCluster(p, 31, 2, 15, 16, Hex("284E46"), Hex("3E7156"), Hex("638C60"));
            p.Line(9, 2, 12, 14, 1, Hex("1F443E"));
            p.Line(26, 3, 28, 19, 1, Hex("1F443E"));
            p.Px(14, 10, Coral); p.Px(16, 12, CoralLight); p.Px(32, 10, Gold);
            return p;
        }

        private static PixelCanvas Tree()
        {
            var p = new PixelCanvas(64, 100);
            Color32 bark = Hex("435247"), barkShadow = Hex("293E39"), barkLight = Hex("63705A");
            p.Poly(barkShadow, 22, 1, 27, 20, 29, 70, 35, 75, 41, 20, 47, 1);
            p.Poly(bark, 27, 2, 31, 21, 31, 70, 34, 71, 37, 20, 41, 2);
            p.Line(33, 9, 33, 60, 2, barkLight);
            p.Line(30, 36, 16, 57, 5, barkShadow);
            p.Line(36, 45, 50, 64, 5, barkShadow);
            p.Line(31, 35, 17, 57, 2, bark);
            p.Line(35, 46, 49, 64, 2, bark);
            p.Line(29, 10, 22, 2, 2, barkLight);
            Color32 dark = Hex("254940"), green = Hex("365F49"), lit = Hex("55794F");
            LeafCluster(p, 4, 52, 31, 27, dark, green, lit);
            LeafCluster(p, 24, 51, 36, 31, dark, green, lit);
            LeafCluster(p, 13, 67, 39, 31, dark, green, lit);
            LeafCluster(p, 3, 65, 22, 24, dark, green, lit);
            LeafCluster(p, 37, 68, 25, 23, dark, green, lit);
            p.Rect(30, 29, 4, 6, barkShadow);
            p.Rect(31, 31, 2, 3, Hex("B9A36A"));
            // A few warm leaf tips catch the light without turning the canopy into random noise.
            p.Rect(21, 92, 5, 2, Hex("82905B"));
            p.Rect(40, 82, 4, 2, Hex("82905B"));
            p.Rect(9, 75, 3, 2, Hex("82905B"));
            p.Ellipse(20, 0, 28, 5, Hex("34533E"));
            return p;
        }

        private static void LeafCluster(PixelCanvas p, int x, int y, int w, int h, Color32 dark, Color32 mid, Color32 light)
        {
            p.Poly(dark, x + w / 5, y, x, y + h / 3, x + 2, y + h * 2 / 3, x + w / 4, y + h - 2,
                x + w * 2 / 3, y + h, x + w - 2, y + h * 2 / 3, x + w, y + h / 3, x + w * 3 / 4, y + 2);
            p.Poly(mid, x + 3, y + h / 2, x + w / 4, y + h - 4, x + w * 2 / 3, y + h - 2,
                x + w - 4, y + h * 2 / 3, x + w * 3 / 4, y + h / 3, x + w / 3, y + h / 3);
            p.Poly(light, x + 4, y + h * 2 / 3, x + w / 4, y + h - 5, x + w / 2, y + h - 3,
                x + w * 2 / 3, y + h - 7, x + w / 3, y + h * 2 / 3);
            p.Rect(x + w - 8, y + h / 2, 4, 2, light);
            p.Rect(x + w / 3, y + 4, 4, 2, dark);
        }

        private static PixelCanvas Cart()
        {
            var p = new PixelCanvas(64, 40);
            p.Line(48, 17, 62, 22, 3, Ink);
            p.Line(49, 18, 62, 23, 1, WoodLight);
            p.Rect(6, 10, 47, 25, Ink);
            // Cage interior is intentionally transparent so the UI can place actual cargo behind bars.
            p.Rect(8, 15, 43, 18, new Color32(0, 0, 0, 0));
            p.Rect(7, 11, 45, 5, Wood);
            p.Rect(7, 15, 45, 1, WoodLight);
            p.Rect(8, 32, 43, 2, WoodLight);
            p.Rect(8, 29, 43, 2, Wood);
            for (int x = 10; x <= 48; x += 9)
            {
                p.Rect(x, 15, 2, 17, Ink);
                p.Rect(x, 16, 1, 15, Hex("728A83"));
                p.Px(x, 30, Ivory);
            }
            p.Poly(Ink, 5, 34, 11, 38, 47, 38, 54, 34);
            p.Poly(Wood, 8, 34, 12, 36, 46, 36, 50, 34);
            p.Line(12, 36, 45, 36, 1, WoodLight);
            Wheel(p, 10, 2);
            Wheel(p, 39, 2);
            p.Rect(27, 21, 5, 6, Ink);
            p.Rect(28, 22, 3, 4, Gold);
            p.Px(29, 24, Ink);
            p.Rect(28, 13, 8, 1, WoodLight);
            p.Px(7, 13, Gold); p.Px(49, 13, Gold);
            return p;
        }

        private static void Wheel(PixelCanvas p, int x, int y)
        {
            p.Ellipse(x, y, 12, 12, Ink);
            p.Ellipse(x + 2, y + 2, 8, 8, Hex("67776D"));
            p.Ellipse(x + 3, y + 3, 6, 6, Hex("3C4E49"));
            p.Line(x + 3, y + 6, x + 8, y + 6, 1, WoodLight);
            p.Line(x + 6, y + 3, x + 6, y + 8, 1, WoodLight);
            p.Rect(x + 5, y + 5, 2, 2, Gold);
        }

        private static PixelCanvas Bag()
        {
            var p = new PixelCanvas(24, 40);
            p.Rect(17, 1, 3, 37, Ink);
            p.Rect(18, 3, 1, 33, WoodLight);
            p.Rect(6, 36, 15, 3, Ink);
            p.Rect(7, 37, 12, 1, WoodLight);
            p.Line(7, 35, 8, 29, 1, Ivory);
            p.Poly(Ink, 3, 7, 2, 11, 3, 28, 6, 30, 12, 29, 14, 25, 13, 9, 10, 6);
            p.Poly(Coral, 4, 8, 3, 12, 4, 27, 7, 28, 11, 27, 12, 24, 11, 10, 9, 8);
            p.Rect(4, 12, 2, 12, CoralLight);
            p.Rect(3, 13, 10, 2, Hex("994B47"));
            p.Rect(4, 23, 9, 2, Ivory);
            p.Rect(6, 16, 4, 4, Ivory);
            p.Rect(7, 17, 2, 2, Teal);
            p.Rect(12, 1, 12, 3, Ink);
            p.Rect(14, 2, 8, 1, Wood);
            return p;
        }

        private static PixelCanvas Hut()
        {
            var p = new PixelCanvas(96, 72);
            Color32 wall = Hex("BAAD82"), wallShadow = Hex("8B906D");
            p.Rect(10, 6, 75, 40, Ink);
            p.Rect(13, 9, 69, 34, wall);
            p.Rect(14, 9, 68, 7, wallShadow);
            for (int x = 17; x < 80; x += 15) p.Rect(x, 13, 2, 27, Wood);
            p.Rect(10, 7, 76, 4, Wood);
            p.Rect(8, 4, 80, 4, Ink);
            p.Rect(10, 5, 76, 2, WoodLight);
            p.Rect(37, 10, 24, 30, Ink);
            p.Rect(40, 11, 18, 27, Hex("344D48"));
            p.Rect(41, 12, 16, 4, Hex("51614D"));
            p.Rect(39, 37, 20, 2, Gold);
            p.Rect(19, 22, 13, 13, Ink);
            p.Rect(21, 24, 9, 9, Hex("E5B968"));
            p.Rect(25, 24, 1, 9, Wood);
            p.Rect(21, 28, 9, 1, Wood);
            p.Rect(65, 22, 13, 13, Ink);
            p.Rect(67, 24, 9, 9, Hex("E5B968"));
            p.Rect(71, 24, 1, 9, Wood);
            p.Rect(67, 28, 9, 1, Wood);
            p.Poly(Ink, 2, 40, 15, 56, 38, 67, 60, 67, 80, 55, 94, 40);
            p.Poly(Hex("31544A"), 6, 42, 17, 54, 39, 64, 59, 64, 78, 53, 90, 42);
            p.Poly(Hex("50826A"), 10, 44, 20, 53, 39, 61, 59, 61, 76, 52, 85, 44);
            p.Line(20, 53, 75, 53, 1, Hex("7B9A72"));
            p.Line(14, 47, 82, 47, 1, Hex("7B9A72"));
            p.Rect(36, 43, 27, 10, Ink);
            p.Rect(38, 44, 23, 8, Wood);
            p.Rect(39, 50, 21, 1, WoodLight);
            // Glove sign, readable even before the UI supplies a label.
            p.Ellipse(46, 45, 9, 6, Coral);
            p.Rect(44, 45, 3, 3, Ivory);
            p.Px(51, 50, CoralLight);
            p.Line(86, 37, 88, 30, 1, Ink);
            p.Rect(84, 23, 8, 8, Ink);
            p.Rect(86, 25, 4, 5, Gold);
            p.Rect(87, 26, 2, 3, Ivory);
            return p;
        }

        private static PixelCanvas Impact()
        {
            var p = new PixelCanvas(32, 32);
            p.Poly(Gold, 15, 3, 12, 12, 3, 10, 10, 16, 2, 22, 12, 20, 14, 30,
                18, 22, 27, 27, 23, 19, 31, 15, 22, 12, 25, 4, 18, 10);
            p.Poly(Ivory, 15, 8, 14, 14, 8, 14, 13, 17, 9, 21, 15, 19, 16, 25,
                18, 20, 23, 23, 21, 18, 26, 15, 20, 14, 21, 9, 17, 13);
            p.Px(4, 4, Ivory); p.Px(27, 29, Gold); p.Px(29, 7, Ivory);
            return p;
        }

        private static PixelCanvas Coin()
        {
            var p = new PixelCanvas(12, 12);
            p.Ellipse(1, 0, 10, 12, Ink);
            p.Ellipse(2, 1, 8, 10, Hex("AA743F"));
            p.Ellipse(2, 3, 7, 8, Gold);
            p.Line(4, 9, 7, 9, 1, Ivory);
            p.Line(3, 7, 3, 9, 1, Ivory);
            p.Rect(5, 4, 2, 4, Hex("B98546"));
            p.Px(7, 3, Ivory);
            return p;
        }

        private static int Hash(int x, int seed)
        {
            unchecked
            {
                uint n = (uint)(x * 374761393 + seed * 668265263);
                n = (n ^ (n >> 13)) * 1274126177;
                return (int)((n ^ (n >> 16)) & 0x7fffffff);
            }
        }

        private static Color32 Hex(string rgb)
        {
            ColorUtility.TryParseHtmlString("#" + rgb, out Color c);
            return c;
        }

        private static void Write(string name, PixelCanvas pixels, List<string> paths)
        {
            var texture = new Texture2D(pixels.Width, pixels.Height, TextureFormat.RGBA32, false);
            try
            {
                texture.SetPixels32(pixels.Pixels);
                texture.Apply(false, false);
                string path = ArtRoot + "/" + name + ".png";
                File.WriteAllBytes(path, texture.EncodeToPNG());
                paths.Add(path);
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        private sealed class PixelCanvas
        {
            public readonly int Width;
            public readonly int Height;
            public readonly Color32[] Pixels;
            public int OffsetX;
            public int OffsetY;

            public PixelCanvas(int width, int height)
            {
                Width = width;
                Height = height;
                Pixels = new Color32[width * height];
            }

            public void Px(int x, int y, Color32 color)
            {
                x += OffsetX;
                y += OffsetY;
                if (x >= 0 && y >= 0 && x < Width && y < Height) Pixels[y * Width + x] = color;
            }

            public void Rect(int x, int y, int w, int h, Color32 color)
            {
                for (int py = y; py < y + h; py++)
                    for (int px = x; px < x + w; px++) Px(px, py, color);
            }

            public void Ellipse(int x, int y, int w, int h, Color32 color)
            {
                float rx = w * .5f, ry = h * .5f;
                for (int py = y; py < y + h; py++)
                    for (int px = x; px < x + w; px++)
                    {
                        float dx = (px + .5f - x - rx) / rx, dy = (py + .5f - y - ry) / ry;
                        if (dx * dx + dy * dy <= 1f) Px(px, py, color);
                    }
            }

            public void Line(int ax, int ay, int bx, int by, int thickness, Color32 color)
            {
                int dx = Mathf.Abs(bx - ax), sx = ax < bx ? 1 : -1;
                int dy = -Mathf.Abs(by - ay), sy = ay < by ? 1 : -1;
                int error = dx + dy;
                while (true)
                {
                    Rect(ax - thickness / 2, ay - thickness / 2, thickness, thickness, color);
                    if (ax == bx && ay == by) break;
                    int e2 = 2 * error;
                    if (e2 >= dy) { error += dy; ax += sx; }
                    if (e2 <= dx) { error += dx; ay += sy; }
                }
            }

            public void Poly(Color32 color, params int[] xy)
            {
                int count = xy.Length / 2;
                int minY = Height, maxY = 0;
                for (int i = 0; i < count; i++) { minY = Mathf.Min(minY, xy[i * 2 + 1]); maxY = Mathf.Max(maxY, xy[i * 2 + 1]); }
                for (int y = minY; y <= maxY; y++)
                {
                    var crossings = new List<float>(count);
                    for (int i = 0, j = count - 1; i < count; j = i++)
                    {
                        int xi = xy[i * 2], yi = xy[i * 2 + 1], xj = xy[j * 2], yj = xy[j * 2 + 1];
                        if ((yi > y + .5f) != (yj > y + .5f))
                            crossings.Add(xi + (y + .5f - yi) * (xj - xi) / (float)(yj - yi));
                    }
                    crossings.Sort();
                    for (int i = 0; i + 1 < crossings.Count; i += 2)
                        for (int x = Mathf.CeilToInt(crossings[i] - .5f); x <= Mathf.FloorToInt(crossings[i + 1] - .5f); x++) Px(x, y, color);
                }
            }
        }
    }
}

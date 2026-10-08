using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DragonMMA.EditorTools
{
    /// <summary>Pixel diorama + event-driven facility UI, separate from persistent rules.</summary>
    public sealed class DragonMmaLayoutSeed : IDisposable
    {
        private static readonly Color Ink = Hex("152C2E"), Panel = Hex("203D3C"), Edge = Hex("45635A");
        private static readonly Color Cream = Hex("F5EDD1"), Muted = Hex("B2C8B3"), Gold = Hex("E8B95C");
        private static readonly Color Mint = Hex("8BCEAB"), Red = Hex("DD806A");
        private readonly DragonMmaGame owner;
        private readonly DragonGameSession game;
        private readonly DesktopOverlayWindow window;
        private readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        private readonly List<Rect> inputRects = new List<Rect>(10);
        private readonly List<RectTransform> interactive = new List<RectTransform>();
        private readonly List<Image> cargo = new List<Image>();
        private readonly List<Image> departingCargo = new List<Image>();
        private readonly List<Image> roamers = new List<Image>();
        private readonly List<int> roamerKinds = new List<int>();
        private readonly List<Image> terrain = new List<Image>();
        private readonly List<Text> timerLabels = new List<Text>();
        private readonly List<TimedDragonSlot> timerSlots = new List<TimedDragonSlot>();
        private readonly Vector3[] corners = new Vector3[4];
        private readonly Font font;
        private Canvas canvas;
        private CanvasScaler scaler;
        private RectTransform root, hunt, baseRoot, content, cartRoot, departure;
        private Image fighter, enemy, impact, helper, trainee, trainingBag;
        private Image progress, cheerProgress;
        private Text resources, state, chance, skill, cartLabel, goal, toast, enemyLabel, fighterLabel;
        private Text trainingProgressText;
        private Image trainingProgress;
        private Button returnButton;
        private RectTransform toastBox, battleArea;
        private float toastUntil, nextRefresh, returnTime, cheerTime, walkOffset;
        private string selectedId = "";
        private int page;
        private bool panelDirty = true;
        private HuntPhase previousPhase;
        public string ActivePanel { get; private set; } = "";
        public Canvas Canvas => canvas;
        private GameSaveData Data => game.Data;

        public DragonMmaLayoutSeed(DragonMmaGame owner, DragonGameSession game, DesktopOverlayWindow window)
        {
            this.owner = owner; this.game = game; this.window = window;
            font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, 18);
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Build();
        }

        private void Build()
        {
            var go = new GameObject("OverlayRoot", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;
            scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            root = go.GetComponent<RectTransform>();
            hunt = Group("Hunt", root, 0, 0, 1920, 360);

            // Transparent above the silhouetted foliage. No sky-sized opaque rectangle.
            Box("Continuous soil bed", hunt, Hex("3B3834"), 0, 56, 1920, 80);
            for (int i = 0; i < 7; i++)
                terrain.Add(Picture("Forest tile", hunt, "forest_tile", i * 384 - 384, 56, 384, 144));
            Picture("Left oak", hunt, "tree", 60, 105, 128, 200);
            Picture("Right oak", hunt, "tree", 1722, 105, 128, 200);
            for (int i = 0; i < 10; i++)
                Picture("Understory", hunt, "shrub", i * 211 + 8, 110, 96, 56);
            Picture("Forest outpost", hunt, "hut", 276, 113, 192, 144);
            // Invisible logical hit bounds. Native color-key void stays click-through;
            // visible fighters and forest ground within these bounds accept cheer.
            battleArea = Group("Battle input patch", hunt, 694, 110, 580, 140);
            cartRoot = Group("PrisonCart", hunt, 506, 114, 156, 100);
            for (int i = 0; i < 3; i++) cargo.Add(Picture("Passenger", cartRoot, "dragon_0", 24 + i * 32, 31, 50, 38));
            Picture("Cage", cartRoot, "cart", 0, 0, 160, 100);
            departure = Group("Departing cart", hunt, 506, 114, 240, 105);
            for (int i = 0; i < 3; i++) departingCargo.Add(Picture("Departing passenger", departure, "dragon_0", 24 + i * 32, 31, 50, 38));
            Picture("Departing cage", departure, "cart", 0, 0, 160, 100);
            helper = Picture("Cart helper", departure, "fighter_walk_0", -65, 4, 120, 90);
            helper.color = Hex("A0C3B3");
            departure.gameObject.SetActive(false);
            fighter = Picture("Hunter", hunt, "fighter_idle_0", 795, 128, 120, 90);
            enemy = Picture("Dragon", hunt, "dragon_0", 926, 125, 160, 120);
            impact = Picture("Contact sparks", hunt, "impact", 912, 161, 70, 70);
            var fighterPlate = Box("Hunter name plate", hunt, Ink, 765, 90, 180, 24).rectTransform;
            var enemyPlate = Box("Dragon name plate", hunt, Ink, 963, 90, 215, 24).rectTransform;
            fighterLabel = Label("Fighter name", fighterPlate, "", 13, Cream, 0, 0, 180, 24, TextAnchor.MiddleCenter);
            enemyLabel = Label("Dragon card", enemyPlate, "", 13, Mint, 0, 0, 215, 24, TextAnchor.MiddleCenter);

            var brand = Card("Field journal", hunt, 24, 274, 474, 72);
            Label("Brand", brand, "DRAGON  /  MMA IDLE", 24, Cream, 16, 34, 430, 32);
            resources = Label("Wallet", brand, "", 16, Gold, 16, 6, 440, 28);
            interactive.Add(brand);

            var status = Card("Battle status", hunt, 700, 274, 572, 72);
            state = Label("Phase", status, "", 18, Cream, 16, 40, 375, 28);
            chance = Label("Live odds", status, "", 21, Gold, 370, 40, 184, 28, TextAnchor.MiddleRight);
            progress = Bar(status, 16, 32, 538, 4, Gold);
            skill = Label("Technique", status, "", 13, Muted, 16, 4, 410, 24);
            cheerProgress = Bar(status, 436, 11, 116, 5, Mint);
            interactive.Add(status);

            var destination = Card("Collection objective", hunt, 1430, 274, 466, 72);
            Label("Chapter", destination, "01  /  THE FOREST", 14, Gold, 16, 40, 422, 24);
            goal = Label("Next goal", destination, "", 15, Cream, 16, 8, 430, 30);
            interactive.Add(destination);

            var nav = Box("Bottom toolbar", hunt, Ink, 0, 0, 1920, 56).rectTransform;
            Box("Toolbar rule", nav, Gold, 0, 54, 1920, 2);
            Button("Camp", nav, "거점 보기  ↑", 20, 8, 150, 38, () => OpenPanel(ActivePanel == "" ? "storage" : ""));
            Button("Training", nav, "수련방", 182, 8, 118, 38, () => Toggle("training"));
            Button("Market", nav, "판매소", 310, 8, 118, 38, () => Toggle("market"));
            Button("Skills", nav, "기술 도감", 438, 8, 132, 38, () => Toggle("skills"));
            Label("Input hint", nav, "전투 영역 클릭 / SPACE  ·  +0.2%p  ·  최대 +10%p", 14, Muted, 594, 8, 666, 38);
            cartLabel = Label("Capacity", nav, "", 16, Cream, 1286, 8, 200, 38, TextAnchor.MiddleRight);
            returnButton = Button("Return", nav, "수레 귀환 →", 1510, 8, 180, 38, owner.ReturnCart, Gold, Ink);
            Button("Settings fixed tab", nav, "설정", 1702, 8, 92, 38, () => Toggle("settings"));
            Button("Exit", nav, "종료", 1806, 8, 94, 38, () => OpenPanel("exit"));
            interactive.Add(nav);
            interactive.Add(battleArea);
            interactive.Add(cartRoot);
            toastBox = Card("Notification", hunt, 680, 238, 610, 30);
            toast = Label("Notification text", toastBox, "", 14, Cream, 8, 0, 594, 30, TextAnchor.MiddleCenter);
            toastBox.gameObject.SetActive(false);

            baseRoot = Card("BaseUI", root, 16, 362, 1888, 348);
            // Management is above the hunt, and never pauses the session.
            Label("Base title", baseRoot, "FOREST CAMP  /  숲 거점", 22, Cream, 22, 298, 490, 40);
            Label("Live tag", baseRoot, "● 사냥 진행 중", 14, Mint, 510, 301, 192, 34);
            string[] tabs = { "storage", "training", "market", "skills" };
            string[] labels = { "수용소", "수련방", "판매소", "기술 도감" };
            for (int i = 0; i < tabs.Length; i++)
            {
                string tab = tabs[i];
                Button("Tab " + tab, baseRoot, labels[i], 902 + i * 174, 300, 162, 36, () => OpenPanel(tab));
            }
            Button("Close base", baseRoot, "접기  ↓", 1638, 300, 226, 36, () => OpenPanel(""));
            Box("Base divider", baseRoot, Edge, 22, 287, 1842, 1);
            content = Group("Facility content", baseRoot, 22, 12, 1842, 264);
            baseRoot.gameObject.SetActive(false);
        }

        public void OpenPanel(string name)
        {
            ActivePanel = name;
            baseRoot.gameObject.SetActive(name.Length > 0);
            window?.SetExpanded(name.Length > 0);
            panelDirty = true;
            Refresh(true);
            DragonMmaGame.ClearSelection();
        }

        private void Toggle(string name) => OpenPanel(ActivePanel == name ? "" : name);
        public void Refresh(bool structural = false)
        {
            if (structural) panelDirty = true;
            resources.text = $"G  {Data.coins:N0}     ◆  {Data.cores:N0}     전투력  {Data.fighterPower}";
            cartLabel.text = $"포획  {Data.cart.Count} / 3";
            returnButton.interactable = Data.cart.Count > 0;
            for (int i = 0; i < 3; i++)
            {
                cargo[i].gameObject.SetActive(i < Data.cart.Count);
                if (i < Data.cart.Count) cargo[i].sprite = Sprite("dragon_" + Data.cart[i].kind);
            }
            int mastered = 0;
            for (int i = 0; i < 5; i++) if (Data.trainingRewardsClaimed[i]) mastered++;
            goal.text = $"수련 도감 {mastered}/5   →   Giant Takedown";
            fighterLabel.text = $"MMA 파이터  ·  힘 {Data.fighterPower}";
            if (Data.currentEnemy >= 0)
            {
                var spec = DragonCatalog.Get((DragonKind)Data.currentEnemy);
                enemyLabel.text = spec.displayName + "  ·  힘 " + spec.power;
                enemyLabel.color = SpeciesColor(Data.currentEnemy);
            }
            if (panelDirty && ActivePanel.Length > 0) BuildPanel();
            RefreshTimers();
        }

        public void Tick(float dt)
        {
            RefreshLayout(Screen.width, Screen.height);
            float time = Time.unscaledTime;
            if (time >= nextRefresh)
            {
                Refresh();
                UpdateRegions();
                nextRefresh = time + 0.1f;
            }
            if (toastBox.gameObject.activeSelf && time > toastUntil) toastBox.gameObject.SetActive(false);
            if (Data.phase != previousPhase)
            {
                previousPhase = Data.phase;
                panelDirty = true;
            }
            bool fighting = Data.phase == HuntPhase.Fighting;
            float duration = fighting ? 20f : Data.phase == HuntPhase.Result ? 2f : 10f;
            SetFill(progress, Mathf.Clamp01(Data.phaseRemaining / duration));
            SetFill(cheerProgress, Data.cheerBonus / 10f);
            state.text = fighting ? $"승부까지 {Data.phaseRemaining:0.0}초" :
                Data.phase == HuntPhase.Walking ? $"숲 탐색  ·  조우까지 {Data.phaseRemaining:0}초" :
                Data.phase == HuntPhase.Recovery ? $"잠깐 휴식  ·  {Data.phaseRemaining:0}초" : "승리  /  " + (Data.lastWasExtortion ? "삥뜯기!" : "포획!");
            chance.text = fighting ? $"{game.CurrentWinChance:0.0}%" : Data.phase == HuntPhase.Recovery ? "K.O." : "AUTO";
            chance.color = Data.phase == HuntPhase.Recovery ? Red : Gold;
            skill.text = fighting ? game.ActiveSkill + $"   응원 +{Data.cheerBonus:0.0}%p" : "자동 사냥 중  /  전투 마지막 순간까지 응원";
            Animate(time, dt);
        }

        public void RefreshLayout(int width, int height)
        {
            scaler.scaleFactor = Mathf.Max(0.25f, Mathf.Min(width / 1920f, height / (ActivePanel.Length > 0 ? 720f : 360f)));
            float inset = Mathf.Max(0, (width / scaler.scaleFactor - 1920) * 0.5f);
            Place(hunt, inset, 0);
            Place(baseRoot, inset + 16, 362);
        }

        private void Animate(float time, float dt)
        {
            int frame = (int)(time * 7) % 4;
            bool fighting = Data.phase == HuntPhase.Fighting;
            bool walking = Data.phase == HuntPhase.Walking;
            string pose = walking ? "walk" : "idle";
            string dragonPose = "idle";
            float fx = 796, ex = 938, fy = 128, ey = 126, fr = 0, er = 0;
            float beat = Mathf.Repeat(20 - Data.phaseRemaining, 2.4f);
            bool hit = false;
            bool powered = false;
            if (fighting)
            {
                int cycle = (int)((20 - Data.phaseRemaining) / 2.4f);
                bool special = cycle % 3 == 2;
                if (beat < 1.05f)
                {
                    pose = cycle % 2 == 0 ? "punch" : "kick";
                    if (special && Data.trainingRewardsClaimed[4]) pose = "takedown";
                    else if (special && Data.trainingRewardsClaimed[3]) pose = "clinch";
                    else if (Data.trainingRewardsClaimed[1] && cycle % 2 == 0) pose = "punch";
                    powered = pose == "punch" && Data.trainingRewardsClaimed[1];
                    float strike = Mathf.Sin(Mathf.Clamp01(beat / 1.05f) * Mathf.PI);
                    fx += strike * (powered ? (Data.trainingRewardsClaimed[2] ? 102 : 88) : 68);
                    dragonPose = strike > 0.7f ? "hurt" : "idle";
                    ex += strike * 14;
                    hit = strike > 0.86f;
                    if (pose == "takedown") { ey += strike * 46; er = strike * 110; ex -= strike * 18; }
                    if (pose == "clinch") { ex -= strike * 34; fr = strike * -12; }
                }
                else if (beat < 2.1f)
                {
                    dragonPose = "attack";
                    float strike = Mathf.Sin((beat - 1.05f) / 1.05f * Mathf.PI);
                    ex -= strike * 56;
                    fx -= strike * 13;
                    if (strike > 0.7f) pose = "hurt";
                    hit = strike > 0.87f;
                }
            }
            else if (Data.phase == HuntPhase.Result)
            {
                pose = "victory";
                dragonPose = "hurt";
                float p = 1 - Mathf.Clamp01(Data.phaseRemaining / 2f);
                er = -85;
                if (Data.lastWasExtortion)
                {
                    ex += Mathf.Max(0, p - 0.45f) * 1300;
                    er = p < 0.5f ? -85 : 0;
                    dragonPose = p < 0.5f ? "hurt" : "attack";
                }
                else
                {
                    ex = Mathf.Lerp(938, 518, Mathf.Clamp01((p - 0.3f) / 0.7f));
                    ey += Mathf.Sin(p * Mathf.PI) * 80;
                }
            }
            else if (Data.phase == HuntPhase.Recovery)
            {
                pose = "hurt";
                float p = Mathf.Clamp01((10 - Data.phaseRemaining) / 0.7f);
                fx -= p * 100; fr = 80 * p; fy -= p * 20;
                ex += Mathf.Max(0, 9 - Data.phaseRemaining) * 200;
            }
            fighter.sprite = Sprite($"fighter_{pose}_{frame}");
            enemy.gameObject.SetActive(!walking && Data.currentEnemy >= 0);
            enemyLabel.gameObject.SetActive(!walking && Data.currentEnemy >= 0 && Data.phase != HuntPhase.Recovery);
            enemyLabel.transform.parent.gameObject.SetActive(!walking && Data.currentEnemy >= 0 && Data.phase != HuntPhase.Recovery);
            if (Data.currentEnemy >= 0) enemy.sprite = Sprite($"dragon_{Data.currentEnemy}_{dragonPose}_{frame}");
            Place(fighter.rectTransform, fx, fy + (walking ? frame % 2 * 2 : 0));
            Place(enemy.rectTransform, ex, ey + (fighting ? Mathf.Sin(time * 8) * 2 : 0));
            fighter.rectTransform.localRotation = Quaternion.Euler(0, 0, fr);
            enemy.rectTransform.localRotation = Quaternion.Euler(0, 0, er);
            impact.gameObject.SetActive(hit || time < cheerTime);
            impact.color = time < cheerTime ? Mint : powered ? Red : Gold;
            Place(impact.rectTransform, beat < 1.05f ? 922 : 866, 166);
            impact.rectTransform.localScale = Vector3.one * (time < cheerTime ? 1.3f : powered ? (Data.trainingRewardsClaimed[2] ? 1.8f : 1.5f) : 1);
            if (fighting)
            {
                string action = pose == "takedown" ? "Giant Takedown" : pose == "clinch" ? "Dragon Clinch" :
                    powered ? (Data.trainingRewardsClaimed[2] ? "Power Punch II" : "Power Punch") : pose == "kick" ? "킥" : pose == "hurt" ? "가드" : "잽";
                skill.text = action + $"   응원 +{Data.cheerBonus:0.0}%p";
            }
            if (walking) walkOffset = Mathf.Repeat(walkOffset + dt * 22, 384);
            for (int i = 0; i < terrain.Count; i++) Place(terrain[i].rectTransform, i * 384 - 384 - walkOffset, 56);
            Place(cartRoot, 506, 114 + (walking ? frame % 2 * 2 : 0));
            if (returnTime > 0)
            {
                returnTime -= dt;
                Place(departure, Mathf.Lerp(-280, 506, returnTime / 3), 114 + frame % 2 * 2);
                helper.sprite = Sprite($"fighter_walk_{frame}");
                helper.rectTransform.localScale = new Vector3(-1, 1, 1);
                if (returnTime <= 0) departure.gameObject.SetActive(false);
            }
            for (int i = 0; i < roamers.Count; i++)
            {
                roamers[i].sprite = Sprite($"dragon_{roamerKinds[i]}_idle_{(frame + i) % 4}");
                Place(roamers[i].rectTransform, 16 + i * 104 + Mathf.Sin(time * 0.6f + i) * 12, 2 + Mathf.Sin(time * 3 + i) * 2);
            }
            if (trainee != null)
            {
                int kind = (int)Data.training[0].Kind;
                bool punch = Mathf.Repeat(time, 2.4f) < 1.4f;
                trainee.sprite = Sprite($"dragon_{kind}_{(punch ? "attack" : "idle")}_{frame}");
                Place(trainee.rectTransform, 370 + (punch ? Mathf.Sin(time * 10) * 5 : 0), 64);
                if (trainingBag != null) trainingBag.rectTransform.localRotation = Quaternion.Euler(0, 0, punch ? Mathf.Sin(time * 10) * 7 : 0);
            }
        }

        public void AnimateReturn(List<DragonInstance> dragons)
        {
            for (int i = 0; i < 3; i++)
            {
                departingCargo[i].gameObject.SetActive(i < dragons.Count);
                if (i < dragons.Count) departingCargo[i].sprite = Sprite("dragon_" + dragons[i].kind);
            }
            returnTime = 3;
            departure.gameObject.SetActive(true);
        }
        public void CheerPulse() { cheerTime = Time.unscaledTime + 0.17f; }
        public void Toast(string message)
        {
            toast.text = message;
            toastUntil = Time.unscaledTime + 3.5f;
            toastBox.gameObject.SetActive(true);
        }

        private void BuildPanel()
        {
            panelDirty = false;
            for (int i = content.childCount - 1; i >= 0; i--)
            {
                var child = content.GetChild(i).gameObject;
                child.SetActive(false);
                UnityEngine.Object.DestroyImmediate(child);
            }
            timerLabels.Clear(); timerSlots.Clear(); roamers.Clear(); roamerKinds.Clear();
            trainee = null; trainingBag = null; trainingProgress = null; trainingProgressText = null;
            switch (ActivePanel)
            {
                case "storage": BuildStorage(); break;
                case "training": BuildTraining(); break;
                case "market": BuildMarket(); break;
                case "skills": BuildSkills(); break;
                case "exit": BuildExit(); break;
                default: BuildSettings(); break;
            }
        }

        private DragonInstance Selected()
        {
            var result = Data.storage.Find(d => d.id == selectedId);
            if (result == null && Data.storage.Count > 0)
            {
                result = Data.storage[Data.storage.Count - 1];
                selectedId = result.id;
            }
            return result;
        }
        private void BuildStorage()
        {
            DragonInstance selected = Selected();
            int pages = Math.Max(1, (Data.storage.Count + 9) / 10);
            page = Mathf.Clamp(page, 0, pages - 1);
            Label("Storage heading", content, $"용 수용소   /   {Data.storage.Count}마리 보유", 21, Cream, 0, 226, 720, 32);
            Label("Storage limits", content, "보관 무제한 · 최근 포획순 · 그래픽은 최대 10마리", 14, Muted, 0, 201, 900, 25);
            for (int i = 0; i < 10; i++)
            {
                int index = Data.storage.Count - 1 - (page * 10 + i);
                if (index < 0) break;
                DragonInstance dragon = Data.storage[index];
                var spec = DragonCatalog.Get(dragon.Kind);
                int col = i % 5, row = i / 5;
                var button = Button("Dragon " + dragon.id, content, "", col * 212, 130 - row * 78, 202, 70,
                    () => { selectedId = dragon.id; panelDirty = true; }, dragon.id == selectedId ? Hex("436C54") : Ink);
                Picture("Portrait", button.transform, "dragon_" + dragon.kind, 3, 5, 68, 54);
                Label("Name", button.transform, spec.displayName, 14, SpeciesColor(dragon.kind), 72, 35, 125, 28);
                Label("Individual state", button.transform, dragon.coreRewardClaimed ? "수련 완료 개체" : "미수련 · " + spec.salePrice + "G", 12, Muted, 72, 8, 125, 24);
            }
            if (Data.storage.Count == 0)
                Label("Empty storage", content, "아직 조용한 수용소입니다.\n용을 포획한 뒤 ‘수레 귀환’을 눌러주세요.", 20, Muted, 20, 78, 930, 104);
            for (int i = 0; i < Math.Min(10, Data.storage.Count); i++)
            {
                int kind = Data.storage[Data.storage.Count - 1 - i].kind;
                roamers.Add(Picture("Recent roaming dragon", content, "dragon_" + kind, 16 + i * 104, 2, 64, 48));
                roamerKinds.Add(kind);
            }
            Button("Previous page", content, "←", 800, 218, 64, 36, () => { page--; panelDirty = true; }, Panel, Cream, page > 0);
            Label("Page", content, $"{page + 1} / {pages}", 14, Muted, 870, 220, 96, 32, TextAnchor.MiddleCenter);
            Button("Next page", content, "→", 974, 218, 64, 36, () => { page++; panelDirty = true; }, Panel, Cream, page + 1 < pages);
            var detail = Card("Selected dragon", content, 1092, 4, 728, 250);
            if (selected == null)
            {
                Label("Selection prompt", detail, "수레의 용들을 데려오세요", 23, Cream, 26, 162, 680, 54);
                Label("Economy primer", detail, "두 마리 판매 → 80G → 숲 수련방 개방\n남겨 둔 용과 수련하면 영구 전투력이 성장합니다.", 17, Muted, 26, 62, 670, 96);
                return;
            }
            var chosen = selected;
            var info = DragonCatalog.Get(chosen.Kind);
            Label("Detail name", detail, info.displayName, 24, SpeciesColor(chosen.kind), 26, 193, 664, 42);
            Label("Detail stats", detail, $"전투력 {info.power}   ·   판매 {info.salePrice}G / {Duration(info.saleSeconds)}\n수련 {Duration(info.trainingSeconds)}   ·   용핵 +{(chosen.coreRewardClaimed ? 0 : info.coreReward)}", 17, Muted, 26, 117, 675, 72);
            Button("Assign training", detail, Data.trainingUnlocked ? "수련방 배치 / 교체" : "수련방 잠김 · 80G", 26, 65, 320, 42,
                () => { game.AssignTraining(chosen.id, DragonMmaGame.Now); OpenPanel("training"); }, Gold, Ink, Data.trainingUnlocked);
            Button("Register sale", detail, "판매소 등록", 364, 65, 336, 42,
                () => { int slot = Array.FindIndex(Data.sales, s => s.IsEmpty); if (slot >= 0) game.RegisterSale(chosen.id, slot, DragonMmaGame.Now); else Toast("판매대 두 칸이 모두 사용 중입니다."); }, Panel, Cream);
            Button("Display room locked", detail, "관상용 방  ·  이후 업데이트", 26, 13, 674, 38, null, Panel, Muted, false);
        }

        private void BuildTraining()
        {
            var slot = Data.training[0];
            var scene = Card("Dojo scene", content, 0, 4, 520, 250);
            Picture("Dojo", scene, "hut", 16, 14, 288, 216);
            trainingBag = Picture("Punching bag", scene, "bag", 340, 54, 72, 120);
            if (!slot.IsEmpty)
            {
                trainee = Picture("Trainee", scene, "dragon_" + (int)slot.Kind, 210, 64, 160, 120);
                trainee.rectTransform.localScale = new Vector3(-1, 1, 1);
                Label("Practice caption", scene, slot.completed ? "계속 수련 중 · 보상 수령 완료" : "힘을 모으는 중", 16, Gold, 14, 8, 492, 36, TextAnchor.MiddleCenter);
            }
            else Label("Dojo caption", scene, Data.trainingUnlocked ? "수련 파트너를 기다립니다" : "아직 잠겨 있는 숲 수련방", 16, Gold, 14, 8, 492, 36, TextAnchor.MiddleCenter);
            Label("Training title", content, "숲 수련방  /  힘 계열", 24, Cream, 566, 211, 800, 42);
            if (!Data.trainingUnlocked)
            {
                Label("Unlock description", content, "용 판매금으로 수련방을 열어주세요.\n첫 수련방은 한 마리 배치 · 용은 소모되지 않습니다.", 19, Muted, 566, 111, 1140, 88);
                Button("Unlock training", content, $"80G로 개방   /   보유 {Data.coins}G", 566, 30, 452, 56,
                    () => game.UnlockTraining(), Gold, Ink, Data.coins >= 80);
                return;
            }
            if (slot.IsEmpty)
            {
                Label("Training empty", content, "수용소에서 용을 선택해 수련방에 배치하세요.\n종별 첫 수련은 전투력과 기술, 각 개체는 용핵을 한 번 지급합니다.", 18, Muted, 566, 111, 1218, 80);
                Button("Go to storage", content, "수용소에서 용 선택", 566, 38, 416, 52, () => OpenPanel("storage"), Gold, Ink);
                return;
            }
            var spec = DragonCatalog.Get(slot.Kind);
            Label("Partner", content, spec.displayName + "  /  " + spec.skillName, 20, SpeciesColor((int)slot.Kind), 566, 166, 1190, 40);
            trainingProgress = Bar(content, 566, 130, 1014, 14, Mint);
            trainingProgressText = Label("Core charge", content, "", 16, Gold, 566, 87, 1220, 34);
            Button("Recall trainee", content, "수용소로 회수", 566, 21, 306, 46, () => game.RecallTraining());
            Button("Replace trainee", content, "파트너 교체", 894, 21, 306, 46, () => OpenPanel("storage"));
            Label("One-time rule", content, "완료 후에도 훈련 연출 유지 · 반복 보상 없음", 15, Muted, 1224, 21, 590, 46);
        }

        private void BuildMarket()
        {
            Label("Market heading", content, "숲 교역소", 24, Cream, 0, 218, 600, 40);
            Label("Market note", content, "판매 완료 후 직접 수령  ·  접속하지 않아도 등록한 타이머만 진행", 16, Muted, 240, 220, 1410, 36);
            for (int i = 0; i < 2; i++)
            {
                int index = i;
                TimedDragonSlot slot = Data.sales[i];
                var card = Card("Market slot " + i, content, i * 660, 8, 638, 194);
                Label("Slot number", card, "SALES  /  0" + (i + 1), 14, Gold, 22, 150, 580, 30);
                if (slot.IsEmpty)
                {
                    Label("Empty listing", card, "비어 있는 판매대", 22, Cream, 22, 98, 590, 40);
                    Button("Choose seller", card, "수용소에서 용 선택", 22, 20, 590, 54, () => OpenPanel("storage"));
                }
                else
                {
                    var spec = DragonCatalog.Get(slot.Kind);
                    Picture("Selling dragon", card, "dragon_" + (int)slot.Kind, 20, 40, 140, 105);
                    Label("Listing name", card, spec.displayName + "   " + spec.salePrice + "G", 21, SpeciesColor((int)slot.Kind), 176, 102, 436, 40);
                    var timer = Label("Sale clock", card, "", 15, Muted, 176, 66, 436, 30);
                    timerSlots.Add(slot); timerLabels.Add(timer);
                    Button("Claim sale " + i, card, slot.completed ? "판매금 수령 +" + spec.salePrice + "G" : "구매자를 기다리는 중", 176, 16, 436, 44,
                        () => game.ClaimSale(index), slot.completed ? Gold : Panel, slot.completed ? Ink : Muted, slot.completed);
                }
            }
            Label("Trading note", content, "판매는 두 칸이\n각각 진행됩니다.\n\n전체 수령 / 자동 수확은\n이후 업데이트 예정", 18, Muted, 1358, 22, 438, 173);
        }

        private void BuildSkills()
        {
            Label("Collection heading", content, "THE FOREST  /  수련 기술 도감", 22, Cream, 0, 226, 1800, 32);
            for (int i = 0; i < 5; i++)
            {
                var spec = DragonCatalog.Get((DragonKind)i);
                bool unlocked = Data.trainingRewardsClaimed[i];
                var card = Card("Skill " + i, content, i * 370, 12, 354, 202);
                Box("Species stripe", card, SpeciesColor(i), 0, 198, 354, 4);
                var portrait = Picture("Skill dragon", card, "dragon_" + i, 17, 104, 106, 80);
                portrait.color = Data.discovered[i] ? Color.white : Hex("52655A");
                Label("Species", card, Data.discovered[i] ? spec.displayName : "미발견", 20, SpeciesColor(i), 130, 143, 212, 34);
                Label("Power reward", card, "최초 수련  힘 +" + spec.powerReward, 14, Muted, 130, 113, 212, 28);
                Label("Technique name", card, spec.skillName, 20, Cream, 18, 63, 320, 36);
                Label("Unlock state", card, unlocked ? "● 해금 완료 · 전투 연출 적용" : "○ 이 종을 포획하고 수련", 15, unlocked ? Mint : Muted, 18, 22, 320, 32);
            }
        }

        private void BuildSettings()
        {
            Label("Settings title", content, "데스크톱 오버레이", 24, Cream, 8, 209, 1760, 42);
            Label("Settings detail", content, "바탕화면 하단 고정 · 투명 영역 클릭 통과 · 전투와 거점은 동시에 진행\n응원: 창이 활성화된 상태에서 SPACE 또는 전투 영역 클릭 / 터치\n오프라인: 판매·수련만 완료되며 사냥이나 자동 포획은 진행되지 않습니다.", 19, Muted, 8, 92, 1800, 112);
            Button("Toggle topmost", content, "항상 위 표시: " + (Data.overlayAlwaysOnTop ? "켜짐" : "꺼짐"), 8, 21, 404, 48,
                () => owner.SetAlwaysOnTop(!Data.overlayAlwaysOnTop), Gold, Ink);
            Button("Save now", content, "지금 저장", 434, 21, 250, 48, () => { owner.Save(); Toast("진행 상황을 저장했습니다."); });
            Label("Save safety", content, "주요 이벤트 자동 저장 · 저장 백업 유지", 16, Muted, 712, 21, 850, 48);
        }
        private void BuildExit()
        {
            Label("Exit title", content, "숲에서 잠시 쉬어갈까요?", 28, Cream, 24, 180, 1500, 56);
            Label("Exit note", content, "진행 상황을 저장하고 종료합니다. 등록한 판매와 수련 타이머만 계속됩니다.", 20, Muted, 24, 108, 1740, 60);
            Button("Confirm quit", content, "저장하고 종료", 24, 24, 410, 58, owner.Quit, Gold, Ink);
            Button("Cancel quit", content, "계속 관찰하기", 458, 24, 410, 58, () => OpenPanel(""));
        }

        private void RefreshTimers()
        {
            long now = DragonMmaGame.Now;
            for (int i = 0; i < timerLabels.Count; i++)
                timerLabels[i].text = timerSlots[i].completed ? "판매 완료 · 수령 대기" : "남은 시간  " + Duration(Math.Max(0, timerSlots[i].endUtc - now));
            if (trainingProgress != null)
            {
                var slot = Data.training[0];
                float ratio = slot.completed ? 1 : Mathf.Clamp01(1 - (float)Math.Max(0, slot.endUtc - now) / Math.Max(1, slot.endUtc - slot.startUtc));
                SetFill(trainingProgress, ratio);
                trainingProgressText.text = slot.completed ? "용핵 보상 확정 · 이 개체의 반복 보상은 없습니다." :
                    $"용핵 충전 {ratio:P0}   ·   남은 시간 {Duration(Math.Max(0, slot.endUtc - now))}";
            }
        }

        private void UpdateRegions()
        {
            inputRects.Clear();
            foreach (var rect in interactive) AddRegion(rect);
            if (ActivePanel.Length > 0) AddRegion(baseRoot);
            window?.SetInteractiveRegions(inputRects);
        }
        private void AddRegion(RectTransform rect)
        {
            rect.GetWorldCorners(corners);
            inputRects.Add(Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y));
        }
        public bool IsBattlePoint(Vector2 screenPoint) =>
            RectTransformUtility.RectangleContainsScreenPoint(battleArea, screenPoint, null);
        public void Dispose()
        {
            if (canvas != null) UnityEngine.Object.Destroy(canvas.gameObject);
            if (font != null && font.name != "LegacyRuntime") UnityEngine.Object.Destroy(font);
        }

        private Sprite Sprite(string name)
        {
            if (!sprites.TryGetValue(name, out var sprite))
            {
                sprite = DragonArtCatalog.Load(name);
                if (sprite == null && name.StartsWith("dragon_"))
                    sprite = DragonArtCatalog.Load(name.Substring(0, 8));
                if (sprite == null && name.StartsWith("fighter_"))
                    sprite = DragonArtCatalog.Load("fighter_idle_0");
                sprites[name] = sprite;
            }
            return sprite;
        }
        private RectTransform Card(string name, Transform parent, float x, float y, float w, float h)
        {
            var rect = Box(name, parent, Panel, x, y, w, h).rectTransform;
            Box("Top edge", rect, Edge, 0, h - 1, w, 1);
            return rect;
        }
        private RectTransform Group(string name, Transform parent, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(x, y); rect.sizeDelta = new Vector2(w, h);
            return rect;
        }
        private Image Box(string name, Transform parent, Color color, float x, float y, float w, float h)
        {
            var rect = Group(name, parent, x, y, w, h);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color; image.raycastTarget = false;
            return image;
        }
        private Image Picture(string name, Transform parent, string sprite, float x, float y, float w, float h)
        {
            var image = Box(name, parent, Color.white, x, y, w, h);
            image.sprite = Sprite(sprite); image.preserveAspect = true;
            return image;
        }
        private Text Label(string name, Transform parent, string text, int size, Color color, float x, float y, float w, float h, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var rect = Group(name, parent, x, y, w, h);
            var label = rect.gameObject.AddComponent<Text>();
            label.font = font; label.fontSize = size; label.color = color; label.text = text;
            label.alignment = align; label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }
        private Button Button(string name, Transform parent, string text, float x, float y, float w, float h, Action action, Color? bg = null, Color? fg = null, bool enabled = true)
        {
            var image = Box(name, parent, bg ?? Edge, x, y, w, h);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.interactable = enabled;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = button.colors;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1);
            colors.pressedColor = new Color(0.75f, 0.85f, 0.8f, 1);
            colors.disabledColor = new Color(0.5f, 0.55f, 0.5f, 0.7f);
            button.colors = colors;
            Label("Label", image.transform, text, 16, fg ?? Cream, 8, 0, w - 16, h, TextAnchor.MiddleCenter);
            if (action != null) button.onClick.AddListener(() => { action(); DragonMmaGame.ClearSelection(); });
            return button;
        }
        private Image Bar(Transform parent, float x, float y, float w, float h, Color color)
        {
            var back = Box("Progress track", parent, Ink, x, y, w, h);
            var fill = Box("Progress fill", back.transform, color, 0, 0, w, h);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
            return fill;
        }
        private static void SetFill(Image image, float amount) => image.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(amount), 1);
        private static void Place(RectTransform rect, float x, float y) => rect.anchoredPosition = new Vector2(Mathf.Round(x), Mathf.Round(y));
        private static Color Hex(string hex) { ColorUtility.TryParseHtmlString("#" + hex, out var c); return c; }
        public static Color SpeciesColor(int kind) => kind == 0 ? Cream : kind == 1 ? Hex("BFE2A0") : kind == 2 ? Hex("73BA83") : kind == 3 ? Hex("EEB36B") : Hex("F38472");
        public static string Duration(float seconds)
        {
            int n = Mathf.CeilToInt(Mathf.Max(0, seconds));
            return n >= 60 ? $"{n / 60}:{n % 60:00}" : n + "초";
        }
    }
}

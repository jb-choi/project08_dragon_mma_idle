using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DragonMMA
{
    // All visual objects are authored in the scene/prefabs. Runtime changes only game state,
    // visibility, progress text and additive motion relative to each authored transform.
    [DisallowMultipleComponent, ExecuteAlways]
    public sealed class DragonMmaView : MonoBehaviour
    {
        [Header("Scene and page references · no runtime layout generation")]
        [SerializeField] private DragonUiBindings overlay;
        [SerializeField] private DragonUiBindings storage, trainingLocked, trainingEmpty, trainingActive, market, skills, settings, quit;
        [SerializeField] private DragonActorView hunter, dragon, helper, trainee;
        [SerializeField] private Sprite[] dragonPortraits;
        [Header("Responsive canvas · child RectTransforms stay authored")]
        [SerializeField, Min(320)] private float referenceWidth = 1920;
        [SerializeField, Min(120)] private float huntReferenceHeight = 360;
        [SerializeField, Min(240)] private float expandedReferenceHeight = 720;
        [Header("Presentation motion · relative to placed actors")]
        [SerializeField] private float jabTravel = 68, powerPunchTravel = 88, upgradedPunchTravel = 102;
        [SerializeField] private float cartReturnSeconds = 3, scrollSpeed = 22, terrainTileWidth = 384;
        [SerializeField] private Color selectedCardTint = new Color(.26f, .42f, .33f);
        [Header("Fighter contact feedback · optional, respects AudioListener mute")]
        [SerializeField] private AudioSource combatAudio;
        [SerializeField] private AudioClip jabSound, heavySound, guardSound, knockdownSound;
        [Header("Combat direction layers · authored in overlay prefabs")]
        [SerializeField] private GameObject[] combatFocusObjects;
        [SerializeField] private Text combatBeatLabel;
        [SerializeField] private Image impactRing, impactEcho, impactDust;
        [SerializeField] private Sprite weakImpactSprite, heavyImpactSprite, guardImpactSprite, knockdownImpactSprite;
        [SerializeField] private Image[] speedLines;
        [Header("Dynamic text formats · static labels are edited directly on Text objects")]
        [SerializeField] private string walletFormat = "G  {0:N0}     ◆  {1:N0}     전투력  {2}";
        [SerializeField] private string capacityFormat = "포획  {0} / {1}";
        [SerializeField] private string cheerHintFormat = "전투 영역 클릭 / SPACE  ·  +{0:0.##}%p  ·  최대 +{1:0.##}%p";
        private DragonMmaGame owner;
        private DragonGameSession game;
        private DesktopOverlayWindow window;
        private CanvasScaler scaler;
        private RectTransform baseRoot, battleArea, cartRoot, departure, notification;
        private Vector2 cartPosition, departurePosition, notificationPosition;
        private Image impact;
        private Text fighterStateText, dragonStateText;
        private Vector3 impactScale;
        private Vector2 impactPosition;
        private Color impactColor;
        private Color fighterLabelColor, dragonLabelColor;
        private readonly FighterContactGate contactGate = new FighterContactGate();
        private readonly CombatSpritePreloader combatSprites = new CombatSpritePreloader();
        private HuntPhase previousPresentationPhase;
        private int previousPresentationEnemy = -1;
        private float previousBattleElapsed = -1;
        private RectTransform[] ground;
        private Vector2[] groundPositions, roamerPositions;
        private Image[] cargo, departingCargo, roamers;
        private Color[] cardColors;
        private readonly List<RectTransform> hitTargets = new List<RectTransform>();
        private readonly List<Rect> hitRects = new List<Rect>();
        private readonly Vector3[] corners = new Vector3[4];
        private readonly List<Action> unbind = new List<Action>();
        private string selectedId = "";
        private int page;
        private float nextRefresh, toastUntil, returnTime, cheerUntil, scroll, knockdownUntil;
        private bool knockdownOnDragon;
        public string ActivePanel { get; private set; } = "";
        public Canvas Canvas => GetComponent<Canvas>();
        public DragonGameSession Session => game;
        private GameSaveData Data => game.Data;
        private DragonRuntimeConfig Rules => game.Config;
        private DragonUiBindings[] cachedPages;
        private DragonUiBindings[] Pages => cachedPages ?? (cachedPages = new[] { storage, trainingLocked, trainingEmpty, trainingActive, market, skills, settings, quit });

        private void Update()
        {
            if (!Application.isPlaying && overlay != null)
            {
                if (scaler == null) scaler = GetComponent<CanvasScaler>();
                bool expanded = overlay.Get<Transform>("BaseUI").gameObject.activeSelf;
                scaler.scaleFactor = Mathf.Max(.1f, Mathf.Min(Screen.width / referenceWidth, Screen.height / (expanded ? expandedReferenceHeight : huntReferenceHeight)));
            }
        }

        public void Initialize(DragonMmaGame gameOwner, DragonGameSession session, DesktopOverlayWindow overlayWindow)
        {
            Dispose();
            if (overlay == null || storage == null || hunter == null || dragon == null)
                throw new InvalidOperationException("DragonMmaView is missing its authored scene references. Open DesktopOverlayScene; do not use the old bootstrap-only scene.");
            owner = gameOwner; game = session; window = overlayWindow;
            combatSprites.Preload();
            scaler = GetComponent<CanvasScaler>();
            baseRoot = overlay.Get<RectTransform>("BaseUI");
            battleArea = overlay.Get<RectTransform>("Hunt/Battle input patch");
            cartRoot = overlay.Get<RectTransform>("Hunt/PrisonCart"); cartPosition = cartRoot.anchoredPosition;
            departure = overlay.Get<RectTransform>("Hunt/Departing cart"); departurePosition = departure.anchoredPosition;
            notification = overlay.Get<RectTransform>("Hunt/Notification"); notificationPosition = notification.anchoredPosition;
            impact = overlay.Get<Image>("Hunt/Contact sparks"); impactScale = impact.rectTransform.localScale;
            impactPosition = impact.rectTransform.anchoredPosition; impactColor = impact.color;
            fighterStateText = overlay.Get<Text>("Hunt/Hunter name plate/Fighter name");
            dragonStateText = overlay.Get<Text>("Hunt/Dragon name plate/Dragon card");
            fighterLabelColor = fighterStateText.color; dragonLabelColor = dragonStateText.color;
            contactGate.Reset(); previousPresentationPhase = Data.phase;
            previousPresentationEnemy = -1; previousBattleElapsed = -1; knockdownUntil = 0;
            hunter.Initialize(); dragon.Initialize(); helper.Initialize(); trainee.Initialize();
            cargo = new Image[3]; departingCargo = new Image[3]; roamers = new Image[10]; roamerPositions = new Vector2[10]; cardColors = new Color[10];
            for (int i = 0; i < 3; i++)
            {
                cargo[i] = overlay.Get<Image>("Hunt/PrisonCart/Passenger" + i);
                departingCargo[i] = overlay.Get<Image>("Hunt/Departing cart/Departing passenger" + i);
            }
            ground = new RectTransform[7]; groundPositions = new Vector2[7];
            for (int i = 0; i < ground.Length; i++) { ground[i] = overlay.Get<RectTransform>("Hunt/Forest tile" + i); groundPositions[i] = ground[i].anchoredPosition; }
            for (int i = 0; i < 10; i++)
            {
                int index = i;
                cardColors[i] = storage.Get<Image>("StorageCard" + i).color;
                roamers[i] = storage.Get<Image>("Roamer" + i); roamerPositions[i] = roamers[i].rectTransform.anchoredPosition;
                Bind(storage, "StorageCard" + i, () => { int n = Data.storage.Count - 1 - page * 10 - index; if (n >= 0) selectedId = Data.storage[n].id; Refresh(true); });
            }
            Bind(overlay, "Hunt/Bottom toolbar/Camp", () => OpenPanel(ActivePanel == "" ? "storage" : ""));
            Bind(overlay, "Hunt/Bottom toolbar/Training", () => Toggle("training"));
            Bind(overlay, "Hunt/Bottom toolbar/Market", () => Toggle("market"));
            Bind(overlay, "Hunt/Bottom toolbar/Skills", () => Toggle("skills"));
            Bind(overlay, "Hunt/Bottom toolbar/Return", owner.ReturnCart);
            Bind(overlay, "Hunt/Bottom toolbar/Settings fixed tab", () => Toggle("settings"));
            Bind(overlay, "Hunt/Bottom toolbar/Exit", () => OpenPanel("exit"));
            foreach (string tab in new[] { "storage", "training", "market", "skills" }) { string name = tab; Bind(overlay, "BaseUI/Tab " + name, () => OpenPanel(name)); }
            Bind(overlay, "BaseUI/Close base", () => OpenPanel(""));
            Bind(storage, "Previous page", () => { page = Mathf.Max(0, page - 1); Refresh(true); });
            Bind(storage, "Next page", () => { page++; Refresh(true); });
            Bind(storage, "Selected dragon/Assign training", () => { var d = Selected(); if (d != null && game.AssignTraining(d.id, DragonMmaGame.Now)) OpenPanel("training"); });
            Bind(storage, "Selected dragon/Register sale", () => { var d = Selected(); int slot = Array.FindIndex(Data.sales, s => s.IsEmpty); if (d != null && slot >= 0) game.RegisterSale(d.id, slot, DragonMmaGame.Now); else Toast("판매대가 가득 찼습니다."); });
            Bind(trainingLocked, "Unlock training", () => game.UnlockTraining());
            Bind(trainingEmpty, "Go to storage", () => OpenPanel("storage"));
            Bind(trainingActive, "Recall trainee", () => { game.ProcessTimers(DragonMmaGame.Now); game.RecallTraining(); });
            Bind(trainingActive, "Replace trainee", () => OpenPanel("storage"));
            for (int i = 0; i < 2; i++)
            {
                int index = i;
                Bind(market, "Market slot " + i + "/Claim sale " + i, () => game.ClaimSale(index));
                Bind(market, "Market slot " + i + "/Choose seller", () => OpenPanel("storage"));
            }
            Bind(settings, "Toggle topmost", () => owner.SetAlwaysOnTop(!Data.overlayAlwaysOnTop));
            Bind(settings, "Save now", () => { owner.Save(); Toast("진행 상황을 저장했습니다."); });
            Bind(quit, "Confirm quit", owner.Quit); Bind(quit, "Cancel quit", () => OpenPanel(""));
            hitTargets.Clear();
            foreach (string key in new[] { "Hunt/Field journal", "Hunt/Battle status", "Hunt/Collection objective", "Hunt/Bottom toolbar", "Hunt/Battle input patch", "Hunt/PrisonCart" })
                hitTargets.Add(overlay.Get<RectTransform>(key));
            departure.gameObject.SetActive(false); overlay.Show("Hunt/Notification", false);
            OpenPanel("");
        }
        private void Bind(DragonUiBindings map, string key, Action action)
        {
            var button = map.Get<Button>(key);
            UnityEngine.Events.UnityAction listener = () => { action(); DragonMmaGame.ClearSelection(); };
            button.onClick.AddListener(listener);
            unbind.Add(() => { if (button != null) button.onClick.RemoveListener(listener); });
        }
        public void Dispose()
        {
            foreach (var remove in unbind) remove();
            unbind.Clear();
            combatSprites.Clear();
        }
        public void OpenPanel(string name)
        {
            ActivePanel = name; baseRoot.gameObject.SetActive(name.Length > 0);
            window?.SetExpanded(name.Length > 0);
            UpdateVisiblePage(); Refresh(true); DragonMmaGame.ClearSelection();
        }
        private void Toggle(string name) => OpenPanel(ActivePanel == name ? "" : name);
        private void UpdateVisiblePage()
        {
            DragonUiBindings show = ActivePanel == "storage" ? storage : ActivePanel == "market" ? market :
                ActivePanel == "skills" ? skills : ActivePanel == "settings" ? settings : ActivePanel == "exit" ? quit :
                ActivePanel == "training" ? (!Data.trainingUnlocked ? trainingLocked : Data.training[0].IsEmpty ? trainingEmpty : trainingActive) : null;
            // Do not disable/re-enable the visible page on every data refresh: doing so
            // resets its Animator and prevents a retained trainee from playing full clips.
            foreach (var p in Pages) p.gameObject.SetActive(p == show);
        }
        public void Refresh(bool structural = false)
        {
            if (game == null) return;
            Set(overlay, "Hunt/Field journal/Wallet", string.Format(walletFormat, Data.coins, Data.cores, Data.fighterPower));
            Set(overlay, "Hunt/Bottom toolbar/Capacity", string.Format(capacityFormat, Data.cart.Count, Rules.CartCapacity));
            Set(overlay, "Hunt/Bottom toolbar/Input hint", string.Format(cheerHintFormat, Rules.CheerPerInput, Rules.MaxCheerBonus));
            long nowUtc = DragonMmaGame.Now;
            Set(overlay, "Hunt/Bottom toolbar/Camp/Label", DragonUiStatusLabels.Camp(Data));
            Set(overlay, "Hunt/Bottom toolbar/Training/Label", DragonUiStatusLabels.Training(Data, nowUtc));
            Set(overlay, "Hunt/Bottom toolbar/Market/Label", DragonUiStatusLabels.Market(Data, nowUtc));
            Set(overlay, "Hunt/Bottom toolbar/Skills/Label", DragonUiStatusLabels.Skills(Data));
            overlay.Get<Button>("Hunt/Bottom toolbar/Return").interactable = Data.cart.Count > 0;
            Set(overlay, "Hunt/Hunter name plate/Fighter name", "MMA 파이터  ·  힘 " + Data.fighterPower);
            int mastered = 0; for (int i = 0; i < 5; i++) if (Data.trainingRewardsClaimed[i]) mastered++;
            Set(overlay, "Hunt/Collection objective/Next goal", $"수련 도감 {mastered}/5   →   {game.GetSpec(DragonKind.Giant).skillName}");
            for (int i = 0; i < cargo.Length; i++) { cargo[i].gameObject.SetActive(i < Data.cart.Count); if (i < Data.cart.Count) cargo[i].sprite = Portrait(Data.cart[i].kind); }
            if (Data.currentEnemy >= 0)
            {
                var spec = game.GetSpec((DragonKind)Data.currentEnemy);
                Set(overlay, "Hunt/Dragon name plate/Dragon card", spec.displayName + "  ·  힘 " + spec.power);
            }
            UpdateVisiblePage();
            if (ActivePanel == "storage") RefreshStorage();
            else if (ActivePanel == "training") RefreshTraining();
            else if (ActivePanel == "market") RefreshMarket();
            else if (ActivePanel == "skills") RefreshSkills();
            else if (ActivePanel == "settings") Set(settings, "Toggle topmost/Label", "항상 위 표시: " + (Data.overlayAlwaysOnTop ? "켜짐" : "꺼짐"));
        }
        private DragonInstance Selected()
        {
            var selected = Data.storage.Find(d => d.id == selectedId);
            if (selected == null && Data.storage.Count > 0) { selected = Data.storage[Data.storage.Count - 1]; selectedId = selected.id; }
            return selected;
        }
        private void RefreshStorage()
        {
            var selected = Selected();
            int pages = Math.Max(1, (Data.storage.Count + 9) / 10); page = Mathf.Clamp(page, 0, pages - 1);
            Set(storage, "Storage heading", $"용 수용소   /   {Data.storage.Count}마리 보유");
            Set(storage, "Page", $"{page + 1} / {pages}");
            storage.Get<Button>("Previous page").interactable = page > 0;
            storage.Get<Button>("Next page").interactable = page + 1 < pages;
            storage.Show("Empty state", selected == null); storage.Show("Selected dragon", selected != null);
            for (int i = 0; i < 10; i++)
            {
                string key = "StorageCard" + i; int index = Data.storage.Count - 1 - page * 10 - i;
                storage.Show(key, index >= 0);
                if (index >= 0)
                {
                    var d = Data.storage[index]; var s = game.GetSpec(d.Kind);
                    storage.Get<Image>(key).color = d.id == selectedId ? selectedCardTint : cardColors[i];
                    storage.Get<Image>(key + "/Portrait").sprite = Portrait(d.kind);
                    Set(storage, key + "/Name", s.displayName);
                    Set(storage, key + "/Individual state", d.coreRewardClaimed ? "수련 완료 개체" : "미수련 · " + s.salePrice + "G");
                }
                int recent = Data.storage.Count - 1 - i;
                roamers[i].gameObject.SetActive(recent >= 0);
                if (recent >= 0) roamers[i].sprite = Portrait(Data.storage[recent].kind);
            }
            if (selected == null) return;
            var spec = game.GetSpec(selected.Kind);
            Set(storage, "Selected dragon/Detail name", spec.displayName);
            Set(storage, "Selected dragon/Detail stats", $"전투력 {spec.power}   ·   판매 {spec.salePrice}G / {Duration(spec.saleSeconds)}\n수련 {Duration(spec.trainingSeconds)}   ·   용핵 +{(selected.coreRewardClaimed ? 0 : spec.coreReward)}");
            storage.Get<Button>("Selected dragon/Assign training").interactable = Data.trainingUnlocked;
            Set(storage, "Selected dragon/Assign training/Label", Data.trainingUnlocked ? "수련방 배치 / 교체" : "수련방 잠김 · " + Rules.TrainingUnlockCost + "G");
        }
        private void RefreshTraining()
        {
            if (!Data.trainingUnlocked)
            {
                Set(trainingLocked, "Unlock training/Label", $"{Rules.TrainingUnlockCost}G로 개방  /  보유 {Data.coins}G");
                trainingLocked.Get<Button>("Unlock training").interactable = Data.coins >= Rules.TrainingUnlockCost;
                return;
            }
            var slot = Data.training[0]; if (slot.IsEmpty) return;
            var spec = game.GetSpec(slot.Kind);
            trainee.SetDragonKind(slot.Kind);
            Set(trainingActive, "Partner", spec.displayName + "  /  " + spec.skillName);
            Set(trainingActive, "Dojo scene/Practice caption", slot.completed ? "계속 수련 중 · 보상 수령 완료" : "힘을 모으는 중");
            float ratio = slot.completed ? 1 : Mathf.Clamp01(1 - (float)Math.Max(0, slot.endUtc - DragonMmaGame.Now) / Math.Max(1, slot.endUtc - slot.startUtc));
            Fill(trainingActive, "Progress track/Progress fill", ratio);
            Set(trainingActive, "Core charge", slot.completed ? "용핵 보상 확정 · 이 개체의 반복 보상은 없습니다." : $"용핵 충전 {ratio:P0}   ·   남은 시간 {Duration(Math.Max(0, slot.endUtc - DragonMmaGame.Now))}");
        }
        private void RefreshMarket()
        {
            for (int i = 0; i < 2; i++)
            {
                string key = "Market slot " + i; var slot = Data.sales[i];
                foreach (string child in new[] { "Selling dragon", "Listing name", "Sale clock", "Claim sale " + i }) market.Show(key + "/" + child, !slot.IsEmpty);
                market.Show(key + "/Empty listing", slot.IsEmpty); market.Show(key + "/Choose seller", slot.IsEmpty);
                if (slot.IsEmpty) continue;
                var spec = game.GetSpec(slot.Kind);
                market.Get<Image>(key + "/Selling dragon").sprite = Portrait((int)slot.Kind);
                Set(market, key + "/Listing name", spec.displayName + "   " + spec.salePrice + "G");
                Set(market, key + "/Sale clock", slot.completed ? "판매 완료 · 수령 대기" : "남은 시간  " + Duration(Math.Max(0, slot.endUtc - DragonMmaGame.Now)));
                Set(market, key + "/Claim sale " + i + "/Label", slot.completed ? "판매금 수령 +" + spec.salePrice + "G" : "구매자를 기다리는 중");
                market.Get<Button>(key + "/Claim sale " + i).interactable = slot.completed;
            }
        }
        private void RefreshSkills()
        {
            for (int i = 0; i < 5; i++)
            {
                string key = "Skill " + i; var spec = game.GetSpec((DragonKind)i);
                Set(skills, key + "/Species", Data.discovered[i] ? spec.displayName : "미발견");
                Set(skills, key + "/Power reward", "최초 수련  힘 +" + spec.powerReward);
                Set(skills, key + "/Technique name", spec.skillName);
                Set(skills, key + "/Unlock state", Data.trainingRewardsClaimed[i] ? "● 해금 완료 · 전투 연출 적용" : "○ 이 종을 포획하고 수련");
            }
        }
        public void Tick(float dt)
        {
            if (game == null) return;
            RefreshLayout(Screen.width, Screen.height);
            float time = Time.unscaledTime;
            if (time >= nextRefresh) { Refresh(); UpdateRegions(); nextRefresh = time + .1f; }
            if (time > toastUntil) overlay.Show("Hunt/Notification", false);
            bool fight = Data.phase == HuntPhase.Fighting;
            float duration = fight ? Rules.BattleSeconds : Data.phase == HuntPhase.Result ? Rules.WinResultSeconds : Data.phase == HuntPhase.Recovery ? Rules.RecoverySeconds : Rules.WalkSeconds;
            Fill(overlay, "Hunt/Battle status/Progress track0/Progress fill", Data.phaseRemaining / duration);
            Fill(overlay, "Hunt/Battle status/Progress track1/Progress fill", Rules.MaxCheerBonus > 0 ? Data.cheerBonus / Rules.MaxCheerBonus : 0);
            Set(overlay, "Hunt/Battle status/Phase", fight ? $"승부까지 {Data.phaseRemaining:0.0}초" : Data.phase == HuntPhase.Walking ? $"숲 탐색 · 조우까지 {Data.phaseRemaining:0}초" : Data.phase == HuntPhase.Recovery ? $"잠깐 휴식 · {Data.phaseRemaining:0}초" : "승리 / " + (Data.lastWasExtortion ? "삥뜯기!" : "포획!"));
            Set(overlay, "Hunt/Battle status/Live odds", fight ? $"{game.CurrentWinChance:0.0}%" : Data.phase == HuntPhase.Recovery ? "K.O." : "AUTO");
            Animate(time, dt);
        }
        public void RefreshLayout(int width, int height)
        {
            if (scaler == null) scaler = GetComponent<CanvasScaler>();
            // Only the Canvas scale changes; all authored child positions, anchors and sizes remain untouched.
            bool expanded = Application.isPlaying ? ActivePanel.Length > 0 : overlay != null && overlay.Get<Transform>("BaseUI").gameObject.activeSelf;
            scaler.scaleFactor = Mathf.Max(.1f, Mathf.Min(width / referenceWidth, height / (expanded ? expandedReferenceHeight : huntReferenceHeight)));
        }
        private void Animate(float time, float dt)
        {
            bool fighting = Data.phase == HuntPhase.Fighting, walking = Data.phase == HuntPhase.Walking;
            bool basicJab = fighting && Data.currentEnemy == (int)DragonKind.Baby && hunter.BasicCombat != null;
            bool babyResolution = Data.currentEnemy == (int)DragonKind.Baby && hunter.BasicCombat != null &&
                (Data.phase == HuntPhase.Result || Data.phase == HuntPhase.Recovery);
            // The result toast must not conceal the new surrender/guard silhouettes.
            // Restore the authored position for every other encounter and phase.
            notification.anchoredPosition = babyResolution ? new Vector2(24, notificationPosition.y) : notificationPosition;
            DragonCombatChoreography.Frame basicFrame = default;
            string pose = walking ? "walk" : "idle", dragonPose = "idle", action = "자동 사냥 중";
            Vector2 h = Vector2.zero, d = Vector2.zero, hs = Vector2.one, ds = Vector2.one; float hr = 0, dr = 0;
            float elapsed = Mathf.Max(0, Rules.BattleSeconds - Data.phaseRemaining);
            float poseSeconds = time, dragonSeconds = time, contactAge = -1, transportScale = 1, carryProgress = 0;
            bool hit = false, powered = false, defending = false, runAway = false, captureTransferred = false;
            float telegraph = 0;
            CombatImpactKind impactKind = CombatImpactKind.None;
            CombatActorState fighterState = CombatActorState.Ready, dragonState = CombatActorState.Ready;
            string combatBeat = "이동 중";
            bool resolvedFromFight = previousPresentationPhase == HuntPhase.Fighting && Data.phase != HuntPhase.Fighting;
            if (previousPresentationPhase != Data.phase || previousPresentationEnemy != Data.currentEnemy ||
                (fighting && previousBattleElapsed >= 0 && elapsed < previousBattleElapsed))
            {
                contactGate.Reset(); StopContactAudio();
                if (resolvedFromFight && (Data.phase == HuntPhase.Result || Data.phase == HuntPhase.Recovery))
                {
                    knockdownUntil = babyResolution ? 0 : time + .18f;
                    knockdownOnDragon = Data.phase == HuntPhase.Result;
                    if (!babyResolution) PlayContact(knockdownSound, CombatFeedbackTiming.Get(CombatImpactKind.Knockdown).AudioScale);
                }
                previousPresentationPhase = Data.phase; previousPresentationEnemy = Data.currentEnemy;
            }
            previousBattleElapsed = fighting ? elapsed : -1;
            if (fighting)
            {
                var f = basicJab ? BabyFirstBattle.Evaluate(elapsed, Rules.BattleSeconds) :
                    DragonCombatChoreography.Evaluate((DragonKind)Mathf.Clamp(Data.currentEnemy, 0, 4), elapsed, Data.trainingRewardsClaimed);
                basicFrame = f;
                pose = f.FighterPose; poseSeconds = f.FighterSeconds; dragonPose = f.DragonPose; dragonSeconds = f.DragonSeconds;
                h = f.FighterOffset + f.StageOffset; d = f.DragonOffset + f.StageOffset; hr = f.FighterRotation; dr = f.DragonRotation;
                float travel = pose == "cross" ? Data.trainingRewardsClaimed[2] ? upgradedPunchTravel : powerPunchTravel :
                    pose == "takedown" ? jabTravel + 28 : pose == "clinch" ? jabTravel + 20 : pose == "kick" ? jabTravel + 12 : jabTravel;
                h.x += f.FighterTravel * travel;
                hit = f.Hit; powered = f.Powered; defending = f.Defending; contactAge = f.ContactAge; telegraph = f.Telegraph;
                impactKind = f.Impact; fighterState = f.FighterState; dragonState = f.DragonState;
                hs = f.FighterScale; ds = f.DragonScale;
                combatBeat = basicJab ? BabyFirstBattle.BeatLabel(elapsed, Rules.BattleSeconds) :
                    f.Pattern == DragonCombatChoreography.ExchangePattern.Power ? "강공 · 무게를 버텨라" :
                    f.Pattern == DragonCombatChoreography.ExchangePattern.Rush ? "속공 · 박자를 읽어라" : "탐색 · 거리를 재라";
                if (f.ContactKey >= 0 && contactGate.TryContact(elapsed, f.ContactKey, f.ContactAt))
                    PlayContact(SoundFor(f.Impact), CombatFeedbackTiming.Get(f.Impact).AudioScale);
                action = pose == "takedown" ? game.GetSpec(DragonKind.Giant).skillName : pose == "clinch" ? game.GetSpec(DragonKind.Stonehorn).skillName :
                    pose == "cross" ? game.GetSpec(Data.trainingRewardsClaimed[2] ? DragonKind.Logtail : DragonKind.Headbutt).skillName :
                    pose == "kick" ? "킥 반격" : pose == "punch" || pose == "jab" ? "왼손 잽" : f.Cue;
                if (basicJab) action = f.Cue;
                action += $"   응원 +{Data.cheerBonus:0.0}%p";
            }
            else if (walking)
            {
                var walk = AnimationMotionQuality.FighterWalk(poseSeconds, FighterCombatTiming.Duration("walk"));
                hs = walk.Scale;
                hr = walk.Rotation;
            }
            else if (Data.phase == HuntPhase.Result)
            {
                combatBeat = "결착 · 포획 완료";
                pose = "victory"; dragonPose = "defeat";
                fighterState = CombatActorState.Victory; dragonState = CombatActorState.Down;
                poseSeconds = Rules.WinResultSeconds - Data.phaseRemaining;
                dragonSeconds = poseSeconds;
                float p = 1 - Mathf.Clamp01(Data.phaseRemaining / Rules.WinResultSeconds);
                if (Data.lastWasExtortion)
                {
                    // Collapse, get back up, then flee facing the direction of travel.
                    if (p >= .55f && p < .72f) dragonSeconds = Mathf.Lerp(.80f, 0, (p - .55f) / .17f);
                    if (p >= .72f) { dragonPose = "walk"; dragonSeconds = poseSeconds; runAway = true; d.x = (p - .72f) * 2200; }
                }
                else
                {
                    carryProgress = Mathf.SmoothStep(0, 1, Mathf.Clamp01((p - .55f) / .45f));
                    transportScale = Mathf.Lerp(1, .35f, carryProgress);
                    captureTransferred = p >= .94f;
                    int newest = Data.cart.Count - 1;
                    if (newest >= 0 && newest < cargo.Length) cargo[newest].gameObject.SetActive(captureTransferred);
                }
            }
            else if (Data.phase == HuntPhase.Recovery)
            {
                combatBeat = "열세 · 자세를 회복하라";
                float recoveryElapsed = Rules.RecoverySeconds - Data.phaseRemaining;
                float riseAt = Mathf.Max(FighterCombatTiming.Duration("knockdown"), Rules.RecoverySeconds - FighterCombatTiming.Duration("recovery"));
                pose = recoveryElapsed < riseAt ? "knockdown" : "recovery";
                fighterState = recoveryElapsed < riseAt ? CombatActorState.Down : CombatActorState.Recover;
                dragonState = CombatActorState.Victory;
                poseSeconds = recoveryElapsed < riseAt ? recoveryElapsed : recoveryElapsed - riseAt;
                float p = Mathf.Clamp01(recoveryElapsed / .7f);
                float returnToFeet = recoveryElapsed < riseAt ? 1 : 1 - Mathf.Clamp01(poseSeconds / FighterCombatTiming.Duration("recovery"));
                h.x = -p * 22 * returnToFeet;
                d.x = Mathf.Max(0, Rules.RecoverySeconds - 1 - Data.phaseRemaining) * 200;
                if (recoveryElapsed > 1) { dragonPose = "walk"; dragonSeconds = recoveryElapsed - 1; runAway = true; }
            }
            if (babyResolution)
            {
                // Keep the same green body and calibrated spacing through resolution.
                // Do not jump back to the orange legacy atlas or the pre-contact hunter anchor.
                if (Data.phase == HuntPhase.Result)
                {
                    pose = "fight_idle";
                    dragonPose = Data.lastWasExtortion && runAway ? "fight_idle" : "yield";
                }
                else
                {
                    dragonPose = "fight_idle"; dragonSeconds = time;
                    d = Vector2.zero; runAway = false;
                }
                basicFrame = new DragonCombatChoreography.Frame
                {
                    FighterPose=pose, DragonPose=dragonPose, FighterSeconds=poseSeconds, DragonSeconds=dragonSeconds,
                    FighterOffset=h, DragonOffset=d, FighterScale=Vector2.one, DragonScale=Vector2.one
                };
            }
            bool downImpact = time < knockdownUntil;
            if (downImpact)
            {
                impactKind = CombatImpactKind.Knockdown;
                contactAge = .18f - (knockdownUntil - time);
            }
            var feedback = CombatFeedbackTiming.Get(impactKind);
            bool stageVisible = !walking && Data.currentEnemy >= 0;
            if (combatFocusObjects != null)
                foreach (var focusObject in combatFocusObjects) if (focusObject != null) focusObject.SetActive(stageVisible);
            if (combatBeatLabel != null)
            {
                combatBeatLabel.text = combatBeat;
                combatBeatLabel.color = impactKind == CombatImpactKind.Heavy || impactKind == CombatImpactKind.Knockdown
                    ? new Color(1f, .64f, .28f, 1) : new Color(.74f, .94f, 1f, 1);
            }
            hunter.PresentationScale(hs);
            hunter.SampleFighter(pose, poseSeconds); hunter.Offset(SnapToScreenPixel(h), hr);
            hunter.HitFlash((defending && pose == "hurt" && contactAge >= 0 && contactAge < feedback.FlashSeconds) ||
                (downImpact && !knockdownOnDragon && contactAge < feedback.FlashSeconds) ? .78f : 0,
                defending ? new Color(.48f, .88f, 1f, 1) : new Color(1f, .43f, .28f, 1));
            dragon.gameObject.SetActive(!walking && Data.currentEnemy >= 0 && !captureTransferred);
            overlay.Show("Hunt/Dragon name plate", !walking && Data.currentEnemy >= 0 && Data.phase != HuntPhase.Recovery);
            if (Data.currentEnemy >= 0)
            {
                dragon.SetDragonKind((DragonKind)Data.currentEnemy);
                dragon.PresentationScale(ds);
                dragon.DragonFacingAndScale(runAway, transportScale); dragon.SampleDragon(dragonPose, dragonSeconds);
                if (Data.phase == HuntPhase.Result && !Data.lastWasExtortion && carryProgress > 0 && Data.cart.Count > 0)
                {
                    int newest = Mathf.Min(Data.cart.Count - 1, cargo.Length - 1);
                    // Cart and dragon have different authored anchors; aim at the passenger,
                    // not the cart root's bottom corner. Hand off to its icon only on arrival.
                    Vector3 target = cargo[newest].rectTransform.TransformPoint(cargo[newest].rectTransform.rect.center);
                    d = dragon.OffsetToVisualWorldCenter(target) * carryProgress;
                    d.y += Mathf.Sin(carryProgress * Mathf.PI) * 50;
                }
                dragon.Offset(SnapToScreenPixel(d), dr);
                dragon.HitFlash((!defending && dragonPose == "hurt" && contactAge >= 0 && contactAge < feedback.FlashSeconds) ||
                    (downImpact && knockdownOnDragon && contactAge < feedback.FlashSeconds) ? .72f : 0,
                    impactKind == CombatImpactKind.Knockdown ? new Color(1f, .24f, .12f, 1) : new Color(1f, .78f, .30f, 1));
            }
            bool contactVisual = hit || downImpact;
            Vector3 basicContactPoint = basicJab || babyResolution ? hunter.BasicCombat.ApplyPair(hunter, dragon, basicFrame) : Vector3.zero;
            if (babyResolution)
            {
                dragon.DragonFacingAndScale(runAway, transportScale);
                if (Data.phase == HuntPhase.Result && !Data.lastWasExtortion && carryProgress > 0 && Data.cart.Count > 0)
                {
                    int newest = Mathf.Min(Data.cart.Count - 1, cargo.Length - 1);
                    Vector3 target = cargo[newest].rectTransform.TransformPoint(cargo[newest].rectTransform.rect.center);
                    d = dragon.OffsetToVisualWorldCenter(target) * carryProgress;
                    d.y += Mathf.Sin(carryProgress * Mathf.PI) * 50;
                    dragon.Offset(SnapToScreenPixel(d), dr);
                }
                if (Data.phase == HuntPhase.Recovery)
                {
                    // Calm opponent exits as a short UI fade, without a legacy-color walking switch.
                    Color tint = dragon.Image.color;
                    tint.a = 1 - Mathf.Clamp01((Rules.RecoverySeconds - Data.phaseRemaining - 1.1f) / .4f);
                    dragon.Image.color = tint;
                }
            }
            impact.gameObject.SetActive(!basicJab && (contactVisual || time < cheerUntil));
            if (hit || telegraph > 0 || downImpact)
            {
                float effect = hit ? (contactAge < .025f ? .72f : contactAge < .055f ? 1.12f : .90f) : 1;
                if (downImpact) effect = Mathf.Lerp(1.35f, .55f, Mathf.Clamp01(contactAge / .18f));
                bool targetDragon = downImpact ? knockdownOnDragon : !defending;
                RectTransform target = targetDragon ? dragon.Image.rectTransform : hunter.Image.rectTransform;
                Rect targetRect = target.rect;
                Vector2 contactPoint = new Vector2(
                    targetRect.xMin + targetRect.width * (targetDragon ? .30f : .70f),
                    targetRect.yMin + targetRect.height * (downImpact ? .30f : .58f));
                Vector3 point = target.TransformPoint(contactPoint);
                if (basicJab) point = basicContactPoint;
                Color cueColor = impactKind == CombatImpactKind.Guard ? new Color(.50f, .82f, 1, 1) :
                    impactKind == CombatImpactKind.Weak ? new Color(1f, .91f, .50f, 1) :
                    impactKind == CombatImpactKind.Heavy ? new Color(1f, .46f, .16f, 1) :
                    impactKind == CombatImpactKind.Knockdown ? new Color(1f, .28f, .18f, 1) : impactColor;
                if (contactVisual)
                {
                    Sprite sprite = ImpactSprite(impactKind);
                    if (sprite != null) impact.sprite = sprite;
                    // Place the effect center on the receiving actor, not an approximated glove coordinate.
                    impact.rectTransform.position = point - impact.rectTransform.TransformVector(impact.rectTransform.rect.center);
                    impact.rectTransform.localScale = impactScale * effect * Mathf.Max(.01f, feedback.VfxScale) * (basicJab ? .35f : 1);
                    impact.rectTransform.localRotation = Quaternion.Euler(0, 0,
                        impactKind == CombatImpactKind.Heavy ? -8 : impactKind == CombatImpactKind.Guard ? 10 : 0);
                    impact.color = Color.white;
                }
                if (basicJab) HideImpactLayers();
                else AnimateImpactLayers(point, target, cueColor, contactAge, telegraph, hit || downImpact, impactKind, feedback.VfxScale);
            }
            else
            {
                impact.rectTransform.anchoredPosition = impactPosition; impact.rectTransform.localScale = impactScale;
                impact.rectTransform.localRotation = Quaternion.identity; impact.color = impactColor;
                HideImpactLayers();
            }
            UpdateCombatStateLabels(walking, fighterState, dragonState);
            Set(overlay, "Hunt/Battle status/Technique", action);
            if (walking)
            {
                float gaitSpeed = AnimationMotionQuality.WalkSpeedMultiplier(poseSeconds, FighterCombatTiming.Duration("walk"));
                scroll = Mathf.Repeat(scroll + dt * scrollSpeed * gaitSpeed, Mathf.Max(1, terrainTileWidth));
            }
            for (int i = 0; i < ground.Length; i++) ground[i].anchoredPosition = groundPositions[i] + Vector2.left * Mathf.Round(scroll);
            cartRoot.anchoredPosition = cartPosition + Vector2.up * (walking ? Mathf.Sin(time * 10) * 1.5f : 0);
            if (returnTime > 0)
            {
                returnTime -= dt; departure.anchoredPosition = departurePosition + Vector2.left * ((1 - returnTime / Mathf.Max(.1f, cartReturnSeconds)) * 800);
                helper.Play("walk"); if (returnTime <= 0) departure.gameObject.SetActive(false);
            }
            for (int i = 0; i < roamers.Length; i++)
                roamers[i].rectTransform.anchoredPosition = roamerPositions[i] + new Vector2(Mathf.Sin(time * .6f + i) * 12, Mathf.Sin(time * 3 + i) * 2);
            if (trainingActive.gameObject.activeInHierarchy && !Data.training[0].IsEmpty)
            {
                DragonKind kind = Data.training[0].Kind;
                var practice = DragonTrainingChoreography.Evaluate(kind, time * .75f);
                trainee.DragonFacingAndScale(false); trainee.SampleDragon(practice.Pose, practice.PoseSeconds); trainee.Offset(Vector2.zero);
            }
        }
        private Vector2 SnapToScreenPixel(Vector2 motion)
        {
            float scale = Mathf.Max(.1f, scaler.scaleFactor);
            return new Vector2(Mathf.Round(motion.x * scale) / scale, Mathf.Round(motion.y * scale) / scale);
        }
        private void AnimateImpactLayers(Vector3 point, RectTransform target, Color color, float age, float telegraph,
            bool contact, CombatImpactKind kind, float strength)
        {
            bool heavy = kind == CombatImpactKind.Heavy || kind == CombatImpactKind.Knockdown;
            float life = Mathf.Clamp01(Mathf.Max(0, age) / (heavy ? .34f : .22f));
            if (impactRing != null) impactRing.sprite = kind == CombatImpactKind.Guard ? guardImpactSprite :
                kind == CombatImpactKind.Weak ? weakImpactSprite : heavyImpactSprite;
            if (impactEcho != null) impactEcho.sprite = heavyImpactSprite;
            if (impactDust != null) impactDust.sprite = knockdownImpactSprite;
            SetEffect(impactRing, contact && age >= 0 && life < 1, point, Color.white,
                Mathf.Lerp(.65f, 1.55f, life) * Mathf.Max(.8f, strength), (1 - life) * .72f);
            SetEffect(impactEcho, contact && heavy && age >= .025f && life < 1, point,
                Color.white, Mathf.Lerp(.82f, 1.72f, life), (1 - life) * .42f);
            Vector3 groundPoint = target.TransformPoint(new Vector2(target.rect.center.x, target.rect.yMin + target.rect.height * .08f));
            SetEffect(impactDust, contact && heavy && age >= 0 && age < .42f, groundPoint,
                Color.white, new Vector2(Mathf.Lerp(.7f, 1.7f, life), Mathf.Lerp(.6f, .95f, life)), 1 - life);

            if (speedLines != null)
                for (int i = 0; i < speedLines.Length; i++)
                {
                    Image line = speedLines[i];
                    if (line == null) continue;
                    bool show = telegraph > .16f || (contact && age >= 0 && age < .11f);
                    line.gameObject.SetActive(show);
                    if (!show) continue;
                    float pulse = .48f + .52f * Mathf.Sin((Time.unscaledTime * 18f) + i * 1.7f);
                    Color lineColor = heavy ? new Color(1f, .49f, .20f, .42f + pulse * .42f) :
                        new Color(.55f, .90f, 1f, .34f + pulse * .34f);
                    line.color = lineColor;
                    line.rectTransform.localScale = new Vector3(Mathf.Lerp(.75f, 1.28f, telegraph), 1, 1);
                }
        }
        private Sprite ImpactSprite(CombatImpactKind kind)
        {
            switch (kind)
            {
                case CombatImpactKind.Guard: return guardImpactSprite != null ? guardImpactSprite : impact.sprite;
                case CombatImpactKind.Heavy: return heavyImpactSprite != null ? heavyImpactSprite : impact.sprite;
                case CombatImpactKind.Knockdown: return knockdownImpactSprite != null ? knockdownImpactSprite : impact.sprite;
                default: return weakImpactSprite != null ? weakImpactSprite : impact.sprite;
            }
        }
        private static void SetEffect(Image image, bool visible, Vector3 point, Color color, float scale, float alpha) =>
            SetEffect(image, visible, point, color, Vector2.one * scale, alpha);
        private static void SetEffect(Image image, bool visible, Vector3 point, Color color, Vector2 scale, float alpha)
        {
            if (image == null) return;
            image.gameObject.SetActive(visible);
            if (!visible) return;
            image.rectTransform.position = point - image.rectTransform.TransformVector(image.rectTransform.rect.center);
            image.rectTransform.localScale = new Vector3(scale.x, scale.y, 1);
            color.a *= Mathf.Clamp01(alpha);
            image.color = color;
        }
        private void HideImpactLayers()
        {
            if (impactRing != null) impactRing.gameObject.SetActive(false);
            if (impactEcho != null) impactEcho.gameObject.SetActive(false);
            if (impactDust != null) impactDust.gameObject.SetActive(false);
            if (speedLines != null)
                foreach (var line in speedLines) if (line != null) line.gameObject.SetActive(false);
        }
        private AudioClip SoundFor(CombatImpactKind kind)
        {
            switch (kind)
            {
                case CombatImpactKind.Guard: return guardSound;
                case CombatImpactKind.Heavy: return heavySound;
                case CombatImpactKind.Knockdown: return knockdownSound;
                default: return jabSound;
            }
        }
        private void PlayContact(AudioClip clip, float volumeScale = 1)
        {
            if (Application.isPlaying && combatAudio != null && combatAudio.isActiveAndEnabled && clip != null && !AudioListener.pause)
                combatAudio.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
        }
        private void UpdateCombatStateLabels(bool walking, CombatActorState fighterState, CombatActorState dragonState)
        {
            if (fighterStateText == null || dragonStateText == null) return;
            if (walking)
            {
                fighterStateText.color = fighterLabelColor;
                dragonStateText.color = dragonLabelColor;
                return;
            }
            fighterStateText.text = "파이터 · " + CombatFeedbackTiming.Label(fighterState);
            fighterStateText.color = CombatFeedbackTiming.StateColor(fighterState);
            if (Data.currentEnemy >= 0)
            {
                dragonStateText.text = game.GetSpec((DragonKind)Data.currentEnemy).displayName + " · " + CombatFeedbackTiming.Label(dragonState);
                dragonStateText.color = CombatFeedbackTiming.StateColor(dragonState);
            }
        }
        private void StopContactAudio()
        {
            if (Application.isPlaying && combatAudio != null && combatAudio.isPlaying) combatAudio.Stop();
        }
        public void AnimateReturn(List<DragonInstance> dragons)
        {
            for (int i = 0; i < departingCargo.Length; i++) { departingCargo[i].gameObject.SetActive(i < dragons.Count); if (i < dragons.Count) departingCargo[i].sprite = Portrait(dragons[i].kind); }
            returnTime = Mathf.Max(.1f, cartReturnSeconds); departure.gameObject.SetActive(true);
        }
        public void CheerPulse() => cheerUntil = Time.unscaledTime + .17f;
        public void Toast(string message) { Set(overlay, "Hunt/Notification/Notification text", message); overlay.Show("Hunt/Notification", true); toastUntil = Time.unscaledTime + 3.5f; }
        public bool IsBattlePoint(Vector2 point) => RectTransformUtility.RectangleContainsScreenPoint(battleArea, point, null);
        private void UpdateRegions()
        {
            hitRects.Clear(); foreach (var target in hitTargets) AddRegion(target);
            if (ActivePanel.Length > 0) AddRegion(baseRoot); window?.SetInteractiveRegions(hitRects);
        }
        private void AddRegion(RectTransform target) { target.GetWorldCorners(corners); hitRects.Add(Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y)); }
        private Sprite Portrait(int kind) => dragonPortraits[Mathf.Clamp(kind, 0, dragonPortraits.Length - 1)];
        private static void Set(DragonUiBindings map, string key, string value) => map.Get<Text>(key).text = value;
        private static void Fill(DragonUiBindings map, string key, float amount) => map.Get<Image>(key).rectTransform.anchorMax = new Vector2(Mathf.Clamp01(amount), 1);
        public static string Duration(float seconds) { int n = Mathf.CeilToInt(Mathf.Max(0, seconds)); return n >= 60 ? $"{n / 60}:{n % 60:00}" : n + "초"; }

#if UNITY_EDITOR
        public void ConfigureEditor(DragonUiBindings root, DragonUiBindings[] pages, DragonActorView[] actors, Sprite[] portraits)
        {
            overlay = root; storage = pages[0]; trainingLocked = pages[1]; trainingEmpty = pages[2]; trainingActive = pages[3];
            market = pages[4]; skills = pages[5]; settings = pages[6]; quit = pages[7];
            hunter = actors[0]; dragon = actors[1]; helper = actors[2]; trainee = actors[3]; dragonPortraits = portraits;
            cachedPages = null;
        }
        public void PreviewEditorPage(string name)
        {
            if (Application.isPlaying) { OpenPanel(name); return; }
            foreach (var p in Pages) if (p != null) p.gameObject.SetActive(false);
            if (overlay == null) return;
            overlay.Show("BaseUI", name != "");
            var show = name == "storage" ? storage : name == "training" ? trainingActive : name == "training-locked" ? trainingLocked :
                name == "training-empty" ? trainingEmpty : name == "market" ? market : name == "skills" ? skills : name == "settings" ? settings : name == "exit" ? quit : null;
            if (show != null) show.gameObject.SetActive(true);
        }
#endif
    }
}


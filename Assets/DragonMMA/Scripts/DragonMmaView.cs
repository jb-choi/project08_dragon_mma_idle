using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DragonMMA
{
    // All visual objects are authored in the scene/prefabs. Runtime changes only game state,
    // visibility, progress text and additive motion relative to each authored transform.
    [DisallowMultipleComponent, ExecuteAlways]
    public sealed partial class DragonMmaView : MonoBehaviour
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


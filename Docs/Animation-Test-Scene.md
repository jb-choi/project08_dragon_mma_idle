# Animation Test Scene

Open `Assets/DragonMMA/Scenes/AnimationTestScene.unity`, or use **Dragon MMA → Animation Lab → Create or Open Test Scene**, then press Play.

- **클립 테스트**: select any of the 12 fighter poses and 6 dragon poses. All five dragon species are available.
- **전투 상호작용**: uses the production `DragonCombatChoreography` shared clock, root offsets, hit-stop and hit flash. Toggle basic/all-unlocked skills; some counter variants appear in later cycles.
- **수련 리듬**: preview idle → windup → attack → rest.
- **자유 애니메이션**: select a registered UI character or environment object, then any of its clips. Left oak is registered with the shared production sway clip. Position, zoom, mirror and reset affect only the preview placement.
- Controls: play/pause, restart, contact jump, timeline scrub, repeat, and 0.25×/0.5×/1×/2× speed. Seeking pauses playback. Frame stepping is 1/60 second in combat/pose modes, and 1 / selected clip FPS in free animation mode (Left oak: 4fps). Contact/skill controls apply to combat modes.

The actors are instances of the original `Hunter.prefab` and `Dragon.prefab`. Edit the original animation clips/controllers or apply intended changes to the source actor prefab to propagate them to the game. Test-scene-only transform overrides do not update the game. Scene view magnification is 2×; original relative actor spacing is retained.

This is a development-only scene: no `DragonMmaGame`, session, save adapter or desktop-overlay component; it is not added to Build Settings. The lab controls its own unscaled presentation clock and does not change global time scale. Full reward/capture logic, audio and impact-spark effects still require integration checks in the real game.

Validation menus: **Validate Scene**, **Run Runtime Smoke Test** and **Run Workbench Smoke Test** (in Play mode), and **Capture Preview**. Captures are written to `Artifacts/AnimationLab/preview.png`. The create/open menu refuses to switch away from unsaved scenes and upgrades the existing test scene without regenerating it.

## 새 애니메이션·캐릭터 작업 순서

1. Play 모드를 멈추고 `AnimationTestScene`의 `Animation Lab`을 선택합니다. `Animation Lab Asset Preview` Inspector에서 프리뷰 대상을 관리합니다.
2. 새 캐릭터는 `RectTransform` 루트와 `Image`, 애니메이션 바인딩 위치의 `Animator`를 포함하는 UI 비주얼 프리팹을 준비합니다. `UI 프리팹`에 연결하고 필요하면 Controller를 지정한 뒤 **프리팹 등록 / Controller·Clip 등록**을 누릅니다. Controller는 프리뷰 인스턴스에만 적용되고 원본 프리팹은 유지됩니다. 프리팹 인스턴스는 원본과 연결되어 있으며, 비주얼 외 스크립트는 프리뷰에서 비활성화됩니다.
3. 단독 스프라이트 애니메이션은 UI 프리팹을 비우고 Controller 또는 Clip을 등록합니다. 이 경로는 루트 `Image.m_Sprite` 클립을 위한 것입니다. 자식 경로나 다른 컴포넌트를 애니메이션하는 클립은 해당 구조를 가진 UI 프리팹으로 등록합니다. 대상에 맞지 않는 클립은 오류 메시지와 함께 거부됩니다.
4. 새 클립을 Controller에 추가했으면 **Controller에서 새 클립 추가**를 누릅니다. 기존 수동 등록 클립도 보존됩니다. 클립을 목록에서 제거하려면 대상의 `clips` 목록을 편집합니다. 씬을 저장합니다.
5. Play → **자유 애니메이션** → 대상·클립 선택으로 재생, 일시정지, 타임라인, 프레임 이동, 느린 재생을 확인합니다. 배율·X·Y·반전 슬라이더는 프리뷰 조정이고 Play 종료 시 복원됩니다. 반복을 끄면 마지막 프레임에서 정지합니다.
6. 유지할 테스트 배치는 Play 종료 후 대상 항목의 `scale`, `offset`, `flip`을 바꾸고 씬을 저장합니다. 공유 클립을 수정하면 본게임에도 반영됩니다. 본게임 캐릭터 배치나 형태를 바꾸려면 원본 프리팹에서 의도한 변경을 적용하고 실제 게임 씬에서도 확인합니다.

기존 씬에는 **Dragon MMA → Animation Lab → Upgrade Workbench**를 사용합니다. 대상이 등록된 씬은 다시 생성하지 않습니다. 일반 레이아웃 수정은 Inspector/Hierarchy에서 진행합니다. 이 도구는 프로젝트의 uGUI 2D 스프라이트 작업용이며 게임 데이터에 새 용 종을 등록하는 도구는 아닙니다.

## Workbench validation — 2026-10-05

- Unity compilation and scene reference validation passed; one enabled AudioListener is attached to the lab camera.
- Runtime: five Left oak frames, 4fps stepping, pause/speed/non-loop end, placement/mirror/reset, dropdown callbacks/template and mode switching passed.
- Existing combat/practice regression smoke passed: 2,010 samples, 12 fighter poses and 30 dragon poses.
- Editor registration: Hunter UI prefab with 12 controller clips, standalone Image clip with white tint, controller refresh and invalid child-binding rejection/rollback passed. Temporary registration fixtures were removed.
- Cross-clip tests verified RectTransform size and Image fill reset to the authored baseline; preview Animator isolation survived disable/re-enable.
- Captured and inspected the 1440×900 free-animation preview. Evidence and pre-change backups: `Artifacts/AnimationWorkbench-20261005/`. No player build was run for this extension.

## Verified on 2026-10-05

- Unity 6000.3.10f1 compilation and scene reference validation passed.
- Runtime smoke: 2,010 samples; all 12 fighter poses and 30 dragon poses, five species, combat/practice, unlock variants, pause/step/speed/loop and UI callbacks passed.
- Real mouse input: pause button, Giant selection, and timeline drag passed; preview inspected with Baby and Giant.
- Existing EditMode suite: 131 passed, 0 failed. Its corrupt-save recovery test emits an expected backup-recovery warning.
- All 662 pre-existing files checked under `Assets/DragonMMA`, `ProjectSettings`, and `Packages` retained their SHA-256 hashes. Existing scenes, prefabs, animation assets and build settings were unchanged.
- No player build was run; audio/VFX and live-game outcome transitions are outside this lab smoke test.

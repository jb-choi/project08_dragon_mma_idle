# Dragon MMA Idle — First Fighter / Baby Dragon combat sprite QA

2026-10-08 · Unity 6000.3.10f1 · graphics-enabled Editor Play Mode

대상 프로젝트: `E:/unity_project/project08_dragon_mma_idle/dragon_mma_idle`.
Fish to Dish 설치 폴더는 작업 파일/백업을 위한 staging 위치로만 사용했다. 해당 게임의 자산을 가져오지 않았다.

## 구현한 것

첫 Fighter + 숲 새끼용의 분리형 기본 타격 조합을 실제 게임과 Animation Lab에 연결했다. 기존 uGUI Image 기반 독립 GameObject/Animator를 유지했다. SpriteRenderer로 구조를 갈아엎거나 통 애니메이션을 만들지 않았다. 일반 Idle 및 다른 용의 기존 전투는 유지한다.

추가한 제작용 스프라이트 시트와 클립:

| 캐릭터 | 스프라이트 파일 | 클립 / 내용 |
|---|---|---|
| Fighter | Assets/Resources/DragonMMA/Art/fighter_fight_idle_sheet.png | fighter_fight_idle.anim · 0.75초 체중 이동 |
| Fighter | Assets/Resources/DragonMMA/Art/fighter_jab_sheet.png | fighter_jab.anim · 준비→왼손 Jab→회수, 0.55초 |
| Baby Dragon | Assets/Resources/DragonMMA/DragonSheets/dragon_0_fight_idle.png | dragon_0_fight_idle.anim · 1.5초 느린 호흡 |
| Baby Dragon | Assets/Resources/DragonMMA/DragonSheets/dragon_0_light_hit.png | dragon_0_light_hit.anim · 짧은 머리/목 flinch, 0.30초 |
| Baby Dragon | Assets/Resources/DragonMMA/DragonSheets/dragon_0_heavy_hit.png | dragon_0_heavy_hit.anim · 큰 머리 회전/앞발 들기, 0.64초 |
| Baby Dragon | Assets/Resources/DragonMMA/DragonSheets/dragon_0_threat.png | dragon_0_threat.anim · 짧은 입 벌리기/위협, 0.50초 |

모든 클립은 `Assets/DragonMMA/Animations/`에 있다. 기존 Hunter/Dragon0 controller에 상태만 추가했다. Fighter 프레임은 192×128, 용 프레임은 256×192, 8프레임 고정 등록이다. 원본 RGBA와 생성 프롬프트는 `Assets/DragonMMA/ArtSource/BasicCombat/`, `Docs/AI/BasicCombat-ImageGen-Prompts.md`, `Docs/AI/BasicCombat-ImageGen-Idle-Correction.md`에 보존했다. built-in ImageGen 사용; CLI/API 우회 없음.

추가/수정한 코드:

- `BasicCombatProfile.cs`, `Configuration/BabyJabContact.asset`: 발 기준점, 주먹/머리 pixel contact, 공용 Jab/피격 시간 데이터.
- `DragonCombatChoreography.EvaluateBasicJab`: 기존 Frame 계약으로 첫 조합의 전투 시퀀스 제공. 기존 contact audio gate 재사용.
- `DragonActorView`: 등록된 sprite pixel의 world 좌표 조회와 접촉 높이 정렬. 캐릭터 root의 이동/회전/squash 없음.
- `DragonMmaView`: 첫 새끼용 전투만 새 시퀀스로 연결. 주먹/머리가 가려지지 않도록 기본 Jab VFX 제거.
- `DragonAnimationLab`: BASIC JAB 모드, 독립 6클립 버튼, contact jump / 프레임 이동 / 재생 배속 지원.
- `CombatSpritePreloader`: 새 6개 시트 사전 로딩. 소유 참조만 정리하며 공유 자산을 언로드하지 않음.
- `Editor/BasicCombatSpriteAuthoring.cs`: artist-drawn 프레임의 선택·발 기준 등록·포인트 샘플링·sprite/clip/controller/prefab 연결. 재실행 시 GUID 보존.
- `Editor/BasicCombatQa.cs`, `Tests/Editor/BasicCombatTests.cs`: 실시간 10회 재생과 독립 접촉 샘플, 고정 anchor/크기, contact gate, clip/prefab 계약 검증.

`Prefabs/Actors/Hunter.prefab`, `Dragon.prefab`에는 contact profile 참조만 추가했다. `AnimationTestScene.unity`에는 preview 배치/새 컨트롤을 추가했다. 기존 사용자 자산을 삭제하지 않았다. 변경 전 파일은 staging의 `Backup/`에 보존했다.

## 현재 전투 Sequence

`Fight Idle 1.50초 → Jab 준비/전진 → 1.70초 Impact + LightHit 시작 → 짧은 recoil → 팔 회수/Guard → 다음 Idle`

한 주기는 약 2.233초. Fighter의 빠른 0.75초 리듬과 용의 1.50초 호흡을 대비시킨다. impact에서 주먹은 2/60초만 유지하며 용의 피격 시계에는 별도 hold/delay를 넣지 않는다. LightHit의 시작 그림은 실제 접촉하는 중립 머리이고, 다음 60Hz key(약 16.7ms)부터 머리/목 변형이 보인다. 임의의 코루틴 delay를 흩뿌리지 않는다.

HeavyHit와 Threat는 별도 클립/소형 화면 QA까지 완료했지만, 요청한 최소 4동작 Jab 반복에는 섞지 않았다. 실제 반격→방어 / PowerPunch→HeavyHit 조율은 다음 범위다.

## QA 결과 — 종료 조건별 판정

시각 PASS는 개발자 프레임 검수의 판단이며, 자동 테스트 숫자만으로 판정하지 않았다. 미학적 자연스러움에 대한 독립 플레이어/사용자의 블라인드 검증은 별도로 하지 않았다.

| # | 조건 | 결과와 근거 |
|---|---|---|
| 1 | MMA 선수처럼 가벼운 리듬 | PASS(프레임 검수): 고정 가드, 앞뒤로 작은 어깨/골반 체중 이동. 큰 상하 bounce 제거. |
| 2 | 제자리 통통 뛰기 아님 | PASS: FightIdle 8프레임의 실루엣 높이 110px, 바닥 동일. 두 발 공중 이동이나 root 수직 이동 없음. |
| 3 | Jab이 팔만 움직이지 않음 | PASS(프레임 검수): 전진하는 앞 어깨/흉곽과 작은 골반 변화가 팔 전진에 연결됨. 팔 길이 확대/전체 sprite 회전 사용 안 함. |
| 4 | Impact 주먹–용 접촉 | PASS: 실제 Animator는 fighter_jab_3 / dragon_0_light_hit_0. 본게임 contact 좌표 오차 약 0.000122 local px. 확대 렌더 contact 양쪽은 (913.03, 587.83)로 일치하며 실루엣 검수에서도 빈 배경 틈이 없음. |
| 5 | Impact–피격 동시 시작 | PASS: 같은 이벤트 시계에서 LightHit 상태 시작. 먼저 반응하지 않음. 눈/목 recoil의 첫 변형은 다음 60Hz key, 별도 reaction 지연 없음. |
| 6 | 용이 더 묵직함 | PASS(프레임 검수): Fighter보다 큰 체형, 느린 호흡, 뒤발/root 안정. Jab은 짧은 머리/목 반응이며 전신 비행 없음. |
| 7 | 10회 이상 전환 안정 | PASS: graphics Play 1배속 10회 연속, 22.34초. 10/10 contact 관측. root/visual size 최대 변동 모두 0.000000. |
| 8 | 하나의 싸움으로 연결 | PASS(프레임 검수): 고정 간격, 주먹 접촉→피격→회수 연동. 과도한 VFX를 제거한 화면으로 검수. |
| 9 | 작은 실제 게임 크기 가독성 | PASS(프레임 검수): 640×240 전체 게임 화면에서 Jab, 머리 recoil, 회수 구별. 1920×720 contact도 대조. Light/Heavy/Threat를 같은 소형 화면에서 비교. |
| 10 | 다른 Dragon에도 같은 구조 사용 | PASS(구조): 독립 controller/sprite + 체형별 contact profile + 같은 Frame/impact clock. 첫 조합에만 활성화했으며 두 번째 체형 런타임 검증은 아직 하지 않음. |

보조 검증:

- 최종 EditMode suite: 199 passed / 0 failed / 0 skipped.
- 새 timing 테스트: 30/60/144fps 각각 10회 contact gate, root 고정, 조기 피격 없음, 피격 hold 없음, prefab 독립성, persistent clip/pivot 검증.
- 마지막 정상 초기화 Play/capture에서 game runtime Console error 0.
- 개발 중 QA 시작 누락/paused deltaTime/Play 중 script reload로 QA 도구 오류가 있었다. 이를 최종 runtime 성공 증거와 구분했다. Editor stopped + synchronous import + fresh Play로 최종 검증했다. null session을 임의로 다시 Awake 호출해서 감추지 않았다.
- Windows Development player build: Succeeded, errors=0, warnings=0, 51.05초. 출력은 `Builds/Windows-CombatSpriteQA-20261008/DragonMMAIdle.exe`. `Artifacts/BasicCombatQA/windows-build.txt` 참고. 별도 player 기동/릴리스 QA는 아직 수행하지 않았다.

증거:

- `Artifacts/BasicCombatQA/live-10-cycles.txt`: 실제 10회 연속 Play 측정.
- `Artifacts/BasicCombatQA/exact-contact-10-cycles.txt`: 본게임에서 encounter를 매번 초기화한 10개 정확한 contact 샘플. 연속 재생 기록과 다른 증거임.
- `Artifacts/BasicCombatQA/combat-loop-640.gif`: 실제 Unity graphics renderer로 60Hz 샘플링한 134장, 약 2.233초 loop preview. **실시간 화면 녹화 영상은 아님.**
- `Artifacts/BasicCombatQA/contact-01-640.png`, `final-phase-0-1920.png`: contact.
- `light_hit-small.png`, `heavy_hit-small.png`, `threat-small.png`: 동일한 소형 화면의 독립 모션 대조.
- `lab-contact-final.png`, `heavy-hit-preview.png`, `threat-preview.png`: Animation Lab 확대 검수.
- `before-640.png`, `before-1920.png`, `cycle2-*`: 이전 상태 및 중간 사이클.
- `Artifacts/upgrade-editmode-results.xml`: 최종 테스트 결과.

## 수정 Cycle

| Cycle | 발견/검증 | 수정 |
|---|---|---|
| 1 · 조사 | 기존 fighter guard 변화 부족, punch 회수 부족, VFX가 실제 주먹과 다른 곳에 위치 | 공용 clock/Frame/독립 Animator 보존. 새 최소 조합 범위 확정. |
| 2 · 최소 연결 | 첫 생성 Jab은 충격 다음 즉시 guard; 용 hit은 눈 감기가 두드러짐 | 독립 Jab 회수/LightHit sprite를 재생성. fixed floor와 profile contact 연결. |
| 3 · 본게임 Play | torso 아래 VFX, 체형/접촉 높이 불일치 | Jab VFX off, 고정 neutral face contact로 높이/거리 등록. |
| 4 · 시각 QA | Fighter Idle 후반 머리 높이 4px 변화; 용 호흡의 head bob; 회수 손끝이 충격보다 더 전진 | 안정된 Fighter 첫 줄 그림을 역순 loop로 사용, 용의 조용한 호흡 재생성, 회수 프레임을 단조 감소하는 손끝 순서로 선택. |
| 5 · regression 수정 | 이미 recoil한 머리에 등록하면 접촉 전에 glove가 neutral face에 침투. LightHit 두 프레임이 턱을 과도하게 올림. preview 잘림/버튼 참조 복제 | 중립 머리 기준 충돌 + 다음 key부터 recoil. 과한 두 그림 제외. preview 전체 체형 표시, 새 버튼 참조 14/14·10/10·5/5 독립으로 확인. |
| 6 · 재검증 | 최종 첫 조합 10회 live/실전 contact/소형 모션 비교 | root와 size drift 0, exact contact 10/10, 199 tests PASS. 최종 graphics loop와 보고서 저장. |

## 남아 있는 문제와 범위

- 자연스러움/타격감의 최종 취향 판단은 첨부 loop를 보고 사용자 검수가 필요하다. 별도 독립 평가자가 없다는 한계를 기술 PASS로 덮지 않는다.
- Legacy walk/hurt/kick/cross/clinch/takedown/victory는 이번 새 sprite 작업에서 교체하지 않았다. 일반 Idle/다른 행동 및 다른 용과의 화풍을 완전히 통일한 상태는 아니다.
- HeavyHit/Threat는 독립 자산·검수 준비 상태다. 본게임 최소 Jab loop에서는 아직 power/counter 이벤트로 호출하지 않는다.
- 두 번째 Dragon은 해당 체형의 전용 그림/피격 포인트를 넣고 새로 contact QA 해야 한다. 모든 용 자동 대응이나 범용 Combat Engine을 만들지 않았다.
- Windows build 성공은 release/standalone window/지속 FPS 검증과 동일하지 않다. 이번 Play 검수는 Editor의 graphics-enabled 경로다.

## 다음 작업 추천

동작 수를 늘리기 전에 이 조합의 loop를 사용자 시각 기준으로 확인한다. 다음 구현 우선순위는 **동일 조합의 Threat→Fighter block 반응**이다. 이후 PowerPunch→HeavyHit, 그 다음 두 번째 Dragon으로 같은 profile/clock 구조를 검증한다. Straight나 더 큰 기술은 그 이후가 적절하다.

## 재현/검수 방법

1. `Assets/DragonMMA/Scenes/AnimationTestScene.unity`를 열고 Play.
2. BASIC JAB / 1× / 반복 ON. 접촉 시점, ±1 frame, 0.25×로 접촉/회수 비교.
3. preview 왼쪽 첫 줄은 Fighter fight_idle/Jab, 아래 두 줄은 Dragon fight_idle/LightHit/HeavyHit/Threat 독립 클립.
4. 자동 반복 측정은 `BasicCombatQa.StartLive()`를 graphics Play에서 호출한다. paused clock을 네 Editor update 동안 안정화한 후 시작한다.
5. 본게임 캡처는 clean Editor→격리 save 준비→DesktopOverlayScene fresh Play→`BasicCombatQa.CaptureMainSequence()`이다. Play 중 script reload 후의 session을 사용하지 않는다.
6. 마지막 작업 상태는 DesktopOverlayScene, Play stopped, save override cleared.

## 사용 스킬

Unity feature-implementation / bug-investigation / build-validation 및 unity-mcp-workflow: 기존 architecture 보존, 증거 기반 fail 수정, 정상 Editor/Play 검증, 저장/scene 보호에 사용했다. ImageGen: 독립 raster motion 생성 및 회수/호흡 수정에 사용했다. 생성 이미지 개수나 테스트 총수를 미학적 성공의 대용품으로 삼지 않았다.

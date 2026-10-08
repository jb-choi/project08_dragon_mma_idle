# 전투 디렉팅 개선 — 3 Cycle 완료 보고서

- 기준: Notion `🥊 전투 디렉팅 개선 — 싸움의 재미·존재감 강화`
- 작업일: 2026-10-07
- Unity: 6000.3.10f1 / Windows 64-bit
- 원칙: 전투 확률, 전투 시간, 응원 보너스, 포획·삥뜯기 보상, 저장 데이터 규칙은 변경하지 않고 프레젠테이션만 개선

## 결과

Notion의 7개 개선 항목을 모두 반영했다.

1. 전투 화면 비중: 데스크톱 전투 입력 영역을 `580×140 → 800×185`, Web을 `680×170 → 840×136`으로 조정하고 캐릭터 배율과 이름표 위치를 함께 재배치했다.
2. 선택 용 디자인: Baby 용의 기능적 실루엣 포인트를 대형 세 잎 왕관 볏으로 통일해 6×6, 36개 모든 포즈에 적용했다.
3. 싸움 변주: `Probe → Rush → Power` 세 리듬을 순환시키고, 종·해금 상태에 따라 punch/kick/cross/clinch/takedown을 선택한다. 인접 리듬 반복은 없다.
4. 대표 공격: 읽기/준비 → 접촉 → 후속 회복으로 나누고 실제 접촉 프레임을 기준으로 히트스톱, 반동, 스테이지 킥을 동기화했다.
5. 피드백 구분: Weak/Guard/Heavy/Knockdown에 서로 다른 히트스톱, 플래시, VFX 크기·색, 화면 흔들림, 음량을 적용하고 다운 전용 효과음을 추가했다.
6. 상태 가독성: 이름표에 실제 `준비/공격/방어/피격/회복/승리/다운` 상태와 색을 표시한다. 이펙트 중심은 공격자 손이 아니라 피격 캐릭터에 맞춘다.
7. 템포·전환: 세 리듬의 읽기·공방 간격·휴식 길이를 분리하고 승리/패배 다운 피드백을 연결했다. 게임 규칙과 보상은 그대로이며 보상은 한 번만 적용된다.

Animation test도 같은 `DragonCombatChoreography`와 `CombatFeedbackTiming`을 사용하므로 본게임과 접촉 시점, 상태, 리듬이 어긋나지 않는다. 새 캐릭터·클립을 추가하는 기존 자유 애니메이션 워크벤치와 한 개 AudioListener 규칙도 유지했다.

## 이미지 생성 기록

- 방식: Codex 내장 ImageGen, 기존 이미지 정밀 편집
- 입력: `Assets/DragonMMA/ArtSource/ForestDragons/dragon_0_sheet.png`
- 최종 프롬프트: `Preserve the exact 1254×1254 transparent 6×6 sprite-sheet layout, all 36 poses, timing silhouettes, transparency, character proportions, palette, pixel-art rendering, and frame order. Redesign only the Baby dragon's dorsal/head silhouette into one oversized three-leaf crown crest, consistently present and readable in every frame. Do not add text, borders, background, extra characters, or change any pose.`
- 승인 원본: `Assets/DragonMMA/ArtSource/ForestDragons/dragon_0_leafcrest_sheet.png`
- 런타임/작업 원본 SHA-256: `A04373A43894B90F0AA644B242A484EFC593E1BC7A12689628EBF4071D258A86`
- 교체 전 백업 SHA-256: `6D7CEF8D9B3AA96DE8B78DA3B725099FFE993DA10A8483D57EF64B32D58C1F65`

## Cycle별 QA

| Cycle | 개선 | 대상 QA | 전체 회귀 |
|---|---|---:|---:|
| 기준선 | 기존 프로젝트 | — | 147/147 PASS |
| 1 | 화면 비중·레이아웃 | 4/4 PASS | 151/151 PASS |
| 2 | 3리듬, 공격 품질, SFX/VFX, 상태, 승패 전환 | 70/70 PASS | 165/165 PASS |
| 3 | 선택 용 디자인·36프레임 재임포트 | 11/11 PASS | 최초 165/166, 재임포터의 Web 이름표 위치 덮어쓰기 발견·수정 후 166/166 PASS |
| 통합 | 실제 Play Mode + Windows 빌드 | Runtime smoke PASS | Windows build SUCCESS, 227,002,285 bytes, errors 0 |

빌드 경고 2건은 동일한 기존 경고 `DragonMmaView.dragonTravel` 미사용(CS0414)이 두 번 집계된 것이다. 빌드 실패나 이번 변경의 런타임 오류는 없다.

## 실제 실행 검증

- Animation test: 12개 파이터 포즈, 5종×6개 용 포즈, Combat/Practice, 세 리듬, 전체 해금 기술, pause/step/speed/loop/contact jump, UI callback을 Play Mode에서 순회했다.
- 본게임: Baby Power 강타 접촉, 승리 Result, 패배 Recovery를 실제 1920×720에서 캡처했다.
- 승리: cart 보상 +1을 확인하고 Result를 추가 진행해도 중복 지급되지 않음을 확인했다.
- 패배: Recovery 진입과 cart 보상 불변을 확인했다.
- Animation test 씬은 게임/저장/윈도 컴포넌트를 포함하지 않고 Build Settings에도 들어가지 않는다.

## 증거

- 전/후: `Artifacts/CombatDirecting-20261007/main-game-before.png`, `main-game-heavy-after.png`
- 상태: `main-game-win-after.png`, `main-game-loss-after.png`
- Animation test: `animation-test-after.png`
- 런타임 보고: `runtime-smoke.txt`, `final-runtime.log`
- 최종 회귀: `cycle3-full-r2-results.xml`
- 빌드: `final-windows-build.log`
- 레이아웃 수치: `layout-before.txt`, `layout-after.txt`

## 유지보수 메모

- 캐릭터 재임포터는 이제 스프라이트 형상만 소유하며 전투 UI 위치를 덮어쓰지 않는다.
- 전투 레이아웃은 `CombatDirectingUpgrade.ApplyBattleLayout`, 피드백 자산은 `ApplyFeedbackAssets`가 각각 소유한다.
- 게임과 Animation test는 순수 타이밍 모델을 공유한다. 새 리듬은 `DragonCombatChoreography`, 강도별 피드백은 `CombatFeedbackTiming`에 추가한다.
- 스프라이트 시트 교체는 36개 연결 실루엣 검증을 먼저 통과한 뒤 기존 GUID·spriteID를 유지해 재슬라이스한다.

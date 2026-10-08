# Unity에서 화면과 설정 수정하기

이제 화면은 실행할 때 코드로 생성되지 않습니다. 저장된 씬과 프리팹이 화면의 원본입니다.

## 시작

1. Project에서 `Assets/DragonMMA/Scenes/DesktopOverlayScene.unity`를 엽니다.
2. Play를 **정지**합니다. Hierarchy에서 `OverlayRoot`를 선택합니다.
3. Inspector의 `Dragon Mma View` 상단에서 **수용소 / 수련방 / 판매소 / 기술 / 설정** 미리보기 버튼을 누릅니다.
4. Hierarchy에서 바꿀 오브젝트를 선택합니다. Scene 창은 2D로 두고 `F`로 선택한 오브젝트에 초점을 맞춥니다.
5. 위치·크기는 `Rect Transform`, 이미지·색은 `Image`, 문구·폰트·글자 크기는 `Text`에서 바꾸고 `Ctrl+S`로 씬을 저장합니다.
6. Play로 확인합니다. 정지 상태에서 저장한 수정은 실행에도 반영됩니다. **Play 도중 씬 오브젝트를 고친 내용은 정지하면 돌아가는 Unity 기본 동작**입니다.

Game 창은 결과를 보는 곳이고, 배치와 속성 편집은 Scene / Hierarchy / Inspector에서 합니다. 씬 미리보기의 돈·보유 용·완료 상태는 배치 확인용 예시이며 실제 세이브가 아닙니다.

## 무엇을 어디서 수정하나요?

| 바꾸려는 것 | 수정 위치 |
|---|---|
| 사냥 배경, 버튼 배치, HUD 크기 | `OverlayRoot/Hunt` 아래의 Rect Transform · Image · Text |
| 시설 탭과 공통 배경 | `OverlayRoot/BaseUI` |
| 수용소 목록과 선택 정보 | `BaseUI/StoragePage`, 반복 카드 원본은 `Prefabs/UI/StorageCard.prefab` |
| 수련 화면 | `TrainingLockedPage`, `TrainingEmptyPage`, `TrainingActivePage` 세 상태 |
| 판매·기술·설정·종료 화면 | `MarketPage`, `SkillsPage`, `SettingsPage`, `QuitPage` |
| 캐릭터 위치와 크기 | `Hunt/Hunter`, `Hunt/Dragon` 및 `Prefabs/Actors` |
| 캐릭터 색·시각 요소 | 캐릭터 프리팹의 자식 `Visual`에 있는 Image |
| 수레·시설 그림 | `Prefabs/Facilities` 및 `Resources/DragonMMA/Art` |
| 프레임·동작 속도 | `Assets/DragonMMA/Animations`의 `.anim` 및 `.controller` |
| 가격·시간·보상·전투력·가중치 | `Assets/DragonMMA/Configuration/DefaultGameConfig.asset` |
| 응원 증가량·최댓값, 걷기·전투·회복 시간 | 같은 Game Config 에셋의 관련 항목 |
| 타격 이동량·스크롤 속도·선택 색상 | OverlayRoot의 Dragon Mma View |
| Windows 창 기본 높이 | Dragon MMA Bootstrap의 Desktop Overlay Window |

## 프리팹 편집

`Assets/DragonMMA/Prefabs/UI/OverlayRoot.prefab`가 전체 화면 원본이며 그 안에 시설 페이지·캐릭터·카드 프리팹이 중첩되어 있습니다. Project에서 프리팹을 더블클릭해 Prefab Mode로 편집하면 해당 프리팹 인스턴스들에 공통 적용됩니다. 씬에서만 수정하면 그 씬의 override로 저장됩니다. 의도한 수정만 Overrides에서 Apply하고, 다른 수정까지 무조건 Apply All하지 마세요.

목록은 10개 카드와 최근 용 10마리를 미리 배치해 두고 데이터만 바꿉니다. 보유량 자체에는 제한이 없습니다. 수레의 시각 표시는 최대 3마리이며 Game Config의 수레 실제 용량과 별개입니다.

오브젝트 이름이나 배치를 바꿔도 저장된 참조로 연결합니다. 다만 `Dragon Ui Bindings`의 key는 코드와 연결되는 식별자이므로 수정하지 마세요. 새 기능 버튼이나 카드 개수 자체를 추가·삭제하는 것은 연결 코드도 함께 수정해야 합니다.

## 애니메이션

캐릭터 루트는 배치 기준점이고 `Visual` 자식의 Animator가 Image의 Sprite를 바꿉니다. 루트 위치를 바꾸면 타격·이동 연출도 그 위치를 기준으로 재생됩니다. Animation 창에서 클립의 Sprite 키를 편집할 수 있습니다. 상태 이름 `idle`, `walk`, `punch`, `kick`, `hurt`, `clinch`, `takedown`, `victory` 및 용의 `idle`, `attack`, `hurt`는 코드가 재생하는 이름이므로 유지하세요.

컨트롤러를 교체할 때는 `Dragon Actor View`의 Hunter Controller 또는 Dragon Controllers 참조를 바꿉니다. Visual의 Animator만 교체하면 런타임 종 선택 시 원래 참조가 적용됩니다.

## 밸런스 설정

Game Config 에셋을 선택해 Inspector에서 수정합니다. Dragons 배열의 Kind는 기존 세이브 ID에 대응하므로 5종의 Kind를 유지하세요. 게임은 시작 시 에셋 값을 독립 복사해 사용하므로 **밸런스 변경 후 Play를 다시 시작**해야 합니다. 런타임 보상·진행도가 에셋 원본에 저장되지는 않습니다.

Starting Coins / Starting Power는 새 세이브에만 적용됩니다. 기존 진행도는 초기화하지 않습니다. 이미 등록한 판매·수련의 종료 시각도 유지됩니다. 등장 조건(이전 종 포획), 개체별 용핵 1회, 종별 기술 보상 1회 등의 규칙은 `DragonGameSession.cs` / `DragonData.cs`에 있습니다.

돈·승률·타이머·용 이름 등 동적 Text는 실행 중 게임 데이터로 갱신됩니다. 이들의 위치·크기·폰트는 Inspector로 편집 가능하지만 내용은 데이터/표시 형식에서 바꿔야 합니다. 지갑·수레·응원 문구 형식은 Dragon Mma View의 Wallet Format / Capacity Format / Cheer Hint Format입니다.

## 주의 및 검증

- `Dragon MMA/Authoring/Convert Current Scene (once)`는 최초 전환용입니다. 이미 전환된 씬에서는 재실행을 막습니다. 이후 화면 변경에 레이아웃 생성 코드를 쓰지 않습니다.
- 레거시 초기 프로젝트 생성도 기존 씬이 있으면 중단합니다.
- 한글 폰트는 Nanum Gothic 원본 TTF를 포함했습니다. 출처는 Google Fonts의 `ofl/nanumgothic`, 라이선스는 `Assets/DragonMMA/Fonts/OFL.txt`입니다. 빌드에는 `Licenses/NanumGothic-OFL.txt`도 복사합니다.
- Unity Editor의 검은 여백은 Windows 실행 파일에서 투명해지는 색상키입니다. 순수 검정색을 보여야 하는 UI 색상으로 사용하지 마세요.
- 변경 후 `Dragon MMA/Run EditMode Tests`, `Dragon MMA/Build Windows Player`로 확인할 수 있습니다. 빌드 위치는 `Builds/Windows-Upgrade/DragonMMAIdle.exe`입니다.
- 전환 전 에셋 백업: `Artifacts/BeforeAuthoring-20261001-1919/DragonMMA/`.

전환 검증: EditMode 58개 통과. 실제 씬의 버튼 위치·색·고정 문구를 임시 변경하여 Play 및 Stop 후 유지되는 것을 확인하고 원래 디자인으로 복원했습니다. 분리된 테스트 세이브로 시설 버튼·수련 잔류·판매 수령·애니메이션 연속 재생을 확인했습니다.

Windows x64 Development 빌드 성공: 오류 0, 경고 1(디버그 심볼의 Unity 서버 업로드가 HTTP403으로 거부됨). 최종 실행 파일에서 한글 폰트 표시와 480px 사냥 창 → 960px 거점 확장 → 480px 복귀 및 투명 색상키 일치를 확인했습니다. 해당 2560×1440 환경 진단은 `Artifacts/WindowsProbe-AuthoringFinal/`에 있습니다. Steam 연동, 물리 터치, 다른 앱으로의 실제 클릭 통과, 다중 모니터 전환은 별도 검증 대상입니다.

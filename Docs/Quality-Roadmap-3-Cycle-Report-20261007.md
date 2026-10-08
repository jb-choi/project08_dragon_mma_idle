# Dragon MMA Idle 품질 개선 로드맵 — 자율 개발 3 Cycle 결과

- 기준 문서: [Dragon MMA Idle 퀄리티 개선 로드맵](https://app.notion.com/p/3eff3731816f8117a7b6e8b83b47a2cd)
- 작업일: 2026-10-07
- Unity: 6000.3.10f1
- 원칙: 게임 규칙·세이브 스키마·아트 GUID·씬/프리팹 직렬화는 유지하고, 각 개선 직후 전체 EditMode QA를 실행한다.

## Cycle 결과

| Cycle | 개선 | QA 결과 |
|---|---|---|
| 0 기준선 | 변경 전 상태 확인 | EditMode 131 passed / 0 failed |
| 1 전투 상호작용·전환 | 접촉 120ms 전 타격 예고, 접촉 프레임에서 예고 종료, 상태 전환 시 남은 접촉음 정리, Animation Lab 상태 문구 연동 | 최초 실행 132 passed / 4 failed: 접촉 시각의 float 잔차로 예고가 남는 경계 오류 발견. 0.1ms 정규화 후 136 passed / 0 failed |
| 2 애니메이션 다양성 | 수련을 준비·공격·회복 길이와 idle 재생률이 다른 3개 결정론적 라운드로 구성. 본게임과 Animation Test가 같은 타이밍 소스를 사용 | 142 passed / 0 failed |
| 3 UI 정보 계층 | 홈 하단 버튼에 거점 보유 수, 수련 상태/남은 시간, 판매 진행/수령 가능, 기술 해금 수를 텍스트로 표시. Desktop/Web 실제 버튼 폭 검사 추가 | 147 passed / 0 failed |
| 최종 묶음 | 세 Cycle의 정확한 최종 파일 재검증 | 147 passed / 0 failed / 0 skipped |

## 런타임 QA

- `AnimationTestScene` Play Mode 스모크: PASS
- 전투/수련 스모크: 2,010 samples, 12 fighter poses, 30 dragon poses, 5종 전투 상호작용, 기술 해금, pause/step/speed/loop/UI callback PASS
- 자유 애니메이션 workbench: left oak 5프레임, clip-fps step, one-shot, 배율/위치/반전/reset, 모드 전환 PASS
- 격리 세이브 시각 QA: 홈 상태 요약, 접촉 60ms 전 예고 프레임, 수련 화면을 1920×720으로 캡처해 글자 잘림·레이아웃 겹침·검은색 투명 키 배경을 확인했다. 결과는 `Artifacts/QualityRoadmap-20261007/isolated-qa/`에 있다.
- 원본 프로젝트가 실행 중이어서 원본 Editor 상태는 변경하지 않았다. `Assets`, `Packages`, `ProjectSettings`만 복제한 F: 격리 사본에서 컴파일·테스트·Play Mode QA를 실행했다.

## 변경 범위

- 신규 런타임 로직: `CombatFeedbackTiming`, `DragonTrainingChoreography`, `DragonUiStatusLabels`
- 연결 수정: `DragonCombatChoreography`, `DragonMmaView`, `DragonAnimationLab`
- 자동 검증: `DragonCombatScenarioTests`, `DragonUiStatusTests`, `DragonAuthoringTests`
- 변경하지 않음: 밸런스/보상/사냥 판정, 세이브 DTO와 저장 경로, 씬/프리팹, 애니메이션 클립/컨트롤러, 스프라이트 시트, ProjectSettings/Packages

## 남은 검증

- 이번 범위에서는 Windows/WebGL 플레이어 빌드, 배포 브라우저, 실제 터치/포커스/멀티모니터 장시간 테스트를 실행하지 않았다.
- 격리 사본 시작 시 Unity SearchDatabase 인덱서의 `ArgumentOutOfRangeException`이 한 번 기록됐지만 프로젝트 테스트와 두 Play Mode 스모크는 모두 통과했다. 런타임 코드 실패로 분류하지 않았다.
- 기존 WebGL IL2CPP AnalyticsWriter 메모리 부족 이력은 별도 이슈로 남아 있다.

## 복구

적용 전 소스·테스트·UI prefab·씬·문서 스냅샷은 `Artifacts/QualityRoadmap-20261007/before/`에 보관한다.

# Dragon MMA Idle — project08

작은 용을 자동으로 사냥하고, 포획·훈련·판매를 통해 성장하는 Unity 게임입니다.
이 저장소의 현재 main은 **project08 Dragon MMA Idle**입니다. 예전 Q/QQ/E 수동 격투 프로토타입이 아닙니다.

## 프로젝트 열기

- Unity: **6000.3.10f1**
- Unity Hub에서 이 저장소의 루트 폴더를 프로젝트로 추가합니다.
- PC 메인 씬: `Assets/DragonMMA/Scenes/DesktopOverlayScene.unity`
- 애니메이션 검수 씬: `Assets/DragonMMA/Scenes/AnimationTestScene.unity`
- WebGL 전용 씬: `Assets/DragonMMA/Scenes/WebGLScene.unity`
- 현재 게임 소스: `Assets/DragonMMA/Scripts/`

## 현재 첫 전투

초반 새끼용 전투는 20초의 관찰 → 단발 잽 → 용의 위협/가드 → 짧은 반격 → 결착 흐름입니다.
승인된 잽·피격 그림을 유지하고, 가드·항복 동작과 결과 화면의 체형·위치 연결을 보완했습니다.
승패·응원·보상은 기존 게임 세션 규칙이 판정합니다.

기획·레퍼런스·검수 범위: [첫 전투 작업 보고서](Docs/AI/Baby-First-Battle-20261008.md)

## 검증 상태

- EditMode 테스트: 217/217 통과.
- 첫 전투 1배속 Lab 재생 3회, 기존 BASIC JAB 10회 회귀 통과.
- Windows x86_64 Development 빌드: 오류 0, 경고 0.
- 해당 exe 실행·네이티브 창 수용 테스트, 새 WebGL 배포·모바일 실기기 검증은 미실시입니다.

`Library/`, `Temp/`, `Builds/`, `Artifacts/`와 사용자 저장 데이터는 버전 관리에 포함하지 않습니다.
로컬 QA 캡처와 빌드 실행 파일은 저장소에 업로드되지 않습니다.

## Git 이력 정리

2026-10-08 사용자 요청에 따라 현재 project08 소스·자산을 새 초기 커밋으로 정리했습니다.
이전 프로토타입 이력과 로컬 작업 커밋 `595ac70`, `93e02c3`은 로컬 백업 브랜치와
`Artifacts/GitReset-20261008/pre-reset.bundle`에 보존했습니다. 이 백업은 원격에 업로드하지 않습니다.

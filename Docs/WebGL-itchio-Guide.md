# itch.io WebGL 배포

## 산출물

- 빌드 폴더: `E:\unity_project\project08_dragon_mma_idle\webGL`
- 업로드 ZIP: `DragonMMAIdle-itchio.zip` (ZIP 최상위에 `index.html`, `Build/`, `Licenses/`)
- 브라우저용 씬: `Assets/DragonMMA/Scenes/WebGLScene.unity`
- 브라우저용 UI: `Assets/DragonMMA/Prefabs/UI/WebOverlayRoot.prefab`
- 웹 시작 화면: `Assets/WebGLTemplates/DragonItch/index.html`
- 재빌드 메뉴: `Dragon MMA > Build itch.io WebGL`

별도 웹 프리팹을 생성한 뒤에는 재빌드 시 다시 생성하지 않습니다. 화면은 해당 프리팹의 RectTransform·Text·Image를, 밸런스는 기존 `DefaultGameConfig.asset`을 수정합니다. PC용 씬과 UI는 별도로 유지합니다.

## itch.io 설정

1. 프로젝트 유형을 **HTML**로 지정하고 ZIP을 업로드합니다.
2. 업로드 파일의 **This file will be played in the browser**를 켭니다.
3. Embed 크기는 **960 × 540**, **Mobile Friendly**, **Fullscreen button**을 켭니다.
4. 모바일은 가로 화면으로 실행합니다. 전체 화면/가로 잠금이 지원되지 않는 브라우저는 기기를 직접 회전합니다.
5. 게시 전 itch.io의 실제 iframe에서 Android Chrome과 iPhone/iPad Safari를 각각 확인합니다.

공식 안내: https://itch.io/docs/creators/html5

## 조작과 저장

- 전투 영역을 터치/클릭하거나 Space로 응원합니다. 시설 UI 버튼을 눌러도 응원은 증가하지 않습니다.
- 시작 화면의 ‘플레이’를 누른 뒤 사냥이 진행됩니다.
- 브라우저가 숨겨지면 사냥은 정지합니다. 진행 중이던 판매·수련 타이머만 실제 경과 시간에 따라 완료됩니다.
- 저장은 Unity WebGL의 IndexedDB/IDBFS에 자동 동기화합니다. 종료 전 설정의 ‘지금 저장’을 사용할 수 있습니다.
- 저장은 기기·브라우저·사이트 주소별로 분리됩니다. 브라우저 데이터 삭제, 시크릿 모드, 저장 차단 환경에서는 유지되지 않을 수 있습니다.
- Unity 공식 문서에는 Safari의 iframe 내 IndexedDB 제한이 명시되어 있습니다. iOS에서는 itch.io 실제 페이지에서 저장 후 새로고침까지 반드시 확인해야 합니다. 로컬 Chromium 검증만으로 Safari의 저장 유지까지 보장하지 않습니다.
- PC의 바탕화면 투명 오버레이/항상 위 기능은 웹에서 지원하지 않습니다.

## 빌드 설정

Unity 6000.3.10f1 / WebGL / IL2CPP / Release. Gzip + JavaScript decompression fallback을 사용하므로 별도 Content-Encoding 설정 없이 `.unityweb` 파일을 제공할 수 있습니다. 스레드는 사용하지 않습니다. 초기 메모리는 64 MB, 최대 512 MB이며 터치 기기 캔버스 DPR은 1로 제한합니다.

`index.html`을 파일 탐색기에서 직접 여는 대신 HTTP 서버 또는 itch.io에서 실행해야 합니다. Unity 6.3의 모바일 브라우저 지원 조건: https://docs.unity3d.com/6000.3/Documentation/Manual/webgl-browsercompatibility.html

## 검증 결과 · 2026-10-02

- 최종 Unity Release 빌드: 성공, 오류 0 / 경고 0. 로그 `Artifacts/WebGL-20261002/build-release.log`.
- EditMode 테스트: 63개 통과, 실패 0. 결과 `Artifacts/WebGL-20261002/editmode-release-results.xml`.
- 로컬 Chromium 브라우저: 실제 WebGL 실행, 한글/픽셀 아트, 844×390 및 640×360 리사이즈, 시설 버튼, 전투 영역 클릭당 승률 +0.2%p 확인. 브라우저 콘솔 오류/경고 없음.
- 수레 귀환 → 수용소, 판매 등록 → 새로고침 → 완료/직접 수령, 수련방 80G 해금 → 배치 → 완료 후 유지 확인. 박치기용 완료 후 용핵 10 / 전투력 12를 확인했으며 새로고침 후에도 진행이 유지됨.
- ZIP: 6파일, 압축 해제 16,418,099바이트, ZIP 15,906,247바이트. 최상위 `index.html`, 상대 경로/대소문자 및 itch.io 용량 제한 확인.
- ZIP SHA-256: `EEDC743346EA0A368CC042E8E8F6BD5B4CFD3FF85B4995E14D14234C16B4BC76`.
- 실제 Android/iOS 기기, 실제 터치 입력/회전/노치, Safari 저장 유지, itch.io 업로드 및 실제 호스팅은 아직 검증하지 않았습니다. 모바일 크기 확인은 데스크톱 브라우저의 viewport를 사용했으며 터치 기기 에뮬레이션은 아닙니다.

MCP에 연결된 에디터가 없어 원본 에디터를 닫지 않고 `Artifacts/WebGLBuildWorkspace-20261002`의 프로젝트 복사본을 Unity 배치 실행으로 빌드했습니다. 최초 임포트의 긴 패키지 경로 오류와 중간 재빌드의 Bee 후처리 중단은 별도 로그에 남겨 두었으며, 재시도 후 최종 빌드와 실행은 성공했습니다. 원본 PC 씬/프리팹, 밸런스 에셋, 기존 PC 저장 파일은 변경하지 않았습니다.

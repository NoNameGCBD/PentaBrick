# PentaBrick Arcade HUD

## 구현 범위

이 저장소는 Unity UI Toolkit/URP를 WebGL로 빌드하는 게임이다. DOM/CSS를 별도로 얹지 않고 기존 입력과 렌더링 경로 안에서 HUD를 구성했다. Core와 Game 파일은 변경하지 않는다.

- 웹 템플릿도 100vw/100dvh 캔버스를 사용하며 기본 Unity 푸터 대신 게임형 로딩 화면을 제공한다. 키보드 포커스, 로딩 실패 메시지, reduced-motion CSS를 포함한다.
- 카메라와 scene-surface는 전체 화면을 사용한다. HUD 바깥의 컨테이너는 PickingMode.Ignore라서 빈 공간에서 클릭/드래그/줌을 방해하지 않는다.
- 상단: 실제 사용량/문제 목표량 게이지, 타이머/시간 보너스 기준, 점수, 실제 높이에 따른 층 토글.
- 좌측: BACK / LEFT-TOP-RIGHT / FRONT 청사진 전개도. 세로 화면에서는 다섯 카드가 수평으로 놓인다. 기존 평면도 좌표와 생성 결과는 유지한다.
- 완료 피드백: 기존 연습 힌트 설정이 켜진 문제에만 OK 배지와 녹색 테두리를 표시한다. 점수 모드의 힌트 정책은 바꾸지 않는다.
- 하단: 색별 남은 블록, 숫자 단축키, Q/W/E 도구, 되돌리기/다시 실행, R 초기화(확인 창), 카메라 복귀, 불가능 선언, 정답 확인.
- 우측 상단: 새 문제(확인 후 생성), 공유(코드/링크 복사), 게임 방법과 튜토리얼 연결. 창이 열리면 3D 편집 입력이 잠긴다.
- 층 보기: 선택한 층의 블록만 렌더러와 충돌체를 표시한다. 다른 층의 블록은 숨기며 바닥 격자는 좌표 기준으로 남긴다. 숨긴 층의 블록, 투영, 개수와 실행 취소 기록은 그대로다. 선택한 층에 쌓을 수 있는 기둥에만 새 블록을 놓으며, 숨긴 층으로의 배치는 막는다. 새 문제와 정답 보기에서는 전체 층으로 복귀한다.
- 성공: STAGE CLEAR!, 색 블록 장식, confetti, 소요 시간/블록 수/높이, 점수, 다음 문제 및 생성 문제 공유. 오답은 TRY AGAIN과 감쇠하는 shake를 사용한다. 기존 정답/불가능 판정과 점수는 그대로다.
- 배경: URP ArcadeBackdrop 셰이더로 #1e293b에서 #0f172a로 가는 라디얼 그라데이션을 그린다. 카메라 종횡비에 따라 세로 작업대 거리를 보정한다.

## Unity에 맞춘 효과

CSS의 box-shadow는 버튼의 얇은 테두리·상단 하이라이트·낮은 투명도의 곡선으로 퍼지는 그림자로, glow는 반투명 외곽 링으로 구현했다. 버튼 hover/active는 USS translate/scale을 사용한다. 반투명 패널의 그라데이션은 런타임 Texture2D 한 장을 공유한다.

UI Toolkit에는 CSS backdrop-filter가 없으므로 실제 화면을 샘플링하는 12px 배경 blur는 구현하지 않았다. 패널은 반투명 그라데이션 유리 표면으로 대체했다. 실시간 blur가 필요하면 별도 URP 패스와 UI 합성을 추가해야 한다.

## 글꼴 및 라이선스

외부 CDN에 접속해야만 게임이 보이는 구조를 피하기 위해 공식 글꼴을 Resources/Fonts에 포함했다.

- 타이틀: [Fredoka, Google Fonts](https://github.com/google/fonts/tree/main/ofl/fredoka), weight 600 / width 100의 정적 인스턴스.
- 숫자: [Space Grotesk, Google Fonts](https://github.com/google/fonts/tree/main/ofl/spacegrotesk), weight 500의 정적 인스턴스.
- 한글/UI: [Pretendard 공식 v1.3.9 배포](https://github.com/orioncactus/pretendard/releases/tag/v1.3.9), Regular.

Pretendard는 Google Fonts 대신 제작자 공식 배포에서 받았다. 각 라이선스 파일을 글꼴과 함께 포함했고, 리소스 로드 실패 시 기존 Noto Sans KR를 유지한다. 가변 글꼴은 fontTools로 정적 인스턴스를 만들었다.

## 검증 경로

- 순수 로직: dotnet test tools/dotnet-tests (81개).
- Unity PlayMode: 26개 모두 통과. 기존 테스트와 함께 전체 카메라 영역, HUD 빈 공간 입력, 새 문제/도움말/공유 모달, 층 전환 시 모형/평면도 불변성, 렌더러/충돌체 동기화, 세로 시점 작업대 프레이밍을 검사한다.
- 테스트 결과: /tmp/pentabrick-layer-playmode.xml.
- 화면 캡처: Logs/ui-game-hard.png, ui-portrait-game.png, ui-result.png, ui-result-wrong.png, ui-tutorial.png.
- 로컬 WebGL 빌드: PentaBrick > 빌드 > WebGL 또는 PentaBrick.EditorTools.Build.WebGL. 결과는 Builds/WebGL.

## 최종 실행 결과

- 순수 로직 81개, Unity PlayMode 26개 모두 통과.
- Core/Game 변경 없음 확인.
- 전체 화면 커스텀 템플릿을 포함한 WebGL 빌드 성공(약 14MB).
- 생성된 HTML 매크로 처리, 100dvh 레이아웃, JavaScript 문법 확인.
- Safari에서 로컬 WebGL 페이지 및 게임 캔버스 로딩 확인. 브라우저 자동 클릭 검증은 Chrome 상태 변경 오류와 macOS 화면 캡처 오류로 완료하지 못했다. 시각 검증은 Unity PlayMode 렌더링 캡처를 사용했다.
- 로컬 미리보기: http://127.0.0.1:8765/ (미리보기 서버 실행 중일 때).

### 층별 단독 표시 수정

1·2·3층 선택 시 그 층의 블록만 표시하고 다른 층의 렌더러와 충돌체를 숨긴다. 바닥 격자는 유지한다. 층 선택, 편집/실행 취소, 전체 복귀를 포함한 PlayMode 26개 통과 및 WebGL 재빌드 성공. 빌드 로그: /tmp/pentabrick-layer-webgl.log.

### 버튼 그림자 및 격자 방향 표시

버튼 아래의 두꺼운 검은 테두리를 제거하고 퍼지는 그림자로 교체했다. 격자 네 가장자리에 앞·뒤·좌·우 표시를 추가했다. 방향 표시는 카메라 회전에 따라 격자 가장자리를 따라가며 입력을 통과시킨다.

2026-11-19 검증: PlayMode 27개 통과. 방향 표시의 시점별 좌우/앞뒤 관계와 입력 통과, 세로 화면 버튼 크기를 확인하고 캡처를 검토했다. 웹·macOS Universal·Windows 64비트 빌드 갱신 성공. 결과 로그: /tmp/pentabrick-polish-builds.log. 화면: Logs/ui-grid-directions.png.

### 인게임 정보 정리 (2026-11-19)

게임 중에는 플레이에 필요한 정보만 남겼다. 상단 캡슐의 PENTA BRICK 로고와 부제, 청사진 패널의 제목·안내 문구, 하단 도크의 영문 묶음 제목, 작업대 아래의 크기·문제 이름·조작 안내 줄을 없앴다. 문제 이름은 일시정지 창에, 조작법은 게임 방법 창에 그대로 있다. 도크 묶음은 얇은 구분선으로 나눈다. PlayMode 27개 통과. 격자 방향 표시는 청사진 카드와 같이 FRONT/BACK/LEFT/RIGHT 영문으로, 펼친 평면도 제목도 "FRONT · 목표"처럼 맞췄다.

### 프로토타입 수정방안 반영 (2026-11-19)

- 판정: 다섯 평면도 일치 + 블록 총수 일치. 색별 개수는 자유(문제의 색만 고를 수 있음). 불가능 문제의 "블록 수 모순"은 총수를 1~2개 바꿔 만들고, 풀이기로 해가 없음을 확인한다. 샘플 p5는 총수 7(해는 6개뿐)로 바꿨다.
- 색 칩의 남은 개수 표시 삭제. 놓기는 총수까지만.
- 블록을 하나라도 놓으면 제출 가능. 오답은 감점 없이 결과 창에서 [다시 도전](모형·실행 취소 기록 유지, 결과 창 시간 제외) 또는 [답 확인하기].
- 답 확인하기: 문제를 마치고 아래 띠에서 [정답 모형 보기] / [내가 배치한 모형 보기]를 전환. 점수 변화 없음. 불가능 문제는 정답 대신 이유를 보여 준다. 틀린 불가능 선언(−2)은 다시 도전 없이 답 확인만 가능.
- 써야 하는 블록 수를 평면도 패널(가로: 전개도 왼쪽 위, 세로: 카드 줄 맨 앞)에 크게 표시. 상단 바의 블록 카운터는 제거.
- 평면도 카드 축소(가로 80→64px, 세로 78→54px), 펼친 평면도 칸 28→22px.
- 칠하기 도구와 W 단축키 삭제.
- 지우기: 선택한 블록과 그 위에 쌓인 블록을 모두 지운다.
- 높이: 격자 위 최대 높이에 얇은 상자 테두리(상한선)를 그린다. 꽉 찬 칸이나 총수를 넘겨 놓으려 하면 경고 문구를 2초간 띄운다.
- 검증: 순수 로직 81개, PlayMode 28개 통과. 화면: Logs/ui-tray.png, ui-portrait-game.png, ui-result-wrong.png, ui-result-answer.png, ui-height-limit.png.

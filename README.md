# PentaBrick (Unity)

웹 프로토타입(저장소 루트)을 Unity로 옮기는 졸업작품 프로젝트입니다.
계획: [docs/개발계획서.md](../docs/개발계획서.md) · 명세: [docs/기획명세서.md](../docs/기획명세서.md)

## 여는 방법

1. Unity Hub에 로그인해 라이선스(Personal)를 활성화합니다.
2. Unity Hub > Add > Add project from disk 에서 이 `unity` 폴더를 고릅니다. 에디터 버전은 6000.6.1f1 입니다.

## 테스트

- Unity: Window > General > Test Runner > EditMode / PlayMode > Run All
  (PlayMode 테스트는 실제 씬에서 메뉴를 거쳐 블록을 쌓고, 각 화면을 `Logs/ui-*.png`로 저장합니다)
- 명령줄(Unity 없이, Core 로직만): `dotnet test unity/tools/dotnet-tests`

두 방법은 `Assets/PentaBrick/Tests`의 같은 테스트 소스를 씁니다.

## 구조

```
Assets/PentaBrick/
  Core/    순수 C# 로직 (UnityEngine 참조 없음): 모형, 평면도, 판정, 샘플 문제, 생성기
  Game/    순수 C# 게임 상태: 액션, 실행 취소, 제출 판정, 타이머, 점수
  View/    3D 블록 에디터와 화면 UI (플레이를 누르면 스스로 만들어짐, 씬 배치 불필요)
           화면 흐름: 인트로 → (아무 버튼) → 메인 → 게임 시작 → 난이도 선택 → 게임 (평면도는 상단 띠)
           게임 중 Esc: 메뉴(새 문제, 코드 복사, 난이도 변경, 설정, 메인 메뉴)
  Tests/   EditMode 테스트 (로직)
  PlayTests/  PlayMode 테스트 (3D 에디터)
tools/dotnet-tests/   명령줄 테스트용 프로젝트 (Assets의 소스를 링크)
```

같은 난이도와 seed는 웹 프로토타입과 같은 문제를 만듭니다(테스트로 고정).

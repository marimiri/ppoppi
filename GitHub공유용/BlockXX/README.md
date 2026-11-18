# Block XX

동아리 기획 스터디용 Unity 3D 공간 퍼즐·육성 시뮬레이션 프로토타입입니다.

- Unity: **6000.3.24f1**
- 시작 씬: `Assets/Scenes/BlockXX.unity`
- 실행 파일 만들기: Unity 메뉴 `Block XX > Build Windows`
- 조작과 구현 규칙: [조작안내.txt](조작안내.txt)

## 주요 코드

| 파일 (`Assets/Scripts/`) | 역할 |
| --- | --- |
| `BlockXXPrototype.cs` | 입력, 카메라, 보드 표시, 배치, UI |
| `BlockXXAwareness.cs` | 전의식·무의식 분류와 부하 가중치, 약 설정 |
| `BlockXXRoutes.cs` | 루트 목표, 주차 조건, 성공·실패, 교신 지급량 |
| `BlockXXWeekResolution.cs` | 휴식 상쇄, 부하 제거, 정산 이펙트 |
| `BlockXXReading.cs` | 독서 루트의 입체 공간 중첩 |
| `BlockXXDreams.cs` | 랜덤 꿈꾸기 배치 |
| `BlockXXRouteProgress.cs` | 루트 조건 현황 표시 |

클래스는 `partial class BlockXXPrototype`으로 나뉘어 있습니다. 자동 검사는 `*Checks.cs`에 있으며, 일반 플레이 때는 실행되지 않습니다.

현재는 초상화 아래 버튼으로 루트를 선택해 체험합니다. 저장 기능과 정식 엔딩 CG는 포함하지 않습니다.

`.meta` 파일은 대응하는 에셋과 함께 관리합니다. Unity가 다시 생성하는 `Library`, `Temp`, `Logs`와 실행 빌드, 로컬 백업은 Git 관리에서 제외합니다.

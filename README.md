# Protocol Aer

5×5 평면에 미노를 배치하고, 겹침과 효과로 완성 형태의 설계 원리를 찾는 Unity 퍼즐 프로토타입입니다.

![게임 화면](docs/Gameplay.png)

**[Windows 실행판 다운로드](https://github.com/marimiri/ppoppi/releases/latest)**

## 실행하기

Windows 배포 ZIP을 **전체 압축 해제**한 뒤 `ProtocolAer.exe`를 실행하세요. 실행 파일 옆의 `ProtocolAer_Data`, `UnityPlayer.dll`, `MonoBleedingEdge`도 함께 있어야 합니다.

소스만 내려받았다면 아래의 Unity 실행 방법을 이용하세요.

## 플레이 구성

- 선택형 튜토리얼 3개
- 레이더 지도에서 명칭 찾기 4개 → 일조 복원 → 명칭 찾기 4개 → 강수 복원 → 종료
- 명칭은 복원도 100%, 날씨는 85% 이상이 필요합니다. 새 미노를 소개하는 명칭 스테이지에서는 해당 효과도 활용해야 합니다.
- 미노 개수는 무제한입니다. 저장 기능은 없으며 종료하면 진행이 초기화됩니다.

| 미노 | 등장 | 효과 |
|---|---|---|
| O | 처음부터 | +1 |
| L | 명칭 3지역 | +1 |
| I | 명칭 5지역 | +1 |
| S | 명칭 7지역 | 덮은 네 칸을 그중 최솟값으로 통일 |
| T | 명칭 8지역 | 덮은 네 칸을 그중 최댓값으로 통일 |

O/L/I를 겹쳐 만든 높이가 모든 효과의 기준입니다. S/T는 자체 높이 +1을 만들지 않고, **자신이 덮는 네 칸만** 바꿉니다. 빈 칸의 높이 0도 최솟값·최댓값에 포함합니다. **S와 T가 함께 덮은 칸은 효과 적용 전 원래 높이를 유지**합니다. 계산은 배치 순서와 무관합니다. 같은 종류끼리 겹치면 S는 후보 최솟값 중 더 낮게, T는 후보 최댓값 중 더 높게 맞춥니다. 효과끼리 연쇄 계산하지 않습니다.

급수 관리소는 O 3개·L 1개·I 1개로 구성하고 I 하나를 세로로 회전하는 과제를 남겼습니다. 앞 기록 보관소보다 일반 미노가 하나 늘지만, 기존 급수소의 여러 L/I 회전 조합은 줄였습니다. 바람길은 O·L·S만 제공해 최솟값 통일을, 환경 제어실은 O·T만 제공해 최댓값 통일을 익힙니다(각 예시 4개). 바람길은 O 2개·L 1개·S 1개로 100% 복원이 가능하여 I를 제외했습니다. S 소개에서는 T를 미리 언급하지 않으며, 공통 칸 보존 규칙은 T 소개에서 처음 안내합니다. 마지막 강수 복원은 두 효과와 공통 칸의 원래 높이 보존을 함께 활용합니다(예시 6개). 클리어는 기존대로 복원도 85%이며, 별도 효과 사용 조건은 없습니다. 튜토리얼·명칭 1~5·첫 날씨는 그대로입니다.

2D 칸의 빨간 짧은 선은 원래보다 낮아진 높이, 노란 두 줄은 높아진 높이입니다. T로 새로 생긴 입체 층은 연노랑으로 표시됩니다.
## 조작

- 오른쪽 미노 클릭 → 2D 보드 위에서 예상 결과 확인 → 클릭해서 배치
- 놓을 수 없는 칸이나 보드 밖 빈 공간 클릭: 들고 있는 미노 반환
- 이미 놓인 미노 클릭: 집어서 이동
- Q/E/R: 회전 · F: 대칭 · 우클릭/Esc: 반환 · Ctrl+Z: 되돌리기
- 3D 화면 드래그/WASD: 가로·세로 360도 시점 회전 (위·아래 끝까지 회전 가능) · 마우스 휠: 확대·축소
- 하늘색 보드 모서리: 2D 보드 아래쪽에 대응하는 고정 기준선
- 실행: 복원 판정. 성공한 경우에만 3D 결과가 불투명하게 채워집니다.

힌트는 현재 배치와 예시 해법을 비교합니다. **금색은 놓을 위치와 클릭 기준 칸**, **빨간색은 먼저 옮기거나 반환할 미노**입니다. 놓기 힌트에서는 종류와 방향도 자동으로 맞춰 줍니다. 힌트만 누르는 것으로 배치가 확정되거나 제거되지는 않습니다.

## Unity에서 열기

1. Unity Hub에 **Unity 6000.3.24f1**과 Windows Build Support를 설치합니다.
2. 이 폴더를 프로젝트로 추가합니다. `Assets`, `Packages`, `ProjectSettings`가 같은 위치에 있어야 합니다.
3. `Assets/Scenes/Main.unity`를 열고 Play를 누릅니다.

글꼴·사진·셰이더는 프로젝트에 포함되어 있습니다. 별도로 구매하거나 설정할 에셋은 없습니다.

## Windows 빌드

Unity 상단 **Protocol Aer → Build Windows**를 선택합니다. 결과는 `Builds/ProtocolAer.exe`에 생성됩니다.

에디터를 닫고 PowerShell에서 빌드할 수도 있습니다.

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\BuildWindows.ps1
```

자동 검증을 재실행할 때는 다음 명령을 사용합니다. 게임 창이 열리고 완료 후 자동 종료됩니다.

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools\VerifyWindows.ps1
```

## 구성

- `Assets/Scripts/ProtocolAer.cs`: 화면, 입력, 진행, 3D 표시
- `Assets/Scripts/EveningBoard.cs`: 미노 배치와 높이 계산
- `Assets/Scripts/AerStages.cs`: 스테이지와 복원 판정
- `Assets/Scripts/AerHints.cs`: 현재 배치 기반 힌트
- `Assets/Scripts/AerTargetOutline.cs`: 블록 이음새를 제외한 입체 표면 모서리
- `Assets/Editor/AerBuilder.cs`: 해법 검사 및 Windows 빌드
- [검증 결과](docs/검증결과.txt) · [개발 상세](docs/Development.md) · [조작 안내](조작안내.txt)

`Library`, `Temp`, `Logs`, `Builds`는 Git에 포함하지 않습니다. `.meta` 파일은 에셋 연결에 필요하므로 함께 올려야 합니다. 개발용 원본은 이 저장소에, Windows 실행판은 [Releases](https://github.com/marimiri/ppoppi/releases/latest)에 있습니다.

## 글꼴

NeoDunggeunmo와 Rock Salt를 사용합니다. 원본 라이선스는 `Assets/Resources/Fonts/Pixel-OFL.txt`, `Assets/Resources/Fonts/ChalkHand-License.txt`에 포함되어 있습니다.



https://github.com/user-attachments/assets/f0f1ccc0-6f73-4bd5-9460-35c77709b0e7

# 비전 위치보정 레이저 마킹 시스템 (Vision-Guided Laser Marking)

3개 프로젝트로 구성됩니다.

```
VisionLaserMarkingSolution/
├── VisionLaserMarking.Core/      # 카메라/보드에 의존하지 않는 핵심 로직 (class library)
│   ├── Models/                   # Point2D, DetectedPart
│   ├── Vision/PartDetector.cs    # OpenCvSharp4 기반 부품 위치/각도 검출
│   ├── Calibration/CoordinateCalibrator.cs   # px<->mm 어파인 변환
│   ├── Laser/                    # ILaserController, SimulatedLaserController, JczLaserController
│   ├── Trigger/ModbusConveyorTrigger.cs      # RS-485 Modbus RTU 포토센서 트리거
│   └── Pipeline/MarkingPipeline.cs
├── VisionLaserMarking.Console/   # 콘솔 데모 (이전에 드렸던 것과 동일, Core 참조로 변경)
└── VisionLaserMarking.Wpf/       # 이번에 추가한 WPF 화면 - 검출 시각화 + 수동/자동 마킹 UI
    ├── MainWindow.xaml / .cs
    ├── SceneGenerator.cs          # 카메라 없이 가상 부품 배치를 OpenCvSharp Mat으로 렌더링
    └── WpfLogLaserController.cs   # ILaserController를 WPF 화면용으로 구현
```

Core를 분리한 이유는 단순합니다 - 검출 알고리즘과 캘리브레이션, 레이저 컨트롤러 인터페이스는 콘솔이든
WPF든 똑같이 써야 하는 부분이라, 한 군데서만 관리하고 두 화면이 그걸 참조하는 구조로 바꿨습니다.

## WPF 화면 동작 방식

`SceneGenerator`가 부품을 무작위 위치/각도로 배치한 뒤 OpenCvSharp의 `Cv2.FillConvexPoly`로
그 배치를 `Mat`에 직접 그립니다. 이 `Mat`이 그대로 "카메라가 찍은 영상" 역할을 하고,
`OpenCvSharp4.WpfExtensions`의 `ToBitmapSource()`로 화면에 띄웁니다. **중요한 건 이 영상 위에 그려진
부품을, 화면에 보여주기용으로 따로 만든 게 아니라 실제 `PartDetector`(지난번 콘솔 버전과 동일한 클래스)가
그대로 픽셀을 읽어서 검출한다는 점**입니다 - 즉 "보이는 것"과 "검출되는 것"이 진짜로 같은 데이터입니다.

버튼별 동작:
- **새 배치**: 부품을 다시 무작위로 흩어놓습니다 (슬라이더로 3~8개 조절).
- **경계 검출**: `PartDetector.Detect()`를 실제로 호출해서 이진화 + 연결요소 분석으로 각 부품의
  중심점과 회전각을 구하고, 그 결과를 초록 박스(경계) + 빨간 십자(중심점) + 각도 라벨로 캔버스에 그립니다.
- **전체 자동 마킹**: 검출된 부품을 x좌표 순(컨베이어 진행 순서)으로 하나씩 가상으로 각인합니다.
  내부적으로 `CoordinateCalibrator.PixelToMachine()`으로 mm 좌표를 구하고 `ILaserController.MarkAt()`을
  호출하는 흐름까지 실제 콘솔 버전과 동일합니다 - 다만 마킹 결과를 콘솔 출력 대신
  `WpfLogLaserController`의 이벤트로 받아서 화면 로그에 표시합니다.
- **수동 마킹**: 켜놓고 화면을 클릭하면 그 자리에 바로 가상으로 마킹됩니다.

검출 결과/마킹 로그 패널에는 픽셀좌표와 함께 캘리브레이션을 통과한 mm 좌표도 같이 표시되어,
지난번 콘솔 프로젝트의 캘리브레이션 개념과 바로 이어집니다.

## 빌드 / 실행

```bash
cd VisionLaserMarkingSolution
dotnet new sln -n VisionLaserMarking
dotnet sln add VisionLaserMarking.Core/VisionLaserMarking.Core.csproj
dotnet sln add VisionLaserMarking.Console/VisionLaserMarking.Console.csproj
dotnet sln add VisionLaserMarking.Wpf/VisionLaserMarking.Wpf.csproj
dotnet build

cd VisionLaserMarking.Wpf
dotnet run
```

WPF 프로젝트는 `net8.0-windows`라서 Windows에서만 빌드/실행됩니다. 콘솔 프로젝트는 OS 무관합니다.

## 실물 장비 연동 시

`VisionLaserMarking.Wpf/WpfLogLaserController.cs`를 `VisionLaserMarking.Core/Laser/JczLaserController.cs`로
바꿔 끼우면 됩니다. 인터페이스(`ILaserController`)가 같아서 `MainWindow.xaml.cs`는 거의 손댈 게 없고,
`JczLaserController` 안의 `MoveCurrentEntityTo()` TODO만 실제 보드 SDK 헤더 보고 채우면 됩니다
(자세한 내용은 그 파일 주석 참고). 카메라도 마찬가지로 `SceneGenerator` 대신
`OpenCvSharp.VideoCapture`로 실제 프레임을 받아서 `PartDetector.Detect()`에 넘기면 됩니다.

## 한계

이 샌드박스는 nuget.org 접근이 막혀 있어서 실제 `dotnet build`로 컴파일 테스트는 못 해봤습니다.
문법, 네이밍, XAML x:Name과 code-behind 참조 일치 여부는 직접 점검했지만, 로컬에서 빌드했을 때
패키지 버전 차이로 인한 오류가 있으면 알려주세요.

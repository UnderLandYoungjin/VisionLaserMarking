using System;
using System.Collections.Generic;
using System.IO;
using OpenCvSharp;
using VisionLaserMarking.Calibration;
using VisionLaserMarking.Laser;
using VisionLaserMarking.Models;
using VisionLaserMarking.Vision;

namespace VisionLaserMarking.Pipeline
{
    /// <summary>
    /// 트리거 -&gt; 프레임 캡처 -&gt; 부품 검출 -&gt; 좌표변환 -&gt; 마킹 까지의 전체 흐름.
    /// </summary>
    public class MarkingPipeline
    {
        private readonly VideoCapture? _camera;
        private readonly PartDetector _detector;
        private readonly CoordinateCalibrator _calibrator;
        private readonly ILaserController _laser;

        private string[] _testImages = Array.Empty<string>();
        private int _testImageIndex;

        public int ProducedCount { get; private set; }

        /// <summary>실제 카메라를 사용하는 생성자</summary>
        public MarkingPipeline(VideoCapture camera, PartDetector detector,
            CoordinateCalibrator calibrator, ILaserController laser)
        {
            _camera = camera;
            _detector = detector;
            _calibrator = calibrator;
            _laser = laser;
        }

        /// <summary>카메라 없이 저장된 이미지로 파이프라인을 검증하는 생성자 (시뮬레이션용)</summary>
        public MarkingPipeline(string testImageFolder, PartDetector detector,
            CoordinateCalibrator calibrator, ILaserController laser)
        {
            _detector = detector;
            _calibrator = calibrator;
            _laser = laser;
            _testImages = Directory.Exists(testImageFolder)
                ? Directory.GetFiles(testImageFolder, "*.jpg")
                : Array.Empty<string>();

            if (_testImages.Length == 0)
                Console.WriteLine(
                    $"[안내] '{testImageFolder}' 폴더에 .jpg 테스트 이미지가 없습니다. " +
                    "어두운 배경 위에 놓인 부품 사진을 넣으면 실제 검출 동작을 확인할 수 있습니다.");
        }

        /// <summary>트리거 1회(부품 도착)에 대응하는 처리. 컨베이어 센서 이벤트에서 호출.</summary>
        public void OnPartArrivedTrigger()
        {
            using Mat frame = CaptureFrame();
            if (frame.Empty())
            {
                Console.WriteLine("[경고] 프레임을 가져오지 못했습니다.");
                return;
            }

            List<DetectedPart> parts = _detector.Detect(frame);
            Console.WriteLine($"검출된 부품 수: {parts.Count}");

            foreach (DetectedPart part in parts)
            {
                Point2D machineXY = _calibrator.PixelToMachine(part.CenterPx);
                double rotationOffset = _calibrator.GetCameraToMachineRotationDeg();
                double markAngle = part.AngleDeg + rotationOffset;

                Console.WriteLine($"  -> {part}  =>  기계좌표 {machineXY}, 마킹각도 {markAngle:F1}\u00b0");
                _laser.MarkAt(machineXY.X, machineXY.Y, markAngle);
                ProducedCount++;
            }
        }

        private Mat CaptureFrame()
        {
            if (_camera != null)
            {
                var frame = new Mat();
                _camera.Read(frame);
                return frame;
            }

            if (_testImages.Length == 0)
                return new Mat();

            string path = _testImages[_testImageIndex % _testImages.Length];
            _testImageIndex++;
            Console.WriteLine($"[테스트 이미지] {Path.GetFileName(path)}");
            return Cv2.ImRead(path, ImreadModes.Color);
        }
    }
}

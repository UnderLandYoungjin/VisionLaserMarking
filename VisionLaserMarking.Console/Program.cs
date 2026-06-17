using System;
using System.Threading;
using VisionLaserMarking.Calibration;
using VisionLaserMarking.Laser;
using VisionLaserMarking.Models;
using VisionLaserMarking.Pipeline;
using VisionLaserMarking.Trigger;
using VisionLaserMarking.Vision;

namespace VisionLaserMarking
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine("=== 비전 위치보정 레이저 마킹 데모 ===");

            // 1) 캘리브레이션: 카메라 픽셀좌표 <-> 레이저(기계) 좌표 대응점 입력.
            //    실제로는 캘리브레이션 지그(점/격자 패턴)를 카메라로 찍어 픽셀좌표를 얻고,
            //    같은 점들을 저전력 레이저 포인팅으로 직접 찍어서 기계좌표(mm)를 기록해 매칭한다.
            //    아래 4점은 예시값이며, 실제 환경에서는 9점 이상을 권장한다.
            var calibrator = new CoordinateCalibrator();
            calibrator.Calibrate(new[]
            {
                (Pixel: new Point2D(120, 100), Machine: new Point2D(-40.0, 30.0)),
                (Pixel: new Point2D(820, 100), Machine: new Point2D(40.0, 30.0)),
                (Pixel: new Point2D(120, 700), Machine: new Point2D(-40.0, -30.0)),
                (Pixel: new Point2D(820, 700), Machine: new Point2D(40.0, -30.0)),
            });
            Console.WriteLine($"카메라-기계 회전 오프셋: {calibrator.GetCameraToMachineRotationDeg():F2}\u00b0");

            // 2) 비전 검출기
            var detector = new PartDetector
            {
                DarkBackground = true,
                MinAreaPx = 500
            };

            // 3) 레이저 컨트롤러 - 실제 보드 연결 전까지는 시뮬레이터 사용
            ILaserController laser = new SimulatedLaserController(markDelayMs: 120);
            laser.Connect();

            // 실제 보드 사용 시 (Laser/JczLaserController.cs의 안내 주석 참고):
            // ILaserController laser = new JczLaserController(@"C:\EzCad2", @"C:\Templates\part_mark.ezd");
            // laser.Connect();

            // 4) 파이프라인 구성 - 테스트 이미지 폴더 모드 (카메라 없이 동작 검증용)
            //    실제 카메라 사용 시: using var cam = new OpenCvSharp.VideoCapture(0);
            //                       var pipeline = new MarkingPipeline(cam, detector, calibrator, laser);
            string testImageFolder = args.Length > 0 ? args[0] : "./test_images";
            var pipeline = new MarkingPipeline(testImageFolder, detector, calibrator, laser);

            // 5) 컨베이어 트리거 - 실제 RS-485 포토센서 연동 (포트/주소는 환경에 맞게 수정)
            //    트리거 하드웨어가 아직 없다면 false로 두고 모의 트리거로 흐름을 확인할 수 있다.
            bool useRealTrigger = false;

            if (useRealTrigger)
            {
                using var trigger = new ModbusConveyorTrigger("COM3", 9600, slaveId: 1, inputAddress: 0);
                trigger.PartArrived += pipeline.OnPartArrivedTrigger;
                trigger.Start();
                Console.WriteLine("Modbus 트리거 대기 중... (Ctrl+C로 종료)");
                Console.CancelKeyPress += (_, _) => trigger.Stop();
                Thread.Sleep(Timeout.Infinite);
            }
            else
            {
                Console.WriteLine("[모의 트리거 모드] 1초마다 부품 도착을 시뮬레이션합니다.");
                for (int i = 0; i < 5; i++)
                {
                    Thread.Sleep(1000);
                    Console.WriteLine($"\n--- 트리거 #{i + 1} ---");
                    pipeline.OnPartArrivedTrigger();
                }
            }

            laser.Disconnect();
            Console.WriteLine($"\n총 생산 수량: {pipeline.ProducedCount}");
        }
    }
}

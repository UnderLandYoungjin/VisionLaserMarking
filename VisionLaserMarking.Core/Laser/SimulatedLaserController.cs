using System;
using System.Threading;

namespace VisionLaserMarking.Laser
{
    /// <summary>
    /// 실제 레이저 보드 없이 비전 -&gt; 좌표변환 -&gt; 마킹 파이프라인 전체를
    /// 검증하기 위한 시뮬레이터. 콘솔에 마킹 좌표/각도를 출력하고
    /// 실제 마킹 소요시간과 비슷한 만큼만 대기한다.
    /// </summary>
    public class SimulatedLaserController : ILaserController
    {
        private readonly int _markDelayMs;
        public bool IsBusy { get; private set; }

        public SimulatedLaserController(int markDelayMs = 150)
        {
            _markDelayMs = markDelayMs;
        }

        public bool Connect()
        {
            Console.WriteLine("[SIM] 레이저 컨트롤러 연결됨 (시뮬레이션 모드)");
            return true;
        }

        public void Disconnect() =>
            Console.WriteLine("[SIM] 레이저 컨트롤러 연결 해제");

        public void MarkAt(double offsetXmm, double offsetYmm, double rotationDeg)
        {
            IsBusy = true;
            Console.WriteLine(
                $"[SIM] 마킹 실행 -> X={offsetXmm:F3}mm, Y={offsetYmm:F3}mm, 회전={rotationDeg:F2}\u00b0");
            Thread.Sleep(_markDelayMs);
            IsBusy = false;
        }
    }
}

using System;
using VisionLaserMarking.Laser;

namespace VisionLaserMarking.Wpf
{
    /// <summary>
    /// 콘솔 출력 대신 이벤트로 마킹 결과를 알려주는 ILaserController 구현.
    /// 실제 보드를 연결할 때는 이 클래스를 VisionLaserMarking.Laser.JczLaserController로
    /// 그대로 교체하면 된다 (인터페이스가 같아 MainWindow 쪽 코드는 거의 안 바뀐다).
    /// </summary>
    public class WpfLogLaserController : ILaserController
    {
        public event Action<double, double, double>? Marked;
        public bool IsBusy { get; private set; }

        public bool Connect() => true;
        public void Disconnect() { }

        public void MarkAt(double offsetXmm, double offsetYmm, double rotationDeg)
        {
            IsBusy = true;
            Marked?.Invoke(offsetXmm, offsetYmm, rotationDeg);
            IsBusy = false;
        }
    }
}

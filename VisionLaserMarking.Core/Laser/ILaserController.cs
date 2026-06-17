namespace VisionLaserMarking.Laser
{
    /// <summary>
    /// 갈바노 레이저 컨트롤러 추상화.
    /// 실제 하드웨어 연동 시 이 인터페이스를 구현한 클래스로 교체하면 된다.
    /// (金橙子/JCZ EzCad2, BJJCZ, SamLight 등 보드별 SDK를 이 인터페이스 뒤에 감싸서 사용)
    /// </summary>
    public interface ILaserController
    {
        bool Connect();
        void Disconnect();

        /// <summary>
        /// 현재 로드된 마킹 도안을 기준 위치에서 (offsetXmm, offsetYmm) 만큼 이동하고
        /// rotationDeg 만큼 회전시킨 뒤 1회 마킹을 실행한다. 마킹이 끝날 때까지 블로킹된다.
        /// </summary>
        void MarkAt(double offsetXmm, double offsetYmm, double rotationDeg);

        bool IsBusy { get; }
    }
}

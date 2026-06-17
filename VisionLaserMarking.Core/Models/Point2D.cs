namespace VisionLaserMarking.Models
{
    /// <summary>
    /// 단순 2D 좌표값. 픽셀 좌표(px)와 기계/레이저 좌표(mm) 양쪽에 모두 사용한다.
    /// 어떤 단위인지는 사용하는 쪽 컨텍스트(주석)로 구분.
    /// </summary>
    public readonly struct Point2D
    {
        public double X { get; }
        public double Y { get; }

        public Point2D(double x, double y)
        {
            X = x;
            Y = y;
        }

        public override string ToString() => $"({X:F2}, {Y:F2})";
    }
}

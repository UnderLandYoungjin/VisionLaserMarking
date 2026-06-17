namespace VisionLaserMarking.Models
{
    /// <summary>
    /// 카메라 영상에서 검출된 부품 1개의 정보 (픽셀 좌표계 기준).
    /// </summary>
    public class DetectedPart
    {
        /// <summary>부품 중심점 (픽셀)</summary>
        public Point2D CenterPx { get; set; }

        /// <summary>부품 장축 기준 회전각 (0~180도, 카메라 좌표계 기준)</summary>
        public double AngleDeg { get; set; }

        public double WidthPx { get; set; }
        public double HeightPx { get; set; }
        public double AreaPx { get; set; }

        public override string ToString() =>
            $"Center={CenterPx} Angle={AngleDeg:F1}\u00b0 Size={WidthPx:F0}x{HeightPx:F0}px Area={AreaPx:F0}";
    }
}

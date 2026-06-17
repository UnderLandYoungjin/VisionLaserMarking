using System;
using System.Collections.Generic;
using OpenCvSharp;
using VisionLaserMarking.Models;

namespace VisionLaserMarking.Vision
{
    /// <summary>
    /// 컨베이어 위에 무작위 위치/각도로 놓인 부품들을 검출한다.
    ///
    /// 방식: 단색 배경(컨베이어 벨트) 대비 부품의 명암차를 이용한 Otsu 이진화 +
    /// 모폴로지 연산 + 외곽선(Contour) 검출. 영상1처럼 어두운 배경에 밝은 부품,
    /// 또는 반대 조합 모두 DarkBackground 옵션으로 전환 가능하다.
    ///
    /// 부품 형태가 복잡하거나 표면 인쇄/로고가 섞여 배경과 대비가 약하면
    /// 이 방식보다 템플릿 매칭(예: HALCON, 海康 VisionMaster) 쪽이 더 안정적이다.
    /// 그 경우에도 이 클래스의 반환 타입(DetectedPart: 중심좌표+각도)은 그대로 재사용하면 된다.
    /// </summary>
    public class PartDetector
    {
        /// <summary>이 면적(px^2) 미만은 노이즈로 간주하고 무시</summary>
        public double MinAreaPx { get; set; } = 300;

        /// <summary>이 면적(px^2) 초과는 겹친 부품/오검출로 간주하고 무시</summary>
        public double MaxAreaPx { get; set; } = 200_000;

        /// <summary>true면 배경이 어둡고 부품이 밝음, false면 반대</summary>
        public bool DarkBackground { get; set; } = true;

        public List<DetectedPart> Detect(Mat frameBgr)
        {
            var result = new List<DetectedPart>();
            if (frameBgr.Empty())
                return result;

            using var gray = new Mat();
            Cv2.CvtColor(frameBgr, gray, ColorConversionCodes.BGR2GRAY);
            Cv2.GaussianBlur(gray, gray, new Size(5, 5), 0);

            using var bin = new Mat();
            ThresholdTypes thType = DarkBackground
                ? ThresholdTypes.Binary | ThresholdTypes.Otsu
                : ThresholdTypes.BinaryInv | ThresholdTypes.Otsu;
            Cv2.Threshold(gray, bin, 0, 255, thType);

            using var kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(5, 5));
            Cv2.MorphologyEx(bin, bin, MorphTypes.Open, kernel, iterations: 1);
            Cv2.MorphologyEx(bin, bin, MorphTypes.Close, kernel, iterations: 2);

            Cv2.FindContours(bin, out Point[][] contours, out _,
                RetrievalModes.External, ContourApproximationModes.ApproxSimple);

            foreach (Point[] cnt in contours)
            {
                double area = Cv2.ContourArea(cnt);
                if (area < MinAreaPx || area > MaxAreaPx)
                    continue;

                RotatedRect rect = Cv2.MinAreaRect(cnt);
                double angle = NormalizeAngle(rect.Angle, rect.Size);

                result.Add(new DetectedPart
                {
                    CenterPx = new Point2D(rect.Center.X, rect.Center.Y),
                    AngleDeg = angle,
                    WidthPx = Math.Max(rect.Size.Width, rect.Size.Height),
                    HeightPx = Math.Min(rect.Size.Width, rect.Size.Height),
                    AreaPx = area
                });
            }

            // 컨베이어 진행 방향(영상 X축) 기준으로 정렬해서 먼저 들어온 부품부터 순서대로 마킹
            result.Sort((a, b) => a.CenterPx.X.CompareTo(b.CenterPx.X));
            return result;
        }

        /// <summary>OpenCV MinAreaRect의 각도(-90~0)를 장축 기준 0~180도 범위로 정규화</summary>
        private static double NormalizeAngle(double angle, Size2f size)
        {
            double a = angle;
            if (size.Width < size.Height)
                a += 90;
            if (a < 0) a += 180;
            if (a >= 180) a -= 180;
            return a;
        }
    }
}

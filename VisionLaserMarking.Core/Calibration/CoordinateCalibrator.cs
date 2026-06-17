using System;
using System.Collections.Generic;
using VisionLaserMarking.Models;

namespace VisionLaserMarking.Calibration
{
    /// <summary>
    /// 카메라 픽셀좌표 -&gt; 레이저(갈바노) 기계좌표 변환.
    ///
    /// 실제 현장에서는 캘리브레이션 지그(점/격자 패턴)를 카메라로 촬영해 픽셀좌표를 얻고,
    /// 동일한 점들을 저전력 레이저 포인팅으로 직접 찍어 기계좌표(mm)를 기록한 뒤
    /// 그 대응점들을 이 클래스에 넣는다. (업계에서 흔히 "9점 캘리브레이션"이라고 부르는 작업과
    /// 동일한 개념 - 여기서는 점 개수를 자유롭게 늘릴 수 있도록 일반화했다.)
    ///
    /// 최소 3개의 대응점으로 2x3 어파인 변환(회전+스케일+이동, 약간의 비틀림까지)을
    /// 최소자승법으로 계산한다. 점이 많을수록 카메라 렌즈 왜곡/조립 오차가 평균화되어
    /// 더 안정적인 변환행렬이 나온다. (보통 9~16점 권장)
    /// </summary>
    public class CoordinateCalibrator
    {
        // x' = a*x + b*y + tx
        // y' = c*x + d*y + ty
        private double a, b, c, d, tx, ty;

        public bool IsCalibrated { get; private set; }

        public void Calibrate(IReadOnlyList<(Point2D Pixel, Point2D Machine)> correspondences)
        {
            if (correspondences.Count < 3)
                throw new ArgumentException("캘리브레이션 대응점은 최소 3개 이상 필요합니다.");

            SolveAxis(correspondences, useY: false, out a, out b, out tx);
            SolveAxis(correspondences, useY: true, out c, out d, out ty);
            IsCalibrated = true;
        }

        public Point2D PixelToMachine(Point2D px)
        {
            if (!IsCalibrated)
                throw new InvalidOperationException("먼저 Calibrate()를 호출해야 합니다.");

            double xm = a * px.X + b * px.Y + tx;
            double ym = c * px.X + d * px.Y + ty;
            return new Point2D(xm, ym);
        }

        /// <summary>
        /// 카메라 좌표계와 기계 좌표계 사이의 전체 회전 오프셋(도).
        /// 보통 카메라가 갈바노 스캔 영역에 대해 약간 비뚤게 장착된 만큼의 오차를 의미하며,
        /// 부품 자체의 검출 각도(AngleDeg)에 더해서 최종 마킹 회전값을 만든다.
        /// </summary>
        public double GetCameraToMachineRotationDeg()
        {
            if (!IsCalibrated)
                throw new InvalidOperationException("먼저 Calibrate()를 호출해야 합니다.");
            return Math.Atan2(c, a) * 180.0 / Math.PI;
        }

        private static void SolveAxis(
            IReadOnlyList<(Point2D Pixel, Point2D Machine)> pts,
            bool useY, out double p, out double q, out double r)
        {
            double sxx = 0, sxy = 0, sx = 0, syy = 0, sy = 0, sn = 0;
            double sxt = 0, syt = 0, st = 0;

            foreach (var (px, mc) in pts)
            {
                double x = px.X, y = px.Y;
                double t = useY ? mc.Y : mc.X;

                sxx += x * x; sxy += x * y; sx += x;
                syy += y * y; sy += y; sn += 1;
                sxt += x * t; syt += y * t; st += t;
            }

            Solve3x3(
                sxx, sxy, sx, sxt,
                sxy, syy, sy, syt,
                sx, sy, sn, st,
                out p, out q, out r);
        }

        private static void Solve3x3(
            double a11, double a12, double a13, double b1,
            double a21, double a22, double a23, double b2,
            double a31, double a32, double a33, double b3,
            out double x1, out double x2, out double x3)
        {
            double det = a11 * (a22 * a33 - a23 * a32)
                       - a12 * (a21 * a33 - a23 * a31)
                       + a13 * (a21 * a32 - a22 * a31);

            if (Math.Abs(det) < 1e-9)
                throw new InvalidOperationException(
                    "캘리브레이션 점들이 한 직선 위에 있어 풀이가 불가능합니다. 점 배치를 다시 확인하세요.");

            double detX1 = b1 * (a22 * a33 - a23 * a32)
                         - a12 * (b2 * a33 - a23 * b3)
                         + a13 * (b2 * a32 - a22 * b3);

            double detX2 = a11 * (b2 * a33 - a23 * b3)
                         - b1 * (a21 * a33 - a23 * a31)
                         + a13 * (a21 * b3 - b2 * a31);

            double detX3 = a11 * (a22 * b3 - b2 * a32)
                         - a12 * (a21 * b3 - b2 * a31)
                         + b1 * (a21 * a32 - a22 * a31);

            x1 = detX1 / det;
            x2 = detX2 / det;
            x3 = detX3 / det;
        }
    }
}

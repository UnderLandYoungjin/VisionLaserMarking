using System;
using System.Collections.Generic;
using OpenCvSharp;

namespace VisionLaserMarking.Wpf
{
    /// <summary>컨베이어 위 부품 1개의 실제(ground-truth) 배치 정보</summary>
    public record TruePart(double Cx, double Cy, double W, double H, double Angle);

    /// <summary>
    /// 실제 카메라 없이 컨베이어 위 부품 배치를 무작위로 생성하고,
    /// 그 결과를 OpenCvSharp Mat으로 렌더링한다. 이 Mat이 곧 "카메라가 찍은 영상" 역할을 하며,
    /// VisionLaserMarking.Vision.PartDetector가 이 Mat을 그대로 입력받아 검출을 수행한다.
    /// </summary>
    public static class SceneGenerator
    {
        public static List<TruePart> GenerateTrueParts(int count, int width, int height)
        {
            var rng = new Random();
            var list = new List<TruePart>();
            int attempts = 0;

            while (list.Count < count && attempts < 3000)
            {
                attempts++;
                double cx = 70 + rng.NextDouble() * (width - 140);
                double cy = 42 + rng.NextDouble() * (height - 84);
                double w = 60 + rng.NextDouble() * 30;
                double h = 24 + rng.NextDouble() * 16;
                double angle = rng.NextDouble() * 180;
                double r = Math.Sqrt(w * w + h * h) / 2 + 12;

                bool ok = true;
                foreach (TruePart p in list)
                {
                    double pr = Math.Sqrt(p.W * p.W + p.H * p.H) / 2 + 12;
                    double dist = Math.Sqrt((cx - p.Cx) * (cx - p.Cx) + (cy - p.Cy) * (cy - p.Cy));
                    if (dist < r + pr) { ok = false; break; }
                }

                if (ok) list.Add(new TruePart(cx, cy, w, h, angle));
            }

            return list;
        }

        public static Mat RenderScene(List<TruePart> parts, int width, int height)
        {
            var mat = new Mat(height, width, MatType.CV_8UC3, new Scalar(26, 28, 28));

            for (int x = 0; x < width; x += 40)
                Cv2.Line(mat, new OpenCvSharp.Point(x, 0), new OpenCvSharp.Point(x, height),
                    new Scalar(19, 20, 20), 1);

            foreach (TruePart p in parts)
            {
                double theta = p.Angle * Math.PI / 180.0;
                double cosT = Math.Cos(theta), sinT = Math.Sin(theta);
                double hl = p.W / 2, hw = p.H / 2;

                (double, double)[] offsets = { (-hl, -hw), (hl, -hw), (hl, hw), (-hl, hw) };
                var pts = new OpenCvSharp.Point[4];
                for (int i = 0; i < 4; i++)
                {
                    (double dx, double dy) = offsets[i];
                    double x = p.Cx + dx * cosT - dy * sinT;
                    double y = p.Cy + dx * sinT + dy * cosT;
                    pts[i] = new OpenCvSharp.Point((int)Math.Round(x), (int)Math.Round(y));
                }

                Cv2.FillConvexPoly(mat, pts, new Scalar(214, 227, 231));
            }

            return mat;
        }
    }
}

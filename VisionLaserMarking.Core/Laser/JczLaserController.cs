using System;
using System.Runtime.InteropServices;

namespace VisionLaserMarking.Laser
{
    /// <summary>
    /// 북경金橙子(JCZ) EZCAD2 / LMC1 계열 제어보드 연동 클래스.
    ///
    /// 사진 속 "SmartLaser" 화면에서 보였던 D:\ProjectManager\...\Layer0.ezd 경로는
    /// EZCAD2의 표준 프로젝트 파일 포맷(.ezd)이다. 즉 이 장비는 金橙子 호환(또는 동일계열)
    /// 갈바노 제어보드를 쓰고 있을 가능성이 매우 높다.
    ///
    /// ※ 실행 전 준비물
    ///   1) MarkEzd.dll - 보드 제조사가 제공하는 2차개발 SDK. ezcad2.exe와 같은 폴더에 있어야 함.
    ///   2) ezcad2.exe로 미리 만들어둔 템플릿 파일(.ezd) - 마킹할 도안/텍스트 오브젝트.
    ///   3) ezcad2.exe 자체가 실행 중이면 MarkEzd.dll 점유 충돌이 나므로 반드시 종료 상태에서 사용.
    ///
    /// ※ 정확도에 대한 솔직한 안내
    ///   아래 P/Invoke 중 lmc1_Initial / lmc1_LoadEzdFile / lmc1_Mark / lmc1_Close /
    ///   lmc1_ChangeTextByName 은 SDK 배포 예제와 매뉴얼에서 공통적으로 확인되는 실제 함수다.
    ///   하지만 "검출된 (X,Y,각도)로 오브젝트 자체를 옮기는" 함수명은 SDK 버전(2.5/2.7/2.9.6 등)마다
    ///   달라서, 보드와 함께 제공되는 MarkEzdDll.h 헤더를 직접 보고 맞춰야 한다.
    ///   여기서는 그 부분을 자리표시(placeholder) 메서드로 남겨뒀다.
    /// </summary>
    public class JczLaserController : ILaserController
    {
        private const string DllName = "MarkEzd.dll";

        [DllImport(DllName, EntryPoint = "lmc1_Initial", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern int Lmc1_Initial(string ezCadExeDir);

        [DllImport(DllName, EntryPoint = "lmc1_Close", CallingConvention = CallingConvention.Cdecl)]
        private static extern void Lmc1_Close();

        [DllImport(DllName, EntryPoint = "lmc1_LoadEzdFile", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern int Lmc1_LoadEzdFile(string ezdFilePath);

        [DllImport(DllName, EntryPoint = "lmc1_Mark", CallingConvention = CallingConvention.Cdecl)]
        private static extern int Lmc1_Mark();

        [DllImport(DllName, EntryPoint = "lmc1_ChangeTextByName", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
        private static extern int Lmc1_ChangeTextByName(string entityName, string newText);

        [DllImport(DllName, EntryPoint = "lmc1_GetEntityCount", CallingConvention = CallingConvention.Cdecl)]
        private static extern int Lmc1_GetEntityCount();

        private readonly string _ezCadDir;
        private readonly string _templateEzdPath;
        private bool _initialized;

        public bool IsBusy { get; private set; }

        public JczLaserController(string ezCadDir, string templateEzdPath)
        {
            _ezCadDir = ezCadDir;
            _templateEzdPath = templateEzdPath;
        }

        public bool Connect()
        {
            int ret = Lmc1_Initial(_ezCadDir);
            _initialized = ret == 0; // 보드사 매뉴얼 기준 0=성공으로 알려져 있으나, 실제 반환값은 콘솔로 로그를 찍어 직접 확인할 것
            if (_initialized)
                Lmc1_LoadEzdFile(_templateEzdPath);
            return _initialized;
        }

        public void Disconnect()
        {
            if (_initialized)
                Lmc1_Close();
            _initialized = false;
        }

        public void MarkAt(double offsetXmm, double offsetYmm, double rotationDeg)
        {
            if (!_initialized)
                throw new InvalidOperationException("Connect()를 먼저 호출하세요.");

            IsBusy = true;
            try
            {
                MoveCurrentEntityTo(offsetXmm, offsetYmm, rotationDeg);
                Lmc1_Mark(); // 가공이 끝날 때까지 블로킹 (보드사 문서 기준)
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// TODO: 실제 보드 SDK 헤더(MarkEzdDll.h)의 엔티티 위치/회전 설정 함수로 반드시 교체.
        ///
        /// 대안 1) 보드 SDK가 위치 설정 API를 제공하면 그 함수를 그대로 P/Invoke 추가.
        /// 대안 2) 구버전 SDK라 해당 API가 없다면, 사전에 ezcad2 GUI에서 여러 회전각/오프셋
        ///        조합의 템플릿(.ezd)을 미리 만들어두고, 검출된 각도/위치에 가장 가까운
        ///        템플릿을 Lmc1_LoadEzdFile로 다시 불러오는 방식으로 우회 구현하는 경우도 흔하다.
        /// 대안 3) lmc1_ChangeTextByName으로 텍스트 내용(예: 일련번호)만 바꾸고,
        ///        위치 보정은 부품을 고정 지그/스토퍼로 맞춘 뒤 비전은 회전각만 보정하는
        ///        절반 자동화 방식으로 단순화.
        /// </summary>
        private void MoveCurrentEntityTo(double xMm, double yMm, double angleDeg)
        {
            throw new NotImplementedException(
                "보드사 SDK 매뉴얼(MarkEzdDll.h)에서 엔티티 위치/회전 설정 함수명을 확인한 뒤 구현하세요.");
        }
    }
}

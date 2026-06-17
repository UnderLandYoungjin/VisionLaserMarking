using System;
using System.IO.Ports;
using System.Threading;

namespace VisionLaserMarking.Trigger
{
    /// <summary>
    /// 컨베이어 포토일렉트릭 센서 신호를 Modbus RTU 디지털입력 모듈로 읽어서
    /// 부품이 카메라 시야에 도착했을 때 이벤트를 발생시킨다.
    /// LS G100 인버터 RS-485 작업과 동일한 패턴(CRC16-Modbus, 시리얼 프레이밍)을 재사용했다.
    /// </summary>
    public class ModbusConveyorTrigger : IDisposable
    {
        private readonly SerialPort _port;
        private readonly byte _slaveId;
        private readonly ushort _inputAddress;
        private readonly int _pollIntervalMs;
        private Thread? _pollThread;
        private volatile bool _running;
        private bool _lastState;

        /// <summary>센서 신호 Rising Edge(부품 도착)에서 발생</summary>
        public event Action? PartArrived;

        public ModbusConveyorTrigger(string portName, int baudRate, byte slaveId,
            ushort inputAddress, int pollIntervalMs = 20)
        {
            _slaveId = slaveId;
            _inputAddress = inputAddress;
            _pollIntervalMs = pollIntervalMs;
            _port = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
            {
                ReadTimeout = 200,
                WriteTimeout = 200
            };
        }

        public void Start()
        {
            _port.Open();
            _running = true;
            _pollThread = new Thread(PollLoop) { IsBackground = true };
            _pollThread.Start();
        }

        public void Stop()
        {
            _running = false;
            _pollThread?.Join(500);
            if (_port.IsOpen)
                _port.Close();
        }

        private void PollLoop()
        {
            while (_running)
            {
                try
                {
                    bool state = ReadDiscreteInput(_inputAddress);
                    if (state && !_lastState)
                        PartArrived?.Invoke();

                    _lastState = state;
                }
                catch (TimeoutException)
                {
                    // 통신 일시 끊김 - 다음 폴링에서 재시도
                }
                catch (InvalidOperationException)
                {
                    // 포트가 아직 안 열렸거나 닫힌 상태 - 다음 폴링에서 재시도
                }

                Thread.Sleep(_pollIntervalMs);
            }
        }

        /// <summary>Modbus RTU 함수코드 0x02(Read Discrete Inputs)로 1비트 읽기</summary>
        private bool ReadDiscreteInput(ushort address)
        {
            byte[] req = BuildReadRequest(_slaveId, 0x02, address, 1);
            _port.Write(req, 0, req.Length);

            byte[] resp = new byte[6]; // SlaveId + Func + ByteCount + Data(1byte) + CRC(2byte)
            int read = 0;
            int attempts = 0;
            while (read < resp.Length && attempts < 50)
            {
                read += _port.Read(resp, read, resp.Length - read);
                attempts++;
            }

            return (resp[3] & 0x01) == 0x01;
        }

        private static byte[] BuildReadRequest(byte slaveId, byte funcCode, ushort startAddr, ushort qty)
        {
            byte[] frame = new byte[8];
            frame[0] = slaveId;
            frame[1] = funcCode;
            frame[2] = (byte)(startAddr >> 8);
            frame[3] = (byte)(startAddr & 0xFF);
            frame[4] = (byte)(qty >> 8);
            frame[5] = (byte)(qty & 0xFF);
            ushort crc = ModbusCrc16(frame, 6);
            frame[6] = (byte)(crc & 0xFF);
            frame[7] = (byte)(crc >> 8);
            return frame;
        }

        private static ushort ModbusCrc16(byte[] data, int length)
        {
            ushort crc = 0xFFFF;
            for (int i = 0; i < length; i++)
            {
                crc ^= data[i];
                for (int b = 0; b < 8; b++)
                {
                    if ((crc & 0x0001) != 0)
                    {
                        crc >>= 1;
                        crc ^= 0xA001;
                    }
                    else
                    {
                        crc >>= 1;
                    }
                }
            }
            return crc;
        }

        public void Dispose() => Stop();
    }
}

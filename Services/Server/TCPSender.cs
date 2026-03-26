using RecordClient.Helpers;
using RecordClient.Services.Sending;
using System.Collections.Concurrent;

namespace RecordClient.Services.Server
{
    public class TCPSender
    {
        private static readonly string TAG = "TCPSender";

        private int senderCount = 2;

        private Thread? senderThread = null;
        private bool senderRunning = false;
        private BlockingCollection<Message>? senderQueue = null;

        private int senderDelay = 0;

        private TCPSendData[]? sendData = null;
        private static Dictionary<string, TCPSender> senderList = new Dictionary<string, TCPSender>();

        public TCPSender(int size)
        {
            senderCount = size;
            sendData = new TCPSendData[senderCount];
            senderQueue = new BlockingCollection<Message>(30000);
        }

        public static TCPSender GetInstance(string key, int size)
        {
            if (!senderList.ContainsKey(key))
            {
                senderList[key] = new TCPSender(size);
            }
            return senderList[key];
        }

        public bool IsConnected(int index)
        {
            if (sendData[index] == null)
            {
                return sendData[index].isConnected;
            }
            else
            {
                return false;
            }
        }

        public void Connection(int index, string ip, int port, bool sendAlive)
        {
            if (index < 0 || index >= senderCount)
            {
                return;
            }

            if (!string.IsNullOrEmpty(ip))
            {
                ip = ip.Trim();
            }

            if (sendData[index] != null)
            {
                sendData[index].Stop();
                sendData[index] = null;
            }

            if (port != 0 && !string.IsNullOrEmpty(ip))
            {
                Logger.Info($"[{TAG}] 서버 연결... {ip}:{port}");
                sendData[index] = new TCPSendData(ip, port, sendAlive, null);
            }
        }

        public bool Start()
        {
            if (senderRunning)
            {
                return true;
            }

            for (int index = 0; index < senderCount; index++)
            {
                if (sendData[index] != null)
                {
                    sendData[index].Start();
                }
            }

            senderRunning = true;
            senderThread = new Thread(new ThreadStart(Run));

            if (senderThread == null)
            {
                senderRunning = false;
                return false;
            }

            senderThread.Start();

            return true;
        }


        public bool Stop()
        {
            if (!senderRunning)
            {
                return true;
            }

            senderRunning = false;

            if (senderThread != null)
            {
                try
                {
                    senderThread.Join();
                }
                catch (Exception e)
                {
                    Logger.Error($"[{TAG}] 스레드 중지 중 오류 발생: {e.Message}");
                }

                senderThread = null;
            }

            for (int id = 0; id < senderCount; id++)
            {
                if (sendData[id] != null)
                {
                    sendData[id].Stop();
                }
            }

            return true;
        }

        public void Clear()
        {
            for (int id = 0; id < senderCount; id++)
            {
                sendData[id].Clear();
            }
        }

        public void Run()
        {
            Message message = null;

            while (senderRunning)
            {
                if (message == null)
                {
                    try
                    {
                        // 500ms 동안 대기 후 메시지를 가져옴 (없으면 null 반환)
                        if (!senderQueue.TryTake(out message, TimeSpan.FromMilliseconds(500)))
                        {
                            message = null;
                        }
                    }
                    catch (Exception e)
                    {
                        Logger.Error($"[{TAG}] 메시지 처리 중 오류 발생: {e.Message}");
                        break;
                    }
                }

                if (message != null)
                {
                    SendMessageQueue(message);
                    message = null;

                    if (senderDelay != 0)
                    {
                        Thread.Sleep(senderDelay);
                    }
                }
            }

            if (senderRunning)
            {
                Clear();
            }
        }

        public bool SendMessage(Message message) => SendMessageDelay(message, 0);

        public bool SendMessageDelay(Message message, int delay)
        {
            if (delay > 0)
            {
                Task.Run(() =>
                {
                    Thread.Sleep(delay);
                    try
                    {
                        senderQueue.TryAdd(message, TimeSpan.FromMilliseconds(1000));
                    }
                    catch (Exception e)
                    {
                        Logger.Error($"[{TAG}] 전송 큐 추가 중 오류 발생: {e.Message}");
                    }
                });
            }
            else
            {
                try
                {
                    senderQueue.TryAdd(message, TimeSpan.FromMilliseconds(1000));
                }
                catch (Exception e)
                {
                    Logger.Error($"[{TAG}] 전송 큐 추가 중 오류 발생: {e.Message}");
                    return false;
                }
            }
            return true;
        }

        private bool SendMessageQueue(Message message)
        {
            for (int id = 0; id < senderCount; id++)
            {
                sendData[id].SendMessage(message);
            }
            return true;
        }
    }
}
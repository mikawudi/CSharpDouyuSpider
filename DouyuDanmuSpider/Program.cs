using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace System
{
    public struct ValueTuple<T1, T2>
    {
        public T1 Item1;
        public T2 Item2;
        public ValueTuple(T1 t1, T2 t2){ this.Item1 = t1; this.Item2 = t2; }
        public static bool operator !=((T1, T2) left, (T1, T2) right)
        {
            return left.GetHashCode() != right.GetHashCode();
        }
        public static bool operator ==((T1, T2) left, (T1, T2) right)
        {
            return !(left != right);
        }
    }
}
namespace DouyuDanmuSpider
{
    class Program
    {
        static void Main(string[] args)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            if (args.Length <= 0)
            {
                Console.WriteLine("please input roomid");
                return;
            }
            if (!int.TryParse(args[0], out int roomid))
            {
                Console.WriteLine("roomid format error");
                return;
            }
            Console.WriteLine($"roomid:{roomid}");
            var conn = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            var address = Dns.GetHostAddressesAsync("openbarrage.douyutv.com").Result;
            if (address == null || address.Length < 1)
                return;
            conn.ConnectAsync(new IPEndPoint(address[0], 8601)).Wait();
            var context = new DouyuContext(conn, roomid);
            context.RecvDanmu += Context_RecvDanmu;
            context.ContextClose += Context_ContextClose;
            context.Logined += Context_Logined;
            context.LiWu += Context_LiWu;
            context.StartRecv();
            context.Login();
            Console.ReadLine();
            context.SendClose();
            Console.WriteLine("CloseSuccess");
            Thread.Sleep(1000);
        }

        private static void Context_LiWu(DouyuContext arg1, string arg2)
        {
            Console.WriteLine(arg2);
        }

        private static void Context_Logined(DouyuContext arg1, string islive)
        {
            Console.WriteLine($"房间{islive.ToString()}");
        }

        private static void Context_RecvDanmu(DouyuContext arg1, (string, string) arg2)
        {
            Console.WriteLine($"[{arg2.Item1}]{arg2.Item2}");
        }

        private static void Context_ContextClose(SocketContext obj)
        {
            Console.WriteLine("Closed!");
        }
    }
    public class SocketContext
    {
        public Socket SocketObj = null;
        private ArraySegment<byte> buffer = new ArraySegment<byte>(new byte[4096]);
        public SocketContext(Socket s) => this.SocketObj = s;
        public event Action<SocketContext, (int, byte[])> RecvData;
        protected virtual void OnRecvData(int length, byte[] data) => RecvData?.Invoke(this, (length, data));
        public event Action<SocketContext> ContextClose;
        protected virtual void OnContextClose() => ContextClose?.Invoke(this);
        public async void StartRecv()
        {
            Exception ex = null;
            while (true)
            {
                int recvCount = 0;
                try
                {
                    recvCount = await this.SocketObj.ReceiveAsync(buffer, SocketFlags.None);
                }
                catch(Exception e)
                {
                    ex = e;
                }
                if (recvCount == 0)
                    break;
                var data = buffer.Array.Take(recvCount).ToArray();
                OnRecvData(recvCount, data);
            }
            this.OnContextClose();

        }
        public virtual void Close()
        {

        }
    }
    public enum DouyuState
    {
        WaitLogin = 1,
        Logined = 2,
        StartRecvDanmu = 3,
        Closed = 4,
    }
    public class DouyuContext
        : SocketContext
    {
        private List<byte> DataBuffer = new List<byte>();
        public DouyuState State { get; private set; } = DouyuState.WaitLogin;
        public DateTime LastRecvKeepalive { get; private set; } = DateTime.Now;
        public event Action<DouyuContext, (string, string)> RecvDanmu;
        protected virtual void OnRecvDanmu(MsgPack msg) => RecvDanmu?.Invoke(this, (msg["nn"], msg["txt"]));
        public event Action<DouyuContext, string> Logined;
        protected virtual void OnLogined(MsgPack msg) => Logined?.Invoke(this, msg["live_stat"]);
        public event Action<DouyuContext, string> LiWu;
        protected virtual void OnLiWu(MsgPack msg) => LiWu?.Invoke(this, $"[{msg["nn"]}]:{msg["gs"]}X{msg["gfcnt"]}");
        public string RoomID { get; private set; }
        public DouyuContext(Socket s, int roomid)
            : base(s)
        {
            this.RoomID = roomid.ToString();
            this.RecvData += DouyuContext_RecvData;
            this.ContextClose += DouyuContext_ContextClose;
        }

        private void DouyuContext_ContextClose(SocketContext obj)
        {
            this.Close();
        }

        private void DouyuContext_RecvData(SocketContext arg1, (int, byte[]) arg2)
        {
            DataBuffer.AddRange(arg2.Item2);
            var result = GetMsgFormData();
            try
            {
                result?.ForEach((msg) => Work(msg));
            }
            catch(Exception ex)
            {
                this.Close();
                return;
            }
        }

        private void Work(MsgPack msg)
        {
            if (string.IsNullOrEmpty(msg.MsgType) || msg.Type != 690)
                return;
            if (msg.MsgType?.Equals("loginres") == true)
            {
                if (this.State == DouyuState.WaitLogin)
                {
                    this.State = DouyuState.Logined;
                    this.LastRecvKeepalive = DateTime.Now;
                    JoinGroup();
                    this.State = DouyuState.StartRecvDanmu;
                    this.OnLogined(msg);
                    this.StartKeepAliveTimer();
                }
            }
            if(msg.MsgType?.Equals("keeplive") == true)
            {
                if (this.State == DouyuState.StartRecvDanmu)
                    this.LastRecvKeepalive = DateTime.Now;
            }
            if(msg.MsgType?.Equals("chatmsg") == true)
            {
                OnRecvDanmu(msg);
            }
            if(msg.MsgType?.Equals("dgb") == true)
            {
                OnLiWu(msg);
            }
        }
        private Timer t = null;
        private void StartKeepAliveTimer()
        {
            t = new Timer((x) =>
            {
                if ((DateTime.Now - this.LastRecvKeepalive).TotalSeconds > 60 || this.State == DouyuState.Closed)
                {
                    this.Close();
                    t.Dispose();
                }
                this.SendKeepAlive();
            }, null, 1000, 45 * 1000);
        }
        private void SendKeepAlive()
        {
            var epoch = (DateTime.Now.ToUniversalTime().Ticks - 621355968000000000) / 10000000;
            var data = new List<(string, string)>()
            {
                ("type", "keeplive"),
                ("tick", epoch.ToString())
            };
            var MsgPack = new MsgPack(data);
            this.SocketObj.SendAsync(MsgPack.GetBytes(), SocketFlags.None);
        }
        private List<MsgPack> GetMsgFormData()
        {
            List<MsgPack> result = null;
            do
            {
                if (this.DataBuffer.Count < 4)
                    break;
                uint length = BitConverter.ToUInt32(this.DataBuffer.Take(4).ToArray(), 0);
                if (this.DataBuffer.Count >= (length + 4))
                {
                    result = result ?? new List<MsgPack>();
                    result.Add(MsgPack.GetObject(this.DataBuffer.Skip(4).Take((int)length).ToArray()));
                    DataBuffer.RemoveRange(0, 4 + (int)length);
                }
                else
                    break;
                
            }
            while (true);
            return result;
        }
        public void Login()
        {
            List<(string, string)> data = new List<(string, string)>();
            data.Add(("type", "loginreq"));
            data.Add(("roomid", this.RoomID));
            var MsgPack = new MsgPack(data);
            this.SocketObj.SendAsync(MsgPack.GetBytes(), SocketFlags.None);
        }
        public void JoinGroup()
        {
            var temp = new List<(string, string)>()
            {
                ("type", "joingroup"),
                ("rid", this.RoomID),
                ("gid", "-9999"),
            };
            var msg = new MsgPack(temp);
            this.SocketObj.SendAsync(msg.GetBytes(), SocketFlags.None);
        }
        public void SendClose()
        {
            var temp = new List<(string, string)>()
            {
                ("type", "logout"),
            };
            var msg = new MsgPack(temp);
            this.SocketObj.SendAsync(msg.GetBytes(), SocketFlags.None).Wait();
            this.SocketObj.Dispose();
        }
        public override void Close()
        {
            this.State = DouyuState.Closed;
            base.Close();
        }
    }
    public class MsgPack
    {
        public uint MsgLength;
        public ushort Type;
        public byte Sec;
        public byte Sav;
        public byte[] Data;
        public MsgPack() { }
        private static byte[] LastHead = new byte[4];
        static MsgPack()
        {
            var t = BitConverter.GetBytes((ushort)689);
            LastHead[0] = t[0];
            LastHead[1] = t[1];
        }
        public MsgPack(List<(string, string)> temp)
        {
            this.DataFormater = temp;
            string data = temp.SerMsg();
            this.Data = Encoding.UTF8.GetBytes(data).Append<byte>(0x00).ToArray();
            this.Type = 689;
            this.MsgLength = (uint)this.Data.Length + 8;//不行的话再加4
        }
        public List<ArraySegment<byte>> GetBytes()
        {
            var lengthBytes = BitConverter.GetBytes(this.MsgLength);
            var result = new List<ArraySegment<byte>>();
            result.Add(new ArraySegment<byte>(lengthBytes));
            result.Add(new ArraySegment<byte>(lengthBytes));
            result.Add(new ArraySegment<byte>(LastHead));
            result.Add(new ArraySegment<byte>(this.Data));
            return result;
        }
        public List<(string, string)> DataFormater = null;
        public string this[string index] => DataFormater.FirstOrDefault(x=>x.Item1 == index).Item2;
        public string MsgType { get { return DataFormater?.FirstOrDefault(x => x.Item1 == "type").Item2; } }
        public static MsgPack GetObject(byte[] data)
        {
            var result = new MsgPack();
            result.Type = BitConverter.ToUInt16(data, 4);
            var bodyData = Encoding.UTF8.GetString(data, 8, data.Length - 8);
            var temp = bodyData.DeSerMsg();
            result.DataFormater = temp;
            return result;
        }
    }
    public static class Helper
    {
        public static string SerMsg(this List<(string, string)> pack)
        {
            return string.Join("/", pack.Select(x => $"{Conver(x.Item1)}@={Conver(x.Item2)}"));
        }
        private static string Conver(string data)
        {
            //注意顺序
            if (data.IndexOf('@') != -1)
                data = data.Replace("@", "@A");
            if (data.IndexOf('/') != -1)
                data = data.Replace("/", "@S");
            return data;
        }
        private static string[] SPLIT_ARRAY = new string[] { "/" };
        private static string[] SPLIT_KV = new string[] { "@=" };
        public static List<(string, string)> DeSerMsg(this string data)
        {
            var kvArray = data.Split(SPLIT_ARRAY, StringSplitOptions.RemoveEmptyEntries);
            return kvArray.Select(x => 
            {
                var temp = x.Split(SPLIT_KV, StringSplitOptions.None);
                if (temp == null || temp.Length != 2)
                    return default((string, string));
                return (DeConver(temp[0]), DeConver(temp[1]));
            }).Where(x=>x != default((string, string))).ToList();
        }
        private static string DeConver(string data)
        {
            data = data.Replace("@S", "/");
            data = data.Replace("@A", "@");
            return data;
        }
    }
}
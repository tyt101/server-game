using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System;
using System.Data.OleDb;

using System.Net;
using System.Net.Sockets;


namespace Server01
{
    public partial class Form1 : Form
    {
        Socket server;
        public int bufferCount = 0;
        StateObject[] stateOs;
        int maxConn = 10;
        int index = 0, num = 0;
        List<RoomObject> roomList;
        List<RoomObject> freeRoomList;

        System.Timers.Timer timer = new System.Timers.Timer(1000);
        public long heartBeatTime = 40;
        public Form1()
        {
            InitializeComponent();
            System.Windows.Forms.Control.CheckForIllegalCrossThreadCalls = false;
            roomList = new List<RoomObject>();
            roomList.Clear();
            freeRoomList = new List<RoomObject>();           
        }

        public void HandleMainTimer(object sender, System.Timers.ElapsedEventArgs e)
        {
            long timeNow = Sys.GetTimeStamp();
            for (int i = 0; i < index; i++)
            {
                if (stateOs[i] == null) continue;
                if (stateOs[i].lastTickTime < timeNow - heartBeatTime)
                {
                    textBox1.Text = "关闭:" + timeNow +"  "+ stateOs[i].lastTickTime +"\r\n" + textBox1.Text;
                    DeleteRoom(roomList, stateOs[i]);
                    stateOs[i].sock.Shutdown(SocketShutdown.Both);
                    System.Threading.Thread.Sleep(30);
                    lock(stateOs[i].sock)
                        stateOs[i].sock.Close(); 
                    DeleteClient(stateOs, i);
                }
            }
            showRoomList(roomList);
            CopyFreeRoomList();
            SendRoomList(freeRoomList);
            timer.Start();
        }
        private void DeleteClient(StateObject[] clients, int i)
        {
            if (index == 0) return;
            lock (clients)
            {
                for (int j = i; j < index - 1; j++)
                {
                    clients[j] = clients[j + 1];
                }
                index--;
            }
        }
        private void DeleteRoom(List<RoomObject> rooms, StateObject client)
        {
            //foreach (RoomObject room in rooms)
            for(int i=rooms.Count-1; i>=0; i--)
            {
                if (client == rooms[i].client0)
                    rooms.Remove(rooms[i]);
            }
        }
        public void SendRoomList(List<RoomObject> rooms)
        {
            String str = "roomList ";
            str += (rooms.Count).ToString();
            foreach (RoomObject room in rooms)
            {
                if (!room.playing)
                {
                    str += " ";
                   // str +=
                    str += ((IPEndPoint)(room.client0.sock.RemoteEndPoint)).ToString();
                }
            }
            byte[] sendBuff = new byte[1024];
            sendBuff = System.Text.Encoding.ASCII.GetBytes(str);
            for (int i = 0; i < index; i++)
            {
                try
                {
                    stateOs[i].sock.Send(sendBuff);
                    //textBox1.Text = "发送:" + str + "To:" + stateOs[i].sock.RemoteEndPoint.ToString() + textBox1.Text;
                }
                catch (System.Exception ex)
                {
                    textBox1.Text = ex.ToString() + textBox1.Text;
                }
            }
        }
        public void showRoomList(List<RoomObject> rooms)
        {
            String str = "";            
           /* for (int i = rooms.Count-1; i >= 0; i--)
            {
                str += ((IPEndPoint)(rooms[i].client0.sock.RemoteEndPoint)).ToString();
                str += "\r\n";
            }*/
            str = index.ToString() + "\r\n";
            for (int i = 0; i < index; i++)
            {
                str += stateOs[i].sock.RemoteEndPoint.ToString();
                str += "\r\n";
            }
            //textBox1.Text = str + textBox1.Text;
        }        
        private void button1_Click(object sender, EventArgs e)
        {
            IPAddress local = IPAddress.Parse("127.0.0.1");
            int iLocalPort = int.Parse(txtListenPort.Text);
            IPEndPoint iep = new IPEndPoint(local, iLocalPort);
            stateOs = new StateObject[maxConn];

            //创建服务器的socket对象
           
            server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                server.Bind(iep);
                server.Listen(10);
                server.BeginAccept(new AsyncCallback(acceptCb), server);
                //this.textBox1.Text = "开始监听"+iLocalPort.ToString()+ "号端口\r\n" + this.textBox1.Text;
                button1.Enabled = false;
                txtListenPort.Enabled = false;
            }
            catch (Exception ex)
            {
                this.textBox1.Text = "该端口已被占用\r\n" + this.textBox1.Text;
            }
             
            

            timer.Elapsed += new System.Timers.ElapsedEventHandler(HandleMainTimer);
            timer.AutoReset = false;
            timer.Enabled = true;
        }
        public void acceptCb(IAsyncResult iar)
        {
            Socket MyServer = (Socket)iar.AsyncState;
                      
            stateOs[index] = new StateObject();
            stateOs[index].sock = MyServer.EndAccept(iar);
            textBox1.Text = "一共收到的连接："+index+"个"+"有新的连接建立"+index.ToString()+stateOs[index].sock.RemoteEndPoint.ToString()+"\r\n" + textBox1.Text;
           
            try
            {
                stateOs[index].sock.BeginReceive(stateOs[index].buffer, 0, StateObject.BUFFER_SIZE, 0, new AsyncCallback(receiveCb), stateOs[index]);
            }
            catch (Exception e)
            {  }
            MyServer.BeginAccept(new AsyncCallback(acceptCb), server);
            index++;
            if (index > 10)
            {
                textBox1.Text = "连接池已满" + textBox1.Text;
                index = 0;
            }  
        }
        public void receiveCb(IAsyncResult ar)
        {
            String recvStr = "";           
            StateObject stateo1 = (StateObject)ar.AsyncState;
            Socket client = stateo1.sock;
            if (client.Connected == false) return;
            try
            {
                int recNum = stateo1.sock.EndReceive(ar);
          /* textBox1.Text += "收到一个数据包共" + recNum.ToString()+"个字节：";
    recvStr = System.Text.Encoding.UTF8.GetString(stateo1.buffer, 0, recNum);
                textBox1.Text += recvStr+"\r\n";  */
                recvStr = System.Text.Encoding.ASCII.GetString(stateo1.buffer, 0, recNum);
                if (recvStr.IndexOf("\0") != -1)
                {
                    recvStr = recvStr.Substring(0,recvStr.IndexOf("\0"));
                }
                string[] args = recvStr.Split(' ');
                if ("createRoom" == args[0]) //创建房间
                {
                    NewRoom(stateo1);                    
                }
                if ("heartBeat" == args[0])//接收心跳包
                {
                    textBox1.Text = client.RemoteEndPoint.ToString().Split(':')[1] +" "+ index.ToString() + "收到一个心跳包\r\n" +  textBox1.Text;
                    num++;                  
                    stateo1.lastTickTime = Sys.GetTimeStamp();
                }
                //textBox1.Text += recvStr;
                if ("Regist" == args[0])//注册
                {
                    RegistHandle(args,stateo1);
                }
                if("Login" == args[0])//登录
                {
                    LoginHandle(args, stateo1);
                }
                if ("enterRoom" == args[0])//进入房间
                {
                    EnterRoom(args[1], stateo1);
                }

                if (args[0] == "Flip")
                {
                    int a = 1;
                }
                if ("Position" == args[0]||"Flip" == args[0]||"Fire"==args[0]) //位置/转身/开火
                    foreach (RoomObject room in roomList)
                    {
                        if (client == room.client0.sock)
                            room.client1.sock.Send(stateo1.buffer);
                        else if (client == room.client1.sock)
                            room.client0.sock.Send(stateo1.buffer);
                    }
                    /*for (int i = 0; i < index; i++)
                        if (stateOs[i].sock != stateo1.sock) stateOs[i].sock.Send(stateo1.buffer);*/

                /*
                for (int i = 0; i < index; i++)
                {
                    stateOs[i].sock.Send(stateo1.buffer);
                }*/

                client.BeginReceive(stateo1.buffer, 0, StateObject.BUFFER_SIZE, 0, receiveCb, stateo1);
            }
            catch (System.Exception ex)
            {
                //textBox1.Text += ex.ToString();//+client.RemoteEndPoint.ToString()
            }
         
        }
        public void NewRoom(StateObject obj0)//新的房间
        {
            RoomObject room = new RoomObject();
            room.CreateRoom(obj0);
            roomList.Add(room);
        }
        public void EnterRoom(String strClient0, StateObject obj1)//加入房间
        {
            foreach (RoomObject room in roomList)
            {
                if (room.client0.sock.RemoteEndPoint.ToString() == strClient0)
                {
                    room.EnterRoom(obj1);                   
                    room.client0.sock.Send(System.Text.Encoding.ASCII.GetBytes("beginGame 0 "));
                    //向自己的客户端发送beginGame 0 协议，表示在创建房间人的客户端收到的编号为0
                    room.client1.sock.Send(System.Text.Encoding.ASCII.GetBytes("beginGame 1 "));
                    //向对方的客户端发送协议，表示对方客户端接收到的编号为1
                }
            }                     
        }
        void CopyFreeRoomList()
        {
            freeRoomList.Clear();
            foreach (RoomObject room in roomList)
            {
                if (!room.playing)//没在玩的房间加入
                {
                    RoomObject tempRoom = new RoomObject();
                    tempRoom = room;
                    freeRoomList.Add(tempRoom);
                }                    
            }
        }
        void RegistHandle(string[] args, StateObject stateo1)//注册
        {
            string userName = args[1];
            string passWordMD5 = args[2];
            //查询是否有同名记录            
            string dbPath = "C:\\Users\\mazii\\Desktop\\Server01\\User1.mdb";
            OleDbConnection conn = new OleDbConnection("Provider=Microsoft.Jet.OLEDB.4.0; Data Source=" + dbPath);
            conn.Open(); //打开数据库连接
            OleDbCommand cmd = conn.CreateCommand();//建立SQL查询
            cmd.CommandText = "SELECT * FROM table1 WHERE username='" + userName + "'";
            OleDbDataReader odrReader = cmd.ExecuteReader(); //执行Command命令

            if (odrReader.HasRows)   //有同名记录
            {
                stateo1.sock.Send(System.Text.Encoding.ASCII.GetBytes("RegistFail 1 "));                
            }
            else  //没有同名记录，入库
            {
                string sql = "INSERT INTO table1 (username,userpassword,online,score) VALUES ('" + userName + "','" + passWordMD5 + "','1','0')";
                cmd = new OleDbCommand(sql, conn); //定义Command对象                
                cmd.ExecuteNonQuery(); //执行Command命令 
                stateo1.sock.Send(System.Text.Encoding.ASCII.GetBytes("RegistSuccess ")); 
            }
            conn.Close(); //关闭数据库连接
        }

        void LoginHandle(string[] args,StateObject stateo1)//登录
        {
            string userName = args[1];
            string passWordMD5 = args[2];
            //查询是否有同名记录            
            string dbPath = "C:\\Users\\tyt\\Desktop\\Server01\\User1.mdb";
            OleDbConnection conn = new OleDbConnection("Provider=Microsoft.Jet.OLEDB.4.0; Data Source=" + dbPath);
            conn.Open(); //打开数据库连接
            OleDbCommand cmd = conn.CreateCommand();//建立SQL查询
            cmd.CommandText = "SELECT * FROM table1 WHERE username='" + userName + "'";
            OleDbDataReader odrReader = cmd.ExecuteReader(); //执行Command命令

            if (odrReader.HasRows)   //有同名记录
            {
                while(odrReader.Read())
                {
                    /*Console.WriteLine(odrReader[2].ToString());*/
                    if (odrReader[3].ToString() == "true")//用户已登录
                    {
                        stateo1.sock.Send(System.Text.Encoding.ASCII.GetBytes("LoginFail 3 "));
                    }
                    else
                    if(odrReader[2].ToString() == passWordMD5)//登录成功
                    {
                        stateo1.sock.Send(System.Text.Encoding.ASCII.GetBytes("LoginSuccess "));
                    }
                    else//密码错误
                    {
                        stateo1.sock.Send(System.Text.Encoding.ASCII.GetBytes("LoginFail 2 "));
                    }
                }
                
            }
            else//用户不存在
            {
                stateo1.sock.Send(System.Text.Encoding.ASCII.GetBytes("LoginFail 1 "));
            }
            conn.Close(); //关闭数据库连接
        }
        private void textBox1_TextChanged(object sender, System.EventArgs e)
        {

        }

        private void Form1_Load(object sender, System.EventArgs e)
        {
            Console.Write("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        }


    }

    public class StateObject
    {
        public const int BUFFER_SIZE = 1024;
        public byte[] buffer = new byte[BUFFER_SIZE];
        public Socket sock = null;
        public bool bIsUsed = false;

        public long lastTickTime;
        public StateObject() { lastTickTime = Sys.GetTimeStamp(); 
        }
       /* public void SendString(String str)
        {
            buffer = System.Text.Encoding.Default.GetBytes(str);
            try
            {
                sock.Send(buffer);
            }
            catch (Exception e)
            {
                Console.Write(e);
                Console.Write("发送" + str + "时出现错误");
            }
        }*/
    }
    public class RoomObject
    {
        public StateObject client0;
        public StateObject client1;
        public bool playing;
        public RoomObject()
        {
            client0 = new StateObject();//创建两个客户
            client1 = new StateObject();
        }
        public void CreateRoom(StateObject c0) { client0 = c0; playing = false; }//创建房间
        public void EnterRoom(StateObject c1) { client1 = c1; playing = true; }//加入房间
    }
    public class Sys
    {
        public static long GetTimeStamp()
        {
            TimeSpan ts = DateTime.UtcNow - new DateTime(1907, 1, 1, 0, 0, 0, 0);
            return Convert.ToInt64(ts.TotalSeconds);
        }
        
    }
}

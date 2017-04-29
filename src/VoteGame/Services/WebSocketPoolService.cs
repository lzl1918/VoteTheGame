using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace VoteGame.Services
{
    public class MessageReceivedEventArgs : EventArgs
    {
        public string Message { get; private set; }
        public MessageReceivedEventArgs(string msg)
        {
            Message = msg;
        }
    }
    public class WebSocketConnectContext
    {
        public string Key { get; set; }
        public string RemoteIP { get; set; }
        public string RemotePort { get; set; }

        public string RemoteAddress { get { return $"{RemoteIP}:{RemotePort}"; } }
    }
    public class WebSocketWrapper : IDisposable
    {
        public WebSocket Socket { get; private set; } = null;
        private ArraySegment<byte> _recvBuffer = new ArraySegment<byte>(new byte[1024]);
        public event EventHandler<EventArgs> Disconnected;
        public event EventHandler<MessageReceivedEventArgs> MessageReceived;
        public Task LoopTask { get; private set; } = null;
        public WebSocketConnectContext ConnectInformation { get; private set; } = null;

        public WebSocketWrapper(HttpContext context, WebSocket socket)
        {
            Socket = socket;

            ConnectInformation = new WebSocketConnectContext()
            {
                RemoteIP = context.Connection.RemoteIpAddress.ToString(),
                RemotePort = context.Connection.RemotePort.ToString(),
                Key = context.Request.Headers["Sec-WebSocket-Key"][0]
            };
            Init();
        }

        public void Init()
        {
            LoopTask = Task.Run(async () =>
            {
                while (true)
                {
                    WebSocketReceiveResult result = await Socket.ReceiveAsync(_recvBuffer, CancellationToken.None);
                    if (result.CloseStatus.HasValue)
                        break;
                    if (result.Count < 0)
                        break;
                    if (result.MessageType == WebSocketMessageType.Close)
                        break;

                    MessageReceived?.Invoke(this, new MessageReceivedEventArgs(Encoding.UTF8.GetString(_recvBuffer.Array, 0, result.Count)));
                }
                Disconnected?.Invoke(this, null);
            });
        }
        public Task Send(string message)
        {
            return Task.Run(async () =>
            {
                byte[] _sendbuffer = Encoding.UTF8.GetBytes(message);
                ArraySegment<byte> buffer = new ArraySegment<byte>(_sendbuffer, 0, _sendbuffer.Length);
                await Socket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
            });

        }

        private bool disposed = false;
        ~WebSocketWrapper()
        {
            if (disposed == false)
                Dispose();
        }
        public void Dispose()
        {
            if (disposed == true)
                return;

            Socket.Dispose();
            Socket = null;
            disposed = true;
        }
    }
    public class WebSocketPoolService
    {
        private bool editLock = false;
        // 更改完board，10s后生效
        private Timer changeTimer = null;
        private int timerremain = 10;
        // board生效后1min才可再次更改
        private Timer canChangeTimer = null;

        public BoardCurrent BoardCurrent { get; set; } = new BoardCurrent();

        private Dictionary<string, WebSocketWrapper> clients = new Dictionary<string, WebSocketWrapper>();
        public WebSocketWrapper AddClient(HttpContext context, WebSocket socket)
        {
            WebSocketWrapper wrapper = new WebSocketWrapper(context, socket);
            string formatedaddr = wrapper.ConnectInformation.RemoteAddress;

            lock (clients)
            {
                wrapper.MessageReceived += ClientMessageReceived;
                wrapper.Disconnected += ClientDisconnected;
                clients.Add(formatedaddr, wrapper);
                wrapper.Send($"{{type: 'set', title: '{BoardCurrent.Title}', leftimg: '{BoardCurrent.LeftImage}', rightimg: '{BoardCurrent.RightImage}', left: {BoardCurrent.LeftCount}, right: {BoardCurrent.RightCount}}}");
            }
            return wrapper;
        }

        public void Broadcast(string msg)
        {
            lock (clients)
            {
                foreach (var pair in clients)
                {
                    pair.Value.Send(msg);
                }
            }
        }

        private void ClientDisconnected(object sender, EventArgs e)
        {
            WebSocketWrapper wrapper = sender as WebSocketWrapper;
            lock (clients)
            {
                clients.Remove(wrapper.ConnectInformation.RemoteAddress);
                wrapper.Disconnected -= ClientDisconnected;
                wrapper.MessageReceived -= ClientMessageReceived;
                wrapper.Dispose();
            }
        }

        private void ClientMessageReceived(object sender, MessageReceivedEventArgs e)
        {
            WebSocketWrapper wrapper = sender as WebSocketWrapper;
            string message = e.Message;
            int index = message.IndexOf(' ');
            if (index <= 0) return;
            string command = message.Substring(0, index);
            string args = "";
            if (index < message.Length - 1)
                args = message.Substring(index + 1);

            switch (command)
            {
                case "/add":
                    if (args == "left")
                        BoardCurrent.AddLeft();
                    else if (args == "right")
                        BoardCurrent.AddRight();
                    Broadcast($"{{type: 'add', side: '{args}', left: {BoardCurrent.LeftCount}, right: {BoardCurrent.RightCount}}}");
                    break;
            }


        }

        public bool CanEdit()
        {
            lock (this)
            {
                return editLock == false;
            }
        }
        public bool RequestEdit()
        {
            lock (this)
            {
                if (editLock == true)
                    return false;
                else
                {
                    editLock = true;
                    changeTimer?.Dispose();
                    changeTimer = null;
                    canChangeTimer?.Dispose();
                    canChangeTimer = null;
                    Broadcast($"{{type:'disableedit'}}");
                    return true;
                }
            }
        }
        public void SubmitEdit(string title, string left, string right)
        {
            lock (this)
            {
                if (editLock == false || changeTimer != null || canChangeTimer != null)
                    return;

                timerremain = 10;
                changeTimer = new Timer((state) =>
                {
                    lock (this)
                    {
                        if (timerremain <= 1)
                        {
                            BoardCurrent.Clear(title, left, right);
                            Broadcast($"{{type: 'set', title: '{title}', leftimg: '{BoardCurrent.LeftImage}', rightimg: '{BoardCurrent.RightImage}', left: {BoardCurrent.LeftCount}, right: {BoardCurrent.RightCount}}}");
                            changeTimer.Dispose();
                            changeTimer = null;

                            canChangeTimer = new Timer((_state) =>
                            {
                                lock (this)
                                {
                                    Broadcast($"{{type:'enableedit'}}");
                                    editLock = false;
                                    canChangeTimer.Dispose();
                                    canChangeTimer = null;
                                }
                            }, null, TimeSpan.FromMinutes(1), Timeout.InfiniteTimeSpan);
                        }
                        else
                        {
                            timerremain--;
                            Broadcast($"{{type:'resettimer', val: {timerremain}}}");
                        }
                    }
                }, null, TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(1));

            }
        }
        public bool CancelEdit()
        {
            lock (this)
            {
                if (editLock == false || changeTimer != null || canChangeTimer != null)
                    return true;
                changeTimer?.Dispose();
                changeTimer = null;
                canChangeTimer?.Dispose();
                canChangeTimer = null;
                editLock = false;
                Broadcast($"{{type:'enableedit'}}");
                return true;
            }
        }
    }

    public class BoardCurrent
    {
        public string Title { get; set; }
        public string LeftImage { get; set; }
        public string RightImage { get; set; }
        public int LeftCount { get; set; }
        public int RightCount { get; set; }

        public void Clear(string title, string left, string right)
        {
            lock (this)
            {
                Title = title;
                LeftImage = left;
                RightImage = right;
                LeftCount = 0;
                RightCount = 0;
            }
        }
        public void AddLeft()
        {
            lock (this)
            {
                LeftCount++;
            }

        }
        public void AddRight()
        {
            lock (this)
            {
                RightCount++;
            }
        }
    }

}

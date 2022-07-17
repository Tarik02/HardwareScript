using System.Collections.Concurrent;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace HardwareScript
{
    public class EventServer
    {
        class Session : WebSocketBehavior
        {
            protected EventServer server;

            public Session(EventServer server)
            {
                this.server = server;
            }

            public void SendMessage(string data)
            {
                ThreadPool.QueueUserWorkItem(delegate {
                    this.Send(data);
                }, null);
            }

            protected override void OnOpen()
            {
                this.server.sessions[ID] = this;
            }

            protected override void OnClose(CloseEventArgs e)
            {
                Session session;
                this.server.sessions.TryRemove(ID, out session);
            }

            protected override void OnMessage(MessageEventArgs e)
            {
                server.OnMessage?.Invoke(ID, e.Data);
            }
        }

        private WebSocketServer webSocketServer;
        private ConcurrentDictionary<string, Session> sessions = new ConcurrentDictionary<string, Session>();

        public delegate void MessageEventHandler(string sender, string message);
        public event MessageEventHandler? OnMessage;

        public EventServer()
        {
            webSocketServer = new WebSocketServer(9081);
            webSocketServer.AddWebSocketService("/", () => new Session(this));
        }

        public void Send(string id, string data)
        {
            Session session;
            if (sessions.TryGetValue(id, out session)) {
                session.SendMessage(data);
            }
        }

        public void Broadcast(string data)
        {
            ThreadPool.QueueUserWorkItem(delegate {
                webSocketServer.WebSocketServices.Broadcast(data);
            }, null);
        }

        public void Start()
        {
            webSocketServer.Start();
        }

        public void Stop()
        {
            webSocketServer.Stop();
        }
    }
}

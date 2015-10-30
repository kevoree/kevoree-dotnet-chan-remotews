using Org.Kevoree.Annotation;
using Org.Kevoree.Core.Api;
using Org.Kevoree.Log.Api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebSocketSharp;

namespace Org.Kevoree.Library
{

    [GroupType]
    [Serializable]
    public class RemoteWSChan : MarshalByRefObject, DeployUnit, ChannelPort
    {

        private readonly List<Port> _inputPorts = new List<Port>();
        private readonly List<Port> _remoteInputPorts = new List<Port>();
        private readonly Dictionary<string, WebSocket> _remoteInputPortWebsockets = new Dictionary<string, WebSocket>();
        private readonly Dictionary<string, WebSocket> _inputPortWebsockets = new Dictionary<string, WebSocket>();

        [KevoreeInject]
        private Context _context;

        [KevoreeInject]
        private ModelService _modelService;

        [KevoreeInject]
        private ILogger _logger;

        [Param(Optional = false)]
        private string host;

        [Param(DefaultValue = "/")]
        private string path = "/";

        [Param(DefaultValue = "80")]
        private int port = 80;

        [Param(Optional = false)]
        private string uuid;

        [Start]
        public void Start()
        {
            var model = _modelService.getPendingModel();

            var thisChan = model.findByPath(_context.getPath());

            // 1 - récupérer l'ensemble des ports input (provided) qui ne sont pas à l'intérieur du noeud courrant
            // 2 - lancer un consumer dessus avec en suffixe d'url le path vers le port en question

            foreach (var remoteInputPort in _remoteInputPorts)
            {
                var websocket = new WebSocket(GetChanURI(remoteInputPort.getPath()));
                websocket.Connect();
                _remoteInputPortWebsockets.Add(remoteInputPort.getPath(), websocket);
            }

            foreach (var inputPort in _inputPorts)
            {
                var websocket = new WebSocket(GetChanURI(inputPort.getPath()));
                websocket.OnMessage += (sender, data) =>
                {
                    inputPort.send(data.Data, null);
                };

                websocket.Connect();
                _inputPortWebsockets.Add(inputPort.getPath(), websocket);
            }
        }

        [Stop]
        public void Stop()
        {
            foreach (var i in _remoteInputPortWebsockets)
            {
                i.Value.CloseAsync();
            }

            foreach (var i in _inputPortWebsockets)
            {
                i.Value.CloseAsync();
            }
        }

        [Dispatch]
        public void Dispatch(string payload, Callback callback)
        {
            DispathLocal(payload, callback);
            DispatchRemote(payload);
        }

        private void DispatchRemote(string payload)
        {
            foreach (var elem in _remoteInputPortWebsockets)
            {
                WebSocket ws = elem.Value;
                ws.SendAsync(payload, (z) => { _logger.Debug("remote send ok"); });
            }
        }

        private void DispathLocal(string payload, Callback callback)
        {
            foreach (var inpt in _inputPorts)
            {
                inpt.send(payload, callback);
            }
        }

        [Update]
        public void Update()
        {
            this.Stop();
            this.Start();
        }

        private string GetChanURI(string fragmentPath)
        {
            if (path.StartsWith("/"))
            {
                path = path.Substring(1);
            }

            if (path.EndsWith("/"))
            {
                path = path.Remove(path.Length - 1);
            }

            if (String.IsNullOrWhiteSpace(path))
            {
                return "ws://" + host + ":" + port + "/" + uuid + fragmentPath;
            }
            else
            {
                return "ws://" + host + ":" + port + "/" + path + "/" + uuid + fragmentPath;
            }
        }

        public void addInputPort(Port p)
        {
            this._inputPorts.Add(p);
        }

        public void removeInputPort(Port p)
        {
            this._inputPorts.Remove(p);
        }

        public void addRemoteInputPort(Port p)
        {
            this._remoteInputPorts.Add(p);
        }

        public void removeRemoteInputPort(Port p)
        {
            this._remoteInputPorts.Remove(p);
        }
    }
}

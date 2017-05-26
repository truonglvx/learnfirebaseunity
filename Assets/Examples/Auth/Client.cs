using UnityEngine;
using WebSocketSharp;

using System.Threading;
using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SimpleJSON;
using UnityToolbag;

using UnityEngine.UI;

public class Client : MonoBehaviour {

	public delegate void Response(string data);

	static Client m_instance;
	static public Client Instance
	{
		get { return m_instance; }
	}
	private string protocolVersion = "2";
    private WebSocket ws = null;
	private int currentRoomID;
	//private Room currentRoom = null;
	private bool isHost = false;
	private Hashtable rooms = new Hashtable();
	private List<EnqueuedMethod> enqueuedMethods;
	private Dictionary<string, Action<JSONClass>> subscriptions;
	private Dictionary<int, Action<JSONClass>> requests;
	private int ids = 0;
	private int heartbeatTimeout = 0;

	public string id = null; //assigned when hello response is receive

	public Text socketStatus;
	public Button ConnectSC;

//	public Define.ConnectionState state;

	void Awake() {
		m_instance = this;
	}

    void Start() {  

		enqueuedMethods = new List<EnqueuedMethod>();
		subscriptions = new Dictionary<string, Action<JSONClass>>();
		requests = new Dictionary<int, Action<JSONClass>>();

		Connect ("192.168.20.140:7777");
		//Connect("localhost:7777");

		ConnectSC.onClick.AddListener(() => SubscribeChannel());
    }

	void Update() {

	}

	void Destroy() {
		cleanup();
	}

	public void Connect(string endpoint)
	{
		//state = Define.ConnectionState.kCStateNone;
		this.ws = new WebSocket ("ws://" + endpoint);


		this.ws.OnOpen += OnOpenHandler;
		this.ws.OnMessage += OnMessageHandler;
		this.ws.OnClose += OnCloseHandler;
		this.ws.OnError += OnErrorHandler;

		this.ws.ConnectAsync();		
	}

	private void reconnect() {
	
	
	}

	private void heartbeat() {
		
		this.ws.Close();
	
	}

	private void beat() {

		if (this.heartbeatTimeout == 0) {
			return;
		}

		//check: Invoke Sync is better ???
		Dispatcher.InvokeAsync(() => {
			this.CancelInvoke("heartbeat");
			this.Invoke("heartbeat", this.heartbeatTimeout/1000.0f); //timeout in miliseconds
		});

	}

	private void hello() {

		JSONClass request = new JSONClass();
		request["type"] = "hello";
		request["version"] = protocolVersion;

		send(request, true, (JSONClass parameters) => {

			//state = Define.ConnectionState.kCStateConnected;
			
		});

	}

	private void cleanup() {
		
		if (ws.ReadyState == WebSocketState.Open ||
		   ws.ReadyState == WebSocketState.Connecting) {

			ws.Close();
		}


		ws.OnOpen -= OnOpenHandler;
		ws.OnMessage -= OnMessageHandler;
		ws.OnClose -= OnCloseHandler;
		ws.OnError -= OnErrorHandler;
		ws = null;

		id = null;
	
	}
		
	private void onSubscribeCallback(string message) {

		Debug.Log ("onSubscribeCallback: " + message);
	} 

	public void Subscribe(string path, Action<JSONClass> handler, Action<JSONClass> callback) {
		if (!subscriptions.ContainsKey(path))
		{
			subscriptions.Add (path, handler);
		}
		JSONClass request = new JSONClass();
		request["type"] = "sub";
		request["path"] = path;
		send(request, true, callback);

	}

	public void UnSubscribe(string path, Action<JSONClass> callback) {

		subscriptions.Remove(path);
		
		JSONClass request = new JSONClass();
		request["type"] = "unsub";
		request["path"] = path;
		send(request, true, callback);

	}

	
	public void Request(JSONClass options, Action<JSONClass> callback) {
		
		JSONClass request = new JSONClass();
		request["type"] = "request";
		request["method"] = options["method"];
		request["path"] = options["path"];

		if (options["payload"] != null)
			request["payload"] = options["payload"];

		send(request, true, callback);
	
	}

	public void Message(string message,  Action<JSONClass> callback) {

		JSONClass request = new JSONClass();
		request["type"] = "message";
		request["message"] = message;
		
		send(request, true, callback);
	}


	private void send(JSONClass data,  bool track = false, Action<JSONClass> callback = null) {

		data["id"].AsInt = ++ids; //request id for determining callback when received response from server
		if (track) {
			requests.Add(ids, callback);
		}
			
		if (ws == null || ws.ReadyState != WebSocketState.Open) {
			
			enqueuedMethods.Add(new EnqueuedMethod("send", new object[]{ data.ToString(), callback }));
		}
		else {
			
			ws.SendAsync(data.ToString(), (bool success) => {
				//Debug.Log("ws.SendAsync: " + success);
			});
		}

	}



	void onUpdate(string message) {
		Debug.Log (message);
	}


	private void parse(string data) {
		JSONNode update = JSON.Parse(data);
		var type = update ["type"].Value;


		if (type == "ping") {
			
			JSONClass ping = new JSONClass();
			ping ["type"] = "ping";
			send(ping);


			return;
		}

		//broadcase and update
		if(type == "update") {
			
			onUpdate(update["message"].Value);


			return;
		}

		//publish and revoke
		if(type == "pub" || type == "revoke") {

			//JSONClass metrics = update["message"];
			//Debug.Log("pub:" + metrics);
			Action<JSONClass> handler;
			subscriptions.TryGetValue(update["path"], out handler);
			handler(update["message"].AsObject);

			//todo: revoke implementing
			return;
		}

		//look up callback (messages must include an id from this point)
		int id = update["id"].AsInt;
		Action<JSONClass> callback;
		requests.TryGetValue (id, out callback);
		requests.Remove(id);

		//response
		if (type == "request") {
			//Debug.Log("update[payload] "+ update["payload"].ToString() );
			callback(update["payload"].AsObject);
			return;
		}

		//custom message
		if(type == "message") {
			callback(update["message"].AsObject);
			return;
		}

		//authentication
		if(type == "hello") {
			this.id = update["socket"];
			if(update["heartbeat"] != null) {
				this.heartbeatTimeout = update["heartbeat"]["interval"].AsInt + update["heartbeat"]["timeout"].AsInt;
				beat();
			}

			callback(null);


			return;
		}

		//subscriptions
		if(type == "sub" || type == "unsub") {
		
			callback(update["payload"].AsObject);

			return;
		}

	}
		

    private void OnMessageHandler(object sender, MessageEventArgs e) {

		beat();
		parse(e.Data);
        
    }

    private void OnCloseHandler(object sender, CloseEventArgs e) {
		
        Debug.Log("WebSocket closed with reason: " + e.Reason);
    }

	private void OnErrorHandler (object sender, ErrorEventArgs e)
	{
		Debug.Log ("Websocket error: " + e.Message);
	}


	private void OnOpenHandler(object sender, System.EventArgs e) {

		Debug.Log("WebSocket connected!");

		if (this.enqueuedMethods.Count > 0) {
			for (int i = 0; i < this.enqueuedMethods.Count; i++) {
				EnqueuedMethod enqueuedMethod = this.enqueuedMethods[i];
				Type thisType = this.GetType();
				MethodInfo method = thisType.GetMethod(enqueuedMethod.methodName);
				method.Invoke (this, enqueuedMethod.arguments);
			}
		}
			
		hello();

	}

	//////////////////////////////////////
	// Request Endpoints


	public void SubscribeChannel()
	{
        Subscribe("/machine/remoteconfig", (JSONClass message) => 
		{
            Dispatcher.InvokeAsync(() =>
            {
            	//update UI
            		socketStatus.text = message["event"];
            });
            Debug.Log(message["event"]);

		}, (JSONClass err) =>
        {
            if (err != null)
            {
                Debug.Log("/machine/remoteconfig subscribe error: ");

            } else {

            	Debug.Log("/machine/remoteconfig subscribe callback: ");
			}
			 Dispatcher.InvokeAsync(() =>
            {
            	//update UI
            		socketStatus.text = "helll!!!";
            });
        });
	}

    private static JSONClass GetErr(JSONClass err)
    {
        return err;
    }

    public void CreatePrivateRoom(JSONClass roomOptions = null) {
		//todo
		//create or automated match making
		JSONClass options = new JSONClass();
		options["method"] = "POST";
		options["path"] = "/gameroom/createprivateroom";
//		options["payload"]["userid"] = User.id;
		options["payload"]["roomoptions"] = roomOptions;

		Request(options, (JSONClass parameters) => {
		
			currentRoomID = parameters["id"].AsInt;

			isHost = true;
			JoinPrivateRoom(currentRoomID/*, roomOptions["maxplayers"].AsInt*/);

			Dispatcher.InvokeAsync(() =>
			{
				// Main.Instance.switchState(Define.AppState.kAStateMeeting);
				// Main.Instance.m_meetingWindow.GetComponent<MeetingWindow>().updateInfo(parameters);
				// Main.Instance.UpdateMeetingRoom(parameters);
				// Main.Instance.m_meetingWindow.GetComponent<MeetingWindow>().m_startBtn.SetActive(true);
			});

		});

	}


	public void JoinPrivateRoom(int roomid/*, int maxplayers*/)
	{		
		JSONClass options = new JSONClass();
		options["method"] = "POST";
		options["path"] = "/gameroom/joinprivateroom";
//		options["payload"]["userid"] = User.id; 
		options["payload"]["roomid"].AsInt = roomid;
		options["payload"]["ishost"].AsBool = isHost;
		//options["payload"]["maxplayers"].AsInt = maxplayers;//todo: clean code

		//game room general messages
		Subscribe("/gameroom/msg/" + roomid, (JSONClass message) => 
		{
			Dispatcher.InvokeAsync(() =>
			{
				// Main.Instance.UpdateMeetingRoom(message);
			});
		}, (JSONClass err) => {

				Debug.Log(roomid + " msg subscribe callback: ");

		});

		//game room metrics messages
		Subscribe("/gameroom/metrics/" + roomid, (JSONClass message) => { //Format: { userid: id, output: value, watt: value }
			Dispatcher.InvokeAsync(() =>
			{
				// Main.Instance.UpdateGameCompetitor(message);
			});

		}, (JSONClass err) => {

			Debug.Log(roomid + " metrics subscribe callback: ");
			
		});

		if(!isHost)
		{
			Request(options, (JSONClass parameters) => {

				Debug.Log("JoinPrivateRoom");
				currentRoomID = roomid;
				Dispatcher.InvokeAsync(() =>
				{
					// Main.Instance.switchState(Define.AppState.kAStateMeeting);
					// Main.Instance.m_meetingWindow.GetComponent<MeetingWindow>().updateInfo(parameters);
					// Main.Instance.UpdateMeetingRoom(parameters);
				});
			});
		}
	
	}

	public void JoinOrCreatePublicRoom(JSONClass roomOptions)
	 {
		 isHost = true;
		JSONClass options = new JSONClass();
		options["method"] = "POST";
		options["path"] = "/gameroom/joinorcreatepublicroom";
//		options["payload"]["userid"] = User.id;
		options["payload"]["roomoptions"] = roomOptions;

		Request(options, (JSONClass parameters) => {
			//returned info after automated match making
			currentRoomID = parameters["id"].AsInt;
			Debug.Log("JoinPublicRoom: " + currentRoomID);

				//game room general messages
			Subscribe("/gameroom/msg/" + currentRoomID, (JSONClass message) => 
			{
				Dispatcher.InvokeAsync(() =>
				{
					// Main.Instance.UpdateMeetingRoom(message);
				});
			}, (JSONClass err) => {

					Debug.Log(currentRoomID + " msg subscribe callback: ");

			});


			//game room metrics messages
			Subscribe("/gameroom/metrics/" + currentRoomID, (JSONClass message) => { //Format: { userid: id, output: value, watt: value }
				Dispatcher.InvokeAsync(() =>
				{
					// Main.Instance.UpdateGameCompetitor(message);
				});

			}, (JSONClass err) => {

				Debug.Log(currentRoomID + " metrics subscribe callback: ");
				
			});

			Dispatcher.InvokeAsync(() =>
			{
				//Main.Instance.switchState(Define.AppState.kAStateMatchMaking);				
				//Main.Instance.m_meetingWindow.GetComponent<MeetingWindow>().m_roomId.text = parameters["id"];
				//Main.Instance.UpdateMeetingRoom(parameters);
			});
			
		});

	}

	public void AddAIToPublicRoom(string id, JSONClass roomOptions)
	{
		JSONClass options = new JSONClass();
		options["method"] = "POST";
		options["path"] = "/gameroom/joinorcreatepublicroom";
		options["payload"]["userid"] = id;
		options["payload"]["roomoptions"] = roomOptions;
		Request(options, (JSONClass parameters) => {
			
			Debug.Log("added " +id + "  to currentRoom");
		});
	}

	public void Send(JSONClass metrics) {

		JSONClass options = new JSONClass();
		options["method"] = "POST";
		options["path"] = "/gameroom/metrics/" + currentRoomID;
		options["payload"] = metrics;
		Request(options, (JSONClass parameters) => {

			//Debug.Log("request callback response: ");

		});
	}


	// public void InviteFriends(string[] userids) {

	// 	JSONClass options = new JSONClass();
	// 	options["method"] = "POST";
	// 	options["path"] = "/gameroom/invitefriends";
	// 	options["payload"]["userid"] = User.id;
	// 	options["payload"]["roomid"].AsInt = currentRoomID;
	// 	options["payload"]["eventtype"].AsInt = (int)Define.RoomEvent.kRoomEventInvite;
	// 	JSONNode friendlist = new JSONNode();
	// 	for(int i=0; i<userids.Length; i++)
	// 	{
	// 		friendlist.Add("", userids[i]);
	// 	}
	// 	options["payload"]["invitedids"] = friendlist;
	// 	Request(options, (JSONClass parameters) => {

	// 		Debug.Log("Invite Friends");
	// 		currentRoom.Leave(true);
	// 	});

	// }

	public void LeaveRoom()
	{
		JSONClass options = new JSONClass();
		options["method"] = "POST";
		options["path"] = "/gameroom/leaveroom";
//		options["payload"]["userid"] = User.id; 
		options["payload"]["roomid"].AsInt = currentRoomID;
		Request(options, (JSONClass parameters) => {
			Debug.Log("LeaveRoom");
			// currentRoom.Leave(true);
		});

	}

	// public void SetRoomState(Define.RoomState state )
	// {
	// 	//if (!isHost)
	// 	//	return;
	// 	JSONClass options = new JSONClass();
	// 	options["method"] = "POST";
	// 	options["path"] = "/gameroom/roomstate";
	// 	options["payload"]["roomid"].AsInt = currentRoomID;
	// 	options["payload"]["roomstate"].AsInt = (int)state; //roomstate enum;
	// 	Request(options, (JSONClass parameters) => {

	// 		Debug.Log("Change Room State");
	// 	});

	// }

	// public void SetPlayerState(Define.PlayerState state) {

	// 	JSONClass options = new JSONClass();
	// 	options["method"] = "POST";
	// 	options["path"] = "/gameroom/playerstate";
	// 	options["payload"]["roomid"].AsInt = currentRoomID;
	// 	options["payload"]["userid"] = User.id;
	// 	options["payload"]["userstate"].AsInt = (int)state;
	// 	Request(options, (JSONClass parameters) => {

	// 		Debug.Log("Change User State");
	// 	});

	// }


	public void GetRoomList() {

		JSONClass options = new JSONClass();
		options["method"] = "GET";
		options["path"] = "/gameroom/roomlist";
		Request(options, (JSONClass parameters) => {

			Debug.Log("GetRoomList");
		});

	}

	public void GetRoomPlayers(int roomid) {

		JSONClass options = new JSONClass();
		options["method"] = "POST";
		options["path"] = "/gameroom/getplayers";
		options["payload"]["roomid"].AsInt = roomid;
		Request(options, (JSONClass parameters) => {

			Debug.Log("GetRoomPlayers");
		});

	}


	// public void SaveGameMetrics(string metrics)
	// {
	// 	if (state != Define.ConnectionState.kCStateConnected)
	// 		return;
		
	// 	JSONClass options = new JSONClass();
	// 	options["method"] = "POST";
	// 	options["path"] = "/gameroom/savegamemetrics";
	// 	options["payload"]["roomid"].AsInt = currentRoomID;
	// 	options["payload"]["userid"] = User.id;
	// 	options["payload"]["metrics"] = metrics;

	// 	Request(options, (JSONClass parameters) => {
	// 		//SetPlayerState(Define.PlayerState.kPlayerStateFinished);
	// 		//Debug.Log("save game metrics");
	// 	});
	// }

	// public void GetGameMetrics(int roomid, string userid, Response response )
	// {
	// 	JSONClass options = new JSONClass();
	// 	options["method"] = "POST";
	// 	options["path"] = "/gameroom/getgamemetrics";
	// 	options["payload"]["roomid"].AsInt = roomid;
	// 	options["payload"]["userid"] = userid;
	

	// 	Request(options, (JSONClass parameters) => {			
	// 		//Debug.Log("get game metrics");
	// 		if (response != null)
	// 		{
	// 			string data = parameters["metrics"];
	// 			Dispatcher.InvokeAsync(() => {
	// 				response(data);
	// 			});
	// 		}
	// 	});
	// }

	// public void SaveCompetitionHistory(Define.GameType gametype, int rank, float output, float calories, float second)
	// {
	// 	JSONClass options = new JSONClass();
	// 	options["method"] = "POST";
	// 	options["path"] = "/gameroom/savecompetitionhistory";
	// 	options["payload"]["roomid"].AsInt = currentRoomID;
	// 	options["payload"]["userid"] = User.id;
	// 	options["payload"]["gametype"].AsInt = (int)gametype;
	// 	options["payload"]["output"].AsFloat = output;
	// 	options["payload"]["calories"].AsFloat = calories;
	// 	//options["payload"]["meters"] = userid;
	// 	//options["payload"]["strokes"] = userid;
	// 	options["payload"]["time"].AsFloat = second;
	// 	options["payload"]["rank"].AsInt = rank;

	

	// 	Request(options, (JSONClass parameters) => {			
	// 		//Debug.Log("save competition history");
			
	// 	});
	// }


	public void GetCompetitionHistory(string userid, int timeInterval, Response response)
	{
		JSONClass options = new JSONClass();
		options["method"] = "POST";
		options["path"] = "/gameroom/getcompetitionhistory";
		options["payload"]["userid"] = userid;
		options["payload"]["timeinterval"].AsInt = timeInterval;
		// options["payload"]["output"] = userid;
		// options["payload"]["calories"] = userid;
		// options["payload"]["meters"] = userid;
		// options["payload"]["strokes"] = userid;
		// options["payload"]["time"] = userid;
		// options["payload"]["rank"] = userid;

	

		Request(options, (JSONClass parameters) => {	
			Debug.Log("GetCompetitionHistory" );
			if (parameters != null && response != null)
			{
				Debug.Log("reply" + parameters["metrics"].ToString());
				Dispatcher.InvokeAsync(() => {
					response(parameters["metrics"].ToString());
				});
			}
		});
	}




	public void GenRoomId()
	{
		JSONClass options = new JSONClass();
		options["method"] = "GET";
		options["path"] = "/gameroom/genroomid";

		Request(options, (JSONClass parameters) => {
			currentRoomID = parameters["roomid"].AsInt;
		});

	}
	
  	// Timers utility

	IEnumerator WaitAndRun(float seconds, Action action) {
		yield return new WaitForSeconds (seconds);
		action();
	}

	IEnumerator WaitAndRunRepeat(float seconds, Action action) {
		yield return new WaitForSeconds(seconds);
		action();
		StartCoroutine(WaitAndRunRepeat(seconds, action));
	}


	//utility class
	class EnqueuedMethod {
		public string methodName;
		public object[] arguments;

		public EnqueuedMethod(string methodName, object[] arguments) {
			this.methodName = methodName;
			this.arguments = arguments;
		}
	}
		
}




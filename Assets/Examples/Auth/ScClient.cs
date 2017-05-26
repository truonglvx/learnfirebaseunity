using System;
using System.Collections.Generic;
using System.Threading;
using SuperSocket.ClientEngine;
using UnityEngine;


namespace ScClient
{
    internal class MyListener: BasicListener
    {
        public void onConnected(Socket socket)
        {
            Debug.Log("connected got called");
        }

        public void onDisconnected(Socket socket)
        {
             Debug.Log("disconnected got called");
        }

        public void onConnectError(Socket socket, ErrorEventArgs e)
        {
             Debug.Log("on connect error got called");
        }

        public void onAuthentication(Socket socket, bool status)
        {
             Debug.Log(status ? "Socket is authenticated" : "Socket is not authenticated");
        }

        public void onSetAuthToken(string token, Socket socket)
        {
            socket.setAuthToken(token);
             Debug.Log("on set auth token got called");
        }


    }

    public class SocketClient
    {

        public Socket socket;
	// Use this for initialization
        public SocketClient () {

                //socket= new Socket("ws://104.198.150.237:8001/socketcluster/");
                socket= new Socket("ws://localhost:8000/socketcluster/");
                socket.setListerner(new MyListener());
                socket.setReconnectStrategy(new ReconnectStrategy().setMaxAttempts(3));
                socket.connect();


            
                // var channel = socket.createChannel("avironchannel");
                // channel.subscribe((channelName, error, data) => {

                //     if(error != null) {
                //         Debug.Log("channel subscribe fails");
                //     } else {
                //         Debug.Log("channel subscribe succeeds");
                //     }

                // });
             
            //    socket.on("avironmsg", (name, data, ack) => {
            //         Debug.Log("Aviron event: " + name + " -data: " + data);
            //         ack(name, null, null);
            //    });

                // socket.on("yell",(name, data, ack) =>
                // {
                //     //Console.WriteLine("event :"+name+" data:"+data);
                //     Debug.Log("event :"+name+" data:"+data);
                //     ack(name, " yell error ", " This is sample data ");
                // });

                
                // socket.onSubscribe("avironchannel", (channelName , data) => {
                //     Debug.Log("Got data from channel: " + channelName + " data: " + data);
                // });

                // socket.onSubscribe("yell", (name, data) =>
                // {
                //     //Console.WriteLine("Got data for channel:: "+name+ " data :: "+data);
                //     Debug.Log("Got data for channel: "+ name + " data :: "+data);
                // });

            
        }

        public void publish(string channel, string msg)
        {
            socket.publish(channel, msg);
        }

        public void emit(string eventName, string msg)
        {
            socket.emit(eventName, msg, (name, error, data) => {
                Debug.Log("got msg for " + eventName + "error is " + error + " data is " + data);
            });
        }
        
    }

}
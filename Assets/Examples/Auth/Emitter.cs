
using System.Collections.Generic;
using UnityEngine;

namespace ScClient
{
    public class Emitter
    {
        public delegate void Listener(string name,object data);
        public delegate void Ackcall(string name, object error, object data);
        public delegate void AckListener(string name,object data,Ackcall ack);


        private Dictionary <string,Listener> singlecallbacks=new Dictionary<string, Listener>();
        private Dictionary<string,AckListener> singleackcallbacks=new Dictionary<string, AckListener>();
        private Dictionary<string,Listener> publishcallbacks=new Dictionary<string, Listener>();

        public Emitter on(string Event, Listener fn)
        {
              Debug.Log("Emitter on Listener");
            if (singlecallbacks.ContainsKey(Event))
            {
                singlecallbacks.Remove(Event);
            }

            singlecallbacks.Add(Event,fn);

            return this;
        }

        public Emitter onSubscribe(string Event,Listener fn){

            Debug.Log("Emitter onSubscribe");
            if (publishcallbacks.ContainsKey(Event))
            {
                publishcallbacks.Remove(Event);
            }
            publishcallbacks.Add(Event, fn);
            return this;
        }

        public Emitter on(string Event, AckListener fn)
        {
              Debug.Log("Emitter on ACK 1");
            if (singleackcallbacks.ContainsKey(Event))
            {
                 Debug.Log("Emitter on ACK 2");
                singleackcallbacks.Remove(Event);
            }
            singleackcallbacks.Add(Event,fn);
            return this;
        }

        public Emitter handleEmit(string Event, object Object)
        {
            
            if (singlecallbacks.ContainsKey(Event))
            {
                Debug.Log("Emitter handleEmit");
                Listener listener = singlecallbacks[Event];
                listener(Event, Object);
            }
            return this;
        }

        public Emitter handlePublish(string Event, object Object){

            Debug.Log("Emitter handlePublish");
            if (publishcallbacks.ContainsKey(Event))
            {
                Debug.Log("Emitter handlePublish");
                Listener listener = publishcallbacks[Event];
                listener(Event,Object);
            }
            return this;
        }

        public bool hasEventAck(string Event)
        {
           // Debug.Log("Emitter hasEventAck");
            return singleackcallbacks.ContainsKey(Event);
        }

        public Emitter handleEmitAck(string Event, object Object , Ackcall ack){

             Debug.Log("Emitter handleEmitAck");
            if (singleackcallbacks.ContainsKey(Event))
            {
               
                AckListener listener = singleackcallbacks[Event];
                listener(Event,Object,ack);
            }
            return this;
        }


    }
}
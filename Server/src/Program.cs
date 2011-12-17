/*
 * Copyright 2011 Olivine Labs, LLC. <http://olivinelabs.com>
 * Licensed under the MIT license: <http://www.opensource.org/licenses/mit-license.php>
 */

/*
 * Includes a reference to the Json.NET library <http://json.codeplex.com>, used under 
 * MIT license. See <http://www.opensource.org/licenses/mit-license.php>  for license details. 
 * Json.NET is copyright 2007 James Newton-King
 */

/* 
 * Includes the Alchemy Websockets Library 
 * <http://www.olivinelabs.com/index.php/projects/71-alchemy-websockets>, 
 * used under LGPL license. See <http://www.gnu.org/licenses/> for license details. 
 * Alchemy Websockets is copyright 2011 Olivine Labs.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Alchemy.Server;
using Alchemy.Server.Classes;
using Newtonsoft.Json;
using System.Net;

namespace ChatServer
{
    class Program
    {
        // We'll just store a list of online users here. Keeping this IList here as it is
        // isn't a good way to do things, as we're running in a multi-threaded environment. 
        /// <summary>
        /// Store the list of online users
        /// </summary>
        protected static IList<User> OnlineUsers = new List<User>();

        /// <summary>
        /// Initialize the application and start the Alchemy Websockets server
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            // Initialize the server on port 8100, accept any IPs, and bind events.
            var aServer = new WebSocketServer(81, IPAddress.Any)
                              {
                                  DefaultOnReceive = new OnEventDelegate(OnReceive),
                                  DefaultOnSend = new OnEventDelegate(OnSend),
                                  DefaultOnConnect = new OnEventDelegate(OnConnect),
                                  DefaultOnDisconnect = new OnEventDelegate(OnDisconnect),
                                  TimeOut = new TimeSpan(0, 5, 0)
                              };

            aServer.Start();

            // Accept commands on the console and keep it alive
            var command = string.Empty;
            while (command != "exit")
            {
                command = Console.ReadLine();
            }

            aServer.Stop();
        }

        /// <summary>
        /// Event fired when a client connects to the Alchemy Websockets server instance.
        /// Adds the client to the online users list.
        /// </summary>
        /// <param name="aContext">The user's connection context</param>
        public static void OnConnect(UserContext aContext)
        {
            Console.WriteLine("Client Connection From : " + aContext.ClientAddress.ToString());

            var me = new User {Context = aContext};

            OnlineUsers.Add(me);
        }

        /// <summary>
        /// Event fired when a data is received from the Alchemy Websockets server instance.
        /// Parses data as JSON and calls the appropriate message or sends an error message.
        /// </summary>
        /// <param name="aContext">The user's connection context</param>
        public static void OnReceive(UserContext aContext)
        {
            Console.WriteLine("Received Data From :" + aContext.ClientAddress.ToString());

            try
            {
                var json = aContext.DataFrame.ToString();

                // <3 dynamics
                dynamic obj = JsonConvert.DeserializeObject(json);

                if ((int)obj.Type == (int)CommandType.Register)
                {
                    Register(obj.Name.Value, aContext);
                }
                else if ((int)obj.Type == (int)CommandType.Message)
                {
                    ChatMessage(obj.Message.Value, aContext);
                }
                else if ((int)obj.Type == (int)CommandType.NameChange)
                {
                    NameChange(obj.Name.Value, aContext);
                }
            }
            catch (Exception e) // Bad JSON! For shame.
            {
                var r = new Response {Type = ResponseType.Error, Data = new {Message = e.Message}};

                aContext.Send(JsonConvert.SerializeObject(r));
            }
        }

        /// <summary>
        /// Event fired when the Alchemy Websockets server instance sends data to a client.
        /// Logs the data to the console and performs no further action.
        /// </summary>
        /// <param name="aContext">The user's connection context</param>
        public static void OnSend(UserContext aContext)
        {
            Console.WriteLine("Data Send To : " + aContext.ClientAddress.ToString());
        }

        // NOTE: This is not safe code. You may end up broadcasting to people who
        // disconnected. Luckily for us, Alchemy handles exceptions in its event methods, so we don't
        // have random, catastrophic changes.
        /// <summary>
        /// Event fired when a client disconnects from the Alchemy Websockets server instance.
        /// Removes the user from the online users list and broadcasts the disconnection message
        /// to all connected users.
        /// </summary>
        /// <param name="aContext">The user's connection context</param>
        public static void OnDisconnect(UserContext aContext)
        {
            Console.WriteLine("Client Disconnected : " + aContext.ClientAddress.ToString());
            var user = OnlineUsers.Where(o => o.Context.ClientAddress == aContext.ClientAddress).Single();

            OnlineUsers.Remove(user);

            if (!String.IsNullOrEmpty(user.Name))
            {
                var r = new Response {Type = ResponseType.Disconnect, Data = new {Name = user.Name}};

                Broadcast(JsonConvert.SerializeObject(r));
            }

            BroadcastNameList();
        }

        /// <summary>
        /// Register a user's context for the first time with a username, and add it to the list of online users
        /// </summary>
        /// <param name="name">The name to register the user under</param>
        /// <param name="aContext">The user's connection context</param>
        private static void Register(string name, UserContext aContext)
        {
            var u = OnlineUsers.Where(o => o.Context.ClientAddress == aContext.ClientAddress).Single();
            var r = new Response();

            if (ValidateName(name)) {
                u.Name = name;

                r.Type = ResponseType.Connection;
                r.Data = new { Name = u.Name };

                Broadcast(JsonConvert.SerializeObject(r));

                BroadcastNameList();
            }
            else
            {
               SendError("Name is of incorrect length.", aContext);
            }
        }

        /// <summary>
        /// Broadcasts a chat message to all online usrs
        /// </summary>
        /// <param name="message">The chat message to be broadcasted</param>
        /// <param name="aContext">The user's connection context</param>
        private static void ChatMessage(string message, UserContext aContext)
        {
            var u = OnlineUsers.Where(o => o.Context.ClientAddress == aContext.ClientAddress).Single();
            var r = new Response {Type = ResponseType.Message, Data = new {Name = u.Name, Message = message}};

            Broadcast(JsonConvert.SerializeObject(r));

        }

        /// <summary>
        /// Update a user's name if they sent a name-change command from the client.
        /// </summary>
        /// <param name="Name">The name to be changed to</param>
        /// <param name="AContext">The user's connection context</param>
        private static void NameChange(string Name, UserContext AContext)
        {
            var u = OnlineUsers.Where(o => o.Context.ClientAddress == AContext.ClientAddress).Single();
            Response r;

            if (ValidateName(Name)) { 
                r = new Response
                        {
                            Type = ResponseType.NameChange,
                            Data = new {Message = u.Name + " is now known as " + Name}
                        };
                Broadcast(JsonConvert.SerializeObject(r));

                u.Name = Name;

                BroadcastNameList();
            }
            else
            {
                SendError("Name is of incorrect length.", AContext);
            }
        }

        /// <summary>
        /// Broadcasts an error message to the client who caused the error
        /// </summary>
        /// <param name="errorMessage">Details of the error</param>
        /// <param name="aContext">The user's connection context</param>
        private static void SendError(string errorMessage, UserContext aContext)
        {
            var r = new Response {Type = ResponseType.Error, Data = new {Message = errorMessage}};

            aContext.Send(JsonConvert.SerializeObject(r));
        }

        /// <summary>
        /// Broadcasts a list of all online users to all online users
        /// </summary>
        private static void BroadcastNameList()
        {
            var r = new Response
                        {
                            Type = ResponseType.UserCount,
                            Data = new {Users = OnlineUsers.Where(o => o.Name != null).Select(o => o.Name).ToArray()}
                        };
            Broadcast(JsonConvert.SerializeObject(r));
        }

        /// <summary>
        /// Broadcasts a message to all users, or if users is populated, a select list of users
        /// </summary>
        /// <param name="message">Message to be broadcast</param>
        /// <param name="users">Optional list of users to broadcast to. If null, broadcasts to all. Defaults to null.</param>
        private static void Broadcast(string message, IList<User> users = null)
        {
            if (users == null)
            {
                foreach (User u in OnlineUsers)
                {
                    u.Context.Send(message);
                }
            }
            else
            {
                foreach (User u in OnlineUsers)
                {
                    if (users.Contains(u))
                    {
                        u.Context.Send(message);
                    }
                }
            }
        }

        /// <summary>
        /// Checks validity of a user's name
        /// </summary>
        /// <param name="name">Name to check</param>
        /// <returns></returns>
        private static bool ValidateName(string name)
        {
            var isValid = false;
            if (name.Length > 3 && name.Length < 25)
            {
                isValid = true;
            }

            return isValid;
        }

        /// <summary>
        /// Defines the type of response to send back to the client for parsing logic
        /// </summary>
        public enum ResponseType : int
        {
            Connection = 0,
            Disconnect = 1,
            Message = 2,
            NameChange = 3,
            UserCount = 4,
            Error = 255
        }

        /// <summary>
        /// Defines the response object to send back to the client
        /// </summary>
        public class Response
        {
            public ResponseType Type { get; set; }
            public dynamic Data { get; set; }
        }

        /// <summary>
        /// Holds the name and context instance for an online user
        /// </summary>
        public class User
        {
            public string Name = String.Empty;
            public UserContext Context { get; set; }
        }

        /// <summary>
        /// Defines a type of command that the client sends to the server
        /// </summary>
        public enum CommandType : int
        {
            Register = 0,
            Message,
            NameChange
        }
    }
}

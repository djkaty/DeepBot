// DeepBot C# API Client Library
// Written by Katy Coe
// (c) Noisy Cow Studios 2015
// http://www.djkaty.com

// Version 1.0.2.0
// Built against DeepBot v0.7.1.5

// Targets .NET Framework 4.5
// Built with Visual Studio 2013

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Web.Helpers;
using WebSocket4Net;
using ErrorEventArgs = SuperSocket.ClientEngine.ErrorEventArgs;

namespace DeepBotServices
{
    /// <summary>
    /// VIP Levels
    /// </summary>
    public enum VIP
    {
        /// <summary>
        /// Regular user
        /// </summary>
        Regular = 0,

        /// <summary>
        /// VIP Bronze
        /// </summary>
        Bronze,

        /// <summary>
        /// VIP Silver
        /// </summary>
        Silver,

        /// <summary>
        /// VIP Gold
        /// </summary>
        Gold
    }

    /// <summary>
    /// User Levels
    /// </summary>
    public enum Level
    {
        /// <summary>
        /// Normal user
        /// </summary>
        User = 0,
        
        /// <summary>
        /// Channel moderator ("mod level 1")
        /// </summary>t
        ChannelMod,

        /// <summary>t
        /// DeepBot moderator ("mod level 2")
        /// </summary>
        BotMod,

        /// <summary>
        /// DeepBot (the bot iself)
        /// </summary>
        Bot = 4,

        /// <summary>
        /// The streamer (channel op)
        /// </summary>
        Op
    }

    /// <summary>
    /// A single DeepBot user.
    /// </summary>
    public class User
    {
        private DeepBot source;

        /// <summary>
        /// The last time the user was synced from the bot, if auto-caching is enabled
        /// </summary>
        public DateTime LastUpdate { get; private set; }

        /// <summary>
        /// The username
        /// </summary>
        public string Name { get; }

        private int points = -1;

        /// <summary>
        /// Gets or sets the number of points locally and remotely. Only updates locally on remote success.
        /// <remarks>Retrieves the local value of points. When setting, calls the API to update points in DeepBot automatically. Local point value is only changed if the remote update was successful.</remarks>
        /// <exception cref="DeepBotException">Throws if the points value cannot be changed on DeepBot.</exception>
        /// <exception cref="InvalidOperationException">Thrown if not connected to the bot and AutoConnect == false, or if the bot did not respond within ResponseWait milliseconds</exception>
        /// </summary>
        public int Points
        {
            get
            {
                update();
                return points;
            }
            set
            {
                // API sync
                // Note we don't use SetPoints because that might corrupt timed point additions in the bot itself
                bool ok = true;

                if (points > value)
                    ok = source.DelPoints(Name, points - value);
                else if (points < value)
                    ok = source.AddPoints(Name, value - points);

                if (ok)
                    points = source[Name].Points;
                else
                    throw new DeepBotException("Could not change points value");
            }
        }

        private int minutes;
        /// <summary>
        /// The total number of minutes the user has watched the stream for.
        /// </summary>
        public int Minutes
        {
            get { update(); return minutes; }
            set { minutes = value; }
        }

        /// <summary>
        /// Get the user's total number of watched hours from local storage
        /// </summary>
        /// <returns>User watched hours</returns>
        public double Hours
        {
            get
            {
                return (double)Minutes / 60;
            }
        }

        private VIP vipLevel;

        /// <summary>
        /// Gets or sets the user's VIP level, locally and remotely. Only updates locally on remote success.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if not connected to the bot and AutoConnect == false, or if the bot did not respond within ResponseWait milliseconds</exception>
        /// <exception cref="DeepBotException">Thrown if the VIP level could not be set, or if invalid parameters were supplied.</exception>
        public VIP VIPLevel
        {
            get { update(); return vipLevel; }
            set
            {
                source.SetVIPLevel(Name, value);
                vipLevel = value;
            }
        }

        private Level userLevel;

        /// <summary>
        /// The user's moderation level.
        /// </summary>
        public Level UserLevel
        {
            get { update(); return userLevel; }
            set { userLevel = value; }
        }

        private DateTime firstSeen;

        /// <summary>
        /// The user's initial join date.
        /// </summary>
        public DateTime FirstSeen
        {
            get { update(); return firstSeen; }
            set { firstSeen = value; }
        }

        private DateTime lastSeen;

        /// <summary>
        /// The user's last seen date.
        /// </summary>
        public DateTime LastSeen
        {
            get { update(); return lastSeen; }
            set { lastSeen = value; }
        }

        private DateTime vipExpiry;

        /// <summary>
        /// The user's VIP expiry date.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if not connected to the bot and AutoConnect == false, or if the bot did not respond within ResponseWait milliseconds</exception>
        /// <exception cref="DeepBotException">Thrown if the VIP expiry could not be set, or if invalid parameters were supplied.</exception>
        public DateTime VIPExpiry
        {
            get { update();  return vipExpiry; }
            set
            {
                source.SetVIPExpiry(Name, value);
                vipExpiry = value;
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="src">The DeepBot instance this user belongs to.</param>
        /// <param name="firstseen">First seen date of user</param>
        /// <param name="lastseen">Last seen date of user</param>
        /// <param name="minutes">Watch minutes of user</param>
        /// <param name="name">Name of user</param>
        /// <param name="points">Number of points</param>
        /// <param name="userlevel">User level</param>
        /// <param name="vipexpiry">VIP expiry time</param>
        /// <param name="viplevel">VIP level of user</param>
        public User(DeepBot src, string name = "", int points = 0, int minutes = 0, VIP viplevel = VIP.Regular, Level userlevel = Level.User,
            DateTime? firstseen = null, DateTime? lastseen = null, DateTime? vipexpiry = null)
        {
            source = src;
            Name = name;
            this.points = points;
            this.minutes = minutes;
            vipLevel = ((int)viplevel == 10 ? VIP.Regular : viplevel);
            userLevel = userlevel;
            firstSeen = (firstseen == null ? DateTime.Now : (DateTime) firstseen);
            lastSeen = (lastseen == null ? DateTime.Now : (DateTime) lastseen);
            vipExpiry = (vipexpiry == null ? DateTime.Now : (DateTime)vipexpiry);
            LastUpdate = DateTime.Now;
        }

        /// <summary>
        /// Update from server if necessary
        /// </summary>
        private void update()
        {
            if (!source.AutoCache)
                return;

            User u = source.GetUser(Name);
            
            points = u.points;
            minutes = u.minutes;
            vipLevel = u.vipLevel;
            userLevel = u.userLevel;
            firstSeen = u.firstSeen;
            lastSeen = u.lastSeen;
            vipExpiry = u.vipExpiry;
            LastUpdate = DateTime.Now;
        }

        /// <summary>
        /// Close a user.
        /// </summary>
        public User Clone()
        {
            return (User)this.MemberwiseClone();
        }

        /// <summary>
        /// Format the DeepBot user as a human-readable string
        /// </summary>
        /// <returns>String representation of user</returns>
        public override string ToString()
        {
            return string.Format("[Name = {0}, Points = {1}, Minutes = {2}, VIPLevel = {3}, UserLevel = {4}, FirstSeen = {5}, LastSeen = {6}, VIPExpiry = {7}]",
                Name, points, minutes, vipLevel, userLevel, firstSeen, lastSeen, vipExpiry);
        }

        /// <summary>
        /// Test two users for value equality
        /// </summary>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(obj, null) || GetType() != obj.GetType())
                return false;

            return ToString() == obj.ToString();
        }

        /// <summary>
        /// Overload == for value equality
        /// </summary>
        public static bool operator ==(User lhs, User rhs)
        {
            if (ReferenceEquals(lhs, null) && ReferenceEquals(rhs, null))
                return true;

            if (ReferenceEquals(lhs, null) || ReferenceEquals(rhs, null))
                return false;

            return lhs.Equals(rhs);
        }

        /// <summary>
        /// Overload != for value inequality
        /// </summary>
        public static bool operator !=(User lhs, User rhs)
        {
            return !(lhs == rhs);
        }

        /// <summary>
        /// Hash code
        /// </summary>
        public override int GetHashCode()
        {
            return ToString().GetHashCode();
        }

        /// <summary>
        /// Set the exact number of points explicitly for a user locally and remotely. Only updates locally on remote success.
        /// </summary>
        /// <param name="points">Number of points</param>
        /// <exception cref="InvalidOperationException">Thrown if not connected to the bot and AutoConnect == false, or if the bot did not respond within ResponseWait milliseconds</exception>
        /// <returns>True on success, false on failure</returns>
        public bool SetPoints(int points)
        {
            if (source.SetPoints(Name, points))
            {
                this.points = points;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Get the user's rank string
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if not connected to the bot and AutoConnect == false, or if the bot did not respond within ResponseWait milliseconds</exception>
        /// <returns>User rank string</returns>
        public string GetRank()
        {
            return source.GetRank(Name);
        }

        /// <summary>
        /// Adds a number of days to the user's VIP access, locally and remotely. Only updates locally on remote success.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if not connected to the bot and AutoConnect == false, or if the bot did not respond within ResponseWait milliseconds</exception>
        /// <param name="daysToAdd">Number of days to add. If the current expiry is in the past, sets the expiry to the specified number of days from now.</param>
        public void AddVIPDays(int daysToAdd)
        {
            source.SetVIPLevel(Name, vipLevel, daysToAdd);
            vipExpiry = source[Name].VIPExpiry;
        }

        /// <summary>
        /// Set the user's VIP expiry time.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if not connected to the bot and AutoConnect == false, or if the bot did not respond within ResponseWait milliseconds</exception>
        /// <exception cref="DeepBotException">Thrown if the VIP expiry could not be set, or if invalid parameters were supplied.</exception>
        /// <exception cref="FormatException">Thrown if the date format is incorrect</exception>
        /// <param name="expiry">The VIP expiry time in local time in the ISO format "yyyy-MM-ddThh:mm:ss"</param>
        public void SetVIPExpiry(string expiry)
        {
            VIPExpiry = DateTime.ParseExact(expiry, "s", System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Add points to escrow remotely. Note: this does not affect the local points value.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if not connected to the bot and AutoConnect == false, or if the bot did not respond within ResponseWait milliseconds</exception>
        /// <param name="points">Number of points to escrow</param>
        public void AddToEscrow(int points)
        {
            source.AddToEscrow(Name, points);
        }

        /// <summary>
        /// Commit points to escrow remotely and update local points value to match.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if not connected to the bot and AutoConnect == false, or if the bot did not respond within ResponseWait milliseconds</exception>
        public void CommitEscrow()
        {
            source.CommitEscrow(Name);
            points = source[Name].Points;
        }

        /// <summary>
        /// Cancel escrowed points remotely. Note: this does not affect the local points value.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if not connected to the bot and AutoConnect == false, or if the bot did not respond within ResponseWait milliseconds</exception>
        public void CancelEscrow()
        {
            source.CancelEscrow(Name);
        }
    }

    /// <summary>
    /// DeepBot exceptions. Thrown when an invalid request is made to DeepBot.
    /// </summary>
    [Serializable]
    public class DeepBotException : Exception
    {
        /// <summary>
        /// Empty exception
        /// </summary>
        public DeepBotException() { }

        /// <summary>
        /// Exception with error message (usually from DeepBot)
        /// </summary>
        /// <param name="message"></param>
        public DeepBotException(string message) : base(message) { }
    }

    /// <summary>
    /// DeepBot API
    /// </summary>
    public class DeepBot
    {
        // WebSocket
        private WebSocket ws;   

        /// <summary>
        /// True if connected, false otherwise
        /// </summary>
        public bool Connected { get { return ws.State == WebSocketState.Open; } }

        /// <summary>
        /// True if the connection has been closed, false otherwise
        /// </summary>
        public bool Closed { get { return ws.State == WebSocketState.Closed; } }

        /// <summary>
        /// True if the API secret has been authenticated, false otherwise
        /// </summary>
        public bool? Authenticated { get; private set; } = null;

        /// <summary>
        /// URI to DeepBot server (default is ws://localhost:3337)
        /// </summary>
        public string URI { get; set; }

        /// <summary>
        /// DeepBot API Secret
        /// </summary>
        public string Secret { get; set; }

        /// <summary>
        /// True to seamlessly connect to the bot and authenticate when required for a transaction, and automatically reconnect if there is a connection failure (eg. bot restart). False to use classical connection paradigm.
        /// </summary>
        public bool AutoConnect { get; set; } = true;

        /// <summary>
        /// Milliseconds to wait for bot response before we assume failure (if DeepBot does not reply due to not understanding or receiving message)
        /// </summary>
        public int ResponseWait { get; set; } = 5000;

        /// <summary>
        /// Get or set whether to auto-cache users (refresh if more than 60 seconds has elapsed since the last request for a given user)
        /// </summary>
        public bool AutoCache { get; set; } = true;

        private Dictionary<string, User> cachedUsers = new Dictionary<string, User>();

        // Last response message
        private dynamic response;

        /// <summary>
        /// Create bot API with standard server URI (ws://localhost:3337)
        /// </summary>
        public DeepBot() : this("ws://localhost:3337/") { }

        /// <summary>
        /// Create bot API with custom server URI and optional API secret
        /// </summary>
        /// <param name="uri">Server URI</param>
        /// <param name="secret">API secret</param>
        public DeepBot(string uri, string secret = "") { URI = uri; Secret = secret; }

        /// <summary>
        /// Connect with optional server URI and optional API secret. Uses constructor or property values if none supplied. Not required if AutoConnect == true.
        /// </summary>
        /// <param name="uri">Server URI</param>
        /// <param name="secret">API secret</param>
        /// <exception cref="System.InvalidOperationException">Throws if could not connect to the bot</exception>
        /// <returns>True if connected and authenticated, false otherwise. API secret (Secret) will be blanked if incorrect.</returns>
        public bool Connect(string uri = "", string secret = "")
        {
            if (uri.Length != 0)
                URI = uri;

            if (secret.Length != 0)
                Secret = secret;

            // Connect to server
            do
            {
                Authenticated = null;

                ws = new WebSocket(URI);
                ws.Opened += websocket_Opened;
                ws.Error += websocket_Error;
                ws.Closed += websocket_Closed;
                ws.MessageReceived += websocket_MessageReceived;
                ws.Open();

                for (int w = 0; w < ResponseWait && (ws.State == WebSocketState.Connecting || Connected) && Authenticated == null; w += 100)
                    System.Threading.Thread.Sleep(100);

                if (Authenticated == false)
                    throw new DeepBotException("Invalid API Secret");

                if (Authenticated == null)
                    if (!AutoConnect)
                        throw new InvalidOperationException("Could not connect to DeepBot");
                    else
                    {
                        ws.Close();
                        System.Threading.Thread.Sleep(2000);
                    }
            }
            while (Authenticated == null && AutoConnect);

            return (bool) Authenticated;
        }

        private void blockingCall(string msg)
        {
            response = null;

            if (Authenticated != true)
                if (!AutoConnect)
                    throw new InvalidOperationException("Not connected");
                else
                    Connect();

            Debug.WriteLine("SEND: " + msg);
            ws.Send(msg);

            for (int w = 0; w < ResponseWait && response == null; w += 10)
                System.Threading.Thread.Sleep(10);

            if (response == null)
                throw new InvalidOperationException("Message processing failure");
        }

        /// <summary>
        /// Access DeepBot users using indexer syntax. Fetches the user from DeepBot using get_user API call
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if not connected to the bot and AutoConnect == false, or if the bot did not respond within ResponseWait milliseconds</exception>
        /// <exception cref="DeepBotException">Thrown if the specified user does not exist</exception>
        /// <param name="username">The user to retrieve</param>
        /// <returns>Strongly typed representation of the DeepBot user</returns>
        /// <remarks>To return null instead of throwing an exception if the user does not exist, use DeepBot.GetUser() instead.</remarks>
        public User this[string username]
        {
            get
            {
                User u = GetUser(username);

                if (u == null)
                    throw new DeepBotException("User not found");

                return u;
            }
        }

        /// <summary>
        /// Fetch a DeepBot user
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if not connected to the bot and AutoConnect == false, or if the bot did not respond within ResponseWait milliseconds</exception>
        /// <exception cref="DeepBotException">Thrown if there was an error processing the request on the bot</exception>
        /// <param name="username">The user to retrieve</param>
        /// <returns>Strongly typed representation of the DeepBot user; null if the user does not exist</returns>
        /// <remarks>To throw an exception instead of returning null if the user does not exist, use the DeepBot username string indexer syntax instead.</remarks>
        public User GetUser(string username)
        {
            if (AutoCache)
                if (cachedUsers.ContainsKey(username))
                    if (cachedUsers[username].LastUpdate < DateTime.Now.AddSeconds(60))
                        return cachedUsers[username];

            blockingCall("api|get_user|" + username);

            if (response is string)
                if (response == "User not found")
                    return null;
                else
                    throw new DeepBotException(response);

            User u = new User(this, (string)response.user, (int)response.points, (int)response.watch_time, (VIP)response.vip, (Level)response.mod,
                DateTime.Parse(response.join_date), DateTime.Parse(response.last_seen), DateTime.Parse(response.vip_expiry));

            if (AutoCache)
                cachedUsers[u.Name] = u;

            return u;
        }

        /// <summary>
        /// Try to fetch a DeepBot user, return null if not found or failure
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if not connected to the bot and AutoConnect == false, or if the bot did not respond within ResponseWait milliseconds</exception>
        /// <exception cref="DeepBotException">Thrown if there was an error processing the request on the bot</exception>
        /// <param name="username">The user to retrieve</param>
        /// <returns>Strongly typed representation of the DeepBot user; null if the user does not exist</returns>
        public User TryGetUser(string username)
        {
            try
            {
                return GetUser(username);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Get number of users in DeepBot's database. Property synonym for GetUsersCount()
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if not connected to the bot and AutoConnect == false, or if the bot did not respond within ResponseWait milliseconds</exception>
        public int Count
        {
            get
            {
                blockingCall("api|get_users_count");
                return response;
            }
        }

        /// <summary>
        /// Fetch a list of users from DeepBot's database. Does not use caching.
        /// </summary>
        /// <param name="offset">First user to fetch (0 for start of database). Optional (defaults to 0).</param>
        /// <param name="count">Number of users to fetch (max of 100 set by DeepBot). Optional (defaults to the maximum).</param>
        /// <exception cref="InvalidOperationException">Thrown if not connected to the bot and AutoConnect == false, or if the bot did not respond within ResponseWait milliseconds</exception>
        /// <exception cref="InvalidOperationException">Thrown if you specify a count without an offset.</exception>
        /// <exception cref="DeepBotException">Thrown if the user list could not be fetched.</exception>
        /// <returns>A list of Users. Empty list if attempting to access users beyond the end of the database.</returns>
        public List<User> GetUsers(int offset = -1, int count = -1)
        {
            if (offset == -1 && count == -1)
                blockingCall("api|get_users");

            else if (offset != -1 && count == -1)
                blockingCall("api|get_users|" + offset);

            else if (offset == -1 && count != -1)
                throw new InvalidOperationException("You must also specify an offset when specifying a count");

            else
                blockingCall("api|get_users|" + offset + "|" + count);

            if (response is string)
                if (response == "List empty")
                    return new List<User>();
                else
                    throw new DeepBotException(response);

            List<User> r = new List<User>();
            foreach (var s in response)
                r.Add(new User(this, (string)s.user, (int)s.points, (int)s.watch_time, (VIP)s.vip, (Level)s.mod,
                    DateTime.Parse(s.join_date), DateTime.Parse(s.last_seen), DateTime.Parse(s.vip_expiry)));

            return r;
        }

        /// <summary>
        /// Fetch a list of every user in DeepBot's database. Does not use caching.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if not connected to the bot and AutoConnect == false, or if the bot did not respond within ResponseWait milliseconds</exception>
        /// <exception cref="InvalidOperationException">Thrown if you specify a count without an offset.</exception>
        /// <exception cref="DeepBotException">Thrown if the user list could not be fetched.</exception>
        /// <remarks>May take considerable time to complete depending on the size of DeepBot's database.</remarks>
        /// <returns>A list of all DeepBot Users.</returns>
        public List<User> GetAllUsers()
        {
            List<User> users = new List<User>(1000);
            List<User> next;

            const int count = 100;

            for (int offset = 0; ; offset += count)
            {
                next = GetUsers(offset, count);

                if (next.Count > 0)
                    users.AddRange(next);
                else
                    break;
            }

            return users;
        }

        /// <summary>
        /// Fetch a list of users from DeepBot's database ordered by number of points (descending). Does not use caching.
        /// </summary>
        /// <param name="offset">First user to fetch (0 for start of database). Optional (defaults to 0).</param>
        /// <param name="count">Number of users to fetch (max of 100 set by DeepBot). Optional (defaults to the maximum).</param>
        /// <exception cref="InvalidOperationException">Thrown if not connected to the bot and AutoConnect == false, or if the bot did not respond within ResponseWait milliseconds</exception>
        /// <exception cref="InvalidOperationException">Thrown if you specify a count without an offset.</exception>
        /// <exception cref="DeepBotException">Thrown if the user list could not be fetched.</exception>
        /// <returns>A list of Users. Empty list if attempting to access users beyond the end of the database.</returns>
        public List<User> GetTopUsers(int offset = -1, int count = -1)
        {
            if (offset == -1 && count == -1)
                blockingCall("api|get_top_users");

            else if (offset != -1 && count == -1)
                blockingCall("api|get_top_users|" + offset);

            else if (offset == -1 && count != -1)
                throw new InvalidOperationException("You must also specify an offset when specifying a count");

            else
                blockingCall("api|get_top_users|" + offset + "|" + count);

            if (response is string)
                if (response == "List empty")
                    return new List<User>();
                else
                    throw new DeepBotException(response);

            List<User> r = new List<User>();
            foreach (var s in response)
                r.Add(new User(this, (string)s.user, (int)s.points, (int)s.watch_time, (VIP)s.vip, (Level)s.mod,
                    DateTime.Parse(s.join_date), DateTime.Parse(s.last_seen), DateTime.Parse(s.vip_expiry)));

            return r;
        }

        /// <summary>
        /// Add points to a user
        /// </summary>
        /// <param name="username">The username to add points to</param>
        /// <param name="points">The number of points to add</param>
        /// <exception cref="InvalidOperationException">Thrown if not connected to the bot and AutoConnect == false, or if the bot did not respond within ResponseWait milliseconds</exception>
        /// <returns>True on success, false otherwise</returns>
        public bool AddPoints(string username, int points)
        {
            blockingCall("api|add_points|" + username + "|" + points);

            return (string)response == "success";
        }

        /// <summary>
        /// Remove points from a user
        /// </summary>
        /// <param name="username">The username to remove points from</param>
        /// <param name="points">The number of points to remove</param>
        /// <exception cref="InvalidOperationException">Thrown if not connected to the bot and AutoConnect == false, or if the bot did not respond within ResponseWait milliseconds</exception>
        /// <returns>True on success, false otherwise</returns>
        public bool DelPoints(string username, int points)
        {
            blockingCall("api|del_points|" + username + "|" + points);

            return (string)response == "success";
        }

        /// <summary>
        /// Set a user's points total.
        /// </summary>
        /// <remarks>NOTE: Prefer to add or subtract points so that points accured by the user in DeepBot since the user was last fetched do not get lost.</remarks>
        /// <param name="username">The username to set the points total for</param>
        /// <param name="points">The number of points to set</param>
        /// <exception cref="InvalidOperationException">Thrown if not connected to the bot and AutoConnect == false, or if the bot did not respond within ResponseWait milliseconds</exception>
        /// <returns>True on success, false otherwise</returns>
        public bool SetPoints(string username, int points)
        {
            blockingCall("api|set_points|" + username + "|" + points);

            return (string)response == "success";
        }

        /// <summary>
        /// Get the user's number of watched hours.
        /// </summary>
        /// <param name="username">The username to fetch for</param>
        /// <exception cref="InvalidOperationException">Thrown if not connected to the bot and AutoConnect == false, or if the bot did not respond within ResponseWait milliseconds</exception>
        /// <returns>The number of watched hours</returns>
        public double GetHours(string username)
        {
            blockingCall("api|get_hours|" + username);

            return (double)response;
        }

        /// <summary>
        /// Get the user's rank string.
        /// </summary>
        /// <param name="username">The username to fetch for</param>
        /// <exception cref="InvalidOperationException">Thrown if not connected to the bot and AutoConnect == false, or if the bot did not respond within ResponseWait milliseconds</exception>
        /// <returns>The user's rank string</returns>
        public string GetRank(string username)
        {
            blockingCall("api|get_rank|" + username);

            return (string)response;
        }

        /// <summary>
        /// Set the user's VIP level and number of days to add.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if not connected to the bot and AutoConnect == false, or if the bot did not respond within ResponseWait milliseconds</exception>
        /// <exception cref="DeepBotException">Thrown if the VIP level or expiry could not be set, or if invalid parameters were supplied.</exception>
        /// <param name="username">The username to set VIP on</param>
        /// <param name="level">Desired VIP level</param>
        /// <param name="daysToAdd">Number of days to add. If the current expiry is in the past, sets the expiry to the specified number of days from now.</param>
        public void SetVIPLevel(string username, VIP level, int daysToAdd = 0)
        {
            if ((int)level == 10)
                level = VIP.Regular;

            blockingCall("api|set_vip|" + username + "|" + (int)level + "|" + daysToAdd);

            if (response != "success")
                throw new DeepBotException(response);
        }

        /// <summary>
        /// Set the user's VIP expiry time.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if not connected to the bot and AutoConnect == false, or if the bot did not respond within ResponseWait milliseconds</exception>
        /// <exception cref="DeepBotException">Thrown if the VIP expiry could not be set, or if invalid parameters were supplied.</exception>
        /// <param name="username">The username to set VIP expiry on</param>
        /// <param name="expiry">The VIP expiry time in local time</param>
        public void SetVIPExpiry(string username, DateTime expiry)
        {
            SetVIPExpiry(username, expiry.ToString("s"));
        }

        /// <summary>
        /// Set the user's VIP expiry time.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if not connected to the bot and AutoConnect == false, or if the bot did not respond within ResponseWait milliseconds</exception>
        /// <exception cref="DeepBotException">Thrown if the VIP expiry could not be set, or if invalid parameters were supplied.</exception>
        /// <param name="username">The username to set VIP expiry on</param>
        /// <param name="expiry">The VIP expiry time in local time in ISO the format "yyyy-MM-ddThh:mm:ss"</param>
        public void SetVIPExpiry(string username, string expiry)
        {
            blockingCall("api|set_vip_expiry|" + username + "|" + expiry);

            if (response != "success")
                throw new DeepBotException(response);
        }

        /// <summary>
        /// Add points to escrow
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if not connected to the bot and AutoConnect == false, or if the bot did not respond within ResponseWait milliseconds</exception>
        /// <exception cref="DeepBotException">Thrown if the points could not be escrowed due to insufficient funds, or if invalid parameters were supplied.</exception>
        /// <param name="username">The user to escrow points for</param>
        /// <param name="points">The number of points to escrow</param>
        public void AddToEscrow(string username, int points)
        {
            blockingCall("api|add_to_escrow|" + username + "|" + points);

            if (response != "success")
                throw new DeepBotException(response);
        }

        /// <summary>
        /// Commit points in escrow
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if not connected to the bot and AutoConnect == false, or if the bot did not respond within ResponseWait milliseconds</exception>
        /// <exception cref="DeepBotException">Thrown if the escrowed points could not be committed, or if invalid parameters were supplied.</exception>
        /// <param name="username">The user to commit escrow for</param>
        public void CommitEscrow(string username)
        {
            blockingCall("api|commit_user_escrow|" + username);

            if (response != "success")
                throw new DeepBotException(response);
        }

        /// <summary>
        /// Cancel escrowed points
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if not connected to the bot and AutoConnect == false, or if the bot did not respond within ResponseWait milliseconds</exception>
        /// <exception cref="DeepBotException">Thrown if the escrowed points could not be cancelled, or if invalid parameters were supplied.</exception>
        /// <param name="username">The user to cancel escrow for</param>
        public void CancelEscrow(string username)
        {
            blockingCall("api|cancel_escrow|" + username);

            if (response != "success")
                throw new DeepBotException(response);
        }

        // Websocket events
        private void websocket_Opened(object sender, EventArgs e)
        {
            Debug.WriteLine("OPEN");

            //System.Threading.Thread.Sleep(1000); // FIX: Fudge for DeepBot not immediately 'hearing' register request
            Debug.WriteLine("SEND: api|register|" + Secret);
            ws.Send("api|register|" + Secret);
        }

        private void websocket_MessageReceived(object sender, MessageReceivedEventArgs e)
        {
            Debug.WriteLine("RECV: " + e.Message);

            //try
            //{
                dynamic response = Json.Decode(e.Message);

                switch ((string)response.function)
                {
                    case "register":
                        Authenticated = (response.msg == "success");

                        if (response.msg == "incorrect api secret")
                            Authenticated = false;
                        else if (Authenticated != true)
                            throw new DeepBotException((string)response.msg);
                        break;

                    default:
                        try
                        {
                            this.response = Json.Decode(response.msg);
                        }
                        catch (ArgumentException)
                        {
                            this.response = response.msg;
                        }
                        break;
                }
            //}
            // FIX: Fudge for DeepBot sending music|getVolume erroneously when connecting to it as soon as it starts up
            //catch (ArgumentException)
            //{
            //    Debug.WriteLine("Unrecognized data ignored");
            //}
        }

        private void websocket_Closed(object sender, EventArgs e)
        {
            Debug.WriteLine("CLOSE: " + e.ToString());

            Authenticated = false;
        }

        private void websocket_Error(object sender, ErrorEventArgs e)
        {
            Debug.WriteLine("ERROR: " + e.Exception.ToString());
        }
    }
}

// DeepBot C# API Client Library Tests
// Written by Katy Coe
// (c) Noisy Cow Studios 2015
// http://www.djkaty.com

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using DeepBotServices;

namespace DeepBotTest
{
    class DeepBotTest
    {
        static void Main(string[] args)
        {
            // Configure test user account
            string userName = "kenludar";

            DeepBot bot;

            // 0. Try an incorrect OAuth string
            bot = new DeepBot("ws://localhost:3337", "foo");

            try
            {
                bot.Connect();
            }
            catch (DeepBotException ex)
            {
                Console.WriteLine("Attempt to log in with incorrect credentials: " + ex.Message);
            }
            
            // Set the right string
            bot.Secret = "D0A8EZPRYKPIBdAMJARQUJWAEeZLFCWNYZdGI";

            // Configure bot connection
            //bot = new DeepBot("ws://localhost:3337", "D0A8EZPRYKPIBdAMJARQUJWAEeZLFCWNYZdGI");

            // List of users for get_users test
            Dictionary<string, User> botUsers = new Dictionary<string, User>();

            // Test user (you can usually just use GetUser(), this is just for the exception handling in the finally block below)
            User u = new User(bot);
            User copy = new User(bot);

            // Regression test all API functions (run with F5 debug to see raw message exchange)
            try
            {
                // 1. Get a user that doesn't exist
                u = bot.GetUser("wefhjowefuho");
                Debug.Assert(u == null);

                // 2. Get a user that exists and a copy to restore later [get_user]
                Console.WriteLine("Test user fetch");

                u = bot[userName];
                Debug.Assert(u != null && u.Name == userName);
                copy = bot[userName];

                // 3. Set VIP expiry to a specific date (24 hours in the past) [set_vip_expiry]
                Console.WriteLine("VIP expiry set 1 day in the past");

                DateTime expiry = DateTime.Now.Subtract(TimeSpan.FromDays(1));
                u.SetVIPExpiry(expiry);
                Debug.Assert(u.VIPExpiry == expiry && u == bot[userName]);

                // 4. Set explicit VIP level and date (1 day in the future) [set_vip]
                Console.WriteLine("VIP set explicitly (gold, +1 day)");

                u.SetVIP(VIP.Gold, 1);
                Debug.Assert(u.VIPLevel == VIP.Gold && u.VIPExpiry > DateTime.Now.AddMinutes(60 * 23 + 59) && u.VIPExpiry < DateTime.Now.AddMinutes(60 * 24 + 1) && u == bot[userName]);

                // 5. Add more time to existing VIP (1 day in the future) [set_vip]
                Console.WriteLine("VIP extended (gold, +1 day)");

                u.SetVIP(VIP.Gold, 1);
                Debug.Assert(u.VIPLevel == VIP.Gold && u.VIPExpiry > DateTime.Now.AddMinutes(60 * 47 + 59) && u.VIPExpiry < DateTime.Now.AddMinutes(60 * 48 + 1) && u == bot[userName]);

                // 6. Make VIP expire now [set_vip_expiry]
                Console.WriteLine("VIP expired");

                u.SetVIPExpiry(DateTime.Now);
                Debug.Assert(u.VIPExpiry <= DateTime.Now && u == bot[userName]);

                // 7. Clear VIP explicitly [set_vip]
                Console.WriteLine("VIP cleared explicitly");

                u.SetVIP(VIP.Regular, 0);
                Debug.Assert(u.VIPLevel == VIP.Regular && u == bot[userName]);

                // 8. Add points to escrow [add_to_escrow] (no exception on success)
                Console.WriteLine("Points added to escrow");

                u.AddToEscrow(100);
                int p = u.Points;

                // 9. Cancel escrow [cancel_escrow] (no exception on success)
                Console.WriteLine("Escrow cancelled");

                u.CancelEscrow();
                Debug.Assert(u.Points == p && u == bot[userName]);

                // 10. Add two sets of points to escrow [add_to_escrow] (no exception on success)
                Console.WriteLine("Points added to escrow in two batches");

                u.AddToEscrow(10);
                u.AddToEscrow(20);

                // 11. Commit escrow [commit_user_escrow]
                Console.WriteLine("Points committed to escrow");

                u.CommitEscrow();
                Debug.Assert(u.Points == p - 30 && u == bot[userName]);

                // 12. Add points [add_points]
                Console.WriteLine("Add points");

                p = u.Points;
                u.Points += 100;
                Debug.Assert(u.Points == p + 100 && u == bot[userName]);

                // 13. Remove points [del_points]
                Console.WriteLine("Remove points");

                p = u.Points;
                u.Points -= 100;
                Debug.Assert(u.Points == p - 100 && u == bot[userName]);
                    
                // 14. Set points total explicitly [set_points]
                Console.WriteLine("Set points");

                u.Points = 12345;
                Debug.Assert(u.Points == 12345 && u == bot[userName]);

                // 15. Get rank [get_rank] (no exception on success)
                Console.WriteLine("Get rank: " + u.GetRank());

                // 16. Get watch hours [get_hours] (no exception on success)
                Console.WriteLine("Get viewing hours: " + u.GetHours());

                // (Restore the user)
                u.Points = copy.Points;
                u.SetVIP(copy.VIPLevel);
                u.SetVIPExpiry(copy.VIPExpiry);

                Console.WriteLine("User restored");

                // 17. Get number of users
                Console.WriteLine("Fetching user data for " + bot.Count + " users...");

                DateTime started = DateTime.Now;

                // 18. Get the user data for all the users with a Linq query
                botUsers = bot.GetAllUsers().ToDictionary(e => e.Name);

                TimeSpan taken = (DateTime.Now - started);

                Console.WriteLine("Fetched data for " + botUsers.Count + " users in " + taken.TotalSeconds + " seconds (" + ((double)botUsers.Count / taken.TotalSeconds) + " users/sec)");

                // 19. By way of example, print all users who have more than 8 hours of watch time with a Linq projection
                var regulars = from n in botUsers.Values where n.Minutes >= 480 orderby n.Minutes descending select new { n.Name, n.Minutes };

                foreach (var m in regulars)
                    Console.WriteLine(m.Name + "    -> " + m.Minutes);
            }
            catch (DeepBotException ex)
            {
                Console.WriteLine("DeepBot Error: " + ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine("API Error: " + ex.Message);
            }
            finally
            {
                // (Restore the user)
                u.Points = copy.Points;
                u.SetVIP(copy.VIPLevel);
                u.SetVIPExpiry(copy.VIPExpiry);

                Console.WriteLine("User restored");
            }

            // Wait for user exit
            Console.ReadLine();
        }
    }
}
